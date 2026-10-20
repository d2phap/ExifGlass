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
using System.Text.RegularExpressions;

namespace ExifGlass.Core.Ai.Parsers;

/// <summary>
/// Fooocus (JSON scheme; the a1111 scheme goes to A1111Parser), Fooocus-MRE and RuinedFooocus.
/// </summary>
public sealed partial class FooocusParser : IAiParser
{
    #region Public methods

    public void Parse(AiParseContext context)
    {
        var scheme = context.Free("Fooocus_scheme", "PNG") ?? context.Free("MakerNoteUnknownText", "IFD0");
        var software = context.Free("Software", "IFD0");

        foreach (var tag in Candidates(context))
        {
            if (!LenientJson.LooksLikeObject(tag.Text)) continue;

            using var doc = LenientJson.TryParse(tag.Text, out _);
            if (doc?.RootElement is not { ValueKind: JsonValueKind.Object } root) continue;

            var soft = LenientJson.Str(root, "software") ?? string.Empty;
            var version = LenientJson.Str(root, "version") ?? string.Empty;

            if (soft.Contains("RuinedFooocus", StringComparison.OrdinalIgnoreCase))
            {
                context.Sources.Add(ParseRuined(context, tag, root));
                return;
            }

            if (soft.Contains("MRE", StringComparison.Ordinal))
            {
                context.Sources.Add(ParseMre(context, tag, root, soft));
                return;
            }

            var isFooocus = version.StartsWith("Fooocus", StringComparison.OrdinalIgnoreCase)
                || scheme?.Text.Equals("fooocus", StringComparison.OrdinalIgnoreCase) == true
                || software?.Text.StartsWith("Fooocus", StringComparison.OrdinalIgnoreCase) == true;
            if (!isFooocus || LenientJson.Prop(root, "prompt") is null) continue;

            var b = ParseJson(context, tag, root);
            b.Use(context, scheme);
            if (software?.Text.StartsWith("Fooocus", StringComparison.OrdinalIgnoreCase) == true) b.Use(context, software);
            if (tag.Key.Group1 == "IFD0") b.Warn("Fooocus writes this comment without a character set; ExifTool reads it as UTF-8");

            context.Sources.Add(b.Build());
            return;
        }
    }

    #endregion // Public methods


    #region Private methods

    // "(1152, 896)" -> 1152x896
    [GeneratedRegex(@"^\(?\s*(\d+)\s*,\s*(\d+)\s*\)?$", RegexOptions.CultureInvariant)]
    private static partial Regex Resolution();


