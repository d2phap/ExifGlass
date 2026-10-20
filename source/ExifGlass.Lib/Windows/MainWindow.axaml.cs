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
using Avalonia.Controls;
using ExifGlass.Core.Common;

namespace ExifGlass.Windows;

public partial class MainWindow : Window
{
    #region Constructors

    public MainWindow()
    {
        InitializeComponent();
        WindowBackdrop.Use(this);

        VersionText.Text = AppInfo.BuildInfo is { } info ? $"Version {info.Version}" : string.Empty;
    }

    #endregion // Constructors
}
