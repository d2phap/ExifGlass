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
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ExifGlass.Core.Export;

namespace ExifGlass.UI.Controls;

public partial class MetadataTable
{
    #region Fields and constants

    private const KeyModifiers TOGGLE_MODIFIERS = KeyModifiers.Control | KeyModifiers.Meta;

    // the column the keyboard returns to after a group header, and a plain click's fallback
    private TableColumns _lastColumn = TableColumns.Value;

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Gives the table keyboard focus; the keyboard starts on the first item when nothing is current.
    /// </summary>
    public void FocusTable()
    {
        _grid.Focus(NavigationMethod.Tab);
        if (Selection is { CurrentKey: null } && _items.Count > 0) MoveTo(0, extend: false);
    }

    #endregion // Public methods


    #region Private methods

    private void InitializeInput()
    {
        // tunnel: the grid's own selection, current cell and Ctrl+C never run; the table's selection is ours (DS 6.9)
        _grid.AddHandler(PointerPressedEvent, Grid_PointerPressed, RoutingStrategies.Tunnel);
        _grid.AddHandler(PointerMovedEvent, Grid_PointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        _grid.AddHandler(PointerExitedEvent, Grid_PointerExited, RoutingStrategies.Direct, handledEventsToo: true);
        _grid.AddHandler(KeyDownEvent, Grid_KeyDown, RoutingStrategies.Tunnel);
        _grid.GotFocus += Grid_GotFocus;
        KeyboardNavigation.SetTabNavigation(_grid, KeyboardNavigationMode.Once);
    }


    /// <summary>
    /// The data column under a point in a row, by the columns' widths; the actions column and the inset count as their neighbours.
    /// </summary>
    private TableColumns ColumnAt(double x)
    {
        var left = 0.0;
        var last = TableColumns.None;
        foreach (var column in _grid.Columns)
        {
            if (!column.IsVisible) continue;

            left += column.ActualWidth;
            var data = ColumnOf(column);
            if (data != TableColumns.None) last = data;
            if (x < left) return data == TableColumns.None ? last : data;
        }

        return last == TableColumns.None ? _lastColumn : last;
    }


    private TableColumns CursorColumn()
    {
        var order = ShownOrder();
        if (Selection?.CurrentColumn is { } current && order.Contains(current)) return current;

        return order.Contains(_lastColumn) ? _lastColumn : order[^1];
    }


    private void MoveTo(int index, bool extend)
    {
        if (_items.Count == 0 || Selection is not { } selection) return;

        index = Math.Clamp(index, 0, _items.Count - 1);
        var item = _items[index];

        // a Shift range is made of rows: it steps over headers in the direction of travel
        if (extend)
        {
            var from = selection.CurrentKey is { } key && _positions.TryGetValue(key, out var at) ? at : index;
            var step = index >= from ? 1 : -1;
            while (item.IsHeader && index + step >= 0 && index + step < _items.Count) item = _items[index += step];
            if (item.IsHeader) return;

            selection.SelectRange(item.Key, RowKeys(_shown));
        }
        else if (item.IsHeader) selection.SelectHeader(item.Key);
        else selection.SelectCell(item.Key, CursorColumn());

        BringIntoView(item);
    }


    private void MoveBy(int delta, bool extend)
    {
        var current = Selection?.CurrentKey is { } key && _positions.TryGetValue(key, out var at) ? at : -1;
        MoveTo(current < 0 ? (delta > 0 ? 0 : _items.Count - 1) : current + delta, extend);
    }


    private void MoveColumn(int delta, bool toEnd)
    {
        if (Selection is not { CurrentKey: { } key } selection || Find(key) is not { IsHeader: false } item) return;

        var order = ShownOrder();
        var at = order.IndexOf(CursorColumn());
        var next = toEnd ? (delta < 0 ? 0 : order.Count - 1) : Math.Clamp(at + delta, 0, order.Count - 1);
        _lastColumn = order[next];
        selection.SelectCell(item.Key, _lastColumn);
        BringIntoView(item);
    }


    private void BringIntoView(TableItem item)
    {
        _grid.ScrollIntoView(item, item.IsHeader ? null : ColumnFor(CursorColumn()));
        _grid.UpdateLayout();

        // the sticky header covers the top 36 px: a row brought to the top would sit under it
        if (item.IsHeader || _scroller is null) return;

        foreach (var row in RealizedRows())
        {
            if (!ReferenceEquals(row.DataContext, item)) continue;

            var hidden = GROUP_HEADER_HEIGHT - row.Bounds.Y;
            if (hidden > 0 && _sticky.IsVisible) _scroller.Offset = _scroller.Offset.WithY(Math.Max(0, _scroller.Offset.Y - hidden));
            break;
        }
    }


    private int PageSize() => Math.Max(1, (int)(_rowsBounds.Height / ROW_HEIGHT) - 1);


    private void ToggleGroup(TableItem header, bool? collapse = null)
    {
        if (header.Group is not { } group) return;
        if (collapse is { } wanted && wanted == group.IsCollapsed) return;

        GroupToggleRequested?.Invoke(this, group.Name);
    }

    #endregion // Private methods


    #region Event handlers

    private void Grid_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _pointer = _presenter is null ? null : e.GetPosition(_presenter);
        if (e.Source is not Visual source || !e.GetCurrentPoint(_grid).Properties.IsLeftButtonPressed) return;

        // the column headers sort, the copy button copies, the scroll bar scrolls: they keep their own presses
        if (source.FindAncestorOfType<DataGridColumnHeader>(includeSelf: true) is not null) return;
        if (source.FindAncestorOfType<ScrollBar>(includeSelf: true) is not null) return;
        if (source.FindAncestorOfType<Button>(includeSelf: true) is not null) return;
        if (source.FindAncestorOfType<DataGridRow>(includeSelf: true) is not { DataContext: TableItem item } row) return;

        e.Handled = true;
        _grid.Focus(NavigationMethod.Pointer);

        // a click only toggles the group; the header's ring is the keyboard's
        if (item.IsHeader)
        {
            ToggleGroup(item);
            return;
        }

        // the first click of a double-click already selected the cell; a quick Ctrl or Shift click is still a selection
        if (e.ClickCount == 2 && e.KeyModifiers == KeyModifiers.None)
        {
            CopyValue(item);
            return;
        }

        if (Selection is not { } selection) return;

        if ((e.KeyModifiers & KeyModifiers.Shift) != 0) selection.SelectRange(item.Key, RowKeys(_shown));
        else if ((e.KeyModifiers & TOGGLE_MODIFIERS) != 0) selection.ToggleRow(item.Key);
        else
        {
            _lastColumn = ColumnAt(e.GetPosition(row).X);
            selection.SelectCell(item.Key, _lastColumn);
        }
    }


