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
using ExifGlass.Core.Diagnostics;
using SkiaSharp;

namespace ExifGlass.Diagnostics;

/// <summary>
/// Self-test checks for what the UI layer brings: the pinned SkiaSharp and its native library.
/// </summary>
public static class LibChecks
{
    #region Public methods

    public static SelfTest AddLibChecks(this SelfTest test) => test
        .Add("skia", _ => Task.FromResult(CheckSkia()));

    #endregion // Public methods


    #region Private methods

    private static string CheckSkia()
    {
        var accent = new SKColor(0x22, 0x5E, 0xDB);

        using var surface = SKSurface.Create(new SKImageInfo(32, 32)) ?? throw new InvalidOperationException("SKSurface.Create returned null");
        using (var paint = new SKPaint { Color = accent, IsAntialias = true })
        {
            surface.Canvas.Clear(SKColors.Transparent);
            surface.Canvas.DrawRoundRect(new SKRect(0, 0, 32, 32), 12, 12, paint);
        }

        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        var centre = bitmap.GetPixel(16, 16);

        if (centre != accent) throw new InvalidOperationException($"Drew {accent}, read back {centre}");

        return $"SkiaSharp {SkiaSharpVersion.Native}, drew and read back a rounded rect";
    }

    #endregion // Private methods
}
