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
namespace ExifGlass.Core.Metadata;

/// <summary>
/// Decides which tags the Edit and Delete actions may touch.
/// </summary>
public static class TagWriteRules
{
    #region Fields and constants

    /// <summary>
    /// Groups the user never edits in the table; PNG text chunks with AI data are still removed by the AI redaction (D24).
    /// </summary>
    public static readonly IReadOnlySet<string> ReadOnlyGroups = new HashSet<string>(StringComparer.Ordinal)
    {
        "File", "Composite", "ExifTool", "ICC_Profile", "PNG", "JUMBF", GroupInfo.AI_GROUP,
    };

    /// <summary>
    /// The groups the Add-tag card offers.
    /// </summary>
    public static readonly IReadOnlyList<string> AddableGroups = ["EXIF", "XMP", "IPTC"];

    /// <summary>
    /// File types ExifTool writes IPTC into; HEIC skips IPTC without an error (S5).
    /// </summary>
    public static readonly IReadOnlySet<string> IptcFileTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "JPEG", "TIFF", "PNG", "PSD", "CR2", "NEF", "ARW", "DNG", "ORF", "RW2", "PEF", "SR2", "ERF", "3FR", "MOS", "MRW", "NRW",
    };

    // the Add-tag card's first suggestions, before anything is typed (Proto)
    private static readonly string[] COMMON_EXIF = ["Artist", "Copyright", "ImageDescription", "UserComment", "Software", "DateTimeOriginal", "Make", "Model", "LensModel"];
    private static readonly string[] COMMON_XMP = ["Title", "Description", "Creator", "Rights", "Rating", "Label", "Subject"];
    private static readonly string[] COMMON_IPTC = ["ObjectName", "Caption-Abstract", "By-line", "Keywords", "City", "Country-PrimaryLocationName", "CopyrightNotice", "Credit"];

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// The groups the Add-tag card offers for a file type: IPTC only where ExifTool writes it (S5); all three when the type is unknown.
    /// </summary>
    public static IReadOnlyList<string> AddableGroupsFor(string? fileType) => fileType is null || IptcFileTypes.Contains(fileType) ? AddableGroups : ["EXIF", "XMP"];


    /// <summary>
    /// The names the Add-tag card suggests for a group before anything is typed.
    /// </summary>
    public static IReadOnlyList<string> CommonNames(string group) => group.ToUpperInvariant() switch
    {
        "XMP" => COMMON_XMP,
        "IPTC" => COMMON_IPTC,
        _ => COMMON_EXIF,
    };


    /// <summary>
    /// True when the tag can't be edited or deleted: a read-only group, a binary value, or not writable in ExifTool.
    /// </summary>
    public static bool IsReadOnly(TagRecord tag, TagDb? tagDb)
    {
        if (ReadOnlyGroups.Contains(tag.Group0)) return true;
        if (tag.IsBinary) return true;
        if (tagDb is null) return false;

        return !tagDb.TryFind(tag.Group0, tag.Name, tag.Group1, out var def) || !def.IsWritable;
    }

    #endregion // Public methods
}
