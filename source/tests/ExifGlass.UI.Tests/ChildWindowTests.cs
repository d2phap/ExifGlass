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
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using ExifGlass.Core.ExifTool;
using ExifGlass.Core.Settings;
using ExifGlass.Core.Update;
using ExifGlass.Services;
using ExifGlass.UI.Controls;
using ExifGlass.UI.Windows;
using ExifGlass.ViewModels;
using ExifGlass.Windows;
using Avalonia.Headless.XUnit;
using Xunit;

namespace ExifGlass.UI.Tests;

/// <summary>
/// P9: the Settings and About windows (DS 6.15, FewerPhotos's About), the update check (D10), the store row (D11) and the macOS menu.
/// </summary>
public class ChildWindowTests
{
    #region Fields and constants

    private static readonly DateTimeOffset NOW = new(2026, 10, 25, 9, 30, 0, TimeSpan.Zero);

    #endregion // Fields and constants


    #region Settings

    [AvaloniaFact]
    public void SettingsShowsTheSavedSettingsAndOkSavesThem()
    {
        using var shell = TestShell.Open(1400);
        shell.Settings.Update(s =>
        {
            s.CheckForUpdates = false;
            s.ExifToolArguments = "-fast -G -n";
        });

        var (window, vm) = OpenSettings(shell);
        var field = Find<TokenField>(window);
        Assert.False(vm.CheckForUpdates);
        Assert.Equal(["-fast", "-G", "-n"], field.Tokens);

        vm.AlwaysOnTop = true;
        field.Add("-u");
        vm.OkCommand.Execute(null);
        shell.Settle();

        Assert.False(window.IsVisible);
        Assert.True(shell.Settings.Current.AlwaysOnTop);
        Assert.True(shell.ViewModel.IsPinned);
        Assert.Equal("-fast -G -n -u", shell.Settings.Current.ExifToolArguments);
        Assert.Equal(AppText.TOAST_SETTINGS_SAVED, shell.Window.Toasts.Title);
        Assert.Equal(AppText.SettingsSavedSub(4, false), shell.Window.Toasts.Subtitle);
    }


    [AvaloniaFact]
    public void TheThemeAppliesAtOnceAndEscRestoresIt()
    {
        var app = Application.Current!;
        var before = app.RequestedThemeVariant;
        try
        {
            using var shell = TestShell.Open(1400);
            var (window, vm) = OpenSettings(shell);

            vm.ThemeIndex = (int)AppTheme.Dark;
            Assert.Equal(AppTheme.Dark, shell.ViewModel.Theme);
            Assert.Equal(ThemeVariant.Dark, app.RequestedThemeVariant);
            Assert.Equal(AppTheme.System, shell.Settings.Current.Theme);

            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            shell.Settle();

            Assert.False(window.IsVisible);
            Assert.Equal(AppTheme.System, shell.ViewModel.Theme);
            Assert.NotEqual(ThemeVariant.Dark, app.RequestedThemeVariant);
        }
        finally
        {
            app.RequestedThemeVariant = before;
        }
    }


    [AvaloniaFact]
    public void ResetPutsTheDefaultsBack()
    {
        using var shell = TestShell.Open(1400);
        var (window, vm) = OpenSettings(shell);
        var field = Find<TokenField>(window);

        vm.AlwaysOnTop = true;
        vm.CheckForUpdates = false;
        vm.ExifToolPath = @"C:\Tools\exiftool.exe";
        field.Add("-n -a");
        vm.ResetCommand.Execute(null);
        shell.Settle();

        Assert.False(vm.AlwaysOnTop);
        Assert.True(vm.CheckForUpdates);
        Assert.Empty(vm.ExifToolPath);
        Assert.Equal(ArgumentPolicy.Split(ArgumentPolicy.DEFAULT_ARGUMENTS), field.Tokens);
        window.Close();
    }


