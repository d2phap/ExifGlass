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
using ExifGlass.Core.Metadata;
using System.Globalization;
using System.Text;

namespace ExifGlass.Core.Ai;

/// <summary>
/// What an AI row is about; the AI quick filters (Prompts, Models, Provenance) select by it.
/// </summary>
public enum AiFieldKind
{
    Generation,
    Prompt,
    Model,
    Parameter,
    Workflow,
    Provenance,
    Raw,
    Character,
}


/// <summary>
/// One row of the synthetic "AI generation" group (D22), linked to the tag it came from.
/// </summary>
public sealed record AiRow(string Key, string Name, string Label, string Value, string? SourceKey, AiFieldKind Kind)
{
    #region Fields and constants

    private readonly string? _display;

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// The text the table draws: Value with the " · " before each chip blanked, so one set of match ranges fits both.
    /// </summary>
    public string Display
    {
        get => _display ?? Value;
        init => _display = value;
    }

    public IReadOnlyList<AiSpan> Spans { get; init; } = [];

    /// <summary>
    /// False when the row's source is the AI group's own, which its header names once (G1, AI addendum question 1).
    /// </summary>
    public bool ShowsSource { get; init; } = true;

    /// <summary>
    /// The other tags that feed the row: "+1" after its source link, listed in the tooltip.
    /// </summary>
    public IReadOnlyList<string> OtherSources { get; init; } = [];

    /// <summary>
    /// The Kind row draws its value as the kind chip.
    /// </summary>
    public AiKind? KindChip { get; init; }

    /// <summary>
    /// How ExifGlass read the field: "A1111 infotext", "ComfyUI API prompt"; the selected-field card shows it.
    /// </summary>
    public string? ParsedAs { get; init; }

    #endregion // Properties
}


/// <summary>
/// The AI group's rows and the one tag most of them come from, which the group header names.
/// </summary>
public sealed record AiRowSet(IReadOnlyList<AiRow> Rows, string? SourceKey)
{
    #region Properties

    public static AiRowSet Empty { get; } = new([], null);

    #endregion // Properties
}


/// <summary>
/// Turns the AI information into the rows of the table's AI group, each field once (Appendix B item 21), in the AI addendum's order.
/// </summary>
public static class AiRows
{
    #region Fields and constants

    // what a row's value shows between its text and each chip; the table draws blanks of the same length
    private const string SEPARATOR = " · ";
    private const string BLANK = "   ";

    private static readonly AiModelKind[] MODEL_ORDER =
    [
        AiModelKind.Checkpoint, AiModelKind.Unet, AiModelKind.Refiner, AiModelKind.TextEncoder, AiModelKind.Vae, AiModelKind.Lora,
        AiModelKind.Embedding, AiModelKind.Upscaler, AiModelKind.ControlNet, AiModelKind.Detailer, AiModelKind.Other,
    ];

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// The AI group's rows: only for a file made or edited with AI, or a character card; provenance alone (a camera's C2PA, training signals) has no AI group.
    /// </summary>
    public static AiRowSet Build(AiGenerationInfo? info)
    {
        if (info is null || !(info.IsAi || info.Kind == AiKind.CharacterCard)) return AiRowSet.Empty;

        var rows = new Rows();
        var primary = info.Primary;
        var main = primary?.SourceKeys.FirstOrDefault();

        var generator = info.GeneratorVersion is null ? info.Generator : $"{info.Generator} {info.GeneratorVersion}";
        rows.Add("generator", "Generator", new Value().Text(generator), main, AiFieldKind.Generation, primary,
            others: primary is null ? [] : [.. primary.SourceKeys.Skip(1)]);

        var kindSource = info.Sources.FirstOrDefault(s => s.Kind == info.Kind) ?? primary;
        var kindKey = kindSource?.SourceKeys.FirstOrDefault();
        rows.Add("kind", "Kind", new Value().Text(AiGenerationInfo.KindLabel(info.Kind)), kindKey, AiFieldKind.Generation, kindSource, kindChip: info.Kind);

        AddPrompts(rows, info);
        AddStyles(rows, info);
        AddModels(rows, info);
        AddParameters(rows, info);

        foreach (var s in info.Sources)
        {
            foreach (var w in s.Workflows) AddWorkflow(rows, s, w);
        }

        foreach (var s in info.Sources) AddProvenance(rows, s);
        foreach (var s in info.Sources) AddCharacter(rows, s);
        foreach (var s in info.Sources) AddRaw(rows, s);

        return rows.ToSet();
    }


