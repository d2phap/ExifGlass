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
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using ExifGlass.Core.Settings;

namespace ExifGlass.Services;

/// <summary>
/// The app theme (DS 1.3): System, Light or Dark, switched instantly; System follows the OS live.
/// </summary>
public static class ThemeService
{
    #region Fields and constants

    /// <summary>
    /// How long the first window waits for Linux's desktop theme; its first read is a provisional Light (FewerPhotos).
    /// </summary>
    public static readonly TimeSpan LinuxGrace = TimeSpan.FromMilliseconds(100);

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// The toolbar cycle: System, Light, Dark, then System again.
    /// </summary>
    public static AppTheme Next(AppTheme theme) => theme switch
    {
        AppTheme.System => AppTheme.Light,
        AppTheme.Light => AppTheme.Dark,
        _ => AppTheme.System,
    };


    /// <summary>
    /// The variant to draw; System reads the OS, Light when it says nothing.
    /// </summary>
    public static ThemeVariant Resolve(AppTheme theme, PlatformThemeVariant? os) => theme switch
    {
        AppTheme.Light => ThemeVariant.Light,
        AppTheme.Dark => ThemeVariant.Dark,
        _ => os == PlatformThemeVariant.Dark ? ThemeVariant.Dark : ThemeVariant.Light,
    };


    /// <summary>
    /// Switches the app's theme at once; no transition (DS 1.3).
    /// </summary>
    public static void Apply(AppTheme theme)
    {
        if (Application.Current is not { } app) return;

        var variant = Resolve(theme, app.PlatformSettings?.GetColorValues().ThemeVariant);
        if (app.RequestedThemeVariant != variant) app.RequestedThemeVariant = variant;
    }


    /// <summary>
    /// Re-applies System whenever the OS theme changes.
    /// </summary>
    public static void Follow(Func<AppTheme> current)
    {
        if (Application.Current?.PlatformSettings is not { } platform) return;

        platform.ColorValuesChanged += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (current() == AppTheme.System) Apply(AppTheme.System);
        });
    }


    /// <summary>
    /// Waits, on Linux only, until the desktop has reported its theme or the grace period ends.
    /// </summary>
    public static async Task WaitForSystemThemeAsync()
    {
        if (!OperatingSystem.IsLinux() || Application.Current?.PlatformSettings is not { } platform) return;

        var reported = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, PlatformColorValues e) => reported.TrySetResult();

        platform.ColorValuesChanged += OnChanged;
        try
        {
            await Task.WhenAny(reported.Task, Task.Delay(LinuxGrace));
        }
        finally
        {
            platform.ColorValuesChanged -= OnChanged;
        }
    }

    #endregion // Public methods
}
