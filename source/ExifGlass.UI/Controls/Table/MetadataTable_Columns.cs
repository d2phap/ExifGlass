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
using Avalonia.VisualTree;
using Avalonia.Rendering.Composition;
using ExifGlass.Core.Export;
using ExifGlass.Core.Query;
using ExifGlass.UI.Tokens;
using System.Diagnostics;

namespace ExifGlass.UI.Controls;

public partial class MetadataTable
{
    #region Fields and constants

    private const double INDEX_WIDTH = 52;
    private const double ID_WIDTH = 88;
    private const double NAME_MIN_WIDTH = 130;
    private const double ACTIONS_WIDTH = 36;

    // the first visible column's left inset (DS 6.9: the selection bar sits in it) and what its width grows by
    private const double INSET = 34;
    private const double INSET_NARROW = 28;
    private const double CELL_PADDING = 12;

    private static readonly TableColumns[] ORDER = [TableColumns.Index, TableColumns.TagId, TableColumns.TagName, TableColumns.Value];

    private DataGridColumn? _colIndex;
    private DataGridColumn? _colId;
    private DataGridColumn? _colName;
    private DataGridColumn? _colValue;
    private DataGridColumn? _colActions;
    private TableColumns _shownColumns = TableColumns.None;
    private bool _firstNarrow;
    private int _columnGeneration;
    private TableColumns _classFirst = TableColumns.None;
    private bool _classNarrow;

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// The columns on screen: the user's set less what the width hides, and never none (Tag name stays).
    /// </summary>
    public TableColumns ShownColumns
    {
        get
        {
            var shown = Columns & TableColumns.All;
            if (HidesIdColumns) shown &= ~(TableColumns.Index | TableColumns.TagId);
            return shown == TableColumns.None ? TableColumns.TagName : shown;
        }
    }

    /// <summary>
    /// Gets whether a column glide is running; the glide sets widths each frame and a resize waits for it.
    /// </summary>
    public bool IsAnimatingColumns { get; private set; }

    #endregion // Properties


    #region Private methods

    private void InitializeColumns() => _firstNarrow = IsNarrow;


    // the generated definitions become columns on the grid's first layout
    private void ConfigureColumns()
    {
        if (_colName is not null || _grid.Columns.Count == 0) return;

        foreach (var column in _grid.Columns)
        {
            switch (column.ColumnKey as string)
            {
                case TableItem.COLUMN_INDEX: _colIndex = column; break;
                case TableItem.COLUMN_ID: _colId = column; break;
                case TableItem.COLUMN_NAME: _colName = column; break;
                case TableItem.COLUMN_VALUE: _colValue = column; break;
                case TableItem.COLUMN_ACTIONS: _colActions = column; break;
            }
        }

        if (_colActions is not null)
        {
            _colActions.CanUserSort = false;
            _colActions.CellStyleClasses.Add("actions");
            _colActions.HeaderStyleClasses.Add("actions");
        }

        ApplyColumns(animate: false);
        ApplySortGlyphs();
    }


    private IEnumerable<DataGridColumn> DataColumns()
    {
        if (_colIndex is not null) yield return _colIndex;
        if (_colId is not null) yield return _colId;
        if (_colName is not null) yield return _colName;
        if (_colValue is not null) yield return _colValue;
    }


    private DataGridColumn? ColumnFor(TableColumns column) => column switch
    {
        TableColumns.Index => _colIndex,
        TableColumns.TagId => _colId,
        TableColumns.TagName => _colName,
        TableColumns.Value => _colValue,
        _ => null,
    };


    private TableColumns ColumnOf(DataGridColumn column) => (column.ColumnKey as string) switch
    {
        TableItem.COLUMN_INDEX => TableColumns.Index,
        TableItem.COLUMN_ID => TableColumns.TagId,
        TableItem.COLUMN_NAME => TableColumns.TagName,
        TableItem.COLUMN_VALUE => TableColumns.Value,
        _ => TableColumns.None,
    };


    /// <summary>
    /// The shown data columns in table order, for the keyboard's left and right.
    /// </summary>
    private List<TableColumns> ShownOrder()
    {
        var shown = ShownColumns;
        var list = new List<TableColumns>(4);
        foreach (var column in ORDER)
        {
            if ((shown & column) != 0) list.Add(column);
        }

        return list;
    }


    /// <summary>
    /// Where each shown data column sits across the table, with its cell padding: the skeleton rows line up with it.
    /// </summary>
    internal List<(TableColumns Column, double X, double Width, double PadLeft)> ColumnLayout()
    {
        var layout = new List<(TableColumns, double, double, double)>(4);
        var first = FirstShown();
        var inset = IsNarrow ? INSET_NARROW : INSET;
        var x = 0.0;
        foreach (var column in _grid.Columns)
        {
            if (!column.IsVisible) continue;

            var data = ColumnOf(column);
            if (data != TableColumns.None) layout.Add((data, x, column.ActualWidth, data == first ? inset : CELL_PADDING));
            x += column.ActualWidth;
        }

        return layout;
    }


