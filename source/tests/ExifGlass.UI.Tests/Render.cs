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
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using ExifGlass.UI.Windows;

namespace ExifGlass.UI.Tests;

/// <summary>
/// Renders controls headlessly with real Skia and saves the frames for review.
/// </summary>
public static class Render
{
    #region Properties

    /// <summary>
    /// Gets the source folder, the one holding ExifGlass.slnx.
    /// </summary>
    public static string SourceDir
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ExifGlass.slnx"))) dir = dir.Parent;

            return dir?.FullName ?? throw new DirectoryNotFoundException("ExifGlass.slnx not found above the test output");
        }
    }


    /// <summary>
    /// Gets source/__artifacts/ui, where rendered frames are written for review.
    /// </summary>
    public static string OutputDir => Directory.CreateDirectory(Path.Combine(SourceDir, "__artifacts", "ui")).FullName;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Shows content in a headless window of a size and theme, lets transitions finish on the render clock, then captures a frame.
    /// </summary>
    public static WriteableBitmap CaptureSettled(Control content, double width, double height, ThemeVariant theme, Action<Window>? inspect = null, int settleMs = 700)
    {
        var window = new Window { Width = width, Height = height, RequestedThemeVariant = theme, Content = content };
        AppWindow.Prepare(window);
        window.Show();

        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < settleMs)
        {
            Thread.Sleep(30);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
        }

        var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("no frame rendered");
        inspect?.Invoke(window);
        window.Close();
        return frame;
    }


    /// <summary>
    /// Copies the part of a frame under an area in DIPs at 1x.
    /// </summary>
    public static unsafe WriteableBitmap Crop(WriteableBitmap frame, Rect area)
    {
        var x = Math.Max(0, (int)Math.Floor(area.X));
        var y = Math.Max(0, (int)Math.Floor(area.Y));
        var w = Math.Min(frame.PixelSize.Width - x, (int)Math.Ceiling(area.Width));
        var h = Math.Min(frame.PixelSize.Height - y, (int)Math.Ceiling(area.Height));

        var crop = new WriteableBitmap(new PixelSize(Math.Max(1, w), Math.Max(1, h)), frame.Dpi, frame.Format, frame.AlphaFormat);
        using var source = frame.Lock();
        using var target = crop.Lock();

        for (var row = 0; row < h; row++)
        {
            Buffer.MemoryCopy((byte*)source.Address + (y + row) * source.RowBytes + x * 4, (byte*)target.Address + row * target.RowBytes, target.RowBytes, w * 4);
        }

        return crop;
    }


    /// <summary>
    /// Saves a frame as OutputDir/name.png and returns the path.
    /// </summary>
    public static string Save(WriteableBitmap frame, string name)
    {
        var path = Path.Combine(OutputDir, name + ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        frame.Save(path, new PngBitmapEncoderOptions());
        return path;
    }

    #endregion // Public methods
}
