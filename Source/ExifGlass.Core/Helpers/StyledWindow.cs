/*
ExifGlass - Standalone Exif tool for ImageGlass
Copyright (C) 2023-2024 DUONG DIEU PHAP
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
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Linq;

namespace ExifGlass;

/// <summary>
/// A window with implemented system theme
/// </summary>
public class StyledWindow : Window
{
    public static bool IsWindows10 => Environment.OSVersion.Version.Major == 10
        && Environment.OSVersion.Version.Build< 22000;

    public StyledWindow()
    {
        FontFamily = new FontFamily("Segoe UI Variable, Segoe UI");
        this.SetDynamicResource(TransparencyBackgroundFallbackProperty, "SystemControlBackgroundAltHighBrush");

        // controls events
        ActualThemeVariantChanged += MainWindow_ActualThemeVariantChanged;
        Activated += MainWindow_Activated;
        Deactivated += MainWindow_Deactivated;
    }


    // Control events
    #region Control events

    private void MainWindow_ActualThemeVariantChanged(object? sender, EventArgs e)
    {
        if (IsActive)
        {
            MainWindow_Activated(sender, e);
        }
        else
        {
            MainWindow_Deactivated(sender, e);
        }
    }


    private void MainWindow_Activated(object? sender, EventArgs e)
    {
        if (!TransparencyLevelHint.Contains(WindowTransparencyLevel.None) && !IsWindows10)
        {
            Background = Brushes.Transparent;
        }
    }


    private void MainWindow_Deactivated(object? sender, EventArgs e)
    {
        if (Application.Current is not Application app) return;

        if (app.ActualThemeVariant == ThemeVariant.Dark)
        {
            Background = new SolidColorBrush(Color.FromRgb(32, 32, 32));
        }
        else
        {
            Background = new SolidColorBrush(Color.FromRgb(243, 243, 243));
        }
    }

    #endregion // Control events

}
