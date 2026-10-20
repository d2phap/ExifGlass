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
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ExifGlass.Core.Ai.Parsers;

/// <summary>
/// ComfyUI: the API prompt first (prompts, samplers, models), then the UI workflow (summary, saved as embedded).
/// </summary>
public sealed class ComfyUiParser : IAiParser
{
    #region Fields and constants

    private const string PROMPT_PREFIX = "prompt:";
    private const string WORKFLOW_PREFIX = "workflow:";

    #endregion // Fields and constants


    #region Public methods

    public void Parse(AiParseContext context)
    {
        var (promptTag, promptJson, promptDoc) = Find(context, "Prompt", PROMPT_PREFIX, IsApiPrompt);
        using var promptScope = promptDoc;
        var (workflowTag, workflowJson, workflowDoc) = Find(context, "Workflow", WORKFLOW_PREFIX, IsUiWorkflow);
        using var workflowScope = workflowDoc;
        if (promptTag is null && workflowTag is null) return;

        var b = new SourceBuilder(AiSourceKind.ComfyUi, "ComfyUI");
        b.Use(context, promptTag).Use(context, workflowTag);

        if (workflowDoc is not null && SummarizeUi(workflowDoc.RootElement, workflowJson!, workflowTag!.TableKey) is { } summary) b.Workflow(summary);

        if (promptDoc is not null && ComfyGraph.TryCreate(promptDoc.RootElement) is { } graph)
        {
            Analyze(b, graph, promptTag!.TableKey, context.CancellationToken);
            b.Workflow(SummarizeApi(graph, promptJson!, promptTag.TableKey));
        }

        if (context.Tags.Get("PNG", "AnimationFrames") is not null && promptTag?.Key.Group0 == "PNG")
        {
            b.Warn("An animated PNG keeps its workflow in comf chunks that ExifTool can't remove");
        }

        context.Sources.Add(b.Build());
    }


    /// <summary>
    /// Reads prompts, samplers and models from an API prompt graph into a source.
    /// </summary>
    internal static void Analyze(SourceBuilder b, ComfyGraph graph, string? key, CancellationToken cancellationToken)
    {
        var samplers = graph.Nodes.Where(ComfyGraph.IsSampler).ToList();
        var depth = new Dictionary<string, int>(StringComparer.Ordinal);
        samplers = [.. samplers.OrderBy(s => LatentDepth(graph, s, depth, 0)).ThenBy(s => SortKey(s.Id), StringComparer.Ordinal)];

        AddModels(b, graph, key);

        for (var i = 0; i < samplers.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (parameters, prompts) = ReadSampler(graph, samplers[i]);
            if (i == 0)
            {
                foreach (var p in prompts) b.Prompt(p.Text, p.Role, p.Label, key);
                foreach (var (k, v) in parameters) b.Param(k, v, key);
                continue;
            }

            var title = samplers[i].Title is { Length: > 0 } t && t != samplers[i].ClassType ? t : $"Pass {i + 1}";
            b.Pass(new AiSamplerPass(title,
                AiParameterNames.Build(parameters.Select(p => (p.Key, (string?)p.Value, key))),
                [.. prompts.Select(p => p with { SourceKey = key })]));
        }

        // no sampler: every encoder's text, as found
        if (samplers.Count == 0)
        {
            foreach (var node in graph.Nodes.Where(ComfyGraph.IsTextEncoder).OrderBy(n => SortKey(n.Id), StringComparer.Ordinal))
            {
                b.Prompt(graph.Text(node, "text") ?? graph.Text(node, "prompt"), AiPromptRole.Positive, string.Empty, key);
            }
        }

        // a sampler whose model chain didn't reach a loader still shows the first model of the graph
        if (graph.Nodes.OrderBy(n => SortKey(n.Id), StringComparer.Ordinal).FirstOrDefault(n => n.InputText("ckpt_name") is not null || n.InputText("unet_name") is not null) is { } loader)
        {
            b.Param("Model", loader.InputText("ckpt_name") ?? loader.InputText("unet_name"), key);
        }
    }


