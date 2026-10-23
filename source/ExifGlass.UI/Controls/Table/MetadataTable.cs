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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExifGlass.Core.Export;
using ExifGlass.Core.Query;
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ExifGlass.UI.Controls;

/// <summary>
/// The metadata table (DS 6.9, S2): ProDataGrid on the flat model, where the query engine's group headers are rows and the grid groups nothing; a new view is applied as one reset.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "ProDataGrid marks DataGrid RequiresUnreferencedCode as a class; the table uses generated columns, recycling factories and no property paths (S2).")]
public partial class MetadataTable : Panel
{
    #region Fields and constants

    public const double COLUMN_HEADER_HEIGHT = 32;
    public const double GROUP_HEADER_HEIGHT = 36;
    public const double ROW_HEIGHT = 36;

    public static readonly StyledProperty<ViewResult?> ViewProperty =
        AvaloniaProperty.Register<MetadataTable, ViewResult?>(nameof(View));

    public static readonly StyledProperty<TableSelection?> SelectionProperty =
        AvaloniaProperty.Register<MetadataTable, TableSelection?>(nameof(Selection));

    public static readonly StyledProperty<TableColumns> ColumnsProperty =
        AvaloniaProperty.Register<MetadataTable, TableColumns>(nameof(Columns), TableColumns.Default);

    public static readonly StyledProperty<bool> HidesIdColumnsProperty =
        AvaloniaProperty.Register<MetadataTable, bool>(nameof(HidesIdColumns));

    public static readonly StyledProperty<bool> IsNarrowProperty =
        AvaloniaProperty.Register<MetadataTable, bool>(nameof(IsNarrow));

    public static readonly StyledProperty<SortColumn> SortColumnProperty =
        AvaloniaProperty.Register<MetadataTable, SortColumn>(nameof(SortColumn));

    public static readonly StyledProperty<SortDirection> SortDirectionProperty =
        AvaloniaProperty.Register<MetadataTable, SortDirection>(nameof(SortDirection));

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<MetadataTable, bool>(nameof(IsLoading));

    public static readonly StyledProperty<object?> PlaceholderProperty =
        AvaloniaProperty.Register<MetadataTable, object?>(nameof(Placeholder));

    public static readonly DirectProperty<MetadataTable, string?> ActiveGroupProperty =
        AvaloniaProperty.RegisterDirect<MetadataTable, string?>(nameof(ActiveGroup), t => t.ActiveGroup);

    private readonly DataGrid _grid;
    private readonly ItemSource _source = new();
    private readonly Panel _rowsArea;
    private readonly Canvas _ghostLayer;
    private readonly SquircleBorder _ring;
    private readonly SkeletonRows _skeleton;
    private readonly ContentControl _placeholder;
    private readonly StickyGroupHeader _sticky;
    private readonly HashSet<RowChrome> _chromes = [];
    private readonly TableMotion _motion;

    private List<TableItem> _items = [];
    private Dictionary<string, int> _positions = new(StringComparer.Ordinal);
    private ViewResult? _shown;
    private DataGridRowsPresenter? _presenter;
    private DataGridColumnHeadersPresenter? _headers;
    private ScrollViewer? _scroller;
    private string? _activeGroup;
    private string? _copiedKey;
    private IDisposable? _copiedTimer;
    private Rect _rowsBounds;
    private Point? _pointer;
    private bool _pointerSnapped;

    #endregion // Fields and constants


    #region Constructors

    public MetadataTable()
    {
        ClipToBounds = true;

        _grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserReorderColumns = false,
            CanUserResizeColumns = false,
            CanUserSortColumns = true,
            SelectionMode = DataGridSelectionMode.Single,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.None,
            ColumnHeaderHeight = COLUMN_HEADER_HEIGHT,
            // a hiding column glides to 0: the default 20 px floor would stop it there and the next column would jump when it hides
            MinColumnWidth = 0,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            UseLightweightFiller = true,
            ColumnDefinitionsSource = TableItemSchema.Instance.CreateColumnDefinitions(),
            FastPathOptions = FastPath(),
            ItemsSource = _source,
        };
        _grid.Classes.Add("table");
        ApplyThemes();
        _grid.Sorting += Grid_Sorting;
        _grid.LoadingRow += Grid_LoadingRow;
        _grid.LayoutUpdated += Grid_LayoutUpdated;
        _grid.TemplateApplied += Grid_TemplateApplied;