    [AvaloniaFact]
    public void ARefusedArgumentKeepsOkOffAndSaysWhy()
    {
        using var shell = TestShell.Open(1400);
        var (window, vm) = OpenSettings(shell);
        var field = Find<TokenField>(window);

        field.Add("-o out.txt");
        shell.Settle();

        Assert.False(vm.OkCommand.CanExecute(null));
        Assert.Equal(NoteKind.Danger, vm.ArgumentHintKind);
        Assert.Equal("-o: Writes or changes files", vm.ArgumentHint);
        Assert.All(window.GetVisualDescendants().OfType<TokenChip>().Where(c => c.Text is "-o" or "out.txt"), c => Assert.NotNull(c.Error));

        field.RemoveLast();
        field.RemoveLast();
        Assert.True(vm.OkCommand.CanExecute(null));
        window.Close();
    }


    [AvaloniaFact]
    public void AValueChipSaysWhichFlagItBelongsTo()
    {
        Assert.Equal("Value for -charset", SettingsViewModel.NoteFor(["-charset", "UTF8"], 1).Description);
        Assert.True(SettingsViewModel.NoteFor(["-G"], 0).IsLocked);
        Assert.Equal("raw values", SettingsViewModel.HintFor("-n"));
    }


    [AvaloniaTheory]
    [InlineData(AppChannel.WindowsZip, true, true, false)]
    [InlineData(AppChannel.WindowsStore, false, true, false)]
    [InlineData(AppChannel.LinuxFlatpak, false, true, false)]
    [InlineData(AppChannel.MacAppStore, false, false, true)]
    public void TheRowsFollowTheChannel(AppChannel channel, bool updates, bool exifToolPath, bool sandboxNote)
    {
        using var shell = TestShell.Open(1400, create: (s, r) => new MainViewModel(s, r) { Channel = new ChannelPolicy(channel) });
        var (window, vm) = OpenSettings(shell);
        var view = (Views.SettingsView)window.Content!;

        Assert.Equal(updates, vm.ShowsUpdates);
        Assert.Equal(updates, view.FindControl<ToggleSwitch>("UpdatesSwitch")!.IsEffectivelyVisible);
        Assert.Equal(exifToolPath, view.FindControl<TextBox>("PathBox")!.IsEffectivelyVisible);
        Assert.Equal(sandboxNote, vm.ShowsSandboxNote);
        window.Close();
    }


    [AvaloniaTheory]
    [InlineData(1400, false)]
    [InlineData(800, true)]
    public void TheFooterLinksToAboutWhileTheToolbarHidesIt(double width, bool link)
    {
        using var shell = TestShell.Open(width);
        var (window, vm) = OpenSettings(shell);

        Assert.Equal(link, vm.ShowsAboutLink);
        Assert.Equal(link, ((Views.SettingsView)window.Content!).FindControl<GlassButton>("AboutLink")!.IsEffectivelyVisible);
        window.Close();
    }


    [AvaloniaFact]
    public void SettingsOpensAsTallAsItsContentThenKeepsItsSize()
    {
        var placements = new MemoryPlacementStore();
        using var shell = TestShell.Open(1400, placements);
        var (window, _) = OpenSettings(shell);

        Assert.Equal(SizeToContent.Manual, window.SizeToContent);
        var height = window.ClientSize.Height;
        Assert.True(height > SettingsWindow.FirstSize.Height, $"{height}");
        Assert.True(window.Bounds.Height >= ((Control)window.Content!).DesiredSize.Height - 1);

        window.Height = 500;
        shell.Settle();
        window.Close();
        shell.Settle();
        Assert.Equal(500, placements.Get(AppSettings.WINDOW_SETTINGS)!.Height);

        var (again, _) = OpenSettings(shell);
        Assert.Equal(500, again.ClientSize.Height);
        again.Close();
    }


    [AvaloniaFact]
    public void AChildWindowStaysAboveAnOwnerKeptOnTop()
    {
        using var shell = TestShell.Open(1400);
        shell.ViewModel.TogglePinCommand.Execute(null);
        shell.Settle();

        var (settings, _) = OpenSettings(shell);
        Assert.True(settings.Topmost);
        settings.Close();
    }

    #endregion // Settings


    #region About

