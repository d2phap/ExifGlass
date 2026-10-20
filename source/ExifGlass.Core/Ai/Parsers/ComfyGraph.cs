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
using System.Text.Json;

namespace ExifGlass.Core.Ai.Parsers;

/// <summary>
/// One node of a ComfyUI API prompt: {"id": {"class_type", "inputs", "_meta": {"title"}}}.
/// </summary>
internal sealed record ComfyNode(string Id, string ClassType, string? Title, JsonElement Inputs)
{
    #region Public methods

    public JsonElement? Input(string name) => LenientJson.Prop(Inputs, name);

    public string? InputText(string name) => LenientJson.Str(Inputs, name);

    public IEnumerable<JsonProperty> AllInputs => Inputs.ValueKind == JsonValueKind.Object ? Inputs.EnumerateObject() : [];

    #endregion // Public methods
}


/// <summary>
/// A ComfyUI API prompt graph and the walks the parser needs: links, text through string nodes, conditioning back from a sampler.
/// </summary>
internal sealed class ComfyGraph
{
    #region Fields and constants

    private const int MAX_DEPTH = 32;

    private static readonly string[] TEXT_INPUTS = ["text", "value", "string", "prompt", "text_g", "text_l", "t5xxl", "clip_l", "clip_g", "wildcard_text", "populated_text"];

    private readonly Dictionary<string, ComfyNode> _nodes;

    #endregion // Fields and constants


    #region Constructors

    private ComfyGraph(Dictionary<string, ComfyNode> nodes) => _nodes = nodes;

    #endregion // Constructors


    #region Properties

    public IReadOnlyCollection<ComfyNode> Nodes => _nodes.Values;

    public int Count => _nodes.Count;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Reads an API prompt; null when the JSON isn't one (an Easy Diffusion "prompt" is plain text, a UI workflow has "nodes").
    /// </summary>
    public static ComfyGraph? TryCreate(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) return null;

        var nodes = new Dictionary<string, ComfyNode>(StringComparer.Ordinal);
        foreach (var p in root.EnumerateObject())
        {
            if (p.Value.ValueKind != JsonValueKind.Object || !p.Value.TryGetProperty("class_type", out var type) || type.ValueKind != JsonValueKind.String) continue;

            var inputs = p.Value.TryGetProperty("inputs", out var i) ? i : default;
            string? title = p.Value.TryGetProperty("_meta", out var meta) ? LenientJson.Str(meta, "title") : null;
            nodes[p.Name] = new ComfyNode(p.Name, type.GetString()!, title, inputs);
        }

