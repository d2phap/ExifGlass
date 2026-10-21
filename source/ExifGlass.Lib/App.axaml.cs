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
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using ExifGlass.Core.Settings;
using ExifGlass.Diagnostics;
using ExifGlass.Services;
using ExifGlass.UI;

namespace ExifGlass;

public partial class App : Application
{
    #region Fields and constants

#if DEBUG
    // Debug only (plan 4.2): logs any UI-thread wait over 50 ms to the debug output
    private UiWatchdog? _watchdog;
#endif

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// Gets the running app's services; null in the gallery and in tests.
    /// </summary>
    public static AppServices? Services { get; private set; }

    #endregion // Properties


    #region Public methods

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        DesignSystem.Initialize(this);
    }


    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            base.OnFrameworkInitializationCompleted();
            return;
        }

        var args = desktop.Args ?? [];

#if DEBUG
        if (AppBootstrap.IsGallery(args))
        {
            if (AppBootstrap.ReadTheme(args) is { } galleryTheme) RequestedThemeVariant = galleryTheme;
            desktop.MainWindow = new UI.Gallery.GalleryWindow();
            base.OnFrameworkInitializationCompleted();
            return;
        }
#endif

        StartupClock.Mark("framework initialized");
        var services = Services = AppServices.Create(args);
        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        desktop.Exit += (_, _) => services.Shutdown();
        this.TryGetFeature<IActivatableLifetime>()?.Activated += App_Activated;
        base.OnFrameworkInitializationCompleted();

        // Linux reports the desktop theme a moment late: wait for it, or a dark desktop flashes light
        if (services.Settings.Current.Theme == AppTheme.System) await ThemeService.WaitForSystemThemeAsync();
        ThemeService.Apply(services.Settings.Current.Theme);
        ThemeService.Follow(() => services.ViewModel.Theme);

        var perf = PerfRun.IsPerf(args);
        var window = services.CreateMainWindow();
        var probe = perf ? new FrameProbe(window) : null;
        desktop.MainWindow = window;
        window.Show();
        StartupClock.Mark("main window shown");

        if (probe is not null)
        {
            // a redirected console on Windows uses the ANSI code page: the report scripts read UTF-8
            Console.OutputEncoding = new System.Text.UTF8Encoding(false);
            using var watchdog = new UiWatchdog();
            desktop.Shutdown(await PerfRun.RunAsync(services, window, probe, watchdog, Console.Out));
            return;
        }

#if DEBUG
        _watchdog = new UiWatchdog();
#endif

        if (services.Options.File is { } file) _ = services.ViewModel.OpenFileAsync(file);
    }

    #endregion // Public methods


    #region Event handlers

    // macOS Open With and file double-clicks arrive here, not on the command line
    private void App_Activated(object? sender, ActivatedEventArgs e)
    {
        if (e is not FileActivatedEventArgs { Files.Count: > 0 } files || Services?.ViewModel is not { } viewModel) return;

        var paths = new List<string>();
        var folders = 0;
        foreach (var item in files.Files)
        {
            if (item is IStorageFolder) folders++;
            else if (item.TryGetLocalPath() is { } path) paths.Add(path);
        }

        Dispatcher.UIThread.Post(() => viewModel.OpenDropped(paths, folders));
    }

    #endregion // Event handlers
}
