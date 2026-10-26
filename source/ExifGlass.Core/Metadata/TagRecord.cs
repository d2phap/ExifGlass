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
/// The JSON token kind of a tag value.
/// </summary>
public enum TagValueKind : byte
{
    String,
    Number,
    Bool,
    List,
    Struct,
    Binary,
    Null,
}


/// <summary>
/// What the app may do with a tag, decided when the document is built.
/// </summary>
[Flags]
public enum TagRecordFlags : byte
{
    None = 0,
    ReadOnly = 1 << 0,
    Writable = 1 << 1,
    AiSource = 1 << 2,

    /// <summary>
    /// Read by a provenance source: C2PA, IPTC source type, the AIGC label, AI-training signals.
    /// </summary>
    AiProvenance = 1 << 3,
}


/// <summary>
/// One row of the table read.
/// </summary>
public sealed class TagRecord
{
    #region Properties

    /// <summary>
    /// The key exactly as ExifTool printed it, unique within a document: "EXIF:ExifIFD:FNumber".
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Family-0 group, the display group: EXIF, XMP, MakerNotes.
    /// </summary>
    public required string Group0 { get; init; }

    /// <summary>
    /// Family-1 group, the write location: ExifIFD, XMP-dc, FujiFilm.
    /// </summary>
    public required string Group1 { get; init; }

    /// <summary>
    /// The family-4 copy number of a duplicate (0 for the first).
    /// </summary>
    public int Copy { get; init; }

    /// <summary>
    /// ExifTool tag name: FNumber.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// ExifTool's description, shown in the Tag name column: F Number.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// The tag ID as ExifTool printed it: "0x829d", "desc", "FileName".
    /// </summary>
    public string IdText { get; init; } = string.Empty;

    /// <summary>
    /// The Tag ID column text after the tag ID rule: "0x829D", "desc" or "—".
    /// </summary>
    public string DisplayId { get; init; } = TagIdRule.NONE;

    /// <summary>
    /// The print-converted value; numbers keep their JSON text and lists join with ", ".
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// The raw value (-n) when print conversion changed it, otherwise null.
    /// </summary>
    public string? Raw { get; init; }

    public TagValueKind Kind { get; init; }

    /// <summary>
    /// Position in ExifTool's output, 1-based: the # column.
    /// </summary>
    public int FileOrder { get; init; }

    public TagRecordFlags Flags { get; init; }

    public bool IsBinary => Kind == TagValueKind.Binary;

    public bool IsList => Kind == TagValueKind.List;

    public bool IsConverted => Raw is not null;

    public bool IsReadOnly => (Flags & TagRecordFlags.ReadOnly) != 0;

    public bool IsWritable => (Flags & TagRecordFlags.Writable) != 0;

    public bool IsAiSource => (Flags & TagRecordFlags.AiSource) != 0;

    public bool IsAiProvenance => (Flags & TagRecordFlags.AiProvenance) != 0;

    /// <summary>
    /// The raw value when there is one, otherwise the value.
    /// </summary>
    public string RawOrValue => Raw ?? Value;

    /// <summary>
    /// The tag as an ExifTool write target: "ExifIFD:FNumber".
    /// </summary>
    public string WriteName => $"{Group1}:{Name}";

    /// <summary>
    /// The tag as an ExifTool argument, shown and copied as the tag key: "-ExifIFD:FNumber".
    /// </summary>
    public string ArgumentKey => $"-{Group1}:{Name}";

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Returns a copy with other flags.
    /// </summary>
    public TagRecord WithFlags(TagRecordFlags flags) => With(flags, Value);


    /// <summary>
    /// Returns a copy with another shown value.
    /// </summary>
    public TagRecord WithValue(string value) => With(Flags, value);


    public override string ToString() => $"{Key} = {Value}";

    #endregion // Public methods


    #region Private methods

    private TagRecord With(TagRecordFlags flags, string value) => new()
    {
        Key = Key,
        Group0 = Group0,
        Group1 = Group1,
        Copy = Copy,
        Name = Name,
        Description = Description,
        IdText = IdText,
        DisplayId = DisplayId,
        Value = value,
        Raw = Raw,
        Kind = Kind,
        FileOrder = FileOrder,
        Flags = flags,
    };

    #endregion // Private methods
}
