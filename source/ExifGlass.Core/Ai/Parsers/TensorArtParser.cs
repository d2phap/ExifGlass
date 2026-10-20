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
/// Tensor.art: generation_data JSON (community-documented keys) and a ComfyUI API prompt with its own loader nodes.
/// </summary>
public sealed class TensorArtParser : IAiParser
{
    #region Public methods

    public void Parse(AiParseContext context)
    {
        var data = context.Free("Generation_data", "PNG");
        if (data is null) return;

        using var doc = LenientJson.TryParse(data.Text, out var warning);
        if (doc?.RootElement is not { ValueKind: JsonValueKind.Object } root) return;

        var key = data.TableKey;
        var b = new SourceBuilder(AiSourceKind.TensorArt, "Tensor.art");
        b.Use(context, data);
        if (warning is not null) b.Warn(warning);

        b.Prompt(LenientJson.Str(root, "prompt"), AiPromptRole.Positive, string.Empty, key);
        b.Prompt(LenientJson.Str(root, "negativePrompt"), AiPromptRole.Negative, string.Empty, key);

        var baseModel = LenientJson.Prop(root, "baseModel") is { } bm ? LenientJson.Str(bm, "label") ?? LenientJson.Str(bm, "modelFileName") : null;
        b.Param("Model", baseModel, key);
        b.Param("ksamplerName", LenientJson.Str(root, "ksamplerName"), key);
        b.Param("schedule", LenientJson.Str(root, "schedule"), key);
        b.Param("steps", LenientJson.Str(root, "steps"), key);
        b.Param("cfgScale", LenientJson.Str(root, "cfgScale"), key);
        b.Param("seed", LenientJson.Str(root, "seed"), key);
        if (LenientJson.Str(root, "width") is { } w && LenientJson.Str(root, "height") is { } h) b.Param("Size", $"{w}x{h}", key);
        b.Param("clipSkip", LenientJson.Str(root, "clipSkip"), key);

        b.Model(AiModelKind.Checkpoint, baseModel, null, LenientJson.Prop(root, "baseModel") is { } id ? LenientJson.Str(id, "modelId") : null, key);
        if (LenientJson.Prop(root, "models") is { ValueKind: JsonValueKind.Array } models)
        {
            foreach (var m in models.EnumerateArray())
            {
                var kind = (LenientJson.Str(m, "type") ?? string.Empty).ToUpperInvariant() switch
                {
                    "LORA" or "LYCORIS" => AiModelKind.Lora,
                    "EMBEDDING" or "TEXTUALINVERSION" => AiModelKind.Embedding,
                    "VAE" => AiModelKind.Vae,
                    "CHECKPOINT" => AiModelKind.Checkpoint,
                    _ => AiModelKind.Other,
                };
                b.Model(kind, LenientJson.Str(m, "label") ?? LenientJson.Str(m, "modelFileName"), LenientJson.Str(m, "weight"), LenientJson.Str(m, "hash"), key);
            }
        }

        // the ComfyUI graph next to it: its summary, but the prompts above stay the source of truth
        if (context.Free("Prompt", "PNG") is { } prompt)
        {
            using var graphDoc = LenientJson.TryParse(prompt.Text, out _);
            if (graphDoc is not null && ComfyGraph.TryCreate(graphDoc.RootElement) is { } graph)
            {
                b.Use(context, prompt);
                b.Workflow(ComfyUiParser.SummarizeApi(graph, prompt.Text, prompt.TableKey));
            }
        }

        context.Sources.Add(b.Build());
    }

    #endregion // Public methods
}