    // while a snapped row's own pointer state lags (a press holds the capture), its hover follows the pointer
    private void Grid_PointerMoved(object? sender, PointerEventArgs e)
    {
        _pointer = _presenter is null ? null : e.GetPosition(_presenter);
        if (_pointerSnapped) SnapPointer(all: false);
    }


    // a capture taken by a press moves pointer-over away from the grid while the pointer stays in it
    private void Grid_PointerExited(object? sender, PointerEventArgs e)
    {
        if (new Rect(_grid.Bounds.Size).Contains(e.GetPosition(_grid))) return;

        _pointer = null;
        if (_pointerSnapped) SnapPointer(all: false);
    }


    private void Grid_KeyDown(object? sender, KeyEventArgs e)
    {
        var primary = (e.KeyModifiers & TOGGLE_MODIFIERS) != 0;
        var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        var current = Selection?.CurrentKey is { } key ? Find(key) : null;

        switch (e.Key)
        {
            case Key.A when primary:
                Selection?.SelectAll(RowKeys(_shown));
                break;
            case Key.C when primary:
                CopyRequested?.Invoke(this, EventArgs.Empty);
                break;
            case Key.Up:
                MoveBy(-1, shift);
                break;
            case Key.Down:
                MoveBy(1, shift);
                break;
            case Key.PageUp:
                MoveBy(-PageSize(), shift);
                break;
            case Key.PageDown:
                MoveBy(PageSize(), shift);
                break;
            case Key.Home when primary:
                MoveTo(0, shift);
                break;
            case Key.End when primary:
                MoveTo(_items.Count - 1, shift);
                break;
            case Key.Home:
                MoveColumn(-1, toEnd: true);
                break;
            case Key.End:
                MoveColumn(1, toEnd: true);
                break;

            // on a group header the arrows collapse and expand it, as in a tree (S2: header rows are toggles)
            case Key.Left when current is { IsHeader: true }:
                ToggleGroup(current, collapse: true);
                break;
            case Key.Right when current is { IsHeader: true }:
                ToggleGroup(current, collapse: false);
                break;
            case Key.Left:
                MoveColumn(-1, toEnd: false);
                break;
            case Key.Right:
                MoveColumn(1, toEnd: false);
                break;
            case Key.Enter or Key.Space when current is { IsHeader: true }:
                ToggleGroup(current);
                break;
            case Key.Enter when current?.Row is { } row:
                RowActivated?.Invoke(this, row);
                break;

            // one tab stop: Tab leaves the table rather than walking its cells
            case Key.Tab:
                if (TopLevel.GetTopLevel(this)?.FocusManager is not { } focus) return;
                focus.TryMoveFocus(shift ? NavigationDirection.Previous : NavigationDirection.Next);
                break;
            default:
                return;
        }

        e.Handled = true;
    }


    private void Grid_GotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (e.NavigationMethod == NavigationMethod.Tab && Selection is { CurrentKey: null } && _items.Count > 0) MoveTo(0, extend: false);
    }


    private void Sticky_Click(object? sender, RoutedEventArgs e)
    {
        // the AI group's source link inside the copy is a button of its own
        if (!ReferenceEquals(e.Source, _sticky)) return;

        if (_sticky.View.Group is { } group && Find(TableItem.HeaderKey(group.Name)) is { } header) ToggleGroup(header);
        else if (_sticky.View.Group is { } shown) GroupToggleRequested?.Invoke(this, shown.Name);
    }

    #endregion // Event handlers
}
