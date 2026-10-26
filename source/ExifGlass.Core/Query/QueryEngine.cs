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
using ExifGlass.Core.Editing;
using ExifGlass.Core.Metadata;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace ExifGlass.Core.Query;

/// <summary>
/// Turns a document and a query into the table's flat item list, counts and highlights; runs off the UI thread.
/// </summary>
public static class QueryEngine
{
    #region Fields and constants

    // the rows depend on the document, the display options and the edits only: a search, sort or collapse reuses them (plan 8: fewer GCs during FLIP)
    private static readonly ConditionalWeakTable<MetadataDocument, RowCache> ROWS = [];

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Runs the query; the synthetic AI group comes first, then the groups in ExifTool's order.
    /// </summary>
    public static ViewResult Run(MetadataDocument? doc, ViewQuery query, CancellationToken cancellationToken = default)
    {
        if (doc is null) return ViewResult.Empty;

        var cache = CachedRows(doc, query);
        var rows = cache.Rows;
        var aiTotal = cache.AiTotal;
        var filterCounts = cache.FilterCounts;
        var search = query.Search.Trim();
        var filtering = search.Length > 0 || query.Filters.Count > 0 || query.HiddenGroups.Count > 0;

        var groupOrder = new List<string>();
        var byGroup = new Dictionary<string, List<ViewRow>>(StringComparer.Ordinal);
        foreach (var r in rows)
        {
            if (!byGroup.TryGetValue(r.Group, out var list))
            {
                byGroup[r.Group] = list = [];
                groupOrder.Add(r.Group);
            }

            list.Add(r);
        }

        var items = new List<ViewItem>(rows.Count + groupOrder.Count);
        var groups = new List<ViewGroup>(groupOrder.Count);
        var inView = new List<ViewRow>(rows.Count);
        var comparer = RowComparer(query);

        var processed = 0;
        var aiInView = 0;
        foreach (var name in groupOrder)
        {
            var all = byGroup[name];
            var passing = new List<ViewRow>(all.Count);
            foreach (var r in all)
            {
                if (++processed % 512 == 0) cancellationToken.ThrowIfCancellationRequested();
                if (!Matches(r, query.Filters)) continue;

                var hit = search.Length == 0 ? r : Highlight(r, search);
                if (hit is not null) passing.Add(hit);
            }

            // OrderBy is stable: equal values keep ExifTool's order
            if (comparer is not null) passing = [.. passing.OrderBy(r => r, Comparer<ViewRow>.Create(comparer))];

            var hidden = query.HiddenGroups.Contains(name);
            var collapsed = query.CollapsedGroups.Contains(name);
            var ai = name == GroupInfo.AI_GROUP;
            var description = ai ? GroupInfo.Describe(name) : doc.Groups.FirstOrDefault(g => g.Name == name)?.Description ?? string.Empty;
            var group = new ViewGroup(name, description, all.Count, passing.Count, hidden, collapsed) { SourceKey = ai ? cache.AiSource : null };
            groups.Add(group);

            if (hidden || passing.Count == 0) continue;

            inView.AddRange(passing);
            if (ai) aiInView += passing.Count;
            items.Add(new GroupHeaderItem(group));
            if (collapsed) continue;

            foreach (var r in passing) items.Add(new RowItem(r));
        }

        return new ViewResult
        {
            Document = doc,
            Query = query,
            Items = items,
            Groups = groups,
            RowsInView = inView,
            Total = rows.Count - aiTotal,
            TagsInView = inView.Count - aiInView,
            AiTotal = aiTotal,
            AiInView = aiInView,
            GroupCount = groupOrder.Count,
            IsFiltering = filtering,
            FilterCounts = filterCounts,
            ChipCounts = query.Filters.Count == 0 && query.HiddenGroups.Count == 0 ? filterCounts : ChipCounts(rows, query),
        };
    }


