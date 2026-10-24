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
using ExifGlass.Core.Ai;
using ExifGlass.Core.Metadata;

namespace ExifGlass.Core.Editing;

/// <summary>
/// The tags each bulk action removes, so the rows show Delete before Save and the Edit menu can count them.
/// </summary>
public static class EditTargets
{
    #region Fields and constants

    // derived or about the file itself: -all= leaves them, and they come back on the reload
    private static readonly HashSet<string> KEPT_BY_STRIP = new(StringComparer.Ordinal) { "File", "Composite", "ExifTool", GroupInfo.AI_GROUP };

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// A GPS tag that -gps*= deletes: named GPS, in a group ExifTool writes.
    /// </summary>
    public static bool IsRemovableGps(TagRecord tag) => tag.Name.StartsWith("GPS", StringComparison.Ordinal) && !TagWriteRules.ReadOnlyGroups.Contains(tag.Group0);


    /// <summary>
    /// The location tags Remove GPS data deletes, family-4 copies included: ExifTool deletes them all.
    /// </summary>
    public static int GpsCount(MetadataDocument document)
    {
        var count = 0;
        foreach (var tag in document.Tags)
        {
            if (IsRemovableGps(tag)) count++;
        }

        return count;
    }


    /// <summary>
    /// A location inside a C2PA manifest (its stds.exif assertion): ExifTool can't write it, so it survives Remove GPS data.
    /// </summary>
    public static bool HasManifestGps(MetadataDocument document)
    {
        foreach (var tag in document.Tags)
        {
            if (tag.Group0 == "JUMBF" && tag.Name.Contains("GPS", StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }


    /// <summary>
    /// Whether a bulk action deletes the tag; an explicit edit written after it still wins (WritePlan order).
    /// </summary>
    public static bool IsBulkDeleted(EditSet edits, TagRecord tag) =>
        (edits.StripAll && !KEPT_BY_STRIP.Contains(tag.Group0))
        || (edits.RemoveGps && (IsRemovableGps(tag) || IsDerivedGps(tag)))
        || (edits.AiRedaction is { } plan && plan.Removes(tag.Key));


    /// <summary>
    /// Whether an AI row goes: every row with Strip all, a generation field read from a removed tag with the redaction (provenance stays, D24).
    /// </summary>
    public static bool IsBulkDeleted(EditSet edits, AiRow row) =>
        edits.StripAll || (row.Kind != AiFieldKind.Provenance && row.SourceKey is { } key && edits.AiRedaction is { } plan && plan.Removes(key));

    #endregion // Public methods


    #region Private methods

    // a Composite GPS tag is made from the ones -gps*= deletes, so it goes on the reload
    private static bool IsDerivedGps(TagRecord tag) => tag.Group0 == "Composite" && tag.Name.StartsWith("GPS", StringComparison.Ordinal);

    #endregion // Private methods
}
