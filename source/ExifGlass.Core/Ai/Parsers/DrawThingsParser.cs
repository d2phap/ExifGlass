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
/// Draw Things: JSON in XMP exif:UserComment, the prompt in dc:description, CreatorTool "Draw Things".
/// </summary>
public sealed class DrawThingsParser : IAiParser
{
    #region Public methods

    public void Parse(AiParseContext context)
    {
        var comment = context.Free("UserComment", "XMP-exif");
        var tool = context.Free("CreatorTool", "XMP-xmp");
        var isDrawThings = tool?.Text.Contains("Draw Things", StringComparison.OrdinalIgnoreCase) == true;
        if (comment is null || (!isDrawThings && !comment.Text.Contains("\"c\"", StringComparison.Ordinal))) return;

        using var doc = LenientJson.TryParse(comment.Text, out _);
        if (doc?.RootElement is not { ValueKind: JsonValueKind.Object } root || LenientJson.Prop(root, "c") is null) return;

        var key = comment.TableKey;
        var b = new SourceBuilder(AiSourceKind.DrawThings, "Draw Things");
        b.Use(context, comment);
        if (isDrawThings) b.Use(context, tool);
        b.Use(context, context.Free("Description", "XMP-dc"));

        b.Prompt(LenientJson.Str(root, "c"), AiPromptRole.Positive, string.Empty, key);
        b.Prompt(LenientJson.Str(root, "uc"), AiPromptRole.Negative, string.Empty, key);

        b.Param("model", LenientJson.Str(root, "model"), key);
        b.Param("sampler", LenientJson.Str(root, "sampler"), key);
        b.Param("steps", LenientJson.Str(root, "steps"), key);
        b.Param("scale", LenientJson.Str(root, "scale"), key);
        b.Param("seed", LenientJson.Str(root, "seed"), key);
        b.Param("size", LenientJson.Str(root, "size"), key);
        b.Param("strength", LenientJson.Str(root, "strength"), key);
        b.Param("Seed mode", LenientJson.Str(root, "seed_mode"), key);
        b.Param("Shift", LenientJson.Str(root, "shift"), key);
        if (LenientJson.Prop(root, "v2") is { ValueKind: JsonValueKind.Object } v2)
        {
            foreach (var p in v2.EnumerateObject()) b.Param(p.Name, AiTagSet.ToText(p.Value), key);
        }

        b.Model(AiModelKind.Checkpoint, LenientJson.Str(root, "model"), null, null, key);
        if (LenientJson.Prop(root, "lora") is { ValueKind: JsonValueKind.Array } loras)
        {
            foreach (var l in loras.EnumerateArray()) b.Model(AiModelKind.Lora, LenientJson.Str(l, "file"), LenientJson.Str(l, "weight"), null, key);
        }

        context.Sources.Add(b.Build());
    }

    #endregion // Public methods
}
