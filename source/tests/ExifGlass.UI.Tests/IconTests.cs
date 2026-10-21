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
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using ExifGlass.UI.Controls;
using ExifGlass.UI.Gallery;
using ExifGlass.UI.Tokens;
using Xunit;

namespace ExifGlass.UI.Tests;

public class IconTests
{
    #region Fields and constants

    private static readonly double[] Sizes = [12, 14, 16, 18];
    private const double CELL = 160;
    private const int COLUMNS = 6;

    #endregion // Fields and constants


    #region Tests

    // plan P3: every icon zoom-checked at 12-18 px in both themes; the sheet is saved for review, and each size must draw ink
    [AvaloniaTheory]
    [InlineData("light")]
    [InlineData("dark")]
    public void EveryIconDrawsAtEverySize(string theme)
    {
        var variant = theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var ids = Enum.GetValues<IconId>();
        var grid = new WrapPanel { Width = CELL * COLUMNS, Orientation = Orientation.Horizontal };
        var icons = new List<(IconId Id, GlassIcon Icon)>();

        foreach (var id in ids)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Height = 22 };
            foreach (var size in Sizes)
            {
                var icon = new GlassIcon { Icon = id, Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center };
                icons.Add((id, icon));
                row.Children.Add(icon);
            }

            var cell = new StackPanel { Width = CELL, Margin = new Thickness(0, 0, 0, 8), Spacing = 2 };
            cell.Children.Add(row);
            cell.Children.Add(DesignGallery.Text(id.ToString(), TypeClasses.META, TokenKeys.TEXT_SECONDARY_BRUSH));
            grid.Children.Add(cell);
        }

        var content = new Border { Padding = new Thickness(12), Child = grid };
        var rows = (int)Math.Ceiling(ids.Length / (double)COLUMNS);
        var areas = new List<(IconId Id, Rect Area)>();
        var frame = Render.CaptureSettled(content, CELL * COLUMNS + 24, rows * 48 + 40, variant, window =>
        {
            foreach (var (id, icon) in icons)
            {
                if (icon.TranslatePoint(default, window) is { } at) areas.Add((id, new Rect(at, icon.Bounds.Size)));
            }
        }, settleMs: 100);

        Render.Save(frame, $"icons-{theme}");

        var blank = new List<string>();
        foreach (var (id, area) in areas)
        {
            if (!HasInk(frame, area)) blank.Add($"{id} at {area.Width}");
        }

        Assert.True(blank.Count == 0, "Icons that draw nothing: " + string.Join(", ", blank));
    }

    #endregion // Tests


    #region Private methods

    // ink: any pixel that differs from the top-left one, which is the window background
    private static unsafe bool HasInk(WriteableBitmap frame, Rect area)
    {
        using var buffer = frame.Lock();
        var background = *(uint*)buffer.Address;
        for (var y = (int)area.Y; y < (int)area.Bottom; y++)
        {
            var row = (uint*)((byte*)buffer.Address + y * buffer.RowBytes);
            for (var x = (int)area.X; x < (int)area.Right; x++)
            {
                if (row[x] != background) return true;
            }
        }

        return false;
    }

    #endregion // Private methods
}
