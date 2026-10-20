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
using ExifGlass.Core.Ai.Parsers;
using System.Diagnostics;
using System.Text.Json;

namespace ExifGlass.Core.Ai;

/// <summary>
/// Runs the parsers over the AI read in order of specificity and merges what they find.
/// </summary>
public static class AiEngine
{
    #region Fields and constants

    /// <summary>
    /// Specific parsers first: each claims its tags, so a shared name ("prompt", "Comment", "Parameters") goes to the right one.
    /// </summary>
    public static IReadOnlyList<IAiParser> Parsers { get; } =
    [
        new NovelAiParser(),
        new FooocusParser(),
        new SwarmUiParser(),
        new TensorArtParser(),
        new InvokeAiParser(),
        new EasyDiffusionParser(),
        new ComfyUiParser(),
        new DrawThingsParser(),
        new MidjourneyParser(),
        new A1111Parser(),
        new C2paParser(),
        new IptcParser(),
        new AigcParser(),
        new TrainingSignalsParser(),
        new CharacterCardParser(),
        new ToolNameParser(),
        new UnknownAiParser(),
    ];

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Parses the AI read; null when the file has no AI data or provenance at all. Malformed data becomes a warning, never an exception.
    /// </summary>
    public static AiGenerationInfo? Parse(AiTagSet tags, string? sidecarText = null, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var context = new AiParseContext(tags, sidecarText, cancellationToken);

        foreach (var parser in Parsers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                parser.Parse(context);
            }
            catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException or ArgumentException or IndexOutOfRangeException or KeyNotFoundException or OverflowException)
            {
                context.Warnings.Add($"{parser.GetType().Name}: {ex.Message}");
            }
        }

        if (context.Sources.Count == 0) return null;

        return new AiGenerationInfo([.. context.Sources], [.. context.Warnings], watch.Elapsed);
    }


    /// <summary>
    /// Reads the .txt sidecar A1111 and SD.Next write next to an image (Tier 2); null when there is none.
    /// </summary>
    public static string? ReadSidecar(string imagePath)
    {
        try
        {
            var txt = Path.ChangeExtension(imagePath, ".txt");
            if (!File.Exists(txt)) return null;

            var info = new FileInfo(txt);
            return info.Length is > 0 and < 1024 * 1024 ? File.ReadAllText(txt) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    #endregion // Public methods
}
