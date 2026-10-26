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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExifGlass.Core.Export;
using ExifGlass.Core.Metadata;
using ExifGlass.Core.Query;
using ExifGlass.Core.Settings;
using ExifGlass.Services;
using ExifGlass.UI.Controls;

namespace ExifGlass.ViewModels;

/// <summary>
/// A removable filter chip in the table toolbar (DS 7.2): hidden groups or a quick filter; an AI one in Accent2.
/// </summary>
public sealed record FilterChipItem(string Label, QuickFilter? Filter)
{
    #region Properties

    public bool IsAi => Filter is QuickFilter.Ai or QuickFilter.AiPrompts or QuickFilter.AiModels or QuickFilter.AiProvenance;

    #endregion // Properties
}


/// <summary>
/// One row of the Columns menu: its tick, whether it can be unticked, and its hint (Proto).
/// </summary>
public sealed record ColumnChoice(TableColumns Column, string Label, bool IsChecked, bool IsLocked, string? Hint);


public sealed partial class MainViewModel
{
    #region Fields and constants

    private static readonly (TableColumns Column, string Label)[] COLUMN_LABELS =
    [
        (TableColumns.Index, AppText.COLUMN_INDEX), (TableColumns.TagId, AppText.COLUMN_TAG_ID),
        (TableColumns.TagName, AppText.COLUMN_TAG_NAME), (TableColumns.Value, AppText.COLUMN_VALUE),
    ];

    private readonly HashSet<string> _collapsed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _hiddenGroups = new(StringComparer.Ordinal);
    private readonly HashSet<QuickFilter> _filters = [];

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// The table's cell or row selection; the table drives it, the toolbar, Esc and Ctrl+C read it.
    /// </summary>
    public TableSelection Selection { get; } = new();

    [ObservableProperty]
    public partial SortColumn SortColumn { get; private set; }

    [ObservableProperty]
    public partial SortDirection SortDirection { get; private set; }

