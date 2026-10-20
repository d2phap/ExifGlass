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
using ExifGlass.Core.Editing;
using ExifGlass.Core.Metadata;

namespace ExifGlass.Core.Query;

/// <summary>
/// The rail's quick filters; several combine with AND.
/// </summary>
public enum QuickFilter
{
    DateTime,
    Gps,
    Converted,
    HasTagId,
    Ai,
}


public enum SortColumn
{
    None,
    Index,
    TagId,
    TagName,
    Value,
}


public enum SortDirection
{
    None,
    Ascending,
    Descending,
}


/// <summary>
/// A match inside a cell's text, for the highlight.
/// </summary>
public readonly record struct TextRange(int Start, int Length);


/// <summary>
/// Everything that shapes the table: search, filters, groups, sort, display options and staged edits.
/// </summary>
public sealed record ViewQuery
{
    #region Properties

    public static ViewQuery Default { get; } = new();

    public string Search { get; init; } = string.Empty;

    public IReadOnlySet<QuickFilter> Filters { get; init; } = new HashSet<QuickFilter>();

    public IReadOnlySet<string> HiddenGroups { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    public IReadOnlySet<string> CollapsedGroups { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    public SortColumn SortColumn { get; init; }

    public SortDirection SortDirection { get; init; }

    /// <summary>
    /// -a: show the family-4 copies of duplicate tags.
    /// </summary>
    public bool ShowDuplicates { get; init; }

    /// <summary>
    /// -n: show raw values.
    /// </summary>
    public bool ShowRawValues { get; init; }

    /// <summary>
    /// -s: show ExifTool tag names instead of descriptions.
    /// </summary>
    public bool ShowTagNames { get; init; }

    public EditSet Edits { get; init; } = EditSet.Empty;

    #endregion // Properties
}


/// <summary>
/// One table row: a real tag, a synthetic AI row, or a staged new tag, with what the cells show.
/// </summary>
public sealed class ViewRow
{
    #region Properties

    public required string Key { get; init; }

    public required string Group { get; init; }

    /// <summary>
    /// The # column: ExifTool's output order; AI rows and new tags have none (0).
    /// </summary>
    public int Index { get; init; }

    public required string Name { get; init; }

    /// <summary>
    /// The ExifTool tag name, also matched by search: "FNumber".
    /// </summary>
    public required string TagName { get; init; }

    public required string Value { get; init; }

    public string DisplayId { get; init; } = TagIdRule.NONE;

    /// <summary>
    /// The text the value sorts by: the raw value when there is one.
    /// </summary>
    public string SortValue { get; init; } = string.Empty;

    public TagRecord? Tag { get; init; }

    /// <summary>
    /// For an AI row: the table key of the tag it was parsed from ("Show source tag").
    /// </summary>
    public string? SourceKey { get; init; }

    public EditState EditState { get; init; }

    /// <summary>
    /// For an edited row: the value before the edit ("Was: ...").
    /// </summary>
    public string? OriginalValue { get; init; }

    public IReadOnlyList<TextRange> NameMatches { get; init; } = [];

    public IReadOnlyList<TextRange> ValueMatches { get; init; } = [];

    public IReadOnlyList<TextRange> IdMatches { get; init; } = [];

    public bool IsAi => Group == GroupInfo.AI_GROUP;

    #endregion // Properties
}


/// <summary>
/// One group as the table and rail show it.
/// </summary>
public sealed record ViewGroup(string Name, string Description, int Total, int Visible, bool IsHidden, bool IsCollapsed)
{
    #region Properties

    /// <summary>
    /// The count badge: "55", or "12 of 55" while filtering.
    /// </summary>
    public string CountLabel(bool filtering) => filtering ? $"{Visible} of {Total}" : Total.ToString(System.Globalization.CultureInfo.InvariantCulture);

    #endregion // Properties
}


/// <summary>
/// A flat table item: a group header or a row (S2: the grid does no grouping of its own).
/// </summary>
public abstract record ViewItem;


public sealed record GroupHeaderItem(ViewGroup Group) : ViewItem;


public sealed record RowItem(ViewRow Row) : ViewItem;


/// <summary>
/// The table after a query: items to show, groups, counts and the rows in view.
/// </summary>
public sealed class ViewResult
{
    #region Properties

    public static ViewResult Empty { get; } = new();

    public IReadOnlyList<ViewItem> Items { get; init; } = [];

    /// <summary>
    /// Every group in table order (AI first), hidden ones included, for the rail.
    /// </summary>
    public IReadOnlyList<ViewGroup> Groups { get; init; } = [];

    /// <summary>
    /// Rows that pass the search and filters in visible groups, collapsed ones included: what Export exports.
    /// </summary>
    public IReadOnlyList<ViewRow> RowsInView { get; init; } = [];

    /// <summary>
    /// All rows of the document (duplicates per the -a option), the "128 tags" total.
    /// </summary>
    public int Total { get; init; }

    public int GroupCount { get; init; }

    public bool IsFiltering { get; init; }

    public IReadOnlyDictionary<QuickFilter, int> FilterCounts { get; init; } = new Dictionary<QuickFilter, int>();

    /// <summary>
    /// "128 tags" or "42 of 128 tags".
    /// </summary>
    public string Summary => IsFiltering ? $"{RowsInView.Count} of {Total} tags" : Total == 1 ? "1 tag" : $"{Total} tags";

    #endregion // Properties
}
