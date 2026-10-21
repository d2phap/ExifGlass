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
using ExifGlass.Core.Settings;
using System.Globalization;

namespace ExifGlass.UI.Windows;

/// <summary>
/// A screen as placement sees it: desktop pixels and the DIP scale.
/// </summary>
public readonly record struct ScreenInfo(PixelRect Bounds, PixelRect WorkingArea, double Scaling, bool IsPrimary);


/// <summary>
/// Where and how large a window opens: outer position in pixels, client size in DIPs.
/// </summary>
public readonly record struct PlacementResult(PixelPoint Position, Size Size, string Decision);


/// <summary>
/// The placement rules of plan 5.6 (S4) as pure functions: reachable title strip, off-screen fallback, shrink to the work area with the frame counted.
/// </summary>
public static class PlacementMath
{
    #region Fields and constants

    // how much of the title strip must land in a work area to count as reachable (DIP)
    public const double MIN_VISIBLE_WIDTH = 96;
    public const double TITLE_STRIP = 32;

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Restores a saved placement on today's screens; the fallback screen is the owner's for an owned window.
    /// </summary>
    public static PlacementResult Restore(WindowPlacementState saved, Size minSize, IReadOnlyList<ScreenInfo> screens, ScreenInfo? fallback = null)
    {
        var size = new Size(Math.Max(saved.Width, minSize.Width), Math.Max(saved.Height, minSize.Height));
        var frame = new Size(Math.Max(0, saved.FrameWidth), Math.Max(0, saved.FrameHeight));
        var pos = new PixelPoint(saved.X, saved.Y);

        if (screens.Count == 0) return new PlacementResult(pos, size, "no screens to check: kept");

        if (FindReachable(screens, pos, size) is { } on)
        {
            var same = on.Bounds == new PixelRect(saved.ScreenX, saved.ScreenY, saved.ScreenWidth, saved.ScreenHeight);
            var note = Invariant($"kept on {(same ? "the same" : "another")} screen (saved scaling {saved.Scaling:0.##}, now {on.Scaling:0.##})");

            // larger than the work area (a smaller or denser screen now): shrink, then pull back inside, frame included
            var area = on.WorkingArea;
            var fit = Fit(area, on.Scaling, frame);
            if (size.Width > fit.Width || size.Height > fit.Height)
            {
                size = new Size(Math.Max(minSize.Width, Math.Min(size.Width, fit.Width)), Math.Max(minSize.Height, Math.Min(size.Height, fit.Height)));
                var outer = PixelSize.FromSize(size + frame, on.Scaling);
                pos = new PixelPoint(Clamp(pos.X, area.X, area.Right - outer.Width), Clamp(pos.Y, area.Y, area.Bottom - outer.Height));
                note += Invariant($", shrunk to {size.Width:0}x{size.Height:0} at {pos.X},{pos.Y}");
            }

            return new PlacementResult(pos, size, note);
        }

        // off-screen (an unplugged monitor, a changed layout): the fallback screen, else the nearest, else the primary; centred
        var target = fallback ?? Nearest(screens, pos) ?? Primary(screens);
        var result = Centre(target, size, minSize, frame);
        return result with { Decision = Invariant($"off-screen at {pos.X},{pos.Y}: moved to {result.Position.X},{result.Position.Y} on {target.Bounds}, size {result.Size.Width:0}x{result.Size.Height:0}") };
    }


    /// <summary>
    /// Centres a window in a screen's work area, shrunk to fit with its frame (first open uses it with the D15 size).
    /// </summary>
    public static PlacementResult Centre(ScreenInfo screen, Size size, Size minSize, Size frame)
    {
        var area = screen.WorkingArea;
        var fit = Fit(area, screen.Scaling, frame);
        size = new Size(Math.Max(minSize.Width, Math.Min(size.Width, fit.Width)), Math.Max(minSize.Height, Math.Min(size.Height, fit.Height)));

        var outer = PixelSize.FromSize(size + frame, screen.Scaling);
        var centred = area.CenterRect(new PixelRect(outer)).Position;
        return new PlacementResult(new PixelPoint(Math.Max(area.X, centred.X), Math.Max(area.Y, centred.Y)), size, "centred");
    }


    /// <summary>
    /// Gets the first screen whose work area holds enough of the window's title strip to drag it back.
    /// </summary>
    public static ScreenInfo? FindReachable(IReadOnlyList<ScreenInfo> screens, PixelPoint pos, Size size)
    {
        foreach (var screen in screens)
        {
            var strip = new PixelRect(pos, PixelSize.FromSize(new Size(size.Width, TITLE_STRIP), screen.Scaling));
            var visible = screen.WorkingArea.Intersect(strip);
            var need = PixelSize.FromSize(new Size(Math.Min(MIN_VISIBLE_WIDTH, size.Width), TITLE_STRIP / 2), screen.Scaling);
            if (visible.Width >= need.Width && visible.Height >= need.Height) return screen;
        }

        return null;
    }

    #endregion // Public methods


    #region Private methods

    // the client size that fits a work area once the frame is added
    private static Size Fit(PixelRect area, double scaling, Size frame)
    {
        var dips = area.Size.ToSize(scaling);
        return new Size(Math.Max(0, dips.Width - frame.Width), Math.Max(0, dips.Height - frame.Height));
    }


    private static ScreenInfo? Nearest(IReadOnlyList<ScreenInfo> screens, PixelPoint pos)
    {
        foreach (var screen in screens)
        {
            if (screen.Bounds.Contains(pos)) return screen;
        }

        return null;
    }


    private static ScreenInfo Primary(IReadOnlyList<ScreenInfo> screens)
    {
        foreach (var screen in screens)
        {
            if (screen.IsPrimary) return screen;
        }

        return screens[0];
    }


    private static int Clamp(int value, int min, int max) => max < min ? min : Math.Clamp(value, min, max);


    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    #endregion // Private methods
}