        _ghostLayer = new Canvas { IsHitTestVisible = false };
        _ring = new SquircleBorder { Classes = { "table-ring" }, IsHitTestVisible = false, IsVisible = false, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        _skeleton = new SkeletonRows(this) { IsVisible = false };
        _placeholder = new ContentControl { IsVisible = false, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Top };
        _sticky = new StickyGroupHeader { IsVisible = false, VerticalAlignment = VerticalAlignment.Top, Height = GROUP_HEADER_HEIGHT };
        _sticky.Click += Sticky_Click;

        // the rows area only: a sticky header pushed up never covers the column header, and the ghost stays under the sticky one
        _rowsArea = new Panel { ClipToBounds = true };
        _rowsArea.Children.AddRange([_ghostLayer, _ring, _skeleton, _placeholder, _sticky]);

        Children.Add(_grid);
        Children.Add(_rowsArea);

        _motion = new TableMotion(this);
        InitializeInput();
        InitializeColumns();
        ConfigureColumns();
    }

    #endregion // Constructors


    #region Properties

    /// <summary>
    /// The query's result; a new one is applied as one reset, with FLIP, the first-load reveal or a group's collapse motion.
    /// </summary>
    public ViewResult? View
    {
        get => GetValue(ViewProperty);
        set => SetValue(ViewProperty, value);
    }

    public TableSelection? Selection
    {
        get => GetValue(SelectionProperty);
        set => SetValue(SelectionProperty, value);
    }

    /// <summary>
    /// The columns the user chose (Columns menu); at least one stays.
    /// </summary>
    public TableColumns Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>
    /// Gets or sets whether # and Tag ID hide for width, below 560 px ("Hidden at this width").
    /// </summary>
    public bool HidesIdColumns
    {
        get => GetValue(HidesIdColumnsProperty);
        set => SetValue(HidesIdColumnsProperty, value);
    }

    /// <summary>
    /// Gets or sets the below-460 rules: first-column inset 28 (+16), Tag name 44 %, no group descriptions.
    /// </summary>
    public bool IsNarrow
    {
        get => GetValue(IsNarrowProperty);
        set => SetValue(IsNarrowProperty, value);
    }

    public SortColumn SortColumn
    {
        get => GetValue(SortColumnProperty);
        set => SetValue(SortColumnProperty, value);
    }

    public SortDirection SortDirection
    {
        get => GetValue(SortDirectionProperty);
        set => SetValue(SortDirectionProperty, value);
    }

    /// <summary>
    /// Gets or sets the loading state: the column header over pulsing skeleton rows (DS 6.9).
    /// </summary>
    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    /// <summary>
    /// What shows under the column header when a view has no rows (the app's no-match state).
    /// </summary>
    public object? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <summary>
    /// The group that owns the top of the rows, for the rail's scroll spy.
    /// </summary>
    public string? ActiveGroup
    {
        get => _activeGroup;
        private set => SetAndRaise(ActiveGroupProperty, ref _activeGroup, value);
    }

    /// <summary>
    /// The items in view, group headers included, in table order.
    /// </summary>
    public IReadOnlyList<TableItem> Items => _items;

    public DataGrid Grid => _grid;

    /// <summary>
    /// The grid's scroll viewer, once its template is applied; the perf run scrolls it.
    /// </summary>
    public ScrollViewer? Scroller => _scroller;

    /// <summary>
    /// How long the last view took on the UI thread, reset and layout (plan 8: applied within one frame).
    /// </summary>
    public TimeSpan LastApply { get; private set; }

    internal StickyGroupHeader Sticky => _sticky;

    internal DataGridRowsPresenter? Presenter => _presenter;

    internal Canvas GhostLayer => _ghostLayer;

    #endregion // Properties


    #region Events

    /// <summary>
    /// A header click: Asc, Desc, then None (the app runs the query).
    /// </summary>
    public event EventHandler<SortColumn>? SortRequested;

