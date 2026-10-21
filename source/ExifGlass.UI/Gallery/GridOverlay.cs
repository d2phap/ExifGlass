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
using Avalonia.Media.Immutable;

namespace ExifGlass.UI.Gallery;

/// <summary>
/// The gallery's 8-pt grid: a hairline every 8 DIPs and a stronger one every 64, for checking alignment, padding and margins.
/// </summary>
public sealed class GridOverlay : Control
{
    #region Fields and constants

    private const double STEP = 8;
    private const int MAJOR = 8;

    private static readonly ImmutablePen Minor = new(new ImmutableSolidColorBrush(Color.FromArgb(0x1A, 0xFF, 0x3D, 0x7F)), 1);
    private static readonly ImmutablePen Major = new(new ImmutableSolidColorBrush(Color.FromArgb(0x40, 0xFF, 0x3D, 0x7F)), 1);

    #endregion // Fields and constants


    #region Constructors

    public GridOverlay()
    {
        IsHitTestVisible = false;
    }

    #endregion // Constructors


    #region Public methods

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        var i = 0;
        for (var x = 0.5; x < size.Width; x += STEP, i++) context.DrawLine(i % MAJOR == 0 ? Major : Minor, new Point(x, 0), new Point(x, size.Height));

        i = 0;
        for (var y = 0.5; y < size.Height; y += STEP, i++) context.DrawLine(i % MAJOR == 0 ? Major : Minor, new Point(0, y), new Point(size.Width, y));
    }

    #endregion // Public methods
}
