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

namespace ExifGlass.Core.Metadata;

/// <summary>
/// One display group (family 0) of a document, in ExifTool's output order.
/// </summary>
public sealed record TagGroup(string Name, string Description, int Count, int FirstOrder);


/// <summary>
/// Everything one load produced for one file; immutable, replaced as a whole by the next load.
/// </summary>
public sealed class MetadataDocument
{
    #region Fields and constants

    // plain dictionaries: built once per load and read a handful of times, where a frozen one costs 10 ms at 5k tags
    private readonly Dictionary<string, TagRecord> _byKey;
    private readonly Dictionary<string, TagRecord> _byGroupAndName;

    // shared by the copies made from one read (derived info, AI data), so a view can tell an update from a new file
    private readonly object _read;

    private bool? _mayHoldAi;

    #endregion // Fields and constants


    #region Constructors

    private MetadataDocument(IReadOnlyList<TagRecord> tags, IReadOnlyList<TagGroup> groups, object? read = null)
    {
        _read = read ?? new object();
        Tags = tags;
        Groups = groups;
        _byKey = new Dictionary<string, TagRecord>(tags.Count, StringComparer.Ordinal);
        foreach (var tag in tags) _byKey.TryAdd(tag.Key, tag);

        // the first tag wins for a name that repeats in a group: ExifTool lists the preferred one first
        _byGroupAndName = new Dictionary<string, TagRecord>(tags.Count * 2, StringComparer.OrdinalIgnoreCase);
        foreach (var tag in tags)
        {
            _byGroupAndName.TryAdd(string.Concat(tag.Group0, ":", tag.Name), tag);
            if (tag.Group1 != tag.Group0) _byGroupAndName.TryAdd(string.Concat(tag.Group1, ":", tag.Name), tag);
        }
    }

    #endregion // Constructors


    #region Properties

    public required string SourcePath { get; init; }

    public string FileName => Path.GetFileName(SourcePath);

    public long FileSize { get; init; }

    public DateTimeOffset? FileModified { get; init; }

    public string? ExifToolVersion { get; init; }

    public IReadOnlyList<TagRecord> Tags { get; }

    public IReadOnlyList<TagGroup> Groups { get; }

    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// ExifTool's error for the file, such as "File format error", or null.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Time from the request to the parsed document.
    /// </summary>
    public TimeSpan ParseTime { get; init; }

    public DerivedInfo Derived { get; init; } = DerivedInfo.Empty;

    /// <summary>
    /// Filled after the table shows, by the AI read; null until then or when the file has none.
    /// </summary>
    public AiGenerationInfo? Ai { get; init; }

    /// <summary>
    /// The tag table the read used, shared by every document; null when none was loaded.
    /// </summary>
    public TagDb? TagDb { get; init; }

