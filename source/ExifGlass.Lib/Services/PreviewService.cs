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
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ExifGlass.Core.Metadata;
using SkiaSharp;
using System.Globalization;

namespace ExifGlass.Services;

/// <summary>
/// The Frame card's and file card's preview (plan 4.4): common formats decode from the file, the rest from ExifTool's embedded preview; decoded at the card's size, turned upright.
/// </summary>
public static class PreviewService
{
    #region Fields and constants

    /// <summary>
    /// The Frame card's box: as wide as the card allows, at most this tall (Proto: min(278, 220 * w / h)).
    /// </summary>
    public const double FRAME_WIDTH = 278;

    public const double FRAME_HEIGHT = 220;

    private static readonly HashSet<string> DIRECT_TYPES = new(StringComparer.OrdinalIgnoreCase) { "JPEG", "PNG", "WEBP", "BMP", "GIF" };

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Whether the file decodes by itself, without asking ExifTool for an embedded preview.
    /// </summary>
    public static bool IsDirect(MetadataDocument doc) => doc.Find("File", "FileType")?.Value is { } type && DIRECT_TYPES.Contains(type);


    /// <summary>
    /// The preview for <paramref name="scaling"/> device pixels per DIP, or null when there is none or it can't be decoded.
    /// </summary>
    public static async Task<Bitmap?> LoadAsync(MetadataDocument doc, IMetadataReader reader, double scaling, CancellationToken cancellationToken)
    {
        var orientation = Orientation(doc);
        try
        {
            if (IsDirect(doc))
            {
                var path = doc.SourcePath;
                return await Task.Run(() =>
                {
                    using var stream = File.OpenRead(path);
                    return Decode(stream, orientation, scaling, cancellationToken);
                }, cancellationToken).ConfigureAwait(false);
            }

            var bytes = await reader.ReadPreviewAsync(doc.SourcePath, cancellationToken).ConfigureAwait(false);
            if (bytes is not { Length: > 0 }) return null;

            return await Task.Run(() =>
            {
                using var stream = new MemoryStream(bytes, writable: false);
                return Decode(stream, orientation, scaling, cancellationToken);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException
            or Core.ExifTool.ExifToolException or ExifToolUnavailableException or TimeoutException)
        {
            return null;
        }
    }


    /// <summary>
    /// Decodes an image no larger than the Frame card needs at this scaling, upright for its EXIF orientation (1 to 8).
    /// </summary>
    public static Bitmap? Decode(Stream stream, int orientation, double scaling, CancellationToken cancellationToken = default)
    {
        using var codec = SKCodec.Create(stream);
        if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0) return null;

        var (width, height) = (codec.Info.Width, codec.Info.Height);
        var turned = orientation >= 5;
        var (shownWidth, shownHeight) = turned ? (height, width) : (width, height);
        var box = BoxFor(shownWidth, shownHeight);
        var scale = Math.Min(1, Math.Max(box.Width * scaling / shownWidth, box.Height * scaling / shownHeight));

        // JPEG decodes at a scale of n/8 directly; other codecs decode whole and are scaled down after
        var decodeSize = codec.GetScaledDimensions((float)scale);
        var info = new SKImageInfo(decodeSize.Width, decodeSize.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var decoded = new SKBitmap(info);
        var result = codec.GetPixels(info, decoded.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput)) return null;
        cancellationToken.ThrowIfCancellationRequested();

        var target = new SKSizeI(Math.Max(1, (int)Math.Ceiling(width * scale)), Math.Max(1, (int)Math.Ceiling(height * scale)));
        using var sized = target.Width < decoded.Width ? Resize(decoded, target) : null;
        var source = sized ?? decoded;

        using var upright = orientation is > 1 and <= 8 ? Orient(source, orientation) : null;
        var final = upright ?? source;

        // the Bitmap copies the pixels, so the Skia bitmaps go with this method
        return new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Premul, final.GetPixels(), new PixelSize(final.Width, final.Height), new Vector(96, 96), final.RowBytes);
    }


    /// <summary>
    /// The Frame card's box for an image of this shown size, in DIPs; 3:2 when the size is unknown.
    /// </summary>
    public static Size BoxFor(double width, double height)
    {
        if (width <= 0 || height <= 0) return new Size(FRAME_WIDTH, FRAME_WIDTH * 2 / 3);

        var boxWidth = Math.Min(FRAME_WIDTH, FRAME_HEIGHT * width / height);
        return new Size(boxWidth, boxWidth * height / width);
    }

    #endregion // Public methods


    #region Private methods

    private static int Orientation(MetadataDocument doc) =>
        doc.FindFirst("Orientation", "EXIF", "MakerNotes", "XMP") is { } tag && int.TryParse(tag.RawOrValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value is >= 1 and <= 8 ? value : 1;


    private static SKBitmap Resize(SKBitmap source, SKSizeI size)
    {
        var resized = new SKBitmap(new SKImageInfo(size.Width, size.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        source.ScalePixels(resized, new SKSamplingOptions(SKCubicResampler.Mitchell));
        return resized;
    }


    // EXIF orientation as an affine map from stored pixels to upright ones
    private static SKBitmap Orient(SKBitmap source, int orientation)
    {
        float w = source.Width, h = source.Height;
        var turned = orientation >= 5;
        var matrix = orientation switch
        {
            2 => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            3 => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            4 => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            5 => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            6 => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            7 => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            _ => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
        };

        var upright = new SKBitmap(new SKImageInfo(turned ? source.Height : source.Width, turned ? source.Width : source.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(upright);
        using var image = SKImage.FromBitmap(source);
        canvas.SetMatrix(matrix);
        canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
        return upright;
    }

    #endregion // Private methods
}
