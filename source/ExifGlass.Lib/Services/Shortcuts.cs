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
using Avalonia.Input;

namespace ExifGlass.Services;

/// <summary>
/// The app's keyboard shortcuts (DS 7.6): Command replaces Ctrl on macOS.
/// </summary>
public static class Shortcuts
{
    #region Properties

    /// <summary>
    /// Gets the shortcut modifier of this OS: Command on macOS, Ctrl elsewhere.
    /// </summary>
    public static KeyModifiers Primary => OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Whether a key press is the primary modifier plus <paramref name="key"/>, and nothing else.
    /// </summary>
    public static bool Is(KeyEventArgs e, Key key) => e.Key == key && e.KeyModifiers == Primary;

    #endregion // Public methods
}
