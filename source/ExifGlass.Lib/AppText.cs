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
using ExifGlass.Core.Common;
using ExifGlass.Core.Settings;
using ExifGlass.UI;
using System.Globalization;

namespace ExifGlass;

/// <summary>
/// The words the app's screens show (plan 5.9: all strings in one place, sentence case); the design system's own are in UiText.
/// </summary>
public static class AppText
{
    #region Constants

    public const string OPEN = "Open";
    public const string OPEN_FILE = "Open file…";
    public const string OPEN_SETTINGS = "Open settings";
    public const string RETRY = "Retry";
    public const string ABOUT = "About ExifGlass";
    public const string SETTINGS = "Settings";
    public const string SEARCH = "Search tags";

    public const string SEARCH_NO_FILE = "Open a file to search";
    public const string SUMMARY_NO_FILE = "No file";
    public const string SUMMARY_READING = "Reading metadata…";

    public const string SHOW_RAIL = "Show groups panel";
    public const string HIDE_RAIL = "Hide groups panel";
    public const string SHOW_INSPECTOR = "Show inspector";
    public const string HIDE_INSPECTOR = "Hide inspector";
    public const string PIN = "Keep window on top";
    public const string UNPIN = "Unpin window";
    public const string COPY_COMMAND = "Copy command";

    public const string EMPTY_TITLE = "Open an AI image or photo to inspect its metadata";
    public const string EMPTY_CAPTION = "JPEG, PNG, HEIC, TIFF, RAW and more — read locally with ExifTool.";
    public const string RELEASE_TO_INSPECT = "Release to inspect";
    public const string DROP_HINT = "or drop a file anywhere";
    public const string EXIFTOOL_MISSING_TITLE = "ExifTool isn't available";
    public const string EXIFTOOL_MISSING_CAPTION = "ExifGlass reads metadata with ExifTool. Choose its path in Settings, or retry.";

    public const string TOAST_PINNED = "Pinned on top";
    public const string TOAST_PINNED_SUB = "ExifGlass stays above other windows";
    public const string TOAST_UNPINNED = "Unpinned";
    public const string TOAST_UNPINNED_SUB = "Window behaves normally";
    public const string TOAST_READ_FAILED = "Couldn't read that file";
    public const string TOAST_FOLDER = "Folders can't be opened";
    public const string TOAST_FOLDER_SUB = "Drop a file instead";
    public const string TOAST_COMMAND_COPIED = "Command copied";
    public const string TOAST_COMMAND_COPIED_SUB = "Paste it into any terminal";

    public const string PICKER_TITLE = "Open a file";
    public const string PICKER_IMAGES = "Images";
    public const string PICKER_ALL = "All files";

    #endregion // Constants


    #region Public methods

    /// <summary>
    /// The window title (Appendix B item 14): "{file} – ExifGlass", or the app name alone.
    /// </summary>
    public static string WindowTitle(string? fileName) => string.IsNullOrEmpty(fileName) ? AppInfo.PRODUCT_NAME : $"{fileName} – {AppInfo.PRODUCT_NAME}";

    public static string SearchTags(int count) => string.Create(CultureInfo.InvariantCulture, $"Search {count:N0} tags");

    public static string InGroups(int count) => count == 1 ? "in 1 group" : string.Create(CultureInfo.InvariantCulture, $"in {count:N0} groups");

    public static string OpenedOneOf(int total) => string.Create(CultureInfo.InvariantCulture, $"Opened 1 of {total:N0} files");

    public static string OpenTooltip => $"Open file ({UiText.Shortcut("O")})";

    public static string SettingsTooltip => $"{SETTINGS} ({UiText.Shortcut(",")})";

    public static string ThemeName(AppTheme theme) => theme switch
    {
        AppTheme.Light => "Light",
        AppTheme.Dark => "Dark",
        _ => "System",
    };


    /// <summary>
    /// DS 1.3: "Theme: System — click for Light".
    /// </summary>
    public static string ThemeTooltip(AppTheme current, AppTheme next) => $"Theme: {ThemeName(current)} — click for {ThemeName(next)}";

    #endregion // Public methods
}