    private TableColumns FirstShown()
    {
        var shown = ShownColumns;
        foreach (var column in ORDER)
        {
            if ((shown & column) != 0) return column;
        }

        return TableColumns.TagName;
    }


    // DS 6.9 / Proto: fixed widths, Tag name 36 % (44 % narrow, at least 130) beside Value, or filling without it; the first column grows by its inset
    private double TargetWidth(TableColumns column, TableColumns shown, TableColumns first, double tableWidth)
    {
        if ((shown & column) == 0) return 0;

        var extra = column == first ? (IsNarrow ? INSET_NARROW : INSET) - CELL_PADDING : 0;
        return column switch
        {
            TableColumns.Index => INDEX_WIDTH + extra,
            TableColumns.TagId => ID_WIDTH + extra,
            TableColumns.TagName => Math.Max(NAME_MIN_WIDTH, Math.Round(tableWidth * (IsNarrow ? 0.44 : 0.36))) + extra,
            _ => 0,
        };
    }


    /// <summary>
    /// Shows, hides and sizes the columns; with animate, a change glides 0 to its width over 380 ms and the cells fade in or out over 220 ms (DS 5.2).
    /// </summary>
    private void ApplyColumns(bool animate)
    {
        if (_colName is null || _colValue is null) return;

        var shown = ShownColumns;
        var first = FirstShown();
        var width = Bounds.Width > 0 ? Bounds.Width : 800;
        var narrowChanged = _firstNarrow != IsNarrow;
        _firstNarrow = IsNarrow;

        // a resize lands here every frame of a docked glide: the classes only change with the first column or the narrow rule
        if (first != _classFirst || IsNarrow != _classNarrow)
        {
            _classFirst = first;
            _classNarrow = IsNarrow;
            foreach (var column in DataColumns())
            {
                var isFirst = ColumnOf(column) == first;
                column.CellStyleClasses.Set("first", isFirst);
                column.HeaderStyleClasses.Set("first", isFirst);
                column.CellStyleClasses.Set("narrow", IsNarrow);
                column.HeaderStyleClasses.Set("narrow", IsNarrow);
            }

            SyncCellClasses();
        }

        var valueShown = (shown & TableColumns.Value) != 0;
        var visibilityChanged = shown != _shownColumns;
        var appearing = shown & ~_shownColumns;
        var leaving = _shownColumns & ~shown;
        _shownColumns = shown;

        // Value fills, so it shows and hides with a fade and no glide (the prototype's grid snaps there too)
        if (_colValue.IsVisible != valueShown)
        {
            _colValue.IsVisible = valueShown;
            if (animate && valueShown) FadeCells(_colValue, appear: true);
        }

        if (!animate || MotionTokens.IsFrozen || (!visibilityChanged && !narrowChanged))
        {
            if (IsAnimatingColumns && !visibilityChanged && !narrowChanged) return;

            _columnGeneration++;
            IsAnimatingColumns = false;
            foreach (var column in ORDER)
            {
                if (ColumnFor(column) is not { } grid || column == TableColumns.Value) continue;

                var length = column == TableColumns.TagName && !valueShown
                    ? new DataGridLength(1, DataGridLengthUnitType.Star)
                    : new DataGridLength(TargetWidth(column, shown, first, width));
                if (grid.IsVisible != ((shown & column) != 0)) grid.IsVisible = (shown & column) != 0;
                if (grid.Width != length) grid.Width = length;
            }

            return;
        }

        _ = GlideColumnsAsync(shown, first, width, appearing, leaving, valueShown);
    }


