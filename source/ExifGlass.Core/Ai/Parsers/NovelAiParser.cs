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
using System.Text.RegularExpressions;

namespace ExifGlass.Core.Ai.Parsers;

/// <summary>
/// NovelAI PNG text chunks; Comment is read as NovelAI's JSON only when Software is NovelAI.
/// </summary>
public sealed partial class NovelAiParser : IAiParser
{
    #region Public methods

    public void Parse(AiParseContext context)
    {
        var software = context.Free("Software", "PNG");
        if (software is null || !software.Text.StartsWith("NovelAI", StringComparison.OrdinalIgnoreCase)) return;

        var title = context.Free("Title", "PNG");
        var description = context.Free("Description", "PNG");
        var source = context.Free("Source", "PNG");
        var time = context.Free("GenerationTime", "PNG");
        var comment = context.Free("Comment", "PNG");

        var b = new SourceBuilder(AiSourceKind.NovelAi, "NovelAI");
        b.Use(context, software).Use(context, title).Use(context, description).Use(context, source).Use(context, time).Use(context, comment);

        if (source is not null)
        {
            var (model, hash) = SplitSource(source.Text);
            b.Model(AiModelKind.Checkpoint, model, null, hash, source.TableKey);
            b.Param("Model", model, source.TableKey);
        }

        using var doc = comment is null ? null : LenientJson.TryParse(comment.Text, out _);
        if (doc?.RootElement is { ValueKind: JsonValueKind.Object } c)
        {
            var key = comment!.TableKey;
            var v4 = LenientJson.Prop(c, "v4_prompt") is { } p4 ? LenientJson.Prop(p4, "caption") : null;
            var v4neg = LenientJson.Prop(c, "v4_negative_prompt") is { } n4 ? LenientJson.Prop(n4, "caption") : null;

            b.Prompt((v4 is { } cap ? LenientJson.Str(cap, "base_caption") : null) ?? LenientJson.Str(c, "prompt"), AiPromptRole.Positive, string.Empty, key);
            b.Prompt((v4neg is { } ncap ? LenientJson.Str(ncap, "base_caption") : null) ?? LenientJson.Str(c, "uc"), AiPromptRole.Negative, string.Empty, key);
            if (v4 is { } chars) AddCharacters(b, chars, AiPromptRole.Positive, key);
            if (v4neg is { } nchars) AddCharacters(b, nchars, AiPromptRole.Negative, key);

            b.Param("sampler", LenientJson.Str(c, "sampler"), key);
            b.Param("noise_schedule", LenientJson.Str(c, "noise_schedule"), key);
            b.Param("steps", LenientJson.Str(c, "steps"), key);
            b.Param("scale", LenientJson.Str(c, "scale"), key);
            b.Param("seed", LenientJson.Str(c, "seed"), key);
            if (LenientJson.Str(c, "width") is { } w && LenientJson.Str(c, "height") is { } h) b.Param("Size", $"{w}x{h}", key);
            b.Param("denoise", LenientJson.Str(c, "strength"), key);
            b.Param("CFG rescale", LenientJson.Str(c, "cfg_rescale"), key);
            b.Param("Uncond scale", LenientJson.Str(c, "uncond_scale"), key);
            b.Param("Request type", LenientJson.Str(c, "request_type"), key);
        }
        else
        {
            b.Prompt(description?.Text, AiPromptRole.Positive, string.Empty, description?.TableKey);
        }

        b.Param("Generation time", time?.Text, time?.TableKey);
        context.Sources.Add(b.Build());
    }

    #endregion // Public methods


    #region Private methods

    // "NovelAI Diffusion V4.5 4BDE2A90": the model and its 8-hex hash
    [GeneratedRegex(@"^(.*?)\s+([0-9A-Fa-f]{8})$", RegexOptions.CultureInvariant)]
    private static partial Regex SourceWithHash();


    private static (string Model, string? Hash) SplitSource(string source)
    {
        var m = SourceWithHash().Match(source.Trim());
        return m.Success ? (m.Groups[1].Value, m.Groups[2].Value) : (source.Trim(), null);
    }


    // v4 per-character captions are regional prompts; their centres say where
    private static void AddCharacters(SourceBuilder b, JsonElement caption, AiPromptRole role, string key)
    {
        if (LenientJson.Prop(caption, "char_captions") is not { ValueKind: JsonValueKind.Array } chars) return;

        var i = 0;
        foreach (var ch in chars.EnumerateArray())
        {
            i++;
            var label = $"Character {i}";
            if (LenientJson.Prop(ch, "centers") is { ValueKind: JsonValueKind.Array } centers && centers.GetArrayLength() > 0)
            {
                var c = centers[0];
                var x = AiTagSet.ToNumber(LenientJson.Prop(c, "x") ?? default);
                var y = AiTagSet.ToNumber(LenientJson.Prop(c, "y") ?? default);
                if (x is not null && y is not null) label += string.Create(CultureInfo.InvariantCulture, $" ({x:0.##}, {y:0.##})");
            }

            b.Prompt(LenientJson.Str(ch, "char_caption"), role, label, key);
        }
    }

    #endregion // Private methods
}
