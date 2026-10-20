/*
ExifGlass - AI & EXIF metadata viewer
Copyright (C) 2023 - 2026 DUONG DIEU PHAP
Project homepage: https://github.com/d2phap/ExifGlass

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
using System.Text.Json;

namespace ExifGlass.Core.Ai.Parsers;

/// <summary>
/// C2PA Content Credentials from ExifTool's JUMBF/CBOR tags, manifests told apart by family-3 document numbers (S9).
/// </summary>
public sealed class C2paParser : IAiParser
{
    #region Fields and constants

    private const string SELF_PREFIX = "self#jumbf=/c2pa/";

    #endregion // Fields and constants


    #region Public methods

    public void Parse(AiParseContext context)
    {
        var jumbf = context.Tags.InGroup("JUMBF").Where(t => t.Key.Group3 is { } d && d.StartsWith("Doc", StringComparison.Ordinal)).ToList();
        var remote = context.Free("Provenance", "XMP-dcterms");
        if (jumbf.Count == 0 && remote is null) return;

        var b = new SourceBuilder(AiSourceKind.C2pa, "C2PA");
        b.Use(context, remote);

        var docs = jumbf.GroupBy(t => t.Key.Group3!).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        string? Label(string doc) => docs.TryGetValue(doc, out var tags) ? tags.FirstOrDefault(t => t.Name == "JUMDLabel")?.Text : null;

        // Doc1-N are the manifests in store order; the active one is the last (S9)
        var manifestDocs = docs.Keys.Where(d => Path(d).Length == 2 && Label(d) is { } l && l.Contains("urn:", StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => Path(d)[1]).ToList();

        var manifests = new List<C2paManifest>();
        for (var i = 0; i < manifestDocs.Count; i++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            manifests.Add(ReadManifest(manifestDocs[i], Label(manifestDocs[i])!, docs, Label, i == manifestDocs.Count - 1));
        }

        foreach (var t in jumbf) context.Claim(t);
        foreach (var t in jumbf.Where(t => t.Name is "Actions" or "Claim_generator" or "Claim_generator_info")) b.Use(context, t);

        var ordered = Order(manifests);
        b.C2pa = new C2paInfo(ordered, remote?.Text);

        var active = ordered.FirstOrDefault(m => m.IsActive);
        b.Generator = FriendlyName(active?.ClaimGenerator) ?? "C2PA";
        if (active?.ClaimGenerator is { } generator)
        {
            b.Param("Claim generator", active.ClaimGeneratorVersion is null ? generator : $"{generator} {active.ClaimGeneratorVersion}", null);
        }

        var aiAction = ordered.SelectMany(m => m.Actions).FirstOrDefault(a => DigitalSourceTypes.Kind(a.DigitalSourceType) >= AiKind.EditedWithAi && a.SoftwareAgent is not null)
            ?? ordered.SelectMany(m => m.Actions).FirstOrDefault(a => AiNames.IsAiAgent(a.SoftwareAgent));
        if (aiAction?.SoftwareAgent is { } agent) b.Param("AI system", aiAction.SoftwareAgentVersion is null ? agent : $"{agent} {aiAction.SoftwareAgentVersion}", null);

        if (remote is not null) b.Param("Remote manifest", remote.Text, remote.TableKey);
        if (manifests.Any(m => m.HasExifAssertion)) b.Warn("A manifest holds EXIF data, GPS included, that Remove GPS doesn't reach");

        b.Kind = Kind(ordered);
        context.Sources.Add(b.Build());
    }


    /// <summary>
    /// The kind the manifests claim: the strongest digital source type, else an AI agent or description by name, else captured.
    /// </summary>
    internal static AiKind Kind(IReadOnlyList<C2paManifest> manifests)
    {
        var actions = manifests.SelectMany(m => m.Actions).ToList();
        var kind = actions.Select(a => DigitalSourceTypes.Kind(a.DigitalSourceType)).DefaultIfEmpty(AiKind.None).Max();
        if (kind >= AiKind.EditedWithAi) return kind;

        if (actions.Any(a => AiNames.IsAiAgent(a.SoftwareAgent) || AiNames.IsAiDescription(a.Description))
            || manifests.Any(m => AiNames.IsAiAgent(m.ClaimGenerator)))
        {
            return actions.Any(a => a.Action is "c2pa.created") ? AiKind.Generated : AiKind.EditedWithAi;
        }

        return kind == AiKind.Captured ? AiKind.Captured : AiKind.None;
    }

    #endregion // Public methods


    #region Private methods

    // the badge shows the service, not the library that signed for it
    private static string? FriendlyName(string? claimGenerator) => claimGenerator switch
    {
        null or "" => null,
        _ when claimGenerator.StartsWith("Google C2PA", StringComparison.OrdinalIgnoreCase) => "Google",
        _ when claimGenerator.StartsWith("Microsoft Responsible AI", StringComparison.OrdinalIgnoreCase) || claimGenerator.StartsWith("Microsoft_Responsible", StringComparison.OrdinalIgnoreCase) => "Microsoft",
        "OpenAI-API" => "OpenAI API",
        _ => claimGenerator.Replace('_', ' '),
    };


    // "Doc1-2-1-1" -> [1, 2, 1, 1]
    private static int[] Path(string doc)
    {
        var parts = doc[3..].Split('-');
        var result = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out result[i])) return [];
        }

        return result;
    }


    private static bool IsUnder(string doc, int[] manifest)
    {
        var p = Path(doc);
        return p.Length > manifest.Length && p.AsSpan(0, manifest.Length).SequenceEqual(manifest);
    }


    private static C2paManifest ReadManifest(string doc, string label, Dictionary<string, List<AiTag>> docs, Func<string, string?> labelOf, bool isActive)
    {
        var path = Path(doc);
        var children = docs.Keys.Where(d => IsUnder(d, path)).OrderBy(d => string.Join('-', Path(d).Select(n => n.ToString("D4", System.Globalization.CultureInfo.InvariantCulture))), StringComparer.Ordinal).ToList();

        string? generator = null, generatorVersion = null, title = null, format = null;
        var claimVersion = 1;
        var actions = new List<C2paAction>();
        var ingredients = new List<C2paIngredient>();
        var authors = new List<string>();
        var hasExif = false;

        foreach (var child in children)
        {
            var childLabel = labelOf(child) ?? string.Empty;
            var tags = docs[child];

            if (childLabel.StartsWith("c2pa.claim", StringComparison.Ordinal))
            {
                claimVersion = childLabel.EndsWith(".v2", StringComparison.Ordinal) ? 2 : 1;
                var info = tags.FirstOrDefault(t => t.Name == "Claim_generator_info")?.Value;
                var first = info is { ValueKind: JsonValueKind.Array } arr && arr.GetArrayLength() > 0 ? arr[0] : info;
                var infoName = first is { ValueKind: JsonValueKind.Object } o ? LenientJson.Str(o, "name") : null;
                generatorVersion = first is { ValueKind: JsonValueKind.Object } v ? LenientJson.Str(v, "version") : null;

                // v1 "Adobe_Firefly adobe_c2pa/0.12.4 c2pa-rs/0.32.7": the info name is nicer when there is one
                var v1 = tags.FirstOrDefault(t => t.Name == "Claim_generator")?.Text;
                generator = infoName ?? (v1 is null ? null : v1.Split(' ')[0].Replace('_', ' '));
                title = tags.FirstOrDefault(t => t.Name == "Title")?.Text;
                format = tags.FirstOrDefault(t => t.Name == "Format")?.Text;
            }
            else if (childLabel.StartsWith("c2pa.actions", StringComparison.Ordinal))
            {
                foreach (var t in tags.Where(t => t.Name == "Actions")) actions.AddRange(ReadActions(t.Value));
            }
            else if (childLabel.StartsWith("c2pa.ingredient", StringComparison.Ordinal))
            {
                ingredients.Add(ReadIngredient(tags));
            }
            else if (childLabel.StartsWith("stds.schema-org.CreativeWork", StringComparison.Ordinal))
            {
                foreach (var t in tags.Where(t => t.Name == "Author")) authors.AddRange(Names(t.Value));
            }
            else if (childLabel.StartsWith("stds.exif", StringComparison.Ordinal))
            {
                hasExif = true;
            }
        }

        return new C2paManifest(label, claimVersion, generator, generatorVersion, title, format, actions, ingredients, authors, isActive, hasExif);
    }


    // one action is an object, several are an array; the agent is a string (v1) or {name, version} (v2)
    private static IEnumerable<C2paAction> ReadActions(JsonElement value)
    {
        var list = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToList() : [value];
        foreach (var a in list.Where(a => a.ValueKind == JsonValueKind.Object))
        {
            string? agent = null, agentVersion = null;
            if (LenientJson.Prop(a, "softwareAgent") is { } sa)
            {
                if (sa.ValueKind == JsonValueKind.Object)
                {
                    agent = LenientJson.Str(sa, "name");
                    agentVersion = LenientJson.Str(sa, "version");
                }
                else
                {
                    agent = AiTagSet.ToText(sa);
                }
            }

            yield return new C2paAction(LenientJson.Str(a, "action") ?? string.Empty, agent, agentVersion, LenientJson.Str(a, "digitalSourceType"), LenientJson.Str(a, "description"));
        }
    }


    private static C2paIngredient ReadIngredient(List<AiTag> tags)
    {
        string? Text(string name) => tags.FirstOrDefault(t => t.Name == name)?.Text;

        var link = tags.FirstOrDefault(t => t.Name is "ActiveManifest" or "C2PA_manifest")?.Value;
        var url = link is { ValueKind: JsonValueKind.Object } l ? LenientJson.Str(l, "url") : null;
        var manifest = url is not null && url.StartsWith(SELF_PREFIX, StringComparison.Ordinal) ? url[SELF_PREFIX.Length..].Split('/')[0] : null;

        return new C2paIngredient(Text("Title") is { Length: > 0 } title ? title : null, Text("Relationship"), Text("Format"), manifest);
    }


    private static IEnumerable<string> Names(JsonElement value)
    {
        var list = value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToList() : [value];
        foreach (var a in list)
        {
            if (a.ValueKind == JsonValueKind.Object && LenientJson.Str(a, "name") is { } name) yield return name;
            else if (a.ValueKind == JsonValueKind.String) yield return a.GetString()!;
        }
    }


    // the active manifest first, then the ones its ingredients point to (depth first), then the rest
    private static List<C2paManifest> Order(List<C2paManifest> manifests)
    {
        var result = new List<C2paManifest>();
        void Visit(C2paManifest m)
        {
            if (result.Contains(m)) return;
            result.Add(m);
            foreach (var i in m.Ingredients)
            {
                if (manifests.FirstOrDefault(x => x.Label == i.ManifestLabel) is { } next) Visit(next);
            }
        }

        if (manifests.LastOrDefault() is { } active) Visit(active);
        foreach (var m in Enumerable.Reverse(manifests)) Visit(m);
        return result;
    }

    #endregion // Private methods
}
