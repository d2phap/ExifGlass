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
    public const string SCROLL_UP = "Scroll up";
    public const string SCROLL_DOWN = "Scroll down";
    public const string SCROLL_LEFT = "Scroll left";
    public const string SCROLL_RIGHT = "Scroll right";

    // the table's column headers are Overline: uppercase (DS 1.1)
    public const string COLUMN_INDEX = "#";
    public const string COLUMN_TAG_ID = "TAG ID";
    public const string COLUMN_TAG_NAME = "TAG NAME";
    public const string COLUMN_VALUE = "VALUE";
    public const string COPY_VALUE = "Copy value";
    public const string BADGE_EDITED = "Edited";
    public const string BADGE_DELETE = "Delete";
    public const string BADGE_NEW = "New";
    public const string AI_GROUP = "AI generation";

    // the inspector's cards (DS 6.11, S4); overlines are uppercase (DS 1.1)
    public const string CLOSE = "Close";
    public const string DELETE_TAG = "Delete tag";
    public const string UNDO_DELETE = "Undo delete";
    public const string COPY_TAG_KEY = "Copy tag key";
    public const string EDIT = "Edit";
    public const string EDIT_VALUE = "Edit value";
    public const string CANCEL = "Cancel";
    public const string APPLY = "Apply";
    public const string CANCEL_TIP = "Cancel (Esc)";
    public const string REVERT = "Revert";
    public const string RAW_VALUE = "Raw (-n)";
    public const string FORMAT = "Format";
    public const string TAG_KEY = "Tag key";
    public const string WAS = "Was: {0}";
    public const string AI_GENERATION_OVERLINE = "AI GENERATION";
    public const string PROMPT_OVERLINE = "PROMPT";
    public const string NEGATIVE_PROMPT_OVERLINE = "NEGATIVE PROMPT";
    public const string COPY_PARAMETERS = "Copy generation parameters";
    public const string FRAME_OVERLINE = "FRAME";
    public const string OPEN_IN_OSM = "Open in OpenStreetMap";
    public const string NO_CAMERA_DATA = "No camera data in this file";
    public const string DETAIL_CAMERA = "Camera";
    public const string DETAIL_LENS = "Lens";
    public const string DETAIL_CAPTURED = "Captured";
    public const string DETAIL_LOCATION = "Location";

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


    /// <summary>
    /// A column header's tooltip, as the prototype's title: "Sort by Tag name".
    /// </summary>
    public static string SortBy(string column) => $"Sort by {column}";


    public static string ApplyTip => $"{APPLY} ({Shortcut("Enter")})";

    #endregion // Public methods
}
