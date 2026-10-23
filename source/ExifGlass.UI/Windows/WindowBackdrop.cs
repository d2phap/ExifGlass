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
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using ExifGlass.Core.Platform;
using Avalonia.Threading;
using ExifGlass.UI.Tokens;

namespace ExifGlass.UI.Windows;

/// <summary>
/// Gives a window its fill (O4): native Mica on Windows 11 22H2 and later with transparency effects on, the solid window colour (Bg.Window) everywhere else.
/// </summary>
public static class WindowBackdrop
{
    #region Fields and constants

    public const string MICA_CLASS = "mica";

    // the OS-wide settings outlive a window; each open window's handler comes off when it closes
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Window, EventHandler<PlatformColorValues>> SETTINGS_HANDLERS = [];

    #endregion // Fields and constants


    #region Public methods

    public static void Use(Window window)
    {
        // a style, not a local value: a theme change re-reads the brush without undoing Mica
        window.Styles.Add(new Style(x => x.Is<Window>())
        {
            Setters = { new Setter(TemplatedControl.BackgroundProperty, window.GetResourceObservable(TokenKeys.BG_WINDOW_BRUSH).ToBinding()) },
        });
        window.Styles.Add(new Style(x => x.Is<Window>().Class(MICA_CLASS))
        {
            Setters = { new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent) },
        });

        window.Opened += Window_Opened;
        window.Closed += Window_Closed;
    }

    #endregion // Public methods


    #region Event handlers

    private static void Window_Opened(object? sender, EventArgs e)
    {
        if (sender is not Window window) return;

        Update(window);
        if (Application.Current?.PlatformSettings is not { } settings || SETTINGS_HANDLERS.TryGetValue(window, out _)) return;

        EventHandler<PlatformColorValues> handler = (_, _) => Dispatcher.UIThread.Post(() => Update(window));
        SETTINGS_HANDLERS.Add(window, handler);
        settings.ColorValuesChanged += handler;
    }


    private static void Window_Closed(object? sender, EventArgs e)
    {
        if (sender is not Window window) return;

        window.Classes.Remove(MICA_CLASS);
        if (!SETTINGS_HANDLERS.TryGetValue(window, out var handler)) return;

        SETTINGS_HANDLERS.Remove(window);
        if (Application.Current?.PlatformSettings is { } settings) settings.ColorValuesChanged -= handler;
    }


    // Windows sends the colour-settings change when transparency effects are switched, so the window follows without a restart
    private static void Update(Window window)
    {
        if (!window.IsVisible || window.TryGetPlatformHandle()?.Handle is not { } handle || PlatformServices.Shell is not { } shell) return;

        var mica = shell.TrySetMicaBackdrop(handle);
        if (!mica && window.Classes.Contains(MICA_CLASS)) shell.ClearMicaBackdrop(handle);
        window.Classes.Set(MICA_CLASS, mica);
    }

    #endregion // Event handlers
}


/// <summary>
/// What every ExifGlass window gets: the app icon, grayscale text (S3), the backdrop and the theme's text colour.
/// </summary>
public static class AppWindow
{
    #region Public methods

    public static void Prepare(Window window)
    {
        window.Icon = CreateIcon();
        window[!TemplatedControl.ForegroundProperty] = window.GetResourceObservable(TokenKeys.TEXT_PRIMARY_BRUSH).ToBinding();
        window[!TemplatedControl.FontFamilyProperty] = window.GetResourceObservable(TokenKeys.FONT_UI).ToBinding();
        TextOptions.SetTextRenderingMode(window, TextRenderingMode.Antialias);
        WindowBackdrop.Use(window);
    }


    /// <summary>
    /// The caption and taskbar icon: the exe's own .ico on Windows, the 256 px logo on Linux; macOS shows the bundle's.
    /// </summary>
    public static WindowIcon CreateIcon()
    {
        var path = OperatingSystem.IsLinux() ? "Assets/Logo/logo-256.png" : "Assets/ExifGlass.ico";
        using var stream = AssetLoader.Open(new Uri($"avares://ExifGlass.UI/{path}"));
        return new WindowIcon(stream);
    }

    #endregion // Public methods
}