    /// <summary>
    /// True when the table read shows a tag the AI read parses (PNG AI keywords, C2PA, IPTC source type, the AIGC label): worth a skeleton while that read runs.
    /// </summary>
    public bool MayHoldAi => _mayHoldAi ??= Tags.Any(t => t.Group0 == "JUMBF" || (t.Group0 == "PNG" && AiNames.IsAiPngKeyword(t.Name))
        || t.Name is "DigitalSourceType" or "AISystemUsed" or "AIPromptInformation" || t.Name.Equals("AIGC", StringComparison.OrdinalIgnoreCase));

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Builds a document from a table read, its groups and derived info.
    /// </summary>
    public static MetadataDocument Create(string sourcePath, TableReadResult read, TagDb? tagDb = null, TimeSpan parseTime = default)
    {
        var tags = tagDb is null ? read.Tags : ApplyFlags(read.Tags, tagDb);
        var groups = BuildGroups(tags);

        long size = 0;
        DateTimeOffset? modified = null;
        try
        {
            var info = new FileInfo(sourcePath);
            if (info.Exists)
            {
                size = info.Length;
                modified = info.LastWriteTimeUtc;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // the read already worked; size and time are only shown in the rail
        }

        var doc = new MetadataDocument(tags, groups)
        {
            SourcePath = sourcePath,
            FileSize = size,
            FileModified = modified,
            ExifToolVersion = read.ExifToolVersion,
            Warnings = read.Warnings,
            Error = read.Error,
            ParseTime = parseTime,
            TagDb = tagDb,
        };

        return doc.With(derived: DerivedInfo.From(doc));
    }


    /// <summary>
    /// Returns a copy that carries the AI information.
    /// </summary>
    public MetadataDocument WithAi(AiGenerationInfo? ai)
    {
        if (ai is null || ai.SourceKeys.Count == 0) return With(ai: ai);

        var matcher = new AiSourceMatcher(ai.SourceKeys);
        var provenance = new AiSourceMatcher(ai.Sources.Where(s => s.IsProvenance).SelectMany(s => s.SourceKeys));
        var marked = Tags.Select(t => Mark(t, matcher, provenance)).ToList();
        return new MetadataDocument(marked, Groups, _read)
        {
            SourcePath = SourcePath,
            FileSize = FileSize,
            FileModified = FileModified,
            ExifToolVersion = ExifToolVersion,
            Warnings = Warnings,
            Error = Error,
            ParseTime = ParseTime,
            Derived = Derived,
            Ai = ai,
            TagDb = TagDb,
        };
    }


    public TagRecord? GetByKey(string key) => _byKey.GetValueOrDefault(key);


    /// <summary>
    /// The Format row (D18): the tag table's type, "—" when the table doesn't list the tag.
    /// </summary>
    public string FormatOf(TagRecord tag) => TagDb is not null && TagDb.TryFind(tag.Group0, tag.Name, tag.Group1, out var def) ? def.Format : TagIdRule.NONE;


    /// <summary>
    /// Whether both come from the same read: the AI data arriving makes a copy, a reload of the file doesn't.
    /// </summary>
    public bool IsSameRead(MetadataDocument? other) => other is not null && ReferenceEquals(_read, other._read);


    /// <summary>
    /// The table row an AI field came from, for "Show source tag"; C2PA and XMP structs match their flattened rows.
    /// </summary>
    public TagRecord? FindSourceTag(string sourceKey)
    {
        if (GetByKey(sourceKey) is { } exact) return exact;

        var matcher = new AiSourceMatcher([sourceKey]);
        return Tags.FirstOrDefault(matcher.Matches);
    }


    /// <summary>
    /// Finds a tag by family-0 or family-1 group and name: Find("EXIF", "FNumber"), Find("ExifIFD", "FNumber").
    /// </summary>
    public TagRecord? Find(string group, string name) => _byGroupAndName.GetValueOrDefault(string.Concat(group, ":", name));


    /// <summary>
    /// Finds a tag by name in the first group that has it.
    /// </summary>
    public TagRecord? FindFirst(string name, params ReadOnlySpan<string> groups)
    {
        foreach (var group in groups)
        {
            if (Find(group, name) is { } tag) return tag;
        }

        return null;
    }

    #endregion // Public methods


    #region Private methods

    private MetadataDocument With(DerivedInfo? derived = null, AiGenerationInfo? ai = null) => new(Tags, Groups, _read)
    {
        SourcePath = SourcePath,
        FileSize = FileSize,
        FileModified = FileModified,
        ExifToolVersion = ExifToolVersion,
        Warnings = Warnings,
        Error = Error,
        ParseTime = ParseTime,
        Derived = derived ?? Derived,
        Ai = ai ?? Ai,
        TagDb = TagDb,
    };


    private static TagRecord Mark(TagRecord tag, AiSourceMatcher sources, AiSourceMatcher provenance)
    {
        if (!sources.Matches(tag)) return tag;

        var flags = tag.Flags | TagRecordFlags.AiSource;
        if (provenance.Matches(tag)) flags |= TagRecordFlags.AiProvenance;
        return tag.WithFlags(flags);
    }


    private static List<TagRecord> ApplyFlags(List<TagRecord> tags, TagDb tagDb)
    {
        var result = new List<TagRecord>(tags.Count);
        foreach (var tag in tags)
        {
            var flags = TagWriteRules.IsReadOnly(tag, tagDb) ? TagRecordFlags.ReadOnly : TagRecordFlags.Writable;
            result.Add(tag.WithFlags(tag.Flags | flags));
        }

        return result;
    }


    private static List<TagGroup> BuildGroups(IReadOnlyList<TagRecord> tags)
    {
        var order = new List<string>();
        var counts = new Dictionary<string, (int Count, int First)>(StringComparer.Ordinal);
        string? make = null;

        foreach (var tag in tags)
        {
            if (counts.TryGetValue(tag.Group0, out var c)) counts[tag.Group0] = (c.Count + 1, c.First);
            else
            {
                counts[tag.Group0] = (1, tag.FileOrder);
                order.Add(tag.Group0);
            }

            if (make is null && tag.Name == "Make" && tag.Group0 == "EXIF") make = tag.Value;
        }

        return [.. order.Select(name => new TagGroup(name, GroupInfo.Describe(name, make), counts[name].Count, counts[name].First))];
    }

    #endregion // Private methods
}
