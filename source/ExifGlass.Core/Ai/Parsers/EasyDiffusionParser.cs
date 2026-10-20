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
/// Easy Diffusion: one PNG chunk per field, or one JSON UserComment; its "prompt" is plain text, unlike ComfyUI's.
/// </summary>
public sealed class EasyDiffusionParser : IAiParser
{
    #region Fields and constants

    private const string MODEL_KEY = "use_stable_diffusion_model";

    private static readonly string[] PROMPT_KEYS = ["prompt", "negative_prompt"];

    #endregion // Fields and constants


    #region Public methods

    public void Parse(AiParseContext context)
    {
        if (context.Free("Use_stable_diffusion_model", "PNG") is not null)
        {
            ParseChunks(context);
            return;
        }

        if (context.Free("UserComment", "ExifIFD") is { } comment && comment.Text.Contains(MODEL_KEY, StringComparison.Ordinal))
        {
            using var doc = LenientJson.TryParse(comment.Text, out _);
            if (doc?.RootElement is { ValueKind: JsonValueKind.Object } root)
            {
                var pairs = root.EnumerateObject().Select(p => (p.Name, AiTagSet.ToText(p.Value))).ToList();
                var b = Build(pairs, comment.TableKey);
                b.Use(context, comment);
                context.Sources.Add(b.Build());
            }
        }
    }

    #endregion // Public methods


    #region Private methods

    private static void ParseChunks(AiParseContext context)
    {
        var tags = context.Tags.InGroup("PNG").Where(t => !context.IsClaimed(t) && IsField(t.Name)).ToList();
        var b = Build(tags.Select(t => (t.Name.ToLowerInvariant(), t.Text)).ToList(), null);
        foreach (var t in tags) b.Use(context, t);
        context.Sources.Add(b.Build());
    }


    // the PNG text chunks Easy Diffusion writes, as ExifTool names them
    private static bool IsField(string name) => name.ToLowerInvariant() is "prompt" or "negative_prompt" or "seed" or MODEL_KEY or "sampler_name"
        or "num_inference_steps" or "guidance_scale" or "width" or "height" or "use_vae_model" or "clip_skip" or "use_lora_model" or "lora_alpha"
        or "use_hypernetwork_model" or "hypernetwork_strength" or "prompt_strength" or "use_upscale" or "use_face_correction" or "tiling";


    private static SourceBuilder Build(List<(string Key, string Value)> pairs, string? key)
    {
        var b = new SourceBuilder(AiSourceKind.EasyDiffusion, "Easy Diffusion");
        string? Get(string k) => pairs.FirstOrDefault(p => p.Key.Equals(k, StringComparison.OrdinalIgnoreCase)).Value;

        b.Prompt(Get("prompt"), AiPromptRole.Positive, string.Empty, key);
        b.Prompt(Get("negative_prompt"), AiPromptRole.Negative, string.Empty, key);

        if (Get("width") is { Length: > 0 } w && Get("height") is { Length: > 0 } h) b.Param("Size", $"{w}x{h}", key);
        foreach (var (k, v) in pairs)
        {
            if (PROMPT_KEYS.Contains(k, StringComparer.OrdinalIgnoreCase) || k is "width" or "height") continue;
            b.Param(k, v, key);
        }

        b.Model(AiModelKind.Checkpoint, Get(MODEL_KEY), null, null, key);
        b.Model(AiModelKind.Vae, Get("use_vae_model"), null, null, key);
        b.Model(AiModelKind.Lora, Get("use_lora_model"), Get("lora_alpha"), null, key);
        b.Model(AiModelKind.Other, Get("use_hypernetwork_model"), Get("hypernetwork_strength"), null, key);
        b.Model(AiModelKind.Upscaler, Get("use_upscale"), null, null, key);
        return b;
    }

    #endregion // Private methods
}