    /// <summary>
    /// The workflow card's summary of a UI workflow (schema 0.4 or 1.0 with subgraphs).
    /// </summary>
    internal static AiWorkflowSummary? SummarizeUi(JsonElement root, string json, string? key)
    {
        if (LenientJson.Prop(root, "nodes") is not { ValueKind: JsonValueKind.Array } nodes) return null;

        int count = 0, samplers = 0, models = 0, links = 0, subgraphs = 0;
        void Count(JsonElement list)
        {
            foreach (var n in list.EnumerateArray())
            {
                count++;
                var type = LenientJson.Str(n, "type") ?? string.Empty;
                if (type.Contains("Sampler", StringComparison.OrdinalIgnoreCase) && !type.Contains("Select", StringComparison.OrdinalIgnoreCase)) samplers++;
                if (type.Contains("Loader", StringComparison.OrdinalIgnoreCase)) models++;
            }
        }

        Count(nodes);
        if (LenientJson.Prop(root, "links") is { ValueKind: JsonValueKind.Array } l) links += l.GetArrayLength();

        if (LenientJson.Prop(root, "definitions") is { } defs && LenientJson.Prop(defs, "subgraphs") is { ValueKind: JsonValueKind.Array } subs)
        {
            foreach (var s in subs.EnumerateArray())
            {
                subgraphs++;
                if (LenientJson.Prop(s, "nodes") is { ValueKind: JsonValueKind.Array } sn) Count(sn);
                if (LenientJson.Prop(s, "links") is { ValueKind: JsonValueKind.Array } sl) links += sl.GetArrayLength();
            }
        }

        var version = LenientJson.Str(root, "version");
        var label = version is null ? "ComfyUI workflow" : $"ComfyUI workflow {FormatVersion(version)}";
        return new AiWorkflowSummary(AiWorkflowFormat.ComfyUiWorkflow, label, count, links, samplers, models, subgraphs, Encoding.UTF8.GetByteCount(json), json, key);
    }


    /// <summary>
    /// The summary of an API prompt; subgraph nodes have ids such as "57:12".
    /// </summary>
    internal static AiWorkflowSummary SummarizeApi(ComfyGraph graph, string json, string? key)
    {
        var links = graph.Nodes.Sum(n => n.AllInputs.Count(p => ComfyGraph.IsLink(p.Value, out _, out _)));
        var samplers = graph.Nodes.Count(ComfyGraph.IsSampler);
        var models = graph.Nodes.Count(n => n.ClassType.Contains("Loader", StringComparison.OrdinalIgnoreCase));
        var subgraphs = graph.Nodes.Where(n => n.Id.Contains(':', StringComparison.Ordinal)).Select(n => n.Id[..n.Id.IndexOf(':')]).Distinct().Count();

        return new AiWorkflowSummary(AiWorkflowFormat.ComfyUiApi, "ComfyUI API prompt", graph.Count, links, samplers, models, subgraphs, Encoding.UTF8.GetByteCount(json), json, key);
    }

    #endregion // Public methods


    #region Private methods

    private static bool IsApiPrompt(JsonElement root) => ComfyGraph.TryCreate(root) is not null;


    private static bool IsUiWorkflow(JsonElement root) => LenientJson.Prop(root, "nodes") is { ValueKind: JsonValueKind.Array };