    /// <summary>
    /// A source tag as the AI group shows it: the write group and the name, "PNG:Parameters", "XMP-iptcExt:DigitalSourceType", "JUMBF:Actions".
    /// </summary>
    public static string SourceLabel(string key)
    {
        var k = TagKey.Parse(key);
        return $"{(k.Group0 == "JUMBF" ? k.Group0 : k.Group1)}:{k.Name}";
    }


    /// <summary>
    /// How ExifGlass read a source, for the selected-field card's "Parsed as".
    /// </summary>
    public static string ParsedAs(AiSourceInfo source, string? sourceKey) => source.Source switch
    {
        AiSourceKind.A1111 => "A1111 infotext",
        AiSourceKind.ComfyUi => sourceKey?.EndsWith("Workflow", StringComparison.OrdinalIgnoreCase) == true ? "ComfyUI workflow" : "ComfyUI API prompt",
        AiSourceKind.InvokeAi => "InvokeAI metadata",
        AiSourceKind.NovelAi => "NovelAI comment JSON",
        AiSourceKind.Fooocus => source.Infotext is not null ? "A1111 infotext" : "Fooocus JSON",
        AiSourceKind.SwarmUi => "SwarmUI JSON",
        AiSourceKind.EasyDiffusion => "Easy Diffusion fields",
        AiSourceKind.DrawThings => "Draw Things JSON",
        AiSourceKind.Midjourney => "Midjourney description",
        AiSourceKind.TensorArt => "Tensor.art JSON",
        AiSourceKind.C2pa => "C2PA manifest",
        AiSourceKind.Iptc => "IPTC Extension",
        AiSourceKind.Aigc => "GB 45438-2025 label",
        AiSourceKind.TrainingSignals => "AI-training signal",
        AiSourceKind.ToolName => "Tool name",
        AiSourceKind.CharacterCard => "Character card JSON",
        _ => "Raw text",
    };


    /// <summary>
    /// A C2PA action as people say it: "c2pa.created" is "Created", "c2pa.color_adjustments" is "Color adjustments".
    /// </summary>
    public static string ActionLabel(string action)
    {
        var a = action.StartsWith("c2pa.", StringComparison.OrdinalIgnoreCase) ? action[5..] : action;
        a = a.Replace('_', ' ').Replace('.', ' ').Trim();
        return a.Length == 0 ? action : char.ToUpperInvariant(a[0]) + a[1..];
    }