    /// <summary>
    /// The columns the user chose in the Columns menu, kept in the settings.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ColumnChoices))]
    public partial TableColumns Columns { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRowSelection), nameof(ShowTableLabels), nameof(ShowSummaryRow))]
    [NotifyPropertyChangedFor(nameof(CollapseAllContent), nameof(EditContent), nameof(ColumnsContent), nameof(ExportContent))]
    public partial int SelectedCount { get; private set; }

    /// <summary>
    /// Set by the table toolbar when a button label would clip at its width (Proto checkBar, 80 px hysteresis).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTableLabels), nameof(CollapseAllContent), nameof(EditContent), nameof(ColumnsContent), nameof(ExportContent))]
    public partial bool LabelsClipped { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFilterChips), nameof(ShowSummaryDetail))]
    public partial IReadOnlyList<FilterChipItem> FilterChips { get; private set; } = [];

    /// <summary>
    /// The group at the top of the table, for the rail (P6).
    /// </summary>
    [ObservableProperty]
    public partial string? ActiveGroup { get; set; }

    public bool HasRowSelection => SelectedCount > 0;

    public bool ShowSummaryRow => !HasRowSelection;

    public bool HasFilterChips => FilterChips.Count > 0;

    public bool IdColumnsHidden => Table.IdColumnsHidden;

    public bool IsTableNarrow => Table.IsNarrow;

    /// <summary>
    /// The toolbar's button labels (P4 rules): the table at 660 px or more, no label clipping, no row selection.
    /// </summary>
    public bool ShowTableLabels => Table.LabelsFit && !LabelsClipped && !HasRowSelection;

    /// <summary>
    /// The no-match state (DS 6.9): a file is shown and nothing passes the search, filters and groups.
    /// </summary>
    public bool ShowNoMatch => Document is not null && Rows.RowsInView.Count == 0;

    /// <summary>
    /// Skeleton rows while the first file reads; a later read keeps the last rows on screen (P4).
    /// </summary>
    public bool ShowSkeleton => IsLoading && Document is null;

    public bool IsAllCollapsed
    {
        get
        {
            var any = false;
            foreach (var group in Rows.Groups)
            {
                if (group.IsHidden || group.Visible == 0) continue;
                if (!group.IsCollapsed) return false;
                any = true;
            }

            return any;
        }
    }

    public string CollapseAllLabel => IsAllCollapsed ? AppText.EXPAND_ALL : AppText.COLLAPSE_ALL;

    public string CollapseAllTip => IsAllCollapsed ? AppText.EXPAND_ALL_TIP : AppText.COLLAPSE_ALL_TIP;

    public IconId CollapseAllIcon => IsAllCollapsed ? IconId.ChevronDoubleDown : IconId.ChevronDoubleUp;

    // a null label leaves the button icon-only (P4 rules)
    public string? CollapseAllContent => ShowTableLabels ? CollapseAllLabel : null;

    public string? EditContent => ShowTableLabels ? AppText.EDIT : null;

    public string? ColumnsContent => ShowTableLabels ? AppText.COLUMNS : null;

    public string? ExportContent => ShowTableLabels ? AppText.EXPORT : null;

    public string ExportHeader => AppText.ExportsInView(Rows.TagsInView, Rows.AiInView);

    public IReadOnlyList<ColumnChoice> ColumnChoices
    {
        get
        {
            var chosen = Columns & TableColumns.All;
            var list = new List<ColumnChoice>(COLUMN_LABELS.Length);
            foreach (var (column, label) in COLUMN_LABELS)
            {
                var on = (chosen & column) != 0;
                var last = on && (chosen & ~column) == TableColumns.None;
                var hidden = on && IdColumnsHidden && column is TableColumns.Index or TableColumns.TagId;
                list.Add(new ColumnChoice(column, label, on, last, last ? AppText.AT_LEAST_ONE : hidden ? AppText.HIDDEN_AT_WIDTH : null));
            }

            return list;
        }
    }

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// A group header's click: collapses or expands it (the table plays the motion).
    /// </summary>
    public void ToggleGroup(string group)
    {
        if (!_collapsed.Remove(group)) _collapsed.Add(group);
        Requery();
    }


    /// <summary>
    /// Shows or hides a group, as the rail's checkbox will (P6).
    /// </summary>
    public void SetGroupHidden(string group, bool hidden)
    {
        if (hidden ? !_hiddenGroups.Add(group) : !_hiddenGroups.Remove(group)) return;
        Requery();
    }


    /// <summary>
    /// Turns a quick filter on or off, as the rail's chips will (P6); several combine with AND.
    /// </summary>
    public void ToggleFilter(QuickFilter filter)
    {
        if (!_filters.Remove(filter)) _filters.Add(filter);
        Requery();
    }


    /// <summary>
    /// A column header's click: Asc, then Desc, then file order (DS 6.9).
    /// </summary>
    public void CycleSort(SortColumn column)
    {
        if (SortColumn != column) (SortColumn, SortDirection) = (column, SortDirection.Ascending);
        else if (SortDirection == SortDirection.Ascending) SortDirection = SortDirection.Descending;
        else (SortColumn, SortDirection) = (SortColumn.None, SortDirection.None);

        Requery();
    }


    /// <summary>
    /// Ctrl+A outside a text box: every row in view.
    /// </summary>
    public void SelectAllRows()
    {
        if (Document is not null) Selection.SelectAll(MetadataTable.RowKeys(Rows));
    }


    /// <summary>
    /// Ctrl+C outside a text box: the selected rows as TSV, else the current cell; false when there is neither.
    /// </summary>
    public bool CopySelection()
    {
        if (Selection.RowCount > 0)
        {
            _ = CopyRowsAsync(asJson: false);
            return true;
        }

        if (!Selection.HasCell || Selection.CurrentKey is not { } key || FindRow(key) is not { } row) return false;

        var text = Exporter.Cell(row, Selection.CurrentColumn);
        _ = CopyAndToastAsync(text, AppText.TOAST_COPIED_CELL, AppText.Clip(text));
        return true;
    }


    /// <summary>
    /// The copy button or a double-click on a row: its value (DS 6.9).
    /// </summary>
    public Task CopyValueAsync(ViewRow row) => CopyAndToastAsync(row.Value, AppText.TOAST_COPIED_VALUE, row.Name);

    #endregion // Public methods


    #region Commands

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void ToggleCollapseAll()
    {
        if (IsAllCollapsed) _collapsed.Clear();
        else
        {
            foreach (var group in Rows.Groups) _collapsed.Add(group.Name);
        }

        Requery();
    }


    [RelayCommand]
    private void ToggleColumn(TableColumns column)
    {
        var next = Columns ^ column;
        if ((next & TableColumns.All) == TableColumns.None) return;

        Columns = next;
        _settings.Update(s => s.VisibleColumns = ColumnNames(next));
    }


    [RelayCommand]
    private void ClearFilters()
    {
        _hiddenGroups.Clear();
        _filters.Clear();
        if (SearchText.Length > 0) SearchText = string.Empty;
        else Requery();
    }


    [RelayCommand]
    private void RemoveChip(FilterChipItem chip)
    {
        if (chip.Filter is { } filter) _filters.Remove(filter);
        else _hiddenGroups.Clear();
        Requery();
    }


    [RelayCommand]
    private void ClearSelection() => Selection.Clear();


    /// <summary>
    /// Export menu (DS 7.4): the rows in view with the chosen columns, through a save dialog.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task ExportAsync(ExportFormat format)
    {
        if (Shell is null || Document is not { } document) return;

        var (rows, tags, ai) = (Rows.RowsInView, Rows.TagsInView, Rows.AiInView);
        var name = Path.GetFileNameWithoutExtension(document.FileName) + "." + Exporter.Extension(format);
        try
        {
            await using var target = await Shell.PickSaveFileAsync(name, format);
            if (target is null) return;

            var content = await Task.Run(() => Exporter.Export(format, rows, Columns, document.SourcePath));
            await Exporter.WriteAsync(target.Stream, format, content);
            Shell.ShowToast(AppText.Exported(target.Name), AppText.ExportedSub(tags, ai, target.Stream.CanSeek ? target.Stream.Length : content.Length));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Shell.ShowToast(AppText.TOAST_EXPORT_FAILED, ex.Message, ToastKind.Danger);
        }
    }


    [RelayCommand(CanExecute = nameof(HasDocument))]
    private async Task CopyAsJsonAsync()
    {
        if (Document is not { } document) return;

        var (rows, tags, ai) = (Rows.RowsInView, Rows.TagsInView, Rows.AiInView);
        var json = await Task.Run(() => Exporter.ToExifToolJson(rows, document.SourcePath));
        await CopyAndToastAsync(json, AppText.TOAST_COPIED_AS_JSON, AppText.TagsInView(tags, ai));
    }


    [RelayCommand]
    private Task CopyRowsTsvAsync() => CopyRowsAsync(asJson: false);


    [RelayCommand]
    private Task CopyRowsJsonAsync() => CopyRowsAsync(asJson: true);

    #endregion // Commands


    #region Private methods

    private Task CopyRowsAsync(bool asJson)
    {
        var rows = SelectedRows();
        if (rows.Count == 0) return Task.CompletedTask;

        return asJson
            ? CopyAndToastAsync(Exporter.ToJsonObject(rows), AppText.CopiedAsJson(rows.Count), AppText.TOAST_COPIED_JSON_SUB)
            : CopyAndToastAsync(Exporter.ToTsv(rows), AppText.CopiedTags(rows.Count), AppText.TOAST_COPIED_TSV_SUB);
    }



    private static TableColumns ParseColumns(IEnumerable<string> names)
    {
        var columns = TableColumns.None;
        foreach (var name in names)
        {
            if (Enum.TryParse<TableColumns>(name, ignoreCase: true, out var column)) columns |= column & TableColumns.All;
        }

        return columns == TableColumns.None ? TableColumns.Default : columns;
    }


    private static List<string> ColumnNames(TableColumns columns)
    {
        var names = new List<string>(4);
        foreach (var (column, _) in COLUMN_LABELS)
        {
            if ((columns & column) != 0) names.Add(column.ToString());
        }

        return names;
    }


    private void RestoreViewChoices(AppSettings settings)
    {
        _hiddenGroups.UnionWith(settings.HiddenGroups);
        foreach (var name in settings.QuickFilters)
        {
            if (Enum.TryParse<QuickFilter>(name, ignoreCase: true, out var filter) && Enum.IsDefined(filter)) _filters.Add(filter);
        }
    }


    // every change of the rail's groups or filters goes through a query, so the settings follow here, and only when they differ
    private void RememberViewChoices()
    {
        var groups = _hiddenGroups.Order(StringComparer.Ordinal).ToList();
        var filters = _filters.Order().Select(f => f.ToString()).ToList();
        var current = _settings.Current;
        if (current.HiddenGroups.SequenceEqual(groups, StringComparer.Ordinal) && current.QuickFilters.SequenceEqual(filters, StringComparer.Ordinal)) return;

        _settings.Update(s =>
        {
            s.HiddenGroups = groups;
            s.QuickFilters = filters;
        });
    }


    private ViewQuery BuildQuery() => new()
    {
        Search = SearchText,
        Filters = new HashSet<QuickFilter>(_filters),
        HiddenGroups = new HashSet<string>(_hiddenGroups, StringComparer.Ordinal),
        CollapsedGroups = new HashSet<string>(_collapsed, StringComparer.Ordinal),
        SortColumn = SortColumn,
        SortDirection = SortDirection,
        ShowDuplicates = _arguments.ShowDuplicates,
        ShowRawValues = _arguments.ShowRawValues,
        ShowTagNames = _arguments.ShowTagNames,
        Edits = _edits,
    };


    private void Requery() => _ = RunQueryAsync(Loader.Document);


    private void UpdateChips()
    {
        var chips = new List<FilterChipItem>(_filters.Count + 1);
        if (Document is not null)
        {
            if (_hiddenGroups.Count > 0) chips.Add(new FilterChipItem(AppText.Hiding(_hiddenGroups), null));
            foreach (var filter in Enum.GetValues<QuickFilter>())
            {
                if (_filters.Contains(filter)) chips.Add(new FilterChipItem(FilterLabel(filter), filter));
            }
        }

        if (chips.Count != FilterChips.Count || !chips.SequenceEqual(FilterChips)) FilterChips = chips;
    }


    private static string FilterLabel(QuickFilter filter) => filter switch
    {
        QuickFilter.DateTime => AppText.FILTER_DATES,
        QuickFilter.Gps => AppText.FILTER_GPS,
        QuickFilter.Converted => AppText.FILTER_CONVERTED,
        QuickFilter.HasTagId => AppText.FILTER_TAG_ID,
        QuickFilter.AiPrompts => AppText.FILTER_PROMPTS,
        QuickFilter.AiModels => AppText.FILTER_MODELS,
        QuickFilter.AiProvenance => AppText.FILTER_PROVENANCE,
        _ => AppText.FILTER_AI,
    };


    private ViewRow? FindRow(string key)
    {
        foreach (var row in Rows.RowsInView)
        {
            if (row.Key == key) return row;
        }

        return null;
    }


    // in view order, so a copy reads as the table does
    private List<ViewRow> SelectedRows()
    {
        var rows = new List<ViewRow>(Selection.RowCount);
        foreach (var row in Rows.RowsInView)
        {
            if (Selection.IsSelected(row.Key)) rows.Add(row);
        }

        return rows;
    }


    private async Task CopyAndToastAsync(string text, string title, string subtitle)
    {
        if (Shell is null) return;

        await Shell.CopyTextAsync(text);
        Shell.ShowToast(title, subtitle);
    }


    private void OnTableLayoutChanged()
    {
        GroupCountFits = Table.GroupCountFits;
        OnPropertyChanged(nameof(IdColumnsHidden));
        OnPropertyChanged(nameof(IsTableNarrow));
        OnPropertyChanged(nameof(ShowTableLabels));
        OnPropertyChanged(nameof(CollapseAllContent));
        OnPropertyChanged(nameof(EditContent));
        OnPropertyChanged(nameof(ColumnsContent));
        OnPropertyChanged(nameof(ExportContent));
        OnPropertyChanged(nameof(ColumnChoices));
    }


    partial void OnRowsChanged(ViewResult value)
    {
        OnPropertyChanged(nameof(ShowNoMatch));
        OnPropertyChanged(nameof(IsAllCollapsed));
        OnPropertyChanged(nameof(CollapseAllLabel));
        OnPropertyChanged(nameof(CollapseAllTip));
        OnPropertyChanged(nameof(CollapseAllIcon));
        OnPropertyChanged(nameof(CollapseAllContent));
        OnPropertyChanged(nameof(ExportHeader));
        OnPropertyChanged(nameof(GpsDescription));
        OnPropertyChanged(nameof(AiSummary));
        OnPropertyChanged(nameof(HasAiSummary));
        UpdateChips();
        UpdateRail();
        UpdateFocus();
    }

    #endregion // Private methods


    #region Event handlers

    private void Selection_Changed(object? sender, EventArgs e)
    {
        SelectedCount = Selection.RowCount;
        UpdateFocus();
    }

    #endregion // Event handlers
}
