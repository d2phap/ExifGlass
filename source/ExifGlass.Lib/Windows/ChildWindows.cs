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
using ExifGlass.Core.Settings;
using ExifGlass.UI.Windows;

namespace ExifGlass.Windows;

/// <summary>
/// The Settings window (DS 6.15): 560 wide at least, owned by the main window, placement saved; P9 builds its rows.
/// </summary>
public sealed class SettingsWindow : OwnedWindow
{
    #region Fields and constants

    public static readonly Size FirstSize = new(560, 640);

    #endregion // Fields and constants


    #region Constructors

    public SettingsWindow(IWindowPlacementStore placements, Window owner)
    {
        Title = AppText.SETTINGS;
        UsePlacement(AppSettings.WINDOW_SETTINGS, FirstSize, placements, owner);
    }

    #endregion // Constructors
}


/// <summary>
/// The About window (DS 6.15): 420 wide at least, owned by the main window, placement saved; P9 builds its content.
/// </summary>
public sealed class AboutWindow : OwnedWindow
{
    #region Fields and constants

    public static readonly Size FirstSize = new(420, 600);

    #endregion // Fields and constants


    #region Constructors

    public AboutWindow(IWindowPlacementStore placements, Window owner)
    {
        Title = AppText.ABOUT;
        UsePlacement(AppSettings.WINDOW_ABOUT, FirstSize, placements, owner);
    }

    #endregion // Constructors
}
