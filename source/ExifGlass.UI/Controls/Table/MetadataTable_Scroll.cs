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
using Avalonia.Media;
using ExifGlass.UI.Motion;
using ExifGlass.UI.Tokens;
using System.Diagnostics;

namespace ExifGlass.UI.Controls;

public partial class MetadataTable
{
    #region Fields and constants

    // Proto: a jump pauses the spy, so the rail doesn't flick through the groups the scroll passes
    private static readonly TimeSpan SPY_PAUSE = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan JUMP = TimeSpan.FromMilliseconds(420);

    private readonly TranslateTransform _stickyShift = new();
    private readonly TranslateTransform _ringShift = new();
    private long _spyPausedUntil;
    private Choreo? _jump;
    private string? _ringKey;
    private ExifGlass.Core.Export.TableColumns _ringColumn;

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Scrolls a group's header to the top of the rows in a short glide (the rail's jump); the app shows and expands the group first.
    /// </summary>
    public async Task JumpToGroupAsync(string group)
    {
        if (_scroller is null || Find(TableItem.HeaderKey(group)) is not { } header) return;

        _spyPausedUntil = Stopwatch.GetTimestamp() + (long)(SPY_PAUSE.TotalSeconds * Stopwatch.Frequency);
        ActiveGroup = group;
        _jump?.Stop(jumpToEnd: false);

        // the header's offset is only known once it is realized: find it there, then glide from where the rows were
        var from = _scroller.Offset.Y;
        _grid.ScrollIntoView(header, null);
        _grid.UpdateLayout();
        var to = from;
        foreach (var row in RealizedRows())
        {
            if (ReferenceEquals(row.DataContext, header)) to = Math.Max(0, _scroller.Offset.Y + row.Bounds.Y);
        }

        to = Math.Min(to, Math.Max(0, _scroller.Extent.Height - _scroller.Viewport.Height));
        if (MotionTokens.IsFrozen || Math.Abs(to - from) < 1)
        {
            _scroller.Offset = _scroller.Offset.WithY(to);
            return;
        }

        _scroller.Offset = _scroller.Offset.WithY(from);
        var scroller = _scroller;
        var jump = _jump = new Choreo().Add(JUMP, MotionTokens.EmphasizedEasing, t => scroller.Offset = scroller.Offset.WithY(from + (to - from) * t));
        await jump.PlayAsync(this);
        if (!ReferenceEquals(_jump, jump)) return;

        // estimated row heights may have moved it: land it exactly
        _grid.UpdateLayout();
        foreach (var row in RealizedRows())
        {
            if (ReferenceEquals(row.DataContext, header) && Math.Abs(row.Bounds.Y) > 0.5) scroller.Offset = scroller.Offset.WithY(Math.Max(0, scroller.Offset.Y + row.Bounds.Y));
        }
    }

    /// <summary>
    /// Glides a row to a third of the way down the rows and selects its value cell: Show source tag (AI addendum state-source-jump).
    /// </summary>
    public async Task RevealRowAsync(string key)
    {
        if (_scroller is null || Find(key) is not { IsHeader: false } item) return;

        _jump?.Stop(jumpToEnd: false);
        var from = _scroller.Offset.Y;
        _grid.ScrollIntoView(item, null);
        _grid.UpdateLayout();
        var to = from;
        foreach (var row in RealizedRows())
        {
            if (ReferenceEquals(row.DataContext, item)) to = Math.Max(0, _scroller.Offset.Y + row.Bounds.Y - _scroller.Viewport.Height / 3);
        }

        to = Math.Min(to, Math.Max(0, _scroller.Extent.Height - _scroller.Viewport.Height));
        Selection?.SelectCell(key, ExifGlass.Core.Export.TableColumns.Value);
        if (MotionTokens.IsFrozen || Math.Abs(to - from) < 1)
        {
            _scroller.Offset = _scroller.Offset.WithY(to);
            return;
        }

        _scroller.Offset = _scroller.Offset.WithY(from);
        var scroller = _scroller;
        var jump = _jump = new Choreo().Add(JUMP, MotionTokens.EmphasizedEasing, t => scroller.Offset = scroller.Offset.WithY(from + (to - from) * t));
        await jump.PlayAsync(this);
    }

    #endregion // Public methods


    #region Private methods

    /// <summary>
    /// The sticky copy shows the group that owns the top of the rows and is pushed up by the next real header (S2); the same pass feeds the scroll spy.
    /// </summary>
    private void UpdateSticky()
    {
        if (_presenter is null || _items.Count == 0)
        {
            _sticky.IsVisible = false;
            if (_items.Count == 0) ActiveGroup = null;
            return;
        }

        DataGridRow? top = null;
        var nextHeader = double.MaxValue;
        foreach (var child in _presenter.Children)
        {
            if (child is not DataGridRow { IsVisible: true, DataContext: TableItem item } row) continue;

            var b = row.Bounds;
            if (b.Height <= 0) continue;
            if (b.Y <= 0.5 && b.Bottom > 0.5) top = row;
            else if (item.IsHeader && b.Y > 0.5 && b.Y < nextHeader) nextHeader = b.Y;
        }

        if (top?.DataContext is not TableItem owner)
        {
            _sticky.IsVisible = false;
            return;
        }

        var name = owner.IsHeader ? owner.Group!.Name : owner.Row!.Group;
        UpdateSpy(name);

        // the real header is in place: no copy
        if (owner.IsHeader && top.Bounds.Y >= -0.5)
        {
            _sticky.IsVisible = false;
            return;
        }

        if (Find(TableItem.HeaderKey(name))?.Group is not { } group)
        {
            _sticky.IsVisible = false;
            return;
        }

        // the same group again (collapsed from the sticky copy) turns its chevron; another group takes its own at once
        if (!ReferenceEquals(_sticky.View.Group, group)) _sticky.View.Show(group, animate: _sticky.View.Group?.Name == name);
        _sticky.View.ShowsDescription = !IsNarrow;
        _stickyShift.Y = nextHeader < GROUP_HEADER_HEIGHT ? nextHeader - GROUP_HEADER_HEIGHT : 0;
        _sticky.RenderTransform ??= _stickyShift;
        _sticky.IsVisible = true;
    }


    /// <summary>
    /// One squircle ring over the current cell (DS 6.9: 1.5 px accent, Accent 8 %, radius 10; O1), or over the group header the keyboard is on; it fades in over 160 ms where it lands.
    /// </summary>
    private void UpdateRing(bool fade)
    {
        if (Selection is not { CurrentKey: { } key } selection || Find(key) is not { } item || _presenter is null || (!item.IsHeader && !selection.HasCell))
        {
            HideRing();
            return;
        }

        Rect? area = null;
        foreach (var child in _presenter.Children)
        {
            if (child is not DataGridRow { IsVisible: true } row || !ReferenceEquals(row.DataContext, item)) continue;

            if (item.IsHeader) area = row.TranslatePoint(default, _rowsArea) is { } p ? new Rect(p, row.Bounds.Size).Deflate(new Thickness(4, 2)) : null;
            else if (ColumnFor(selection.CurrentColumn)?.GetCellContent(row)?.Parent is DataGridCell cell && cell.IsVisible && cell.Bounds.Width > 1)
            {
                area = cell.TranslatePoint(default, _rowsArea) is { } p ? new Rect(p, cell.Bounds.Size) : null;
            }

            break;
        }

        if (area is not { } rect)
        {
            HideRing();
            return;
        }

        var moved = !_ring.IsVisible || _ringKey != key || _ringColumn != selection.CurrentColumn;
        _ringKey = key;
        _ringColumn = selection.CurrentColumn;
        _ring.Classes.Set("header", item.IsHeader);

        // a render offset moves it with no layout pass; only a new size measures it again
        _ringShift.X = rect.X;
        _ringShift.Y = rect.Y;
        _ring.RenderTransform ??= _ringShift;
        if (_ring.Width != rect.Width) _ring.Width = rect.Width;
        if (_ring.Height != rect.Height) _ring.Height = rect.Height;
        _ring.IsVisible = true;

        // a ring that lands on another cell fades in there; scrolling and layout only move it
        if (moved && fade && !MotionTokens.IsFrozen) CompositionMotion.Fade(_ring, 0, 1, MotionTokens.Select, MotionTokens.SelectEasing);
    }


    private void HideRing()
    {
        _ring.IsVisible = false;
        _ringKey = null;
    }


    private void UpdateSpy(string group)
    {
        if (Stopwatch.GetTimestamp() < _spyPausedUntil) return;

        // at the very end the last group is the active one, as short groups never reach the top
        if (_scroller is { } s && s.Offset.Y + s.Viewport.Height >= s.Extent.Height - 4 && _items.Count > 0)
        {
            for (var i = _items.Count - 1; i >= 0; i--)
            {
                if (_items[i].Group is { } last)
                {
                    group = last.Name;
                    break;
                }
            }
        }

        ActiveGroup = group;
    }

    #endregion // Private methods


    #region Event handlers

    private void Scroller_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        UpdateSticky();
        UpdateRing(fade: false);
    }

    #endregion // Event handlers
}
