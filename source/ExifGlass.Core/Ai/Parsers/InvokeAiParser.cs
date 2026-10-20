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
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ExifGlass.Core.Ai.Parsers;

/// <summary>
/// InvokeAI 3 and later (invokeai_metadata, _workflow, _graph) and 2.3 and earlier (Dream, sd-metadata).
/// </summary>
public sealed partial class InvokeAiParser : IAiParser
{
    #region Public methods

    public void Parse(AiParseContext context)
    {
        var metadata = context.Free("Invokeai_metadata", "PNG");
        var workflow = context.Free("Invokeai_workflow", "PNG");
        var graph = context.Free("Invokeai_graph", "PNG");
        var dream = context.Free("Dream", "PNG");
        var legacy = context.Free("Sd-metadata", "PNG");

        if (metadata is not null || workflow is not null || graph is not null) context.Sources.Add(ParseCurrent(context, metadata, workflow, graph));
        else if ((dream is not null || legacy is not null) && ParseLegacy(context, dream, legacy) is { } source) context.Sources.Add(source);
    }

    #endregion // Public methods


    #region Private methods

    // "a castle on a hill" -s 50 -S 3357757885 -W 512 -H 512 -C 7.5 -A k_lms
    [GeneratedRegex(@"^\s*""((?:[^""\\]|\\.)*)""(.*)$", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex DreamPrompt();


    [GeneratedRegex(@"(?:^|\s)-([A-Za-z]{1,2})\s+([^\s-][^\s]*)", RegexOptions.CultureInvariant)]
    private static partial Regex DreamFlag();


    // the legacy negative prompt sits in [brackets] inside the prompt
    [GeneratedRegex(@"\[([^\]]*)\]", RegexOptions.CultureInvariant)]
    private static partial Regex BracketNegative();


    private static AiSourceInfo ParseCurrent(AiParseContext context, AiTag? metadata, AiTag? workflow, AiTag? graph)
    {
        var b = new SourceBuilder(AiSourceKind.InvokeAi, "InvokeAI");
        b.Use(context, metadata).Use(context, workflow).Use(context, graph);

        if (metadata is not null)
        {
            using var doc = LenientJson.TryParse(metadata.Text, out var warning);
            if (warning is not null) b.Warn(warning);
            if (doc?.RootElement is { ValueKind: JsonValueKind.Object } m) ReadMetadata(b, m, metadata.TableKey);
        }

        if (workflow is not null && Summarize(workflow, AiWorkflowFormat.InvokeAiWorkflow, "InvokeAI workflow", b) is { } w) b.Workflow(w);

        if (graph is not null)
        {
            if (Summarize(graph, AiWorkflowFormat.InvokeAiGraph, "InvokeAI graph", b) is { } g) b.Workflow(g);
            if (metadata is null) PromptsFromGraph(b, graph);
        }

        return b.Build();
    }


    private static void ReadMetadata(SourceBuilder b, JsonElement m, string key)
    {
        b.Version = LenientJson.Str(m, "app_version");
        b.Prompt(LenientJson.Str(m, "positive_prompt"), AiPromptRole.Positive, string.Empty, key);
        b.Prompt(LenientJson.Str(m, "negative_prompt"), AiPromptRole.Negative, string.Empty, key);
        b.Prompt(LenientJson.Str(m, "positive_style_prompt"), AiPromptRole.Positive, "Style", key);
        b.Prompt(LenientJson.Str(m, "negative_style_prompt"), AiPromptRole.Negative, "Style", key);

        var model = LenientJson.Prop(m, "model");
        var modelName = model is { } mo ? LenientJson.Str(mo, "name") ?? LenientJson.Str(mo, "model_name") : null;
        b.Param("Model", modelName, key);
        b.Model(AiModelKind.Checkpoint, modelName, null, model is { } mh ? LenientJson.Str(mh, "hash") : null, key);

        b.Param("scheduler", LenientJson.Str(m, "scheduler"), key);
        b.Param("steps", LenientJson.Str(m, "steps"), key);
        b.Param("cfg_scale", LenientJson.Str(m, "cfg_scale"), key);
        b.Param("seed", LenientJson.Str(m, "seed"), key);
        if (LenientJson.Str(m, "width") is { } w && LenientJson.Str(m, "height") is { } h) b.Param("Size", $"{w}x{h}", key);
        b.Param("denoise", LenientJson.Str(m, "strength"), key);
        b.Param("Generation mode", LenientJson.Str(m, "generation_mode"), key);
        b.Param("CFG rescale", LenientJson.Str(m, "cfg_rescale_multiplier"), key);
        b.Param("Random device", LenientJson.Str(m, "rand_device"), key);
        b.Param("Refiner steps", LenientJson.Str(m, "refiner_steps"), key);
        b.Param("Metadata version", LenientJson.Str(m, "metadata_version"), key);

        if (LenientJson.Prop(m, "vae") is { } vae) b.Model(AiModelKind.Vae, LenientJson.Str(vae, "name") ?? LenientJson.Str(vae, "model_name"), null, LenientJson.Str(vae, "hash"), key);
        if (LenientJson.Prop(m, "refiner_model") is { } refiner) b.Model(AiModelKind.Refiner, LenientJson.Str(refiner, "name") ?? LenientJson.Str(refiner, "model_name"), null, LenientJson.Str(refiner, "hash"), key);

        if (LenientJson.Prop(m, "loras") is { ValueKind: JsonValueKind.Array } loras)
        {
            foreach (var l in loras.EnumerateArray())
            {
                var lm = LenientJson.Prop(l, "model") ?? LenientJson.Prop(l, "lora");
                var name = lm is { } x ? LenientJson.Str(x, "name") ?? LenientJson.Str(x, "model_name") : null;
                b.Model(AiModelKind.Lora, name, LenientJson.Str(l, "weight"), lm is { } y ? LenientJson.Str(y, "hash") : null, key);
            }
        }

        if (LenientJson.Prop(m, "controlnets") is { ValueKind: JsonValueKind.Array } cns)
        {
            foreach (var c in cns.EnumerateArray())
            {
                var cm = LenientJson.Prop(c, "control_model");
                b.Model(AiModelKind.ControlNet, cm is { } x ? LenientJson.Str(x, "name") : null, LenientJson.Str(c, "control_weight"), null, key);
            }
        }
    }


    private static AiWorkflowSummary? Summarize(AiTag tag, AiWorkflowFormat format, string label, SourceBuilder b)
    {
        var json = tag.Text;
        using var doc = LenientJson.TryParse(json, out var warning);
        if (warning is not null) b.Warn(warning);
        if (doc?.RootElement is not { ValueKind: JsonValueKind.Object } root) return null;

        var nodes = LenientJson.Prop(root, "nodes") switch
        {
            { ValueKind: JsonValueKind.Array } a => a.GetArrayLength(),
            { ValueKind: JsonValueKind.Object } o => o.EnumerateObject().Count(),
            _ => 0,
        };
        var edges = LenientJson.Prop(root, "edges") is { ValueKind: JsonValueKind.Array } e ? e.GetArrayLength() : 0;
        var version = LenientJson.Prop(root, "meta") is { } meta ? LenientJson.Str(meta, "version") : null;

        return new AiWorkflowSummary(format, version is null ? label : $"{label} {version}", nodes, edges, 0, 0, 0, Encoding.UTF8.GetByteCount(json), json, tag.TableKey);
    }


    // a graph-only image: prompts from compel nodes, the role from the edge that feeds a denoise node
    private static void PromptsFromGraph(SourceBuilder b, AiTag tag)
    {
        using var doc = LenientJson.TryParse(tag.Text, out _);
        if (doc?.RootElement is not { ValueKind: JsonValueKind.Object } root || LenientJson.Prop(root, "nodes") is not { ValueKind: JsonValueKind.Object } nodes) return;

        var roles = new Dictionary<string, AiPromptRole>(StringComparer.Ordinal);
        if (LenientJson.Prop(root, "edges") is { ValueKind: JsonValueKind.Array } edges)
        {
            foreach (var e in edges.EnumerateArray())
            {
                var source = LenientJson.Prop(e, "source") is { } s ? LenientJson.Str(s, "node_id") : null;
                var field = LenientJson.Prop(e, "destination") is { } d ? LenientJson.Str(d, "field") : null;
                if (source is null || field is null) continue;
                if (field.StartsWith("negative", StringComparison.Ordinal)) roles[source] = AiPromptRole.Negative;
                else if (field.StartsWith("positive", StringComparison.Ordinal)) roles.TryAdd(source, AiPromptRole.Positive);
            }
        }

        foreach (var n in nodes.EnumerateObject())
        {
            var text = LenientJson.Str(n.Value, "prompt");
            if (text is null) continue;
            b.Prompt(text, roles.GetValueOrDefault(n.Name, AiPromptRole.Positive), string.Empty, tag.TableKey);
        }
    }


    // null when neither chunk reads as InvokeAI's, so the Unknown AI fallback still shows them
    private static AiSourceInfo? ParseLegacy(AiParseContext context, AiTag? dream, AiTag? legacy)
    {
        var b = new SourceBuilder(AiSourceKind.InvokeAi, "InvokeAI");

        if (legacy is not null)
        {
            using var doc = LenientJson.TryParse(legacy.Text, out var warning);
            if (warning is not null) b.Warn(warning);
            if (doc?.RootElement is { ValueKind: JsonValueKind.Object } m)
            {
                b.Version = LenientJson.Str(m, "app_version");
                b.Model(AiModelKind.Checkpoint, LenientJson.Str(m, "model_weights"), null, LenientJson.Str(m, "model_hash"), legacy.TableKey);
                b.Param("Model", LenientJson.Str(m, "model_weights"), legacy.TableKey);

                if (LenientJson.Prop(m, "image") is { } image)
                {
                    if (LenientJson.Prop(image, "prompt") is { ValueKind: JsonValueKind.Array } prompts)
                    {
                        foreach (var p in prompts.EnumerateArray()) AddLegacyPrompt(b, LenientJson.Str(p, "prompt"), legacy.TableKey);
                    }
                    else
                    {
                        AddLegacyPrompt(b, LenientJson.Str(image, "prompt"), legacy.TableKey);
                    }

                    b.Param("sampler", LenientJson.Str(image, "sampler"), legacy.TableKey);
                    b.Param("steps", LenientJson.Str(image, "steps"), legacy.TableKey);
                    b.Param("cfg_scale", LenientJson.Str(image, "cfg_scale"), legacy.TableKey);
                    b.Param("seed", LenientJson.Str(image, "seed"), legacy.TableKey);
                    if (LenientJson.Str(image, "width") is { } w && LenientJson.Str(image, "height") is { } h) b.Param("Size", $"{w}x{h}", legacy.TableKey);
                    b.Param("Type", LenientJson.Str(image, "type"), legacy.TableKey);
                }
            }
        }

        if (dream is not null && DreamPrompt().Match(dream.Text) is { Success: true } dm)
        {
            AddLegacyPrompt(b, Regex.Unescape(dm.Groups[1].Value), dream.TableKey);
            foreach (Match f in DreamFlag().Matches(dm.Groups[2].Value))
            {
                var v = f.Groups[2].Value;
                switch (f.Groups[1].Value)
                {
                    case "s": b.Param("steps", v, dream.TableKey); break;
                    case "S": b.Param("seed", v, dream.TableKey); break;
                    case "C": b.Param("cfg_scale", v, dream.TableKey); break;
                    case "A": b.Param("sampler", v, dream.TableKey); break;
                    case "f": b.Param("denoise", v, dream.TableKey); break;
                    default: break;
                }
            }

            var width = DreamFlag().Matches(dm.Groups[2].Value).FirstOrDefault(x => x.Groups[1].Value == "W")?.Groups[2].Value;
            var height = DreamFlag().Matches(dm.Groups[2].Value).FirstOrDefault(x => x.Groups[1].Value == "H")?.Groups[2].Value;
            if (width is not null && height is not null) b.Param("Size", $"{width}x{height}", dream.TableKey);
        }

        if (!b.HasContent) return null;

        b.Use(context, legacy).Use(context, dream);
        return b.Build();
    }


    private static void AddLegacyPrompt(SourceBuilder b, string? text, string key)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var negatives = BracketNegative().Matches(text).Select(m => m.Groups[1].Value.Trim()).Where(s => s.Length > 0).ToList();
        b.Prompt(BracketNegative().Replace(text, string.Empty).Trim(), AiPromptRole.Positive, string.Empty, key);
        if (negatives.Count > 0) b.Prompt(string.Join(", ", negatives), AiPromptRole.Negative, string.Empty, key);
    }

    #endregion // Private methods
}
