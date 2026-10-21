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
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExifGlass.Core.Common;
using ExifGlass.Core.Settings;
using ExifGlass.Services;
using ExifGlass.UI.Controls;
using ExifGlass.UI.Tokens;
using ExifGlass.UI.Windows;
using ExifGlass.ViewModels;
using ExifGlass.Views;
using ExifGlass.Windows;
using Xunit;

namespace ExifGlass.UI.Tests;

public class MainWindowTests
{
    #region Tests

    [AvaloniaFact]
    public void OpensAtTheSpecMinimumSizeAndRenders()
    {
        AppInfo.BuildInfo = new TestBuildInfo();
        var window = new MainWindow();
        window.Show();

        Assert.Equal("ExifGlass", window.Title);
        Assert.Equal(420, window.MinWidth);
        Assert.Equal(480, window.MinHeight);
        Assert.NotNull(window.CaptureRenderedFrame());

        window.Close();
    }


    [AvaloniaTheory]
    [InlineData(new[] { "--theme", "dark" }, "Dark")]
    [InlineData(new[] { "--THEME", "Light" }, "Light")]
    [InlineData(new[] { "--theme" }, null)]
    [InlineData(new[] { "photo.jpg" }, null)]
    public void ReadsTheThemeOverride(string[] args, string? expected)
    {
        Assert.Equal(expected, AppBootstrap.ReadTheme(args)?.Key?.ToString());
    }


    [AvaloniaTheory]
    [InlineData(1400, true, true, true, true)]
    [InlineData(1100, true, false, true, true)]
    [InlineData(760, false, false, false, true)]
    [InlineData(480, false, false, false, false)]
    public void EachBandDocksAndShowsWhatDs21Says(double width, bool railDocked, bool inspectorDocked, bool secondaryButtons, bool openLabel)
    {
        using var shell = TestShell.Open(width);
        var w = shell.Window;

        Assert.Equal(railDocked, w.RailSlot.Openness > 0);
        Assert.Equal(inspectorDocked, w.InspectorSlot.Openness > 0);
        Assert.Equal(secondaryButtons, w.Toolbar.PinToggle.IsVisible);
        Assert.Equal(secondaryButtons, w.Toolbar.ThemeButton.IsVisible);
        Assert.Equal(secondaryButtons, w.Toolbar.AboutButton.IsVisible);
        Assert.True(w.Toolbar.SettingsButton.IsVisible);
        Assert.Equal(openLabel, w.Toolbar.OpenButton.Content is not null);
        Assert.Equal(!openLabel, w.Toolbar.Search.Classes.Contains("no-hint"));
    }


    [AvaloniaFact]
    public void TheLayoutGridMatchesDs2()
    {
        using var shell = TestShell.Open(1400);
        var w = shell.Window;

        Assert.Equal(48, w.Toolbar.Bounds.Height);
        Assert.Equal(244, w.RailSlot.Bounds.Width);
        Assert.Equal(312, w.InspectorSlot.Bounds.Width);
        Assert.Equal(236, w.RailDockFrame.Bounds.Width);
        Assert.Equal(304, w.InspectorDockFrame.Bounds.Width);

        // padding 8 at the sides and bottom, gap 8 between the rail, table and inspector
        var table = w.Table.TranslatePoint(default, w)!.Value;
        Assert.Equal(8 + 244, table.X);
        Assert.Equal(1400 - 8 - 312, table.X + w.Table.Bounds.Width);
        Assert.Equal(40, w.CommandBarView.Bounds.Height);
        Assert.Equal(TestShell.HEIGHT - 8, w.CommandBarView.TranslatePoint(default, w)!.Value.Y + 40);
    }


    [AvaloniaFact]
    public void TheDockedToggleClosesTheColumnAndRemembersIt()
    {
        using var shell = TestShell.Open(1400);

        shell.Window.Toolbar.RailToggle.Command!.Execute(null);
        shell.Settle();

        Assert.Equal(0, shell.Window.RailSlot.Bounds.Width);
        Assert.False(shell.ViewModel.IsRailShown);
        Assert.False(shell.Settings.Current.RailOpen);
        Assert.False(shell.Window.RailContent.IsEffectivelyVisible);
    }