    /// <summary>
    /// The quick-filter predicates: Proto's dates, GPS, converted values, real tag IDs and AI, and the AI addendum's prompts, models and provenance.
    /// </summary>
    public static bool Passes(ViewRow row, QuickFilter filter) => filter switch
    {
        QuickFilter.Ai => row.IsAi || row.Tag?.IsAiSource == true,
        QuickFilter.AiPrompts => row.Ai?.Kind == AiFieldKind.Prompt,
        QuickFilter.AiModels => row.Ai?.Kind == AiFieldKind.Model,
        QuickFilter.AiProvenance => row.Ai?.Kind == AiFieldKind.Provenance || row.Tag?.IsAiProvenance == true,
        QuickFilter.DateTime => !row.IsAi && (ContainsAny(row.TagName, "date", "time", "stamp") || ContainsAny(row.Name, "date", "time", "stamp")),
        QuickFilter.Gps => row.TagName.StartsWith("GPS", StringComparison.Ordinal) || row.Name.StartsWith("GPS", StringComparison.Ordinal),
        QuickFilter.Converted => row.Tag?.IsConverted == true,
        QuickFilter.HasTagId => row.DisplayId != TagIdRule.NONE,
        _ => true,
    };


    /// <summary>
    /// Every case-insensitive, non-overlapping occurrence of the search text.
    /// </summary>
    public static IReadOnlyList<TextRange> FindAll(string text, string search)
    {
        if (search.Length == 0 || text.Length < search.Length) return [];

        List<TextRange>? result = null;
        var start = 0;
        while (start <= text.Length - search.Length)
        {
            var i = text.IndexOf(search, start, StringComparison.OrdinalIgnoreCase);
            if (i < 0) break;

            (result ??= []).Add(new TextRange(i, search.Length));
            start = i + search.Length;
        }

        return result is null ? [] : result;
    }

    #endregion // Public methods


    #region Private methods

    private static RowCache CachedRows(MetadataDocument doc, ViewQuery query)
    {
        if (ROWS.TryGetValue(doc, out var cached) && cached.Fits(query)) return cached;

        var ai = AiRows.Build(doc.Ai);
        var rows = BuildRows(doc, ai, query);

        // the rail's quick-filter counts ignore the search, so they come with the rows
        var counts = new Dictionary<QuickFilter, int>();
        foreach (var f in Enum.GetValues<QuickFilter>()) counts[f] = rows.Count(r => Passes(r, f));

        var cache = new RowCache(query.ShowDuplicates, query.ShowRawValues, query.ShowTagNames, query.Edits, rows, counts, ai.Rows.Count, ai.SourceKey);
        ROWS.AddOrUpdate(doc, cache);
        return cache;
    }


    private static List<ViewRow> BuildRows(MetadataDocument doc, AiRowSet aiRows, ViewQuery query)
    {
        var rows = new List<ViewRow>(doc.Tags.Count + aiRows.Rows.Count);
        var edits = query.Edits;

        foreach (var ai in aiRows.Rows)
        {
            rows.Add(new ViewRow
            {
                Key = ai.Key,
                Group = GroupInfo.AI_GROUP,
                Name = query.ShowTagNames ? ai.Name : ai.Label,
                TagName = ai.Name,
                Value = ai.Value,
                SortValue = ai.Value,
                SourceKey = ai.SourceKey,
                Ai = ai,
                EditState = EditTargets.IsBulkDeleted(edits, ai) ? EditState.Removed : EditState.None,
            });
        }

        foreach (var tag in doc.Tags)
        {
            if (tag.Copy > 0 && !query.ShowDuplicates) continue;

            var value = query.ShowRawValues ? tag.RawOrValue : tag.Value;
            var state = EditState.None;
            string? original = null;
            if (edits.Edits.TryGetValue(tag.Key, out var edit))
            {
                state = edit.IsDelete ? EditState.Deleted : EditState.Edited;
                original = value;
                if (!edit.IsDelete) value = edit.Value!;
            }
            else if (EditTargets.IsBulkDeleted(edits, tag))
            {
                state = EditState.Removed;
                original = value;
            }

            rows.Add(new ViewRow
            {
                Key = tag.Key,
                Group = tag.Group0,
                Index = tag.FileOrder,
                Name = query.ShowTagNames ? tag.Name : tag.Description,
                TagName = tag.Name,
                Value = value,
                DisplayId = tag.DisplayId,
                SortValue = tag.RawOrValue,
                Tag = tag,
                EditState = state,
                OriginalValue = original,
            });
        }

        // staged new tags show in their group, or in a group of their own when the file has none yet
        foreach (var added in edits.Added)
        {
            rows.Add(new ViewRow
            {
                Key = "New:" + added.Key,
                Group = added.Group,
                Name = added.Name,
                TagName = added.Name,
                Value = added.Value,
                SortValue = added.Value,
                EditState = EditState.Added,
            });
        }

        return rows;
    }