    private async Task GlideColumnsAsync(TableColumns shown, TableColumns first, double width, TableColumns appearing, TableColumns leaving, bool valueShown)
    {
        if (TopLevel.GetTopLevel(this) is not { } top) return;

        var generation = ++_columnGeneration;
        IsAnimatingColumns = true;

        var glides = new List<(DataGridColumn Column, double From, double To, bool Hide)>(3);
        foreach (var column in ORDER)
        {
            if (column == TableColumns.Value || ColumnFor(column) is not { } grid) continue;

            // Tag name fills when Value is gone: it snaps to the star width, the others still glide
            if (column == TableColumns.TagName && !valueShown)
            {
                grid.IsVisible = true;
                grid.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                continue;
            }

            var from = grid.IsVisible ? grid.ActualWidth : 0;
            if (!grid.IsVisible && (shown & column) != 0)
            {
                grid.Width = new DataGridLength(0);
                grid.IsVisible = true;
            }

            var to = TargetWidth(column, shown, first, width);
            if (Math.Abs(from - to) < 0.5 && (leaving & column) == 0)
            {
                grid.Width = new DataGridLength(to);
                continue;
            }

            glides.Add((grid, from, to, (leaving & column) != 0));
            if ((appearing & column) != 0) FadeCells(grid, appear: true);
            else if ((leaving & column) != 0) FadeCells(grid, appear: false);
        }

        if (glides.Count == 0)
        {
            IsAnimatingColumns = false;
            return;
        }

        var done = new TaskCompletionSource();
        long? start = null;
        var ms = MotionTokens.Column.TotalMilliseconds;
        void Tick(TimeSpan _)
        {
            if (generation != _columnGeneration)
            {
                done.TrySetResult();
                return;
            }

            var now = Stopwatch.GetTimestamp();
            start ??= now;
            var t = Math.Min(1, Stopwatch.GetElapsedTime(start.Value, now).TotalMilliseconds / ms);
            var eased = MotionTokens.ColumnEasing.Ease(t);
            foreach (var (column, from, to, _) in glides) column.Width = new DataGridLength(Math.Max(0, from + (to - from) * eased));

            if (t < 1)
            {
                top.RequestAnimationFrame(Tick);
                return;
            }

            foreach (var (column, _, _, hide) in glides)
            {
                if (hide) column.IsVisible = false;
            }

            done.TrySetResult();
        }

        top.RequestAnimationFrame(Tick);
        await done.Task;

        if (generation != _columnGeneration) return;

        IsAnimatingColumns = false;
        ApplyColumns(animate: false);
    }


    // the grid copies a column's cell classes into a cell when it makes it; cells already made, recycled ones too, follow here
    private void SyncCellClasses()
    {
        if (_presenter is null) return;

        foreach (var child in _presenter.Children)
        {
            if (child is not DataGridRow row || row.FindDescendantOfType<DataGridCellsPresenter>() is not { } cells) continue;

            foreach (var item in cells.Children)
            {
                if (item is not DataGridCell { OwningColumn: { } column } cell) continue;

                cell.Classes.Set("first", column.CellStyleClasses.Contains("first"));
                cell.Classes.Set("narrow", column.CellStyleClasses.Contains("narrow"));
                cell.Classes.Set("actions", column.CellStyleClasses.Contains("actions"));
            }
        }
    }


    private void FadeCells(DataGridColumn column, bool appear)
    {
        foreach (var row in RealizedRows())
        {
            if (column.GetCellContent(row)?.Parent is DataGridCell cell) Fade(cell, appear);
        }

        // the label fades with its cells, or a closing column would clip it letter by letter
        foreach (var header in _headers?.Children ?? [])
        {
            if (header is DataGridColumnHeader { IsVisible: true } h && Equals(h.Content, column.Header)) Fade(h, appear);
        }
    }


    private static void Fade(Visual target, bool appear)
    {
        if (ElementComposition.GetElementVisual(target) is not { } visual) return;

        var fade = visual.Compositor.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(0f, appear ? 0f : 1f);
        fade.InsertKeyFrame(1f, appear ? 1f : 0f);
        fade.Duration = MotionTokens.Fade;
        visual.StartAnimation("Opacity", fade);
    }


    private void ApplySortGlyphs()
    {
        var sorted = SortDirection == SortDirection.None ? null : ColumnFor(SortColumn switch
        {
            SortColumn.Index => TableColumns.Index,
            SortColumn.TagId => TableColumns.TagId,
            SortColumn.TagName => TableColumns.TagName,
            SortColumn.Value => TableColumns.Value,
            _ => TableColumns.None,
        });

        foreach (var column in DataColumns())
        {
            var on = ReferenceEquals(column, sorted);
            column.HeaderStyleClasses.Set("asc", on && SortDirection == SortDirection.Ascending);
            column.HeaderStyleClasses.Set("desc", on && SortDirection == SortDirection.Descending);
        }
    }

    #endregion // Private methods


    #region Event handlers

    // the query engine sorts within groups off the UI thread; the grid's own sorting model stays out (S2)
    private void Grid_Sorting(object? sender, DataGridColumnEventArgs e)
    {
        e.Handled = true;
        var column = ColumnOf(e.Column) switch
        {
            TableColumns.Index => SortColumn.Index,
            TableColumns.TagId => SortColumn.TagId,
            TableColumns.TagName => SortColumn.TagName,
            TableColumns.Value => SortColumn.Value,
            _ => SortColumn.None,
        };

        if (column != SortColumn.None && _items.Count > 0) SortRequested?.Invoke(this, column);
    }

    #endregion // Event handlers
}