    [AvaloniaFact]
    public void APushedPanelMovesTheTableAsideWithoutResizingIt()
    {
        using var shell = TestShell.Open(760);
        var vm = shell.ViewModel;
        var w = shell.Window;
        var tableWidth = w.Table.Bounds.Width;

        vm.ToggleRailCommand.Execute(null);
        shell.Settle();
        Assert.True(w.Pushes.IsLeftOpen);
        Assert.Same(w.RailPushFrame, w.RailContent.Parent);
        Assert.Equal(244, w.Pushes.Offset);
        Assert.Equal(tableWidth, w.Table.Bounds.Width);
        Assert.Equal(8 + 244, w.Table.TranslatePoint(default, w)!.Value.X);
        Assert.Equal(8, w.RailPushFrame.TranslatePoint(default, w)!.Value.X);

        // the table's far side is clipped at the host, which never scrolls
        Assert.True(w.Pushes.ClipToBounds);

        vm.ToggleInspectorCommand.Execute(null);
        shell.Settle();
        Assert.False(w.Pushes.IsLeftOpen);
        Assert.True(w.Pushes.IsRightOpen);
        Assert.Equal(-312, w.Pushes.Offset);
        Assert.Equal(8 - 312, w.Table.TranslatePoint(default, w)!.Value.X);
        Assert.Equal(760 - 8 - 304, w.InspectorPushFrame.TranslatePoint(default, w)!.Value.X);
        Assert.True(vm.IsInspectorShown);

        shell.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        shell.Settle();
        Assert.False(w.Pushes.IsRightOpen);
        Assert.Equal(0, w.Pushes.Offset);
        Assert.Equal(8, w.Table.TranslatePoint(default, w)!.Value.X);
        Assert.False(vm.IsInspectorShown);
    }


    [AvaloniaFact]
    public void DoubleClickingTheToolbarMaximizesAndRestores()
    {
        using var shell = TestShell.Open(1400);
        var empty = new Point(120, 24);

        shell.Window.MouseDown(empty, MouseButton.Left);
        shell.Window.MouseUp(empty, MouseButton.Left);
        shell.Window.MouseDown(empty, MouseButton.Left);
        shell.Window.MouseUp(empty, MouseButton.Left);
        shell.Settle();

        Assert.Equal(WindowState.Maximized, shell.Window.WindowState);
    }


    [AvaloniaFact]
    public void WideningDocksAPushedRail()
    {
        using var shell = TestShell.Open(760);
        shell.ViewModel.ToggleRailCommand.Execute(null);
        shell.Settle();

        shell.Window.Width = 1000;
        shell.Settle();

        Assert.False(shell.ViewModel.IsRailPushed);
        Assert.Equal(0, shell.Window.Pushes.Offset);
        Assert.Same(shell.Window.RailDockFrame, shell.Window.RailContent.Parent);
        Assert.Equal(244, shell.Window.RailSlot.Bounds.Width);
    }


    [AvaloniaFact]
    public async Task OpeningAFileFillsTitleSearchAndSummary()
    {
        using var shell = TestShell.Open(1400);
        var vm = shell.ViewModel;
        Assert.Equal(AppText.SEARCH_NO_FILE, vm.SearchPlaceholder);
        Assert.False(shell.Window.Toolbar.Search.IsEffectivelyEnabled);
        Assert.True(shell.Window.Table.EmptyCard.IsVisible);

        shell.Reader.Hold("C:/photos/DSCF4127.JPG");
        var open = vm.OpenFileAsync("C:/photos/DSCF4127.JPG");
        shell.Settle();
        Assert.True(shell.Window.Table.LoadLine.IsVisible);
        Assert.Equal(AppText.SUMMARY_READING, vm.Summary);
        Assert.False(shell.Window.Table.EmptyCard.IsVisible);

        shell.Reader.Release("C:/photos/DSCF4127.JPG", tags: 128);
        await open;
        await shell.WaitForAsync(() => vm.HasDocument);

        Assert.Equal("DSCF4127.JPG – ExifGlass", shell.Window.Title);
        Assert.Equal("Search 128 tags", vm.SearchPlaceholder);
        Assert.Equal("128 tags", vm.Summary);
        Assert.Equal("in 7 groups", vm.SummaryDetail);
        Assert.False(shell.Window.Table.LoadLine.IsVisible);
        Assert.True(shell.Window.Toolbar.Search.IsEffectivelyEnabled);
    }


