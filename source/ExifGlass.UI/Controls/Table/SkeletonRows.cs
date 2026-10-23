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
using ExifGlass.Core.Export;

namespace ExifGlass.UI.Controls;

/// <summary>
/// The loading table's 14 skeleton rows (DS 6.9, Proto): an 8 px bar per shown column, lined up with the columns, pulsing with a 70 ms stagger per row.
/// </summary>
public sealed class SkeletonRows : Panel
{
    #region Fields and constants

    private const int ROWS = 14;

    private static readonly TableColumns[] COLUMNS = [TableColumns.Index, TableColumns.TagId, TableColumns.TagName, TableColumns.Value];

    private readonly MetadataTable _table;
    private readonly Skeleton[,] _bars = new Skeleton[ROWS, 4];
    private readonly Border[] _lines = new Border[ROWS];
    private bool _isPulsing;

    #endregion // Fields and constants


    #region Constructors

    internal SkeletonRows(MetadataTable table)
    {
        _table = table;
        IsHitTestVisible = false;

        for (var r = 0; r < ROWS; r++)
        {
            for (var c = 0; c < COLUMNS.Length; c++)
            {
                var bar = new Skeleton { Index = r, Classes = { "table-skeleton", ClassOf(COLUMNS[c]) } };
                _bars[r, c] = bar;
                Children.Add(bar);
            }

            _lines[r] = new Border { Height = 1, Classes = { "table-separator" } };
            Children.Add(_lines[r]);
        }
    }

    #endregion // Constructors


    #region Properties

    public bool IsPulsing
    {
        get => _isPulsing;
        set
        {
            if (_isPulsing == value) return;

            _isPulsing = value;
            foreach (var bar in _bars) bar.IsPulsing = value;
        }
    }

    #endregion // Properties


    #region Protected methods

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in Children) child.Measure(availableSize);
        return new Size(0, ROWS * MetadataTable.ROW_HEIGHT);
    }


    protected override Size ArrangeOverride(Size finalSize)
    {
        var layout = _table.ColumnLayout();
        for (var r = 0; r < ROWS; r++)
        {
            var top = r * MetadataTable.ROW_HEIGHT;
            for (var c = 0; c < COLUMNS.Length; c++)
            {
                var bar = _bars[r, c];
                var at = layout.FindIndex(l => l.Column == COLUMNS[c]);
                if (at < 0)
                {
                    bar.Arrange(default);
                    continue;
                }

                var (_, x, width, padLeft) = layout[at];
                var room = Math.Max(0, width - padLeft - 12);
                bar.Arrange(new Rect(x + padLeft, top + (MetadataTable.ROW_HEIGHT - 8) / 2, room * Share(COLUMNS[c], r), 8));
            }

            _lines[r].Arrange(new Rect(0, top + MetadataTable.ROW_HEIGHT - 1, finalSize.Width, 1));
        }

        return finalSize;
    }

    #endregion // Protected methods


    #region Private methods

    // Proto's SKEL widths: # 60 %, Tag ID 70 %, the name and value bars vary by row
    private static double Share(TableColumns column, int row) => column switch
    {
        TableColumns.Index => 0.6,
        TableColumns.TagId => 0.7,
        TableColumns.TagName => (35 + row * 37 % 45) / 100.0,
        _ => (25 + row * 53 % 55) / 100.0,
    };


    private static string ClassOf(TableColumns column) => column switch
    {
        TableColumns.Index => "index",
        TableColumns.TagId => "id",
        TableColumns.TagName => "name",
        _ => "value",
    };

    #endregion // Private methods
}
