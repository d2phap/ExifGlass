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
using Avalonia.Threading;
using ExifGlass.Core.Settings;
using ExifGlass.UI.Windows;
using Xunit;

namespace ExifGlass.UI.Tests;

public class PlacementTests
{
    #region Fields and constants

    // S4's machine: one 3200 x 2000 screen at 200%, taskbar on the right
    private static readonly ScreenInfo Laptop = new(new PixelRect(0, 0, 3200, 2000), new PixelRect(0, 0, 3100, 2000), 2, true);

    // a 4K screen at 100% to its left
    private static readonly ScreenInfo Left4K = new(new PixelRect(-3840, 0, 3840, 2160), new PixelRect(-3840, 0, 3840, 2120), 1, false);

    private static readonly Size MinMain = new(420, 480);

    #endregion // Fields and constants


    #region Tests

    [Fact]
    public void FirstOpenIsCentredAndClampedToTheWorkArea()
    {
        var result = PlacementMath.Centre(Laptop, new Size(1240, 820), MinMain, default);

        Assert.Equal(new Size(1240, 820), result.Size);
        Assert.Equal(new PixelPoint((3100 - 2480) / 2, (2000 - 1640) / 2), result.Position);

        var small = new ScreenInfo(new PixelRect(0, 0, 1366, 768), new PixelRect(0, 0, 1366, 728), 1, true);
        Assert.Equal(new Size(1240, 728), PlacementMath.Centre(small, new Size(1240, 820), MinMain, default).Size);
    }


    [Fact]
    public void ASavedPlaceOnTodaysScreenIsKept()
    {
        var saved = Saved(200, 150, 1000, 700, Laptop);
        var result = PlacementMath.Restore(saved, MinMain, [Laptop]);

        Assert.Equal(new PixelPoint(200, 150), result.Position);
        Assert.Equal(new Size(1000, 700), result.Size);
        Assert.Contains("the same screen", result.Decision);
    }


    [Fact]
    public void ATooLargeWindowShrinksWithItsFrameCounted()
    {
        // S4 P12: saved 2400 x 1600 DIP on a 4K screen at 100%, restored at 200% with a 13 x 15 DIP frame
        var saved = Saved(100, 100, 2400, 1600, Laptop, frame: new Size(13, 15));
        var result = PlacementMath.Restore(saved, MinMain, [Laptop]);

        Assert.Equal(new Size(1550 - 13, 1000 - 15), result.Size);
        var outer = PixelSize.FromSize(result.Size + new Size(13, 15), 2);
        Assert.True(result.Position.X + outer.Width <= 3100);
        Assert.True(result.Position.Y + outer.Height <= 2000);
    }


    [Fact]
    public void AnOffScreenWindowMovesToThePrimaryScreen()
    {
        // S4 P9 and P13: no screen there, or the monitor it was on is gone
        foreach (var saved in new[] { Saved(5000, 3000, 1000, 700, Laptop), Saved(-3000, 200, 1000, 700, Left4K) })
        {
            var result = PlacementMath.Restore(saved, MinMain, [Laptop]);

            Assert.Equal(new Size(1000, 700), result.Size);
            Assert.Equal(new PixelPoint((3100 - 2000) / 2, (2000 - 1400) / 2), result.Position);
            Assert.StartsWith("off-screen", result.Decision);
        }
    }


    [Fact]
    public void AnOwnedWindowFallsBackToItsOwnersScreen()
    {
        // S4 P16
        var saved = Saved(9000, 9000, 560, 520, Laptop);
        var result = PlacementMath.Restore(saved, new Size(560, 400), [Left4K, Laptop], fallback: Left4K);

        Assert.True(Left4K.WorkingArea.Contains(result.Position));
    }


    [Fact]
    public void TheTitleStripMustStayReachable()
    {
        // S4 P10: partly off the left edge but enough of the strip is on screen
        Assert.NotNull(PlacementMath.FindReachable([Laptop], new PixelPoint(-900, 100), new Size(1000, 700)));

        // S4 P11: only 25 DIP of the strip left
        Assert.Null(PlacementMath.FindReachable([Laptop], new PixelPoint(-1950, 100), new Size(1000, 700)));
    }


    [Fact]
    public void MemoryStoreHandsOutCopies()
    {
        var store = new MemoryPlacementStore();
        var saved = Saved(1, 2, 300, 400, Laptop);
        store.Set("Main", saved);
        saved.X = 99;

        Assert.Equal(1, store.Get("Main")!.X);
        Assert.Null(store.Get("About"));
    }


    [AvaloniaFact]
    public void ARestoreFromMaximizedIsKeptWhenTheWindowClosesMinimized()
    {
        // G4 finding 9: the restore's size events come while the state still reads Maximized
        var store = new MemoryPlacementStore();
        var saved = Saved(100, 100, 800, 600, Laptop);
        saved.Maximized = true;
        store.Set("Test", saved);

        var window = new Window();
        WindowPlacement.Attach(window, "Test", new Size(800, 600), store);
        window.Show();
        Pump();

        window.WindowState = WindowState.Normal;
        Pump();
        window.WindowState = WindowState.Minimized;
        Pump();
        window.Close();

        Assert.False(store.Get("Test")!.Maximized);
    }

    #endregion // Tests


    #region Private methods

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(50));
        Dispatcher.UIThread.PushFrame(frame);
    }


    private static WindowPlacementState Saved(int x, int y, double width, double height, ScreenInfo screen, Size frame = default) => new()
    {
        X = x,
        Y = y,
        Width = width,
        Height = height,
        ScreenX = screen.Bounds.X,
        ScreenY = screen.Bounds.Y,
        ScreenWidth = screen.Bounds.Width,
        ScreenHeight = screen.Bounds.Height,
        Scaling = screen.Scaling,
        FrameWidth = frame.Width,
        FrameHeight = frame.Height,
    };

    #endregion // Private methods
}