    [AvaloniaFact]
    public async Task CtrlKFocusesTheSearchAndEscClearsIt()
    {
        using var shell = TestShell.Open(1400);
        await shell.ViewModel.OpenFileAsync("a.jpg");
        await shell.WaitForAsync(() => shell.ViewModel.HasDocument);

        shell.Window.KeyPress(Key.K, Modifier, PhysicalKey.K, null);
        shell.Settle();
        Assert.True(shell.Window.Toolbar.Search.IsFocused);

        shell.Window.KeyTextInput("tag 1");
        await shell.WaitForAsync(() => shell.ViewModel.Rows.IsFiltering);
        Assert.Equal("tag 1", shell.ViewModel.SearchText);
        Assert.StartsWith("11 of 24", shell.ViewModel.Summary, StringComparison.Ordinal);

        // the search box's own Esc clears it; the next leaves it; the window's chain never closes the window
        shell.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        shell.Settle();
        Assert.Equal(string.Empty, shell.ViewModel.SearchText);

        shell.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        shell.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        shell.Settle();
        Assert.True(shell.Window.IsVisible);
    }


    [AvaloniaFact]
    public void TheWindowEscClearsASearchTypedElsewhere()
    {
        using var shell = TestShell.Open(1400);
        shell.ViewModel.SearchText = "iso";

        Assert.True(shell.ViewModel.Escape());
        Assert.Equal(string.Empty, shell.ViewModel.SearchText);
        Assert.False(shell.ViewModel.Escape());
    }


    [AvaloniaFact]
    public async Task ADraggedFileLightsTheEmptyCardOrTheScrim()
    {
        using var shell = TestShell.Open(1400);
        var path = Path.Combine(shell.Folder, "drop.jpg");
        await File.WriteAllBytesAsync(path, [0xFF, 0xD8, 0xFF, 0xD9], TestContext.Current.CancellationToken);
        var file = await shell.Window.StorageProvider.TryGetFileFromPathAsync(path);
        Assert.NotNull(file);

        var data = new DataTransfer();
        var item = new DataTransferItem();
        item.SetFile(file);
        data.Add(item);
        var centre = new Point(700, 430);

        shell.Window.DragDrop(centre, RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.None);
        shell.Settle();
        Assert.True(shell.Window.Table.EmptyCard.IsDragOver);
        Assert.False(shell.Window.Scrim.IsVisible);

        shell.Window.DragDrop(centre, RawDragEventType.Drop, data, DragDropEffects.Copy, RawInputModifiers.None);
        await shell.WaitForAsync(() => shell.ViewModel.HasDocument);
        Assert.False(shell.Window.Table.EmptyCard.IsDragOver);
        Assert.Equal("drop.jpg", shell.ViewModel.Document?.FileName);

        // with a file open the whole window takes a scrim
        shell.Window.DragDrop(centre, RawDragEventType.DragEnter, data, DragDropEffects.Copy, RawInputModifiers.None);
        shell.Settle();
        Assert.True(shell.Window.Scrim.IsVisible);

        shell.Window.DragDrop(centre, RawDragEventType.DragLeave, data, DragDropEffects.Copy, RawInputModifiers.None);
        shell.Settle(400);
        Assert.False(shell.Window.Scrim.IsVisible);
    }


