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
using ExifGlass.Core.Export;

namespace ExifGlass.UI.Controls;

/// <summary>
/// The table's selection (DS 6.9): one current cell from a plain click, or a set of rows from Ctrl, Shift and Ctrl+A; keyed by row key, so it outlives a new view.
/// </summary>
public sealed class TableSelection
{
    #region Fields and constants

    private readonly HashSet<string> _rows = new(StringComparer.Ordinal);

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// The row of the current cell, or of the current group header while the keyboard is on one.
    /// </summary>
    public string? CurrentKey { get; private set; }

    /// <summary>
    /// The column of the current cell; None for a group header.
    /// </summary>
    public TableColumns CurrentColumn { get; private set; }

    /// <summary>
    /// Whether the current cell shows its ring: a row selection keeps a cursor for the keyboard but no ring.
    /// </summary>
    public bool HasCell => CurrentKey is not null && CurrentColumn != TableColumns.None && _rows.Count == 0;

    public IReadOnlySet<string> Rows => _rows;

    public int RowCount => _rows.Count;

    /// <summary>
    /// Where a Shift range starts.
    /// </summary>
    public string? Anchor { get; private set; }

    /// <summary>
    /// The tag the inspector shows: the current cell's row, or the last row Ctrl or Shift selected.
    /// </summary>
    public string? FocusedKey { get; private set; }

    #endregion // Properties


    #region Events

    public event EventHandler? Changed;

    #endregion // Events


    #region Public methods

    public bool IsSelected(string key) => _rows.Contains(key);

    public bool IsCurrent(string key, TableColumns column) => HasCell && column == CurrentColumn && key == CurrentKey;


    /// <summary>
    /// A plain click: one cell, no rows.
    /// </summary>
    public void SelectCell(string key, TableColumns column)
    {
        if (_rows.Count == 0 && CurrentKey == key && CurrentColumn == column && FocusedKey == key) return;

        _rows.Clear();
        CurrentKey = key;
        CurrentColumn = column;
        Anchor = key;
        FocusedKey = key;
        OnChanged();
    }


    /// <summary>
    /// Puts the keyboard on a group header: no ring, no rows.
    /// </summary>
    public void SelectHeader(string key)
    {
        _rows.Clear();
        CurrentKey = key;
        CurrentColumn = TableColumns.None;
        Anchor = null;
        OnChanged();
    }


    /// <summary>
    /// Ctrl+click: adds or removes one row.
    /// </summary>
    public void ToggleRow(string key)
    {
        if (!_rows.Remove(key)) _rows.Add(key);

        CurrentKey = key;
        Anchor = key;
        FocusedKey = _rows.Contains(key) ? key : null;
        OnChanged();
    }


    /// <summary>
    /// Shift+click or Shift+arrow: the rows from the anchor to this one, in view order.
    /// </summary>
    public void SelectRange(string key, IReadOnlyList<string> visibleKeys)
    {
        var anchor = Anchor ?? CurrentKey ?? key;
        var a = IndexOf(visibleKeys, anchor);
        var b = IndexOf(visibleKeys, key);
        if (b < 0) return;
        if (a < 0) a = b;

        _rows.Clear();
        for (var i = Math.Min(a, b); i <= Math.Max(a, b); i++) _rows.Add(visibleKeys[i]);

        Anchor = visibleKeys[a];
        CurrentKey = key;
        FocusedKey = key;
        OnChanged();
    }


    /// <summary>
    /// Ctrl+A: every row in view; group headers and collapsed groups' rows aren't in the list.
    /// </summary>
    public void SelectAll(IReadOnlyList<string> visibleKeys)
    {
        if (visibleKeys.Count == 0) return;

        _rows.Clear();
        foreach (var key in visibleKeys) _rows.Add(key);

        CurrentKey ??= visibleKeys[0];
        Anchor ??= visibleKeys[0];
        OnChanged();
    }


    /// <summary>
    /// The Esc chain's last steps: the cell first, then the rows; true when something was cleared.
    /// </summary>
    public bool ClearStep()
    {
        if (HasCell || (CurrentKey is not null && _rows.Count == 0 && CurrentColumn == TableColumns.None))
        {
            Reset();
            return true;
        }

        if (_rows.Count == 0) return false;

        Reset();
        return true;
    }


    /// <summary>
    /// The inspector card's Close: it lets go of the tag, the cell keeps its ring.
    /// </summary>
    public void Unfocus()
    {
        if (FocusedKey is null) return;

        FocusedKey = null;
        OnChanged();
    }


    public void Clear()
    {
        if (CurrentKey is null && _rows.Count == 0 && FocusedKey is null) return;

        Reset();
    }


    /// <summary>
    /// Drops what a new view no longer shows; the cursor survives only on a key still in it.
    /// </summary>
    public void Prune(Func<string, bool> inView)
    {
        var changed = _rows.RemoveWhere(k => !inView(k)) > 0;
        if (CurrentKey is { } current && !inView(current))
        {
            CurrentKey = null;
            CurrentColumn = TableColumns.None;
            changed = true;
        }

        if (Anchor is { } anchor && !inView(anchor)) Anchor = null;
        if (FocusedKey is { } focused && !inView(focused))
        {
            FocusedKey = null;
            changed = true;
        }

        if (changed) OnChanged();
    }

    #endregion // Public methods


    #region Private methods

    private void Reset()
    {
        _rows.Clear();
        CurrentKey = null;
        CurrentColumn = TableColumns.None;
        Anchor = null;
        FocusedKey = null;
        OnChanged();
    }


    private static int IndexOf(IReadOnlyList<string> keys, string key)
    {
        for (var i = 0; i < keys.Count; i++)
        {
            if (keys[i] == key) return i;
        }

        return -1;
    }


    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    #endregion // Private methods
}
