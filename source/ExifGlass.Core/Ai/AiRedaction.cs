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
using ExifGlass.Core.Metadata;

namespace ExifGlass.Core.Ai;

/// <summary>
/// "Remove AI generation data" (D24): the tags that hold prompts, workflows and parameters, as ExifTool delete arguments.
/// </summary>
public sealed record AiRedactionPlan(IReadOnlyList<string> Arguments, IReadOnlyList<string> TableKeys, IReadOnlyList<string> Warnings)
{
    #region Properties

    public bool IsEmpty => Arguments.Count == 0;

    #endregion // Properties
}


/// <summary>
/// Builds the D24 redaction plan: provenance (C2PA, IPTC source type, AIGC) and AI-training signals are kept.
/// </summary>
public static class AiRedaction
{
    #region Fields and constants

    // tags a generation source reads that say who or what made the file rather than how: kept (D24)
    private static readonly HashSet<string> KEEP = new(StringComparer.OrdinalIgnoreCase)
    {
        "Software", "Title", "CreatorTool", "Author", "CreationTime", "DigitalImageGUID", "Artist", "MakerNoteUnknownText",
        "DigitalSourceType", "AISystemUsed", "AISystemVersionUsed", "Credit",
    };

    // groups ExifTool reads but can't write: their AI tags stay (S5, S9)
    private static readonly HashSet<string> READ_ONLY_GROUPS = new(StringComparer.OrdinalIgnoreCase)
    {
        "Matroska", "Vorbis", "ID3", "ID3v2_4", "ID3v2_3", "OpenEXR", "RIFF", "FLAC",
    };

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// The plan for a file's AI information; TagDb, when given, flags tags ExifTool can't delete.
    /// </summary>
    public static AiRedactionPlan Plan(AiGenerationInfo info, TagDb? tagDb = null)
    {
        var args = new List<string>();
        var keys = new List<string>();
        var warnings = new List<string>();

        foreach (var source in info.Sources)
        {
            if (source.IsProvenance && source.Source != AiSourceKind.Iptc) continue;
            if (source.Source is AiSourceKind.CharacterCard or AiSourceKind.ToolName) continue;

            foreach (var tableKey in source.SourceKeys)
            {
                var key = TagKey.Parse(tableKey);
                if (KEEP.Contains(key.Name)) continue;

                // IPTC is provenance except the prompt fields of IPTC 2025.1
                if (source.Source == AiSourceKind.Iptc && key.Name is not ("AIPromptInformation" or "AIPromptWriterName")) continue;

                if (READ_ONLY_GROUPS.Contains(key.Group1) || READ_ONLY_GROUPS.Contains(key.Group0))
                {
                    warnings.Add($"{key.Group1}:{key.Name} can't be removed: ExifTool doesn't write {key.Group0} metadata");
                    continue;
                }

                if (tagDb is not null && tagDb.TryFind(key.Group0, key.Name, key.Group1, out var def) && !def.IsWritable)
                {
                    warnings.Add($"{key.Group1}:{key.Name} can't be removed: ExifTool treats it as read-only");
                    continue;
                }

                var arg = $"-{key.Group1}:{key.Name}=";
                if (args.Contains(arg)) continue;

                args.Add(arg);
                keys.Add(tableKey);
            }

            warnings.AddRange(source.Warnings.Where(w => w.Contains("can't remove", StringComparison.OrdinalIgnoreCase)));
        }

        return new AiRedactionPlan(args, keys, [.. warnings.Distinct()]);
    }

    #endregion // Public methods
}