    /// <summary>
    /// A group header's click, Enter or arrow: the app collapses or expands the group.
    /// </summary>
    public event EventHandler<string>? GroupToggleRequested;

    /// <summary>
    /// The copy button or a double-click: the row's value goes to the clipboard.
    /// </summary>
    public event EventHandler<ViewRow>? ValueCopyRequested;

    /// <summary>
    /// Ctrl+C in the table: the cell or the selected rows.
    /// </summary>
    public event EventHandler? CopyRequested;

    /// <summary>
    /// Enter on a row: the inspector takes it (Appendix B item 10).
    /// </summary>
    public event EventHandler<ViewRow>? RowActivated;

    #endregion // Events


    #region Public methods

    /// <summary>
    /// The keys of the rows in view in order, group headers and collapsed groups left out: Ctrl+A and Shift ranges.
    /// </summary>
    public static List<string> RowKeys(ViewResult? view)
    {
        var keys = new List<string>(view?.Items.Count ?? 0);
        if (view is null) return keys;

        foreach (var item in view.Items)
        {
            if (item is RowItem row) keys.Add(row.Row.Key);
        }

        return keys;
    }


    public TableItem? Find(string key) => _positions.TryGetValue(key, out var i) ? _items[i] : null;


    /// <summary>
    /// Shows the copied check on a row's button for 1.3 s (DS 6.9).
    /// </summary>
    public void MarkCopied(string key)
    {
        _copiedKey = key;
        RefreshCopyButtons();
        _copiedTimer?.Dispose();
        _copiedTimer = DispatcherTimer.RunOnce(() =>
        {
            _copiedKey = null;
            RefreshCopyButtons();
        }, TimeSpan.FromMilliseconds(1300));
    }

    #endregion // Public methods


    #region Internal methods

    internal bool IsCopied(string key) => _copiedKey == key;


    internal void CopyValue(TableItem item)
    {
        if (item.Row is not { } row) return;

        ValueCopyRequested?.Invoke(this, row);
        MarkCopied(item.Key);
    }


    internal void MaterializeCopy(DataGridRow row)
    {
        if (_colActions?.GetCellContent(row) is CopyCell cell) cell.Materialize();
    }


    internal void Register(RowChrome chrome) => _chromes.Add(chrome);

    internal void Unregister(RowChrome chrome) => _chromes.Remove(chrome);


    #endregion // Internal methods