    // RuinedFooocus LoRA "0.5 - detail_tweaker_xl.safetensors"
    [GeneratedRegex(@"^\s*([-+]?[\d.]+)\s*-\s*(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex WeightAndName();


    private static IEnumerable<AiTag> Candidates(AiParseContext context)
    {
        foreach (var (group, name) in new[] { ("PNG", "Parameters"), ("IFD0", "UserComment"), ("ExifIFD", "UserComment"), ("PNG", "Comment"), ("File", "Comment") })
        {
            if (context.Free(name, group) is { } tag) yield return tag;
        }
    }


    private static SourceBuilder ParseJson(AiParseContext context, AiTag tag, JsonElement root)
    {
        var key = tag.TableKey;
        var b = new SourceBuilder(AiSourceKind.Fooocus, "Fooocus");
        b.Use(context, tag);

        var version = LenientJson.Str(root, "version");
        b.Version = version is null ? null : version.Replace("Fooocus", string.Empty, StringComparison.OrdinalIgnoreCase).Trim().TrimStart('v');

        b.Prompt(LenientJson.Str(root, "prompt"), AiPromptRole.Positive, string.Empty, key);
        b.Prompt(LenientJson.Str(root, "negative_prompt"), AiPromptRole.Negative, string.Empty, key);
        b.Prompt(JoinList(LenientJson.Prop(root, "full_prompt")), AiPromptRole.Positive, "Expanded", key);
        b.Prompt(JoinList(LenientJson.Prop(root, "full_negative_prompt")), AiPromptRole.Negative, "Expanded", key);

        b.Param("base_model", LenientJson.Str(root, "base_model"), key);
        b.Param("sampler", LenientJson.Str(root, "sampler"), key);
        b.Param("scheduler", LenientJson.Str(root, "scheduler"), key);
        b.Param("steps", LenientJson.Str(root, "steps"), key);
        b.Param("guidance_scale", LenientJson.Str(root, "guidance_scale"), key);
        b.Param("seed", LenientJson.Str(root, "seed"), key);
        b.Param("Size", Size(LenientJson.Str(root, "resolution")), key);
        b.Param("Performance", LenientJson.Str(root, "performance"), key);
        b.Param("Styles", LenientJson.Str(root, "styles"), key);
        b.Param("Sharpness", LenientJson.Str(root, "sharpness"), key);
        b.Param("ADM guidance", LenientJson.Str(root, "adm_guidance"), key);
        b.Param("Refiner switch", LenientJson.Str(root, "refiner_switch"), key);

        b.Model(AiModelKind.Checkpoint, LenientJson.Str(root, "base_model"), null, LenientJson.Str(root, "base_model_hash"), key);
        b.Model(AiModelKind.Refiner, LenientJson.Str(root, "refiner_model"), null, null, key);
        b.Model(AiModelKind.Vae, LenientJson.Str(root, "vae") is { } vae && !vae.StartsWith("Default", StringComparison.Ordinal) ? vae : null, null, null, key);

        // loras: [[name, weight, hash], ...]
        if (LenientJson.Prop(root, "loras") is { ValueKind: JsonValueKind.Array } loras)
        {
            foreach (var l in loras.EnumerateArray().Where(l => l.ValueKind == JsonValueKind.Array && l.GetArrayLength() >= 2))
            {
                b.Model(AiModelKind.Lora, AiTagSet.ToText(l[0]), AiTagSet.ToText(l[1]), l.GetArrayLength() > 2 ? AiTagSet.ToText(l[2]) : null, key);
            }
        }

        return b;
    }


    private static AiSourceInfo ParseMre(AiParseContext context, AiTag tag, JsonElement root, string software)
    {
        var key = tag.TableKey;
        var b = new SourceBuilder(AiSourceKind.Fooocus, "Fooocus-MRE");
        b.Use(context, tag);
        b.Version = software.Replace("Fooocus", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("MRE", string.Empty, StringComparison.Ordinal).Trim();

        b.Prompt(LenientJson.Str(root, "prompt"), AiPromptRole.Positive, string.Empty, key);
        b.Prompt(LenientJson.Str(root, "negative_prompt"), AiPromptRole.Negative, string.Empty, key);
        b.Prompt(LenientJson.Str(root, "real_prompt"), AiPromptRole.Positive, "Expanded", key);
        b.Prompt(LenientJson.Str(root, "real_negative_prompt"), AiPromptRole.Negative, "Expanded", key);

        b.Param("base_model_name", LenientJson.Str(root, "base_model_name"), key);
        b.Param("sampler_name", LenientJson.Str(root, "sampler_name"), key);
        b.Param("scheduler", LenientJson.Str(root, "scheduler"), key);
        b.Param("steps", LenientJson.Str(root, "steps"), key);
        b.Param("cfg", LenientJson.Str(root, "cfg"), key);
        b.Param("seed", LenientJson.Str(root, "seed"), key);
        if (LenientJson.Str(root, "width") is { } w && LenientJson.Str(root, "height") is { } h) b.Param("Size", $"{w}x{h}", key);
        b.Param("Styles", LenientJson.Str(root, "styles"), key);
        b.Param("Sharpness", LenientJson.Str(root, "sharpness"), key);
        b.Param("Refiner switch", LenientJson.Str(root, "switch"), key);

        b.Model(AiModelKind.Checkpoint, LenientJson.Str(root, "base_model_name"), null, null, key);
        b.Model(AiModelKind.Refiner, LenientJson.Str(root, "refiner_model_name"), null, null, key);
        for (var i = 1; i <= 5; i++)
        {
            b.Model(AiModelKind.Lora, LenientJson.Str(root, "l" + i), LenientJson.Str(root, "w" + i), null, key);
        }

        return b.Build();
    }


    private static AiSourceInfo ParseRuined(AiParseContext context, AiTag tag, JsonElement root)
    {
        var key = tag.TableKey;
        var b = new SourceBuilder(AiSourceKind.Fooocus, "RuinedFooocus");
        b.Use(context, tag);

        b.Prompt(LenientJson.Str(root, "Prompt"), AiPromptRole.Positive, string.Empty, key);
        b.Prompt(LenientJson.Str(root, "Negative"), AiPromptRole.Negative, string.Empty, key);

        b.Param("base_model_name", LenientJson.Str(root, "base_model_name"), key);
        b.Param("sampler_name", LenientJson.Str(root, "sampler_name"), key);
        b.Param("scheduler", LenientJson.Str(root, "scheduler"), key);
        b.Param("steps", LenientJson.Str(root, "steps"), key);
        b.Param("cfg", LenientJson.Str(root, "cfg"), key);
        b.Param("seed", LenientJson.Str(root, "seed"), key);
        if (LenientJson.Str(root, "width") is { } w && LenientJson.Str(root, "height") is { } h) b.Param("Size", $"{w}x{h}", key);
        b.Param("denoise", LenientJson.Str(root, "denoise"), key);
        b.Param("clip_skip", LenientJson.Str(root, "clip_skip"), key);
        b.Param("Start step", LenientJson.Str(root, "start_step"), key);

        b.Model(AiModelKind.Checkpoint, LenientJson.Str(root, "base_model_name"), null, LenientJson.Str(root, "base_model_hash"), key);

        // loras: [[hash, "0.5 - name"], ...]
        if (LenientJson.Prop(root, "loras") is { ValueKind: JsonValueKind.Array } loras)
        {
            foreach (var l in loras.EnumerateArray().Where(l => l.ValueKind == JsonValueKind.Array && l.GetArrayLength() >= 2))
            {
                var hash = AiTagSet.ToText(l[0]);
                var m = WeightAndName().Match(AiTagSet.ToText(l[1]));
                if (m.Success) b.Model(AiModelKind.Lora, m.Groups[2].Value, m.Groups[1].Value, hash, key);
                else b.Model(AiModelKind.Lora, AiTagSet.ToText(l[1]), null, hash, key);
            }
        }

        return b.Build();
    }


    private static string? Size(string? resolution) =>
        resolution is not null && Resolution().Match(resolution) is { Success: true } m ? $"{m.Groups[1].Value}x{m.Groups[2].Value}" : resolution;


    private static string? JoinList(JsonElement? list) =>
        list is { ValueKind: JsonValueKind.Array } a ? string.Join(", ", a.EnumerateArray().Select(AiTagSet.ToText).Where(s => s.Length > 0)) : null;

    #endregion // Private methods
}