    /// <summary>
    /// A payload size: "599 B", "38 KB", "2.3 MB".
    /// </summary>
    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes} B"),
        < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{Math.Max(1, Math.Round(bytes / 1024.0)):0} KB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024):0.0} MB"),
    };


    /// <summary>
    /// The model kind as the AI group and the AI card name it.
    /// </summary>
    public static string ModelLabel(AiModelKind kind) => kind switch
    {
        AiModelKind.Checkpoint => "Model",
        AiModelKind.Unet => "Diffusion model",
        AiModelKind.TextEncoder => "Text encoder",
        AiModelKind.Vae => "VAE",
        AiModelKind.Lora => "LoRA",
        AiModelKind.Embedding => "Embedding",
        AiModelKind.Upscaler => "Upscaler",
        AiModelKind.ControlNet => "ControlNet",
        AiModelKind.Refiner => "Refiner",
        AiModelKind.Detailer => "Detailer",
        _ => "Model",
    };


    /// <summary>
    /// Models in the AI addendum's order: checkpoint, refiner, VAE, LoRAs, embeddings, upscalers, then the rest.
    /// </summary>
    public static IReadOnlyList<AiModelRef> OrderModels(IReadOnlyList<AiModelRef> models) =>
        [.. models.Select((m, i) => (m, i)).OrderBy(x => Array.IndexOf(MODEL_ORDER, x.m.Kind)).ThenBy(x => x.i).Select(x => x.m)];


    /// <summary>
    /// Fooocus styles, written as a Python or JSON list: "['Fooocus V2', 'Fooocus Enhance']".
    /// </summary>
    public static IReadOnlyList<string> ParseList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];

        var v = value.Trim();
        if (v.StartsWith('[') && v.EndsWith(']')) v = v[1..^1];

        return [.. v.Split(',').Select(s => s.Trim().Trim('\'', '"').Trim()).Where(s => s.Length > 0)];
    }

    #endregion // Public methods


    #region Private methods

    private static void AddPrompts(Rows rows, AiGenerationInfo info)
    {
        var embeddings = info.Models.Where(m => m.Kind == AiModelKind.Embedding).Select(m => m.Name).ToList();

        foreach (var s in info.Sources)
        {
            // the main pair first, then the labelled ones (ADetailer, Hires, NovelAI characters) as found
            var ordered = s.Prompts.Select((p, i) => (p, i)).OrderBy(x => x.p.Label.Length > 0 ? 2 : x.p.Role == AiPromptRole.Positive ? 0 : 1).ThenBy(x => x.i).Select(x => x.p);
            foreach (var p in ordered)
            {
                var value = new Value().Prompt(p.Text, embeddings);
                var (id, name) = PromptName(p, value);
                rows.Add(id, name, value, p.SourceKey ?? s.SourceKeys.FirstOrDefault(), AiFieldKind.Prompt, s);
            }
        }
    }


    // NovelAI's "Character 1 (0.5, 0.5)" keeps its centre as a chip
    private static (string Id, string Name) PromptName(AiPrompt p, Value value)
    {
        var negative = p.Role == AiPromptRole.Negative;
        if (p.Label.Length == 0) return negative ? ("negativePrompt", "Negative prompt") : ("prompt", "Prompt");

        if (p.Label.StartsWith("Character ", StringComparison.Ordinal) && p.Label.IndexOf(" (", StringComparison.Ordinal) is var open and > 0 && p.Label.EndsWith(')'))
        {
            value.Chip("at " + p.Label[(open + 2)..^1]);
            return ("character", p.Label[..open]);
        }

        if (p.Label == "Prompt information") return ("promptInformation", p.Label);

        return negative ? ("negativePrompt", "Negative prompt · " + p.Label) : ("prompt", "Prompt · " + p.Label);
    }


    private static void AddStyles(Rows rows, AiGenerationInfo info)
    {
        foreach (var s in info.Sources)
        {
            if (s.Parameter("styles") is not { } styles) continue;

            var value = new Value();
            foreach (var style in ParseList(styles.Value)) value.Chip(style);
            rows.Add("styles", "Styles", value, styles.SourceKey ?? s.SourceKeys.FirstOrDefault(), AiFieldKind.Parameter, s);
        }
    }


    private static void AddModels(Rows rows, AiGenerationInfo info)
    {
        foreach (var m in OrderModels(info.Models))
        {
            var source = info.Sources.FirstOrDefault(s => s.Models.Contains(m));
            var value = new Value().Text(m.Name).Chip(m.Weight, AiSpanKind.WeightChip).Chip(m.Hash);
            rows.Add(m.Kind.ToString().ToLowerInvariant(), ModelLabel(m.Kind), value, m.SourceKey ?? source?.SourceKeys.FirstOrDefault(), AiFieldKind.Model, source);
        }
    }


    private static void AddParameters(Rows rows, AiGenerationInfo info)
    {
        var hasModel = info.Models.Any(m => m.Kind is AiModelKind.Checkpoint or AiModelKind.Unet);
        var hasVae = info.Models.Any(m => m.Kind == AiModelKind.Vae);

        foreach (var s in info.Sources)
        {
            // provenance has rows of its own, from what its parser read
            if (s.IsProvenance) continue;

            foreach (var p in s.Parameters)
            {
                if (p.Id is AiParameterNames.MODEL or AiParameterNames.MODEL_HASH && hasModel) continue;
                if (p.Id is AiParameterNames.VAE or AiParameterNames.VAE_HASH && hasVae) continue;
                if (p.Id == "styles") continue;

                rows.Add(p.Id, p.Label, new Value().Text(p.Value), p.SourceKey ?? s.SourceKeys.FirstOrDefault(), AiFieldKind.Parameter, s);
            }

            foreach (var pass in s.Passes)
            {
                rows.Add("pass", "Sampler · " + pass.Title, new Value().Text(PassSummary(pass)), s.SourceKeys.FirstOrDefault(), AiFieldKind.Parameter, s);
            }
        }
    }


    // "euler · karras · 30 steps · CFG 6.5 · seed 42", the rest as "Label value"
    private static string PassSummary(AiSamplerPass pass)
    {
        var parts = new List<string>(pass.Parameters.Count);
        foreach (var p in pass.Parameters)
        {
            parts.Add(p.Id switch
            {
                AiParameterNames.SAMPLER or AiParameterNames.SCHEDULER => p.Value,
                AiParameterNames.STEPS => p.Value + " steps",
                AiParameterNames.CFG => "CFG " + p.Value,
                AiParameterNames.SEED => "seed " + p.Value,
                _ => $"{p.Label} {p.Value}",
            });
        }

        return string.Join(SEPARATOR, parts);
    }


    private static void AddWorkflow(Rows rows, AiSourceInfo s, AiWorkflowSummary w)
    {
        var (id, name) = w.Format switch
        {
            AiWorkflowFormat.ComfyUiApi => ("apiPrompt", "API prompt"),
            AiWorkflowFormat.InvokeAiGraph => ("graph", "Graph"),
            _ => ("workflow", "Workflow"),
        };

        var text = string.Join(SEPARATOR,
            Count(w.NodeCount, "node"), Count(w.SamplerCount, "sampler"), Count(w.ModelCount, "model"), FormatSize(w.Size));
        rows.Add(id, name, new Value().Text(text).Chip(w.FormatLabel), w.SourceKey, AiFieldKind.Workflow, s);
    }


    private static void AddProvenance(Rows rows, AiSourceInfo s)
    {
        if (s.C2pa is { } c2pa) AddC2pa(rows, s, c2pa);

        if (s.Iptc is { } iptc)
        {
            if (iptc.DigitalSourceType is { } dst)
            {
                rows.Add("sourceType", "Source type", new Value().Text(DigitalSourceTypes.Label(dst)).Chip(DigitalSourceTypes.Term(dst)), Key(s, "DigitalSourceType"), AiFieldKind.Provenance, s);
            }

            var system = iptc.AiSystemVersion is null ? iptc.AiSystem : $"{iptc.AiSystem} {iptc.AiSystemVersion}";
            if (iptc.AiSystem is not null) rows.Add("aiSystem", "AI system", new Value().Text(system!), Key(s, "AISystemUsed"), AiFieldKind.Provenance, s);
            if (iptc.PromptWriter is { } writer) rows.Add("promptWriter", "Prompt writer", new Value().Text(writer), Key(s, "AIPromptWriterName"), AiFieldKind.Provenance, s);
            if (iptc.Credit is { } credit) rows.Add("credit", "Credit", new Value().Text(credit), Key(s, "Credit"), AiFieldKind.Provenance, s);
        }

        if (s.Aigc is { } aigc)
        {
            var key = s.SourceKeys.FirstOrDefault();
            rows.Add("aigcLabel", "AI label", new Value().Text(aigc.Meaning).Chip("GB 45438-2025 code " + aigc.Label), key, AiFieldKind.Provenance, s);
            if (aigc.ContentProducer is { } producer) rows.Add("contentProducer", "Content producer", new Value().Text(producer), key, AiFieldKind.Provenance, s);
            if (aigc.ProduceId is { } produceId) rows.Add("produceId", "Produce ID", new Value().Text(produceId), key, AiFieldKind.Provenance, s);
            if (aigc.ContentPropagator is { } propagator) rows.Add("contentPropagator", "Content propagator", new Value().Text(propagator), key, AiFieldKind.Provenance, s);
            if (aigc.PropagateId is { } propagateId) rows.Add("propagateId", "Propagate ID", new Value().Text(propagateId), key, AiFieldKind.Provenance, s);
        }

        if (s.Training is { } training)
        {
            if (training.DataMining is { } mining) rows.Add("dataMining", "AI training · Data mining", new Value().Text(mining), Key(s, "DataMining"), AiFieldKind.Provenance, s);
            if (training.LearningOptOutIn is { } optOut) rows.Add("learningOptOut", "AI training · Opt-out", new Value().Text(optOut), Key(s, "LearningOptOutIn"), AiFieldKind.Provenance, s);
        }
    }


    // the active manifest's claim, actions and source types, then the manifests its ingredients point to; never verified (D21)
    private static void AddC2pa(Rows rows, AiSourceInfo s, C2paInfo c2pa)
    {
        var claimKey = Key(s, "Claim_generator_info") ?? Key(s, "Claim_generator") ?? s.SourceKeys.FirstOrDefault();
        var actionKey = Key(s, "Actions") ?? claimKey;

        if (c2pa.Active is { } active)
        {
            if (active.ClaimGenerator is { } claim)
            {
                var text = active.ClaimGeneratorVersion is null ? claim : $"{claim} {active.ClaimGeneratorVersion}";
                rows.Add("claimGenerator", "Claim generator", new Value().Text(text), claimKey, AiFieldKind.Provenance, s);
            }

            AddActions(rows, s, active, actionKey);
        }

        var n = 0;
        foreach (var m in c2pa.Manifests.Where(m => !m.IsActive))
        {
            var first = m.Actions.FirstOrDefault();
            var by = first?.SoftwareAgent ?? m.ClaimGenerator;
            var text = m.Title ?? m.ClaimGenerator ?? "Ingredient";
            if (first is not null) text += SEPARATOR + ActionLabel(first.Action) + (by is null ? string.Empty : " by " + by);

            rows.Add("ingredient", string.Create(CultureInfo.InvariantCulture, $"Ingredient {++n}"), new Value().Text(text), claimKey, AiFieldKind.Provenance, s);
            foreach (var a in m.Actions.Where(a => a.DigitalSourceType is not null)) AddSourceType(rows, s, a, actionKey);
        }

        if (c2pa.RemoteManifestUrl is { } url) rows.Add("remoteManifest", "Remote manifest", new Value().Text(url), Key(s, "Provenance"), AiFieldKind.Provenance, s);

        rows.Add("signature", "Signature", new Value().Text("Not verified").Note("ExifGlass doesn't check C2PA signatures"), claimKey, AiFieldKind.Provenance, s);
    }


    private static void AddActions(Rows rows, AiSourceInfo s, C2paManifest manifest, string? key)
    {
        foreach (var a in manifest.Actions)
        {
            var text = ActionLabel(a.Action);
            if (a.Description is { Length: > 0 } description) text += SEPARATOR + description;
            if (a.SoftwareAgent is { } agent) text += SEPARATOR + agent;

            rows.Add("action", "Action", new Value().Text(text).Chip(a.Action), key, AiFieldKind.Provenance, s);
        }

        foreach (var a in manifest.Actions.Where(a => a.DigitalSourceType is not null)) AddSourceType(rows, s, a, key);
    }


    private static void AddSourceType(Rows rows, AiSourceInfo s, C2paAction action, string? key) =>
        rows.Add("sourceType", "Source type", new Value().Text(action.DigitalSourceLabel!).Chip(DigitalSourceTypes.Term(action.DigitalSourceType!)), key, AiFieldKind.Provenance, s);


    private static void AddCharacter(Rows rows, AiSourceInfo s)
    {
        if (s.Source != AiSourceKind.CharacterCard) return;

        var key = s.SourceKeys.FirstOrDefault();
        var card = s.Character;
        if ((card?.Name ?? s.CharacterName) is { } name) rows.Add("character", "Character", new Value().Text(name), key, AiFieldKind.Character, s);
        if (card?.Creator is { Length: > 0 } creator) rows.Add("creator", "Creator", new Value().Text(creator), key, AiFieldKind.Character, s);
        if (s.GeneratorVersion is { } spec) rows.Add("cardSpec", "Card spec", new Value().Text(spec).Chip(card?.CharacterVersion is { Length: > 0 } v ? "v" + v : null), key, AiFieldKind.Character, s);
    }


    private static void AddRaw(Rows rows, AiSourceInfo s)
    {
        if (s.RawText is { } raw && s.Kind == AiKind.UnknownAi && s.Parameters.Count == 0)
        {
            rows.Add("raw", "Raw text", new Value().Text(raw), s.SourceKeys.FirstOrDefault(), AiFieldKind.Raw, s);
        }
    }


    // the source key whose tag name is this one, for a source that read several tags
    private static string? Key(AiSourceInfo s, string name) =>
        s.SourceKeys.FirstOrDefault(k => k.EndsWith(":" + name, StringComparison.OrdinalIgnoreCase));


    private static string Count(int n, string noun) => string.Create(CultureInfo.InvariantCulture, $"{n} {noun}{(n == 1 ? string.Empty : "s")}");

    #endregion // Private methods


    #region Nested types

    // a row's value with its spans; chips go after a separator the table blanks out
    private sealed class Value
    {
        private readonly StringBuilder _value = new();
        private readonly StringBuilder _display = new();
        private readonly List<AiSpan> _spans = [];

        public string Content => _value.ToString();

        public string Display => _display.ToString();

        public IReadOnlyList<AiSpan> Spans => _spans;


        public Value Text(string text)
        {
            _value.Append(text);
            _display.Append(text);
            return this;
        }


        public Value Prompt(string text, IReadOnlyCollection<string> embeddings)
        {
            var at = _value.Length;
            foreach (var span in AiPromptSyntax.Spans(text, embeddings)) _spans.Add(span with { Start = span.Start + at });
            return Text(text);
        }


        public Value Chip(string? text, AiSpanKind kind = AiSpanKind.Chip)
        {
            if (string.IsNullOrWhiteSpace(text)) return this;

            if (_value.Length > 0)
            {
                _value.Append(SEPARATOR);
                _display.Append(BLANK);
            }

            _spans.Add(new AiSpan(_value.Length, text.Length, kind));
            return Text(text);
        }


        public Value Note(string text) => Chip(text, AiSpanKind.Note);
    }


    private sealed class Rows
    {
        private readonly List<AiRow> _rows = [];
        private readonly Dictionary<string, int> _ids = new(StringComparer.Ordinal);


        public void Add(string id, string name, Value value, string? sourceKey, AiFieldKind kind, AiSourceInfo? source, IReadOnlyList<string>? others = null, AiKind? kindChip = null)
        {
            var text = value.Content;
            if (string.IsNullOrWhiteSpace(text)) return;

            if (_rows.Any(r => r.Label == name && r.Value == text)) return;

            var n = _ids[id] = _ids.GetValueOrDefault(id) + 1;
            var key = n == 1 ? $"AI:{id}" : string.Create(CultureInfo.InvariantCulture, $"AI:{id}#{n}");
            _rows.Add(new AiRow(key, id, name, text, sourceKey, kind)
            {
                Display = value.Display,
                Spans = [.. value.Spans],
                OtherSources = others ?? [],
                KindChip = kindChip,
                ParsedAs = source is null ? null : ParsedAs(source, sourceKey),
            });
        }


        // the tag most rows came from is named once, in the group header; ties go to the first one seen
        public AiRowSet ToSet()
        {
            var main = _rows.Where(r => r.SourceKey is not null).GroupBy(r => r.SourceKey!)
                .Select((g, i) => (g.Key, Count: g.Count(), i)).OrderByDescending(x => x.Count).ThenBy(x => x.i).Select(x => x.Key).FirstOrDefault();

            var rows = _rows.Select(r => r with { ShowsSource = r.SourceKey is not null && r.SourceKey != main }).ToList();
            return new AiRowSet(rows, main);
        }
    }

    #endregion // Nested types
}
