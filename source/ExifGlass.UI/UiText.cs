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
namespace ExifGlass.UI;

/// <summary>
/// The words the design-system controls show themselves (plan 5.9: all strings in one place, sentence case).
/// </summary>
public static class UiText
{
    #region Constants

    public const string SWITCH_ON = "On";
    public const string SWITCH_OFF = "Off";
    public const string CLEAR = "Clear";
    public const string COPY = "Copy";
    public const string CLEAR_SEARCH = "Clear search";
    public const string REMOVE = "Remove";
    public const string SHOW_MORE = "Show more";
    public const string SHOW_LESS = "Show less";
    public const string LOCKED_ARGUMENT = "Always on: ExifGlass needs it to read the file";
    public const string ADD_ARGUMENT = "Add an argument";
    public const string STORE_CAPTION = "Get it from";
    public const string STORE_TARGET = "Microsoft Store";
    public const string RELEASES_CAPTION = "Download from";
    public const string RELEASES_TARGET = "GitHub Releases";
    public const string DIALOG_CANCEL = "Cancel";

    #endregion // Constants


    #region Public methods

    /// <summary>
    /// Writes a shortcut as the OS does: "Ctrl+K" on Windows and Linux, Command-K on macOS (DS 6.2).
    /// </summary>
    public static string Shortcut(string key, bool shift = false)
    {
        if (OperatingSystem.IsMacOS()) return (shift ? "⇧" : "") + "⌘" + key;

        return (shift ? "Ctrl+Shift+" : "Ctrl+") + key;
    }

    #endregion // Public methods
}
