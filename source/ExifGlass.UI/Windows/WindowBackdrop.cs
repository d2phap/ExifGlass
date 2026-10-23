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
using ExifGlass.UI.Tokens;

namespace ExifGlass.UI.Windows;

/// <summary>
/// Gives a window its fill (O4): native Mica on Windows 11 22H2 and later, the solid window colour everywhere else.
/// </summary>
public static class WindowBackdrop
{
    #region Fields and constants

    public const string MICA_CLASS = "mica";

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
    }

    #endregion // Public methods


    #region Event handlers

    private static void Window_Opened(object? sender, EventArgs e)
    {
        if (sender is not Window window || window.Classes.Contains(MICA_CLASS)) return;

        // with transparency effects off too: Windows then fills window and title bar with its own gray (plan Appendix A item 46)
        if (window.TryGetPlatformHandle()?.Handle is { } handle && PlatformServices.Shell?.TrySetMicaBackdrop(handle) == true)
        {
            window.Classes.Add(MICA_CLASS);
        }
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