    // Prompt/Workflow in any container, or EXIF IFD0 with "prompt:"/"workflow:" prefixes (animated WebP/AVIF); parsed once, the caller disposes
    private static (AiTag? Tag, string? Json, JsonDocument? Doc) Find(AiParseContext context, string name, string prefix, Func<JsonElement, bool> shape)
    {
        foreach (var tag in context.FreeWithName(name))
        {
            if (TryShape(tag.Text, shape) is { } doc) return (tag, tag.Text, doc);
        }

        foreach (var tag in context.Tags.InGroup("IFD0"))
        {
            if (context.IsClaimed(tag) || !tag.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

            var json = tag.Text[prefix.Length..];
            if (TryShape(json, shape) is { } doc) return (tag, json, doc);
        }

        return (null, null, null);
    }


    private static JsonDocument? TryShape(string text, Func<JsonElement, bool> shape)
    {
        if (!LenientJson.LooksLikeObject(text)) return null;

        var doc = LenientJson.TryParse(text, out _);
        if (doc is not null && shape(doc.RootElement)) return doc;

        doc?.Dispose();
        return null;
    }


    private static string FormatVersion(string version) =>
        double.TryParse(version, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v == Math.Floor(v) ? v.ToString("0.0", CultureInfo.InvariantCulture) : version;


    // numeric ids sort as numbers: "3" before "10"; subgraph ids "57:12" after their parent
    private static string SortKey(string id) => string.Join(':', id.Split(':').Select(p => p.PadLeft(10, '0')));


    private static int LatentDepth(ComfyGraph graph, ComfyNode sampler, Dictionary<string, int> memo, int guard)
    {
        if (memo.TryGetValue(sampler.Id, out var d)) return d;
        if (guard > 16) return 0;

        var latent = sampler.Input("latent_image") ?? sampler.Input("latent");
        var previous = latent is { } l ? graph.Upstream(l, ComfyGraph.IsSampler) : null;
        d = previous is null ? 0 : 1 + LatentDepth(graph, previous, memo, guard + 1);
        memo[sampler.Id] = d;
        return d;
    }


    private static (List<(string Key, string Value)> Parameters, List<AiPrompt> Prompts) ReadSampler(ComfyGraph graph, ComfyNode s)
    {
        var parameters = new List<(string, string)>();
        var prompts = new List<AiPrompt>();
        void Add(string k, string? v)
        {
            if (!string.IsNullOrWhiteSpace(v)) parameters.Add((k, v));
        }

        JsonElement? positive = s.Input("positive"), negative = s.Input("negative"), model = s.Input("model");
        var noise = graph.Linked(s, "noise", out _);
        var guider = graph.Linked(s, "guider", out _);
        var samplerSelect = graph.Linked(s, "sampler", out _);
        var sigmas = graph.Linked(s, "sigmas", out _);

        if (guider is not null)
        {
            positive ??= guider.Input("positive") ?? guider.Input("conditioning");
            negative ??= guider.Input("negative");
            model ??= guider.Input("model");
            Add("cfg", guider.InputText("cfg"));
        }

        Add("seed", graph.Text(s, "seed") ?? graph.Text(s, "noise_seed") ?? (noise is null ? null : graph.Text(noise, "noise_seed") ?? graph.Text(noise, "seed")));
        Add("steps", graph.Text(s, "steps") ?? (sigmas is null ? null : graph.Text(sigmas, "steps")));
        Add("cfg", graph.Text(s, "cfg"));
        Add("sampler_name", graph.Text(s, "sampler_name") ?? (samplerSelect is null ? null : graph.Text(samplerSelect, "sampler_name")));
        Add("scheduler", graph.Text(s, "scheduler") ?? (sigmas is null ? null : graph.Text(sigmas, "scheduler")));
        Add("denoise", graph.Text(s, "denoise") ?? (sigmas is null ? null : graph.Text(sigmas, "denoise")));
        Add("start_at_step", graph.Text(s, "start_at_step"));
        Add("end_at_step", graph.Text(s, "end_at_step"));

        if (positive is { } pos)
        {
            foreach (var (text, label, _) in graph.Conditioning(pos)) prompts.Add(new AiPrompt(text, AiPromptRole.Positive, label));
            if (graph.Upstream(pos, n => n.ClassType.Contains("FluxGuidance", StringComparison.OrdinalIgnoreCase)) is { } fg) Add("guidance", graph.Text(fg, "guidance"));
            else if (graph.Upstream(pos, n => n.Input("guidance") is not null && ComfyGraph.IsTextEncoder(n)) is { } enc) Add("guidance", graph.Text(enc, "guidance"));
        }

        if (negative is { } neg)
        {
            foreach (var (text, label, _) in graph.Conditioning(neg)) prompts.Add(new AiPrompt(text, AiPromptRole.Negative, label));
        }

        if (model is { } m && graph.Upstream(m, n => n.InputText("ckpt_name") is not null || n.InputText("unet_name") is not null) is { } loader)
        {
            Add("Model", loader.InputText("ckpt_name") ?? loader.InputText("unet_name"));
        }

        var latent = s.Input("latent_image") ?? s.Input("latent");
        if (latent is { } lat && graph.Upstream(lat, n => n.Input("width") is not null && n.Input("height") is not null) is { } empty)
        {
            Add("Size", $"{graph.Text(empty, "width")}x{graph.Text(empty, "height")}");
        }

        return (parameters, prompts);
    }


    private static void AddModels(SourceBuilder b, ComfyGraph graph, string? key)
    {
        foreach (var n in graph.Nodes.OrderBy(n => SortKey(n.Id), StringComparer.Ordinal))
        {
            b.Model(AiModelKind.Checkpoint, graph.Text(n, "ckpt_name"), null, null, key);
            b.Model(AiModelKind.Unet, graph.Text(n, "unet_name"), null, null, key);
            b.Model(AiModelKind.TextEncoder, graph.Text(n, "clip_name"), null, null, key);
            b.Model(AiModelKind.TextEncoder, graph.Text(n, "clip_name1"), null, null, key);
            b.Model(AiModelKind.TextEncoder, graph.Text(n, "clip_name2"), null, null, key);
            b.Model(AiModelKind.TextEncoder, graph.Text(n, "clip_name3"), null, null, key);
            b.Model(AiModelKind.Vae, graph.Text(n, "vae_name"), null, null, key);
            b.Model(AiModelKind.ControlNet, graph.Text(n, "control_net_name"), graph.Text(n, "strength"), null, key);
            b.Model(AiModelKind.Lora, graph.Text(n, "lora_name"), graph.Text(n, "strength_model"), null, key);

            if (n.ClassType.Contains("Upscale", StringComparison.OrdinalIgnoreCase)) b.Model(AiModelKind.Upscaler, graph.Text(n, "model_name"), null, null, key);
            if (ComfyGraph.IsTextEncoder(n)) b.ModelsFromPrompt(graph.Text(n, "text"), key);
        }
    }

    #endregion // Private methods
}
