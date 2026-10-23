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
using System.Text.RegularExpressions;

namespace ExifGlass.Core.Ai;

/// <summary>
/// How a part of an AI value is drawn: prompt syntax tints, and the chips and notes after a value.
/// </summary>
public enum AiSpanKind
{
    /// <summary>
    /// "(volumetric fog:1.2)": AccentText.
    /// </summary>
    Weight,

    /// <summary>
    /// "&lt;lora:add_detail:0.8&gt;": Accent2 mono on an Accent2 tint.
    /// </summary>
    Lora,

    /// <summary>
    /// Midjourney "--ar 16:9": AccentText mono.
    /// </summary>
    Flag,

    /// <summary>
    /// BREAK and AND: Accent2 Medium.
    /// </summary>
    Keyword,

    /// <summary>
    /// An embedding the AI engine found: Accent2, dotted underline.
    /// </summary>
    Embedding,

    /// <summary>
    /// A mono chip after the value: a hash, a raw term, a node.
    /// </summary>
    Chip,

    /// <summary>
    /// A LoRA or model weight: Accent2 mono on an Accent2 tint.
    /// </summary>
    WeightChip,

    /// <summary>
    /// A quiet note after the value, in TextTertiary.
    /// </summary>
    Note,
}


/// <summary>
/// One styled part of an AI value, by character range.
/// </summary>
public readonly record struct AiSpan(int Start, int Length, AiSpanKind Kind);


/// <summary>
/// The prompt syntax the AI addendum tints: weights, LoRA tags, Midjourney flags, BREAK and AND, and the embeddings a prompt names.
/// </summary>
public static partial class AiPromptSyntax
{
    #region Public methods

    /// <summary>
    /// The prompt's syntax spans in order, embeddings included where no other span covers them.
    /// </summary>
    public static IReadOnlyList<AiSpan> Spans(string? text, IReadOnlyCollection<string>? embeddings = null)
    {
        if (string.IsNullOrEmpty(text)) return [];

        var spans = new List<AiSpan>();
        var at = 0;
        foreach (Match match in SyntaxRegex().Matches(text))
        {
            AddEmbeddings(spans, text, at, match.Index, embeddings);

            var kind = match.Value[0] switch
            {
                '<' => AiSpanKind.Lora,
                '(' => AiSpanKind.Weight,
                '-' => AiSpanKind.Flag,
                _ => AiSpanKind.Keyword,
            };
            spans.Add(new AiSpan(match.Index, match.Length, kind));
            at = match.Index + match.Length;
        }

        AddEmbeddings(spans, text, at, text.Length, embeddings);
        return spans;
    }

    #endregion // Public methods


    #region Private methods

    [GeneratedRegex(@"<(?:lora|lyco|hypernet):[^:>]+:[\d.]+>|\(([^()]+):([\d.]+)\)|--[a-z]+ [\w:.]+|\b(?:BREAK|AND)\b", RegexOptions.CultureInvariant)]
    private static partial Regex SyntaxRegex();


    // embeddings only between the other spans, so a span never overlaps another
    private static void AddEmbeddings(List<AiSpan> spans, string text, int from, int to, IReadOnlyCollection<string>? names)
    {
        if (names is not { Count: > 0 }) return;

        var at = from;
        while (at < to)
        {
            var (hit, length) = (-1, 0);
            foreach (var name in names)
            {
                if (name.Length == 0) continue;

                var i = text.IndexOf(name, at, to - at, StringComparison.OrdinalIgnoreCase);
                if (i >= 0 && (hit < 0 || i < hit)) (hit, length) = (i, name.Length);
            }

            if (hit < 0 || hit + length > to) return;

            spans.Add(new AiSpan(hit, length, AiSpanKind.Embedding));
            at = hit + length;
        }
    }

    #endregion // Private methods
}