    [AvaloniaFact]
    public void ManyDroppedFilesOpenTheFirstAndSaySo()
    {
        using var shell = TestShell.Open(1400);

        shell.ViewModel.OpenDropped(["a.jpg", "b.jpg", "c.jpg"], folderCount: 0);
        shell.Settle();

        Assert.Equal("Opened 1 of 3 files", shell.Window.Toasts.Title);
        Assert.Equal("a.jpg", shell.Window.Toasts.Subtitle);
    }


    [AvaloniaFact]
    public void AFailedReadShowsADangerToast()
    {
        using var shell = TestShell.Open(1400);
        shell.Reader.Failure = new Core.ExifTool.ExifToolException("File not found");

        _ = shell.ViewModel.OpenFileAsync("missing.jpg");
        shell.Settle();

        Assert.Equal(AppText.TOAST_READ_FAILED, shell.Window.Toasts.Title);
        Assert.Equal(ToastKind.Danger, shell.Window.Toasts.Kind);
        Assert.Equal(LoadPhase.Empty, shell.ViewModel.Phase);
    }


    [AvaloniaFact]
    public void PinKeepsTheWindowOnTopAndSaysSo()
    {
        using var shell = TestShell.Open(1400);

        shell.ViewModel.TogglePinCommand.Execute(null);
        shell.Settle();

        Assert.True(shell.Window.Topmost);
        Assert.True(shell.Settings.Current.AlwaysOnTop);
        Assert.Equal(AppText.TOAST_PINNED, shell.Window.Toasts.Title);
        Assert.Equal(AppText.UNPIN, shell.ViewModel.PinTooltip);
    }


    [AvaloniaFact]
    public void TheThemeButtonCyclesAndAppliesAtOnce()
    {
        var app = Application.Current!;
        var before = app.RequestedThemeVariant;
        using var shell = TestShell.Open(1400);
        try
        {
            shell.ViewModel.CycleThemeCommand.Execute(null);
            Assert.Equal(ThemeVariant.Light, app.RequestedThemeVariant);
            Assert.Equal(IconId.WeatherSunny, shell.ViewModel.ThemeIcon);
            Assert.Equal("Theme: Light — click for Dark", shell.ViewModel.ThemeTooltip);

            shell.ViewModel.CycleThemeCommand.Execute(null);
            Assert.Equal(ThemeVariant.Dark, app.RequestedThemeVariant);
            Assert.Equal(AppTheme.Dark, shell.Settings.Current.Theme);
        }
        finally
        {
            app.RequestedThemeVariant = before;
        }
    }


    [AvaloniaFact]
    public void ThePlacementIsSavedUnderMainWhenTheWindowCloses()
    {
        var store = new MemoryPlacementStore();
        using (var shell = TestShell.Open(1000, store))
        {
            shell.Window.Width = 1100;
            shell.Settle();
        }

        Assert.Equal(1100, store.Get(AppSettings.WINDOW_MAIN)?.Width);
    }


    [AvaloniaFact]
    public void TheSummaryDropsTheGroupCountBelow520()
    {
        using var shell = TestShell.Open(560);

        // 560 window: rail and inspector are pushed panels, so the table takes 544
        Assert.True(shell.ViewModel.GroupCountFits);

        shell.Window.Width = 500;
        shell.Settle();
        Assert.False(shell.ViewModel.GroupCountFits);
    }

    #endregion // Tests


    #region Private methods

    private static RawInputModifiers Modifier => OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

    #endregion // Private methods


    #region Nested types

    private sealed class TestBuildInfo : IAppBuildInfo
    {
        public string Version => "3.0.0.0";
        public string FullVersion => "3.0.0.0-test";
        public string ReleaseType => "test";
        public string Channel => "Direct";
        public string PlatformId => "test";
        public string Architecture => "x64";
        public string BuildDate => "2026.10.20";
        public string Copyright => "© 2023 - 2026 Duong Dieu Phap";
    }

    #endregion // Nested types
}