    [AvaloniaFact]
    public void AboutShowsTheVersionAndFiveCreditsUntilShowAll()
    {
        using var shell = TestShell.Open(1400);
        shell.Window.ShowAbout();
        shell.Settle();

        var window = Assert.Single(shell.Window.OwnedWindows.OfType<AboutWindow>());
        var vm = window.ViewModel;
        Assert.StartsWith("Version ", vm.VersionLine, StringComparison.Ordinal);
        Assert.Contains(AboutViewModel.OsName, vm.VersionLine, StringComparison.Ordinal);
        Assert.Equal(AboutWindow.FirstSize.Width, window.ClientSize.Width);

        var all = AppText.Credits(!OperatingSystem.IsWindows(), shell.ViewModel.Channel.ImageGlass10, shell.ViewModel.Channel.ImageGlass9).Count;
        Assert.Equal(Math.Min(all, AboutViewModel.CREDITS_SHOWN), vm.ShownCredits.Count);
        Assert.Equal(AppText.SHOW_ALL, vm.CreditsToggleText);

        vm.ToggleCreditsCommand.Execute(null);
        shell.Settle();
        Assert.Equal(all, vm.ShownCredits.Count);
        Assert.Equal(all, window.GetVisualDescendants().OfType<Badge>().Count(b => b.Kind == BadgeKind.Licence));
        Assert.Equal(AppText.SHOW_FEWER, vm.CreditsToggleText);
        window.Close();
    }


    [AvaloniaTheory]
    [InlineData(AppChannel.WindowsZip, true, true)]
    [InlineData(AppChannel.WindowsSideload, true, true)]
    [InlineData(AppChannel.MacDirect, true, false)]
    [InlineData(AppChannel.LinuxAppImage, true, false)]
    [InlineData(AppChannel.WindowsStore, false, false)]
    [InlineData(AppChannel.MacAppStore, false, false)]
    [InlineData(AppChannel.LinuxFlatpak, false, false)]
    public void TheStoreRowFollowsTheChannel(AppChannel channel, bool shows, bool microsoftStore)
    {
        var shell = new RecordingShell();
        var vm = new AboutViewModel(new MainViewModel(new SettingsStore(Path.GetTempPath()), new FakeReader()) { Channel = new ChannelPolicy(channel), Shell = shell });

        Assert.Equal(shows, vm.ShowsStore);
        Assert.Equal(shows && microsoftStore, vm.IsMicrosoftStore);
        if (!shows) return;

        vm.OpenStoreCommand.Execute(null);
        Assert.Equal(microsoftStore ? AppText.STORE_URL : UpdateService.RELEASES_URL, Assert.Single(shell.Opened));
    }


    [AvaloniaFact]
    public async Task TheUpdatesRowWalksThroughACheck()
    {
        var folder = Directory.CreateTempSubdirectory("eg-about-").FullName;
        try
        {
            var settings = new SettingsStore(folder);
            var feed = """{ "Version": "9.0.0", "PublishedDate": "2026-11-02T10:00:00", "DownloadUrl": "https://github.com/d2phap/ExifGlass/releases/tag/9.0.0" }""";
            Exception? failure = null;
            var updates = new UpdateService(settings, new ChannelPolicy(AppChannel.WindowsZip), "3.0.0", _ => failure is null ? Task.FromResult(feed) : Task.FromException<string>(failure), () => NOW);
            var shell = new RecordingShell();
            var vm = new AboutViewModel(new MainViewModel(settings, new FakeReader()) { Channel = updates.Channel, Updates = updates, Shell = shell });

            Assert.True(vm.ShowsUpdates);
            Assert.Equal(AppText.NOT_CHECKED, vm.UpdateText);

            await vm.CheckCommand.ExecuteAsync(null);
            Assert.True(vm.IsUpdateAvailable);
            Assert.False(vm.CanCheck);
            Assert.Equal(AppText.UpdateReady("9.0.0", "2 Nov 2026"), vm.UpdateText);
            await vm.DownloadCommand.ExecuteAsync(null);
            Assert.Equal("https://github.com/d2phap/ExifGlass/releases/tag/9.0.0", Assert.Single(shell.Opened));

            feed = """{ "Version": "2.0.0.0" }""";
            await updates.CheckNowAsync();
            Assert.Equal(AppText.UpToDate(AboutViewModel.AppVersion, NOW.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)), vm.UpdateText);

            failure = new HttpRequestException("offline");
            await vm.CheckCommand.ExecuteAsync(null);
            Assert.True(vm.IsUpdateError);
            Assert.True(vm.CanCheck);
            Assert.Equal(AppText.UPDATE_ERROR, vm.UpdateText);
            vm.Close();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }


    [AvaloniaFact]
    public async Task CopyVersionInfoGivesTheBuildForABugReport()
    {
        var shell = new RecordingShell();
        var vm = new AboutViewModel(new MainViewModel(new SettingsStore(Path.GetTempPath()), new FakeReader()) { Shell = shell });
        string? toast = null;
        vm.ToastRequested += (_, message) => toast = message;

        await vm.CopyInfoCommand.ExecuteAsync(null);

        var copied = Assert.Single(shell.Copied);
        Assert.StartsWith($"ExifGlass {AboutViewModel.AppVersion} ({AboutViewModel.BuildDate})", copied, StringComparison.Ordinal);
        Assert.Contains($".NET {Environment.Version}", copied, StringComparison.Ordinal);
        Assert.Equal(AppText.TOAST_VERSION_COPIED, toast);
    }

    #endregion // About


    #region Update check

    [AvaloniaFact]
    public async Task TheStartupCheckWaitsFiveDaysAndStaysQuiet()
    {
        var folder = Directory.CreateTempSubdirectory("eg-update-").FullName;
        try
        {
            var settings = new SettingsStore(folder);
            var calls = 0;
            var fail = false;
            var updates = new UpdateService(settings, new ChannelPolicy(AppChannel.WindowsZip), "3.0.0",
                _ => { calls++; return fail ? Task.FromException<string>(new HttpRequestException("offline")) : Task.FromResult("""{ "Version": "3.0.0" }"""); }, () => NOW);

            settings.Update(s => s.LastUpdateCheck = UpdateChecker.Stamp(NOW.AddDays(-1)));
            Assert.Equal(UpdateStatus.Skipped, (await updates.CheckOnStartupAsync()).Status);
            Assert.Equal(0, calls);

            settings.Update(s => s.LastUpdateCheck = UpdateChecker.Stamp(NOW.AddDays(-6)));
            fail = true;
            Assert.Equal(UpdateStatus.Failed, (await updates.CheckOnStartupAsync()).Status);
            Assert.Equal(UpdateState.Idle, updates.State);
            Assert.Equal(UpdateChecker.Stamp(NOW.AddDays(-6)), settings.Current.LastUpdateCheck);

            fail = false;
            Assert.Equal(UpdateStatus.UpToDate, (await updates.CheckOnStartupAsync()).Status);
            Assert.Equal(UpdateChecker.Stamp(NOW), settings.Current.LastUpdateCheck);
            Assert.Equal(NOW, updates.CheckedAt);

            settings.Update(s => s.CheckForUpdates = false);
            settings.Update(s => s.LastUpdateCheck = string.Empty);
            Assert.Equal(UpdateStatus.Skipped, (await updates.CheckOnStartupAsync()).Status);
            Assert.Equal(2, calls);
            await settings.FlushAsync();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }


    [AvaloniaFact]
    public async Task AStoreBuildNeverChecksAndOneCheckRunsAtATime()
    {
        var calls = 0;
        var store = new UpdateService(new SettingsStore(Path.GetTempPath()), new ChannelPolicy(AppChannel.WindowsStore), "3.0.0", _ => { calls++; return Task.FromResult("{}"); });
        Assert.False(store.IsAvailable);
        Assert.Equal(UpdateStatus.Skipped, (await store.CheckNowAsync()).Status);
        Assert.Equal(0, calls);

        var folder = Directory.CreateTempSubdirectory("eg-update-").FullName;
        try
        {
            var settings = new SettingsStore(folder);
            var held = new TaskCompletionSource<string>();
            var direct = new UpdateService(settings, new ChannelPolicy(AppChannel.WindowsZip), "3.0.0", _ => { calls++; return held.Task; });
            var first = direct.CheckNowAsync();
            var second = direct.CheckNowAsync();
            Assert.Same(first, second);
            Assert.Equal(UpdateState.Checking, direct.State);

            held.SetResult("""{ "Version": "3.0.1" }""");
            Assert.Equal(UpdateStatus.Available, (await first).Status);
            Assert.Equal(1, calls);
            await settings.FlushAsync();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    #endregion // Update check


    #region macOS menu

    [AvaloniaFact]
    public void TheMacMenuBarHasTheAppMenuAndTheWindowMenus()
    {
        using var shell = TestShell.Open(1400);

        var app = MacMenuBar.BuildAppMenu(shell.ViewModel).Items;
        Assert.Equal(AppText.ABOUT, ((NativeMenuItem)app[0]).Header);
        Assert.IsType<NativeMenuItemSeparator>(app[1]);
        var settings = (NativeMenuItem)app[2];
        Assert.Equal(AppText.MENU_SETTINGS, settings.Header);
        Assert.Equal(new KeyGesture(Key.OemComma, KeyModifiers.Meta), settings.Gesture);

        var menus = MacMenuBar.BuildWindowMenu(shell.Window).Items.Cast<NativeMenuItem>().ToList();
        Assert.Equal([AppText.MENU_FILE, AppText.MENU_EDIT, AppText.MENU_VIEW, AppText.MENU_WINDOW], menus.Select(m => m.Header));

        var edit = menus[1].Menu!.Items.OfType<NativeMenuItem>().ToList();
        Assert.Equal(new KeyGesture(Key.C, KeyModifiers.Meta), edit[0].Gesture);
        Assert.Equal(new KeyGesture(Key.K, KeyModifiers.Meta), edit[^1].Gesture);

        var commandBar = menus[2].Menu!.Items.OfType<NativeMenuItem>().Last();
        Assert.False(commandBar.IsChecked);
        shell.ViewModel.IsCommandExpanded = true;
        Assert.True(commandBar.IsChecked);
    }

    #endregion // macOS menu


    #region Snapshots

    [AvaloniaTheory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task TheChildWindowsMatchTheirSnapshots(string theme)
    {
        var variant = theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var failures = new List<string>();
        UpdateService? updates = null;
        using var shell = TestShell.Open(1400, theme: variant, create: (s, r) =>
        {
            updates = new UpdateService(s, new ChannelPolicy(AppChannel.WindowsZip), "3.0.0",
                _ => Task.FromResult("""{ "Version": "3.1.0", "PublishedDate": "2026-11-02T10:00:00" }"""), () => NOW);
            return new MainViewModel(s, r) { Channel = updates.Channel, Updates = updates };
        });

        var (settings, _) = OpenSettings(shell);
        Check(settings, variant, "window-settings", theme, failures);
        settings.Close();

        shell.Window.ShowAbout();
        shell.Settle(200);
        var about = shell.Window.OwnedWindows.OfType<AboutWindow>().Single(w => w.IsVisible);
        Check(about, variant, "window-about", theme, failures);

        await updates!.CheckNowAsync();
        about.ViewModel.ToggleCreditsCommand.Execute(null);
        shell.Settle(200);
        ((Views.AboutView)about.Content!).FindControl<ScrollViewer>("Scroller")!.ScrollToEnd();
        Check(about, variant, "window-about-update", theme, failures);
        about.Close();
        await shell.Settings.FlushAsync();

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    #endregion // Snapshots


    #region Private methods

    private static (SettingsWindow Window, SettingsViewModel ViewModel) OpenSettings(TestShell shell)
    {
        shell.Window.ShowSettings();
        shell.Settle(200);

        var window = shell.Window.OwnedWindows.OfType<SettingsWindow>().Single(w => w.IsVisible);
        return (window, window.ViewModel);
    }


    private static T Find<T>(Window window) where T : Control => window.GetVisualDescendants().OfType<T>().First();


    // an owned window follows the app's theme, not its owner's RequestedThemeVariant
    private static void Check(Window window, ThemeVariant variant, string id, string theme, List<string> failures)
    {
        window.RequestedThemeVariant = variant;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        for (var i = 0; i < 12; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("no frame rendered");
        Render.Save(frame, $"shell/{id}-{theme}");
        if (Render.CompareToBaseline(frame, id, theme) is { } failure) failures.Add(failure);
    }

    #endregion // Private methods
}