        return nodes.Count == 0 ? null : new ComfyGraph(nodes);
    }


    public ComfyNode? Get(string id) => _nodes.GetValueOrDefault(id);


    /// <summary>
    /// True for a link input: ["node id", output slot].
    /// </summary>
    public static bool IsLink(JsonElement value, out string id, out int slot)
    {
        id = string.Empty;
        slot = 0;
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != 2) return false;

        var first = value[0];
        var second = value[1];
        if (second.ValueKind != JsonValueKind.Number || !second.TryGetInt32(out slot)) return false;

        id = first.ValueKind switch
        {
            JsonValueKind.String => first.GetString()!,
            JsonValueKind.Number => first.GetRawText(),
            _ => string.Empty,
        };

        return id.Length > 0;
    }


    /// <summary>
    /// The node a link input points to.
    /// </summary>
    public ComfyNode? Linked(ComfyNode node, string input, out int slot)
    {
        slot = 0;
        return node.Input(input) is { } v && IsLink(v, out var id, out slot) ? Get(id) : null;
    }


    /// <summary>
    /// An input's value as text, following links to string and primitive nodes.
    /// </summary>
    public string? Text(ComfyNode node, string input, int depth = 0)
    {
        if (node.Input(input) is not { } v) return null;
        return ResolveText(v, depth);
    }


    /// <summary>
    /// An input's value as text, following links (number and string primitives, string concatenation).
    /// </summary>
    public string? ResolveText(JsonElement value, int depth = 0)
    {
        if (depth > MAX_DEPTH) return null;

        if (!IsLink(value, out var id, out _))
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => AiTagSet.ToText(value),
                _ => null,
            };
        }

        if (Get(id) is not { } source) return null;

        if (source.ClassType.Contains("Concat", StringComparison.OrdinalIgnoreCase))
        {
            var parts = source.AllInputs.Where(p => p.Name.StartsWith("string", StringComparison.OrdinalIgnoreCase) || p.Name.StartsWith("text", StringComparison.OrdinalIgnoreCase))
                .Select(p => ResolveText(p.Value, depth + 1)).Where(s => !string.IsNullOrEmpty(s));
            var delimiter = source.InputText("delimiter") ?? string.Empty;
            return string.Join(delimiter, parts);
        }

        foreach (var name in TEXT_INPUTS)
        {
            if (source.Input(name) is { } inner && ResolveText(inner, depth + 1) is { } text) return text;
        }

        return null;
    }


    /// <summary>
    /// The prompts that reach a conditioning input, traced back through guidance, combine, area and ControlNet nodes.
    /// </summary>
    public List<(string Text, string Label, string NodeId)> Conditioning(JsonElement input, int depth = 0, string region = "")
    {
        var result = new List<(string, string, string)>();
        if (depth > MAX_DEPTH || !IsLink(input, out var id, out var slot) || Get(id) is not { } node) return result;

        if (IsTextEncoder(node))
        {
            foreach (var (text, label) in EncoderTexts(node))
            {
                result.Add((text, Join(region, label), node.Id));
            }

            return result;
        }

        if (node.ClassType.Contains("ZeroOut", StringComparison.OrdinalIgnoreCase)) return result;

        var isArea = node.ClassType.StartsWith("ConditioningSet", StringComparison.OrdinalIgnoreCase);
        var nextRegion = isArea ? Join(region, node.Title is { Length: > 0 } t && t != node.ClassType ? t : "Region") : region;

        // a node with both positive and negative inputs (ControlNetApplyAdvanced) passes slot 0 on as positive, slot 1 as negative
        if (node.Input("positive") is { } pos && node.Input("negative") is { } neg)
        {
            result.AddRange(Conditioning(slot == 1 ? neg : pos, depth + 1, nextRegion));
            return result;
        }

        foreach (var p in node.AllInputs)
        {
            if (p.Name.Contains("conditioning", StringComparison.OrdinalIgnoreCase) || p.Name is "positive" or "cond")
            {
                result.AddRange(Conditioning(p.Value, depth + 1, nextRegion));
            }
        }

        return result;
    }


    /// <summary>
    /// The first node of a class reachable from an input, walking every link (for the latent size and guidance).
    /// </summary>
    public ComfyNode? Upstream(JsonElement input, Func<ComfyNode, bool> match, int depth = 0)
    {
        if (depth > MAX_DEPTH || !IsLink(input, out var id, out _) || Get(id) is not { } node) return null;
        if (match(node)) return node;

        foreach (var p in node.AllInputs)
        {
            if (Upstream(p.Value, match, depth + 1) is { } found) return found;
        }

        return null;
    }


    public static bool IsTextEncoder(ComfyNode node) =>
        node.ClassType.Contains("TextEncode", StringComparison.OrdinalIgnoreCase)
        || node.ClassType.Contains("PromptEncode", StringComparison.OrdinalIgnoreCase);


    public static bool IsSampler(ComfyNode node) =>
        node.ClassType.Contains("Sampler", StringComparison.OrdinalIgnoreCase)
        && !node.ClassType.Contains("Select", StringComparison.OrdinalIgnoreCase)
        && (node.Input("positive") is not null || node.Input("guider") is not null);


    public static double? Number(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;

    #endregion // Public methods


    #region Private methods

    private static string Join(string a, string b) => a.Length == 0 ? b : b.Length == 0 ? a : a + " · " + b;


    // CLIPTextEncode text; SDXL text_g/text_l; Flux clip_l/t5xxl; SD3 clip_l/clip_g/t5xxl; Qwen prompt
    private IEnumerable<(string Text, string Label)> EncoderTexts(ComfyNode node)
    {
        var texts = new List<(string Name, string Text)>();
        foreach (var name in TEXT_INPUTS)
        {
            if (Text(node, name) is { Length: > 0 } t && !texts.Any(x => x.Text == t)) texts.Add((name, t));
        }

        if (texts.Count == 1) return [(texts[0].Text, string.Empty)];
        return texts.Select(x => (x.Text, x.Name switch
        {
            "text_g" or "clip_g" => "G",
            "text_l" or "clip_l" => "L",
            "t5xxl" => "T5",
            _ => string.Empty,
        }));
    }

    #endregion // Private methods
}