    // each chip's count among the shown groups' rows the chips already on let through (AND)
    private static Dictionary<QuickFilter, int> ChipCounts(List<ViewRow> rows, ViewQuery query)
    {
        var filters = Enum.GetValues<QuickFilter>();
        var counts = filters.ToDictionary(f => f, _ => 0);
        foreach (var row in rows)
        {
            if (query.HiddenGroups.Contains(row.Group) || !Matches(row, query.Filters)) continue;

            foreach (var f in filters)
            {
                if (Passes(row, f)) counts[f]++;
            }
        }

        return counts;
    }


    private static bool Matches(ViewRow row, IReadOnlySet<QuickFilter> filters)
    {
        foreach (var f in filters)
        {
            if (!Passes(row, f)) return false;
        }

        return true;
    }


    // null when the row doesn't match; matched on the shown name, the tag name, the value and the ID
    private static ViewRow? Highlight(ViewRow row, string search)
    {
        var name = FindAll(row.Name, search);
        var value = FindAll(row.Value, search);
        var id = FindAll(row.DisplayId, search);
        var tagName = name.Count == 0 && row.TagName.Contains(search, StringComparison.OrdinalIgnoreCase);
        if (name.Count == 0 && value.Count == 0 && id.Count == 0 && !tagName) return null;

        return new ViewRow
        {
            Key = row.Key,
            Group = row.Group,
            Index = row.Index,
            Name = row.Name,
            TagName = row.TagName,
            Value = row.Value,
            DisplayId = row.DisplayId,
            SortValue = row.SortValue,
            Tag = row.Tag,
            SourceKey = row.SourceKey,
            Ai = row.Ai,
            EditState = row.EditState,
            OriginalValue = row.OriginalValue,
            NameMatches = name,
            ValueMatches = value,
            IdMatches = id,
        };
    }


    private static bool ContainsAny(string text, params ReadOnlySpan<string> words)
    {
        foreach (var w in words)
        {
            if (text.Contains(w, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }


    private static Comparison<ViewRow>? RowComparer(ViewQuery query)
    {
        if (query.SortDirection == SortDirection.None || query.SortColumn == SortColumn.None) return null;

        var sign = query.SortDirection == SortDirection.Descending ? -1 : 1;
        return query.SortColumn switch
        {
            SortColumn.Index => (a, b) => sign * a.Index.CompareTo(b.Index),
            SortColumn.TagId => (a, b) => sign * NaturalComparer.Instance.Compare(a.DisplayId, b.DisplayId),
            SortColumn.TagName => (a, b) => sign * NaturalComparer.Instance.Compare(a.Name, b.Name),
            SortColumn.Value => (a, b) => sign * CompareValues(a, b),
            _ => null,
        };
    }


    // numbers by raw value before text in natural order; mixing the two per pair would make the order depend on the input
    private static int CompareValues(ViewRow a, ViewRow b)
    {
        var aIsNumber = double.TryParse(a.SortValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var x);
        var bIsNumber = double.TryParse(b.SortValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var y);

        if (aIsNumber && bIsNumber) return x.CompareTo(y);
        if (aIsNumber != bIsNumber) return aIsNumber ? -1 : 1;

        return NaturalComparer.Instance.Compare(a.Value, b.Value);
    }

    #endregion // Private methods


    #region Nested types

    // read-only once made, so concurrent queries may share it
    private sealed record RowCache(bool ShowDuplicates, bool ShowRawValues, bool ShowTagNames, EditSet Edits, List<ViewRow> Rows,
        IReadOnlyDictionary<QuickFilter, int> FilterCounts, int AiTotal, string? AiSource)
    {
        public bool Fits(ViewQuery query) => query.ShowDuplicates == ShowDuplicates && query.ShowRawValues == ShowRawValues
            && query.ShowTagNames == ShowTagNames && ReferenceEquals(query.Edits, Edits);
    }

    #endregion // Nested types
}