    #region Protected methods

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ViewProperty) RequestApply(change.GetNewValue<ViewResult?>());
        else if (change.Property == SelectionProperty)
        {
            if (change.GetOldValue<TableSelection?>() is { } old) old.Changed -= Selection_Changed;
            if (change.GetNewValue<TableSelection?>() is { } selection) selection.Changed += Selection_Changed;
            RefreshRows();
        }
        else if (change.Property == ColumnsProperty || change.Property == HidesIdColumnsProperty) ApplyColumns(animate: true);
        else if (change.Property == IsNarrowProperty)
        {
            ApplyColumns(animate: true);
            RefreshRows();
            _sticky.View.ShowsDescription = !IsNarrow;
        }
        else if (change.Property == SortColumnProperty || change.Property == SortDirectionProperty) ApplySortGlyphs();
        else if (change.Property == IsLoadingProperty || change.Property == PlaceholderProperty) UpdateStates();
    }


    protected override Size MeasureOverride(Size availableSize)
    {
        _grid.Measure(availableSize);
        _rowsArea.Measure(_rowsBounds.Size);
        return _grid.DesiredSize;
    }


    protected override Size ArrangeOverride(Size finalSize)
    {
        _grid.Arrange(new Rect(finalSize));

        // the overlays cover exactly the rows presenter, whatever the grid's template puts around it
        var area = RowsBounds(finalSize);
        if (area != _rowsBounds)
        {
            _rowsBounds = area;
            _rowsArea.Measure(area.Size);
        }

        _rowsArea.Arrange(area);
        return finalSize;
    }


    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (e.WidthChanged) ApplyColumns(animate: false);
    }

    #endregion // Protected methods


    #region Private methods

    // accessors only (no reflection under AOT); the grid's own search index is off, as the query engine searches
    private static DataGridFastPathOptions FastPath()
    {
        var options = TableItemSchema.Instance.CreateFastPathOptions();
        options.EnableHighPerformanceSearching = false;
        options.HighPerformanceSearchTrackItemChanges = false;
        return options;
    }


    // DataGrid-owned theme properties raise IL2026 in a XAML setter (S2), so the grid takes its parts' themes here
    private void ApplyThemes()
    {
        if (Application.Current is not { } app) return;

        if (app.TryFindResource("TableGridTheme", out var grid) && grid is ControlTheme gridTheme) _grid.Theme = gridTheme;
        if (app.TryFindResource("TableRowTheme", out var row) && row is ControlTheme rowTheme) _grid.RowTheme = rowTheme;
        if (app.TryFindResource("TableCellTheme", out var cell) && cell is ControlTheme cellTheme) _grid.CellTheme = cellTheme;
        if (app.TryFindResource("TableColumnHeaderTheme", out var header) && header is ControlTheme headerTheme) _grid.ColumnHeaderTheme = headerTheme;
    }


    private Rect RowsBounds(Size size)
    {
        if (_presenter is { } presenter && presenter.TranslatePoint(default, _grid) is { } origin && presenter.Bounds.Width > 0)
        {
            return new Rect(origin, presenter.Bounds.Size);
        }

        return new Rect(0, COLUMN_HEADER_HEIGHT, size.Width, Math.Max(0, size.Height - COLUMN_HEADER_HEIGHT));
    }


    // the newest view wins; a collapse copies what it hides before the reset
    private void RequestApply(ViewResult? view)
    {
        if (ReferenceEquals(view, _shown)) return;
        if (view is null)
        {
            Apply(null, default, null);
            return;
        }

        var motion = TableMotion.Decide(_shown, view);
        var ghost = motion.Kind is ViewMotionKind.Collapse or ViewMotionKind.Expand ? _motion.Capture(motion) : null;
        Apply(view, motion, ghost);
    }


    private void Apply(ViewResult? view, ViewMotion motion, TableMotion.Ghost? ghost)
    {
        var started = Stopwatch.GetTimestamp();
        var before = motion.Kind is ViewMotionKind.Flip or ViewMotionKind.Collapse or ViewMotionKind.Expand ? _motion.Snapshot() : null;

        var items = new List<TableItem>(view?.Items.Count ?? 0);
        var positions = new Dictionary<string, int>(items.Capacity, StringComparer.Ordinal);
        int rows = 0, groups = 0;
        foreach (var viewItem in view?.Items ?? [])
        {
            var item = viewItem switch
            {
                GroupHeaderItem header => new TableItem(this, header.Group, groups++),
                RowItem row => new TableItem(this, row.Row, rows++),
                _ => null,
            };
            if (item is null) continue;

            positions[item.Key] = items.Count;
            items.Add(item);
        }

        // cells take their column's classes when they are made, so the columns are set up before the first rows
        ConfigureColumns();
        _items = items;
        _positions = positions;
        _shown = view;
        Selection?.Prune(key => positions.ContainsKey(key));

        if (motion.Kind == ViewMotionKind.Reveal) _motion.StopAll();
        _source.ResetTo(items, positions);
        if (motion.Kind == ViewMotionKind.Reveal && _scroller is { } scroller) scroller.Offset = default;

        _grid.UpdateLayout();
        LastApply = Stopwatch.GetElapsedTime(started);
        switch (motion.Kind)
        {
            case ViewMotionKind.Reveal:
                _motion.Reveal();
                break;
            case ViewMotionKind.Flip:
                _motion.Play(before!);
                break;
            case ViewMotionKind.Collapse:
            case ViewMotionKind.Expand:
                _motion.PlayGroup(before!, motion, ghost);
                break;
        }

        SnapPointer(all: true);
        UpdateStates();
        UpdateSticky();
        UpdateRing(fade: false);
    }


    // a reset hands the rows to other items: the hover stays with the pointer, not with the row that was under it
    private void SnapPointer(bool all)
    {
        var snapped = false;
        foreach (var chrome in _chromes)
        {
            if (!all && !chrome.IsPointerSnapped) continue;

            chrome.SnapPointer(_pointer);
            snapped |= chrome.IsPointerSnapped;
        }

        _pointerSnapped = snapped;
    }


    private void UpdateStates()
    {
        var loading = IsLoading && _items.Count == 0;
        _skeleton.IsVisible = loading;
        _skeleton.IsPulsing = loading;

        var empty = !loading && _shown is not null && _items.Count == 0;
        _placeholder.Content = empty ? Placeholder : null;
        _placeholder.IsVisible = empty && Placeholder is not null;
    }


    /// <summary>
    /// Re-reads every realized row's state after a selection change, without a new view.
    /// </summary>
    private void RefreshRows()
    {
        foreach (var chrome in _chromes) chrome.Refresh(animate: true);
        RefreshCopyButtons();
        UpdateRing(fade: true);
    }


    private void RefreshCopyButtons()
    {
        if (_presenter is null || _colActions is null) return;

        foreach (var child in _presenter.Children)
        {
            if (child is DataGridRow { DataContext: TableItem item } row && _colActions.GetCellContent(row) is CopyCell cell) cell.Update(item);
        }
    }


    internal IEnumerable<DataGridRow> RealizedRows()
    {
        if (_presenter is null) yield break;

        var height = _presenter.Bounds.Height;
        foreach (var child in _presenter.Children)
        {
            if (child is not DataGridRow { IsVisible: true, DataContext: TableItem } row) continue;

            var b = row.Bounds;
            if (b.Bottom <= 0 || b.Top >= height || b.Height <= 0) continue;
            yield return row;
        }
    }

    #endregion // Private methods


    #region Event handlers

    private void Grid_TemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _presenter = _grid.FindDescendantOfType<DataGridRowsPresenter>();
            _scroller = _grid.FindDescendantOfType<ScrollViewer>();
            _headers = _grid.FindDescendantOfType<DataGridColumnHeadersPresenter>();
            if (_scroller is not null) _scroller.ScrollChanged += Scroller_ScrollChanged;

            ConfigureColumns();
            InvalidateArrange();
        }, DispatcherPriority.Loaded);
    }


    // a recycled row never carries another item's motion
    private void Grid_LoadingRow(object? sender, DataGridRowEventArgs e) => _motion.Recycled(e.Row);


    private void Grid_LayoutUpdated(object? sender, EventArgs e)
    {
        UpdateSticky();
        UpdateRing(fade: false);
    }


    private void Selection_Changed(object? sender, EventArgs e) => RefreshRows();

    #endregion // Event handlers


    #region Nested types

    /// <summary>
    /// The rows in view, replaced as one Reset (plan 4.2: a new view is applied as one reset, never item by item).
    /// </summary>
    private sealed class ItemSource : IList, INotifyCollectionChanged
    {
        private static readonly NotifyCollectionChangedEventArgs RESET = new(NotifyCollectionChangedAction.Reset);
        private List<TableItem> _items = [];
        private Dictionary<string, int> _positions = [];

        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        public void ResetTo(List<TableItem> items, Dictionary<string, int> positions)
        {
            _items = items;
            _positions = positions;
            CollectionChanged?.Invoke(this, RESET);
        }

        public object? this[int index] { get => _items[index]; set => throw new NotSupportedException(); }

        public int Count => _items.Count;

        public bool IsFixedSize => false;

        public bool IsReadOnly => true;

        public bool IsSynchronized => false;

        public object SyncRoot => this;

        public int Add(object? value) => throw new NotSupportedException();

        public void Clear() => throw new NotSupportedException();

        public bool Contains(object? value) => IndexOf(value) >= 0;

        public void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

        public IEnumerator GetEnumerator() => _items.GetEnumerator();

        // the grid asks often (current item, scrolling into view): by key, not a walk over 5,000 items
        public int IndexOf(object? value) => value is TableItem item && _positions.TryGetValue(item.Key, out var i) && ReferenceEquals(_items[i], item) ? i : -1;

        public void Insert(int index, object? value) => throw new NotSupportedException();

        public void Remove(object? value) => throw new NotSupportedException();

        public void RemoveAt(int index) => throw new NotSupportedException();
    }

    #endregion // Nested types
}
