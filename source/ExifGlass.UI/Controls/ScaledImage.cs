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
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Globalization;

namespace ExifGlass.UI.Controls;

/// <summary>
/// A PNG rendered at 1x, 2x and 3x of its box (Source names the pixel size as {0}): the display scale picks the smallest that covers the box.
/// </summary>
public sealed class ScaledImage : Control
{
    #region Fields and constants

    private const int MAX_SCALE = 3;

    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<ScaledImage, string?>(nameof(Source));

    private static readonly Dictionary<string, Bitmap> Bitmaps = [];

    #endregion // Fields and constants


    #region Properties

    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    #endregion // Properties


    #region Constructors

    static ScaledImage()
    {
        AffectsRender<ScaledImage>(SourceProperty);
    }

    #endregion // Constructors


    #region Public methods

    /// <summary>
    /// Gets the file drawn for a box of a width at a display scale.
    /// </summary>
    public static string Pick(string source, double width, double scaling)
    {
        var scale = Math.Clamp((int)Math.Ceiling(scaling - 0.01), 1, MAX_SCALE);
        return string.Format(CultureInfo.InvariantCulture, source, (int)Math.Round(width) * scale);
    }


    public override void Render(DrawingContext context)
    {
        if (Source is not { } source || double.IsNaN(Width)) return;

        var uri = Pick(source, Width, TopLevel.GetTopLevel(this)?.RenderScaling ?? 1);
        if (!Bitmaps.TryGetValue(uri, out var bitmap))
        {
            using var stream = AssetLoader.Open(new Uri(uri));
            Bitmaps[uri] = bitmap = new Bitmap(stream);
        }

        // by its pixels, whatever DPI the file claims
        using (context.PushRenderOptions(new RenderOptions { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
        {
            context.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), new Rect(Bounds.Size));
        }
    }

    #endregion // Public methods
}
