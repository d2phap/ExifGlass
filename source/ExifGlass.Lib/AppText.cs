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
    public const string ABOUT = "About";
    public const string SETTINGS = "Settings";
    public const string SEARCH = "Search tags";

    public const string SEARCH_PLACEHOLDER = "Search";
    public const string SUMMARY_NO_FILE = "No file";
    public const string SUMMARY_READING = "Reading metadata…";

    public const string SHOW_RAIL = "Show groups panel";
    public const string HIDE_RAIL = "Hide groups panel";
    public const string SHOW_INSPECTOR = "Show inspector";
    public const string HIDE_INSPECTOR = "Hide inspector";
    public const string PIN = "Keep window on top";
    public const string UNPIN = "Unpin window";
    public const string COPY_COMMAND = "Copy command";

    public const string EMPTY_TITLE = "Drop a file here to view its metadata";
    public const string EMPTY_CAPTION = "Or click the button below";
    public const string RELEASE_TO_INSPECT = "Release to inspect";
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

    // the table (DS 6.9, 7.2, 7.4; Proto)
    public const string COLLAPSE_ALL = "Collapse all";
    public const string EXPAND_ALL = "Expand all";
    public const string COLLAPSE_ALL_TIP = "Collapse all groups";
    public const string EXPAND_ALL_TIP = "Expand all groups";
    public const string EDIT = "Edit";
    public const string EDIT_TIP = "Edit metadata";
    public const string COLUMNS = "Columns";
    public const string COLUMNS_TIP = "Show or hide columns";
    public const string COLUMNS_HEADER = "COLUMNS";
    public const string EXPORT = "Export";
    public const string SELECTED = "selected";
    public const string COPY = "Copy";
    public const string JSON = "JSON";
    public const string CLEAR = "Clear";
    public const string CLEAR_ALL = "Clear all";
    public const string REMOVE_FILTER = "Remove filter";
    public const string COPY_ROWS_TIP = "Copy as tab-separated name and value";
    public const string COPY_ROWS_JSON_TIP = "Copy as JSON with group-prefixed keys";
    public const string CLEAR_SELECTION_TIP = "Clear the selection (Esc)";
    public const string NO_MATCH_TITLE = "No tags match";
    public const string NO_MATCH_CAPTION = "Try a different search, or clear the filters.";
    public const string CLEAR_FILTERS = "Clear filters";

    public const string COLUMN_INDEX = "Index";
    public const string COLUMN_TAG_ID = "Tag ID";
    public const string COLUMN_TAG_NAME = "Tag name";
    public const string COLUMN_VALUE = "Value";
    public const string AT_LEAST_ONE = "At least one";
    public const string HIDDEN_AT_WIDTH = "Hidden at this width";

    public const string EXPORT_CSV = "CSV";
    public const string EXPORT_CSV_DESC = "Spreadsheet-ready, one row per tag";
    public const string EXPORT_TEXT = "Plain text";
    public const string EXPORT_TEXT_DESC = "Tab-separated, like exiftool -t";
    public const string EXPORT_JSON = "JSON";
    public const string EXPORT_JSON_DESC = "Compatible with exiftool -j -G";
    public const string EXPORT_COPY = "Copy as JSON";
    public const string EXPORT_COPY_DESC = "Straight to the clipboard";

    public const string EDIT_ADD = "Add tag…";
    public const string EDIT_ADD_DESC = "Write a new EXIF, XMP or IPTC tag";
    public const string EDIT_GPS = "Remove GPS data";
    public const string EDIT_GPS_NONE = "No GPS data in this file";
    public const string EDIT_AI = "Remove AI generation data";
    public const string EDIT_AI_DESC = "Prompts, workflows and parameters · provenance kept";
    public const string EDIT_STRIP = "Strip all metadata";
    public const string EDIT_STRIP_DESC = "Removes every writable tag · -all=";

    public const string FILTER_DATES = "Date & time";
    public const string FILTER_GPS = "GPS";
    public const string FILTER_CONVERTED = "Converted";
    public const string FILTER_TAG_ID = "Has tag ID";
    public const string FILTER_AI = "AI generation";
    public const string FILTER_PROMPTS = "Prompts";
    public const string FILTER_MODELS = "Models";
    public const string FILTER_PROVENANCE = "Provenance";
    public const string FILTER_AI_TIP = "Show AI fields and the tags they were read from";
    public const string FILTER_PROMPTS_TIP = "Show the prompts";
    public const string FILTER_MODELS_TIP = "Show the models, LoRAs and embeddings";
    public const string FILTER_PROVENANCE_TIP = "Show Content Credentials, IPTC, the AI label and AI-training signals";

    public const string TOAST_COPIED_VALUE = "Copied value";
    public const string TOAST_COPIED_CELL = "Copied cell";
    public const string TOAST_COPIED_TSV_SUB = "Tab-separated name and value";
    public const string TOAST_COPIED_JSON_SUB = "Group-prefixed keys";
    public const string TOAST_COPIED_AS_JSON = "Copied as JSON";
    public const string TOAST_EXPORT_FAILED = "Export failed";

    // the rail (DS 6.10; Proto) and the inspector (DS 6.11; Appendix B item 16)
    public const string GROUPS = "GROUPS";
    public const string QUICK_FILTERS = "QUICK FILTERS";
    public const string HIDE_ALL = "Hide all";
    public const string SHOW_ALL = "Show all";
    public const string FILE_READING = "Reading…";
    public const string FILE_READING_SUB = "ExifTool is parsing the file";
    public const string FILE_NONE = "No file open";
    public const string FILE_NONE_SUB = "Open or drop a file";
    public const string TOAST_COPIED_KEY = "Copied tag key";
    public const string TOAST_COPIED_RAW = "Copied raw value";
    public const string TOAST_COPIED_FORMAT = "Copied format";
    public const string TOAST_EDIT_STAGED = "Edit staged";
    public const string TOAST_NO_CHANGE = "No change";
    public const string TOAST_COPIED_PROMPT = "Copied prompt";
    public const string TOAST_COPIED_NEGATIVE = "Copied negative prompt";
    public const string TOAST_COPIED_PARAMETERS = "Copied generation parameters";
    public const string TOAST_COPIED_AI_JSON = "Copied AI data as JSON";
    public const string TOAST_COPIED_WORKFLOW = "Copied workflow JSON";
    public const string TOAST_COPIED_CHARACTER = "Copied character card";
    public const string TOAST_SAVED_WORKFLOW = "Saved workflow";
    public const string TOAST_SAVED_PARAMETERS = "Saved generation parameters";
    public const string TOAST_SAVED_AI_JSON = "Saved AI data";
    public const string TOAST_SAVED_CHARACTER = "Saved character card";

    // the Export menu's AI items (P7)
    public const string EXPORT_AI_HEADER = "AI GENERATION";
    public const string EXPORT_AI_PARAMETERS = "Generation parameters";
    public const string EXPORT_AI_PARAMETERS_DESC = "A1111 text, as .txt";
    public const string EXPORT_AI_JSON = "AI data as JSON";
    public const string EXPORT_AI_JSON_DESC = "Every parsed field with its source tag";
    public const string EXPORT_AI_WORKFLOW = "Workflow";
    public const string EXPORT_AI_WORKFLOW_DESC = "Exactly as embedded, as .json";

    // the Details card's AI-training row (AI addendum question 10)
    public const string AI_TRAINING = "AI training";
    public const string NOT_ALLOWED = "Not allowed";
    public const string PLUS_DATA_MINING = "PLUS DataMining";
    public const string EXIF_OPT_OUT = "Exif 3.1 LearningOptOutIn";

    #endregion // Constants


    #region Public methods

    /// <summary>
    /// The window title (Appendix B item 14): "{file} – ExifGlass", or the app name alone.
    /// </summary>
    public static string WindowTitle(string? fileName) => string.IsNullOrEmpty(fileName) ? AppInfo.PRODUCT_NAME : $"{fileName} – {AppInfo.PRODUCT_NAME}";

    public static string InGroups(int count) => count == 1 ? "in 1 group" : string.Create(CultureInfo.InvariantCulture, $"in {count:N0} groups");

    public static string OpenedOneOf(int total) => string.Create(CultureInfo.InvariantCulture, $"Opened 1 of {total:N0} files");

    public static string OpenTooltip => $"Open file ({UiText.Shortcut("O")})";

    public static string Tags(int count) => count == 1 ? "1 tag" : string.Create(CultureInfo.InvariantCulture, $"{count:N0} tags");

    public static string CopiedTags(int count) => $"Copied {Tags(count)}";

    public static string CopiedAsJson(int count) => string.Create(CultureInfo.InvariantCulture, $"Copied {count:N0} as JSON");

    public static string TagsInView(int tags, int ai) => $"{TagsAndAi(tags, ai)} in view";

    /// <summary>
    /// The Export menu's header (DS 8: be concrete), uppercase as a menu header (DS 6.8).
    /// </summary>
    public static string ExportsInView(int tags, int ai) => $"EXPORTS THE {TagsAndAi(tags, ai).ToUpperInvariant()} IN VIEW";

    /// <summary>
    /// "22 tags and 15 AI fields": AI fields are counted apart from tags (AI addendum question 2).
    /// </summary>
    public static string TagsAndAi(int tags, int ai) => ai == 0 ? Tags(tags) : $"{Tags(tags)} and {AiFields(ai)}";

    public static string Exported(string file) => $"Exported {file}";

    public static string ExportedSub(int tags, int ai, long bytes) => string.Create(CultureInfo.InvariantCulture, $"{TagsAndAi(tags, ai)} · {bytes / 1024.0:0.0} kB");

    public static string GpsTags(int count) => string.Create(CultureInfo.InvariantCulture, $"{count:N0} location {(count == 1 ? "tag" : "tags")} · -gps:all=");

    /// <summary>
    /// The hidden-groups chip (Proto): "Hiding EXIF" or "Hiding 3 groups".
    /// </summary>
    public static string Hiding(IReadOnlyCollection<string> groups) => groups.Count == 1
        ? $"Hiding {groups.First()}"
        : string.Create(CultureInfo.InvariantCulture, $"Hiding {groups.Count} groups");

    /// <summary>
    /// A copied cell's toast line: the text, cut at 40 characters (Proto).
    /// </summary>
    public static string Clip(string text) => text.Length > 40 ? string.Concat(text.AsSpan(0, 40), "…") : text;

    public static string SettingsTooltip => $"{SETTINGS} ({UiText.Shortcut(",")})";

    public static string HideGroup(string group) => $"Hide {group} tags";

    public static string ShowGroup(string group) => $"Show {group} tags";

    public static string JumpTo(string group) => $"Jump to {group}";

    public static string Characters(int count) => count == 1 ? "1 character" : string.Create(CultureInfo.InvariantCulture, $"{count:N0} characters");

    public static string AboutTooltip => $"{ABOUT} (F1)";

    public static string AiFields(int count) => count == 1 ? "1 AI field" : string.Create(CultureInfo.InvariantCulture, $"{count:N0} AI fields");

    public static string A1111Text(string generator) => $"{generator} · A1111 text";

    /// <summary>
    /// A copied AI value's toast: "Copied seed", "Copied character 1"; "Copied value" without a label.
    /// </summary>
    public static string CopiedField(string? label) => string.IsNullOrEmpty(label) ? TOAST_COPIED_VALUE
        : label.Length > 1 && char.IsUpper(label[1]) ? "Copied " + label
        : "Copied " + char.ToLowerInvariant(label[0]) + label[1..];

    /// <summary>
    /// R19 in the AI card: "Your ExifTool 12.40 can't read every AI field. C2PA Content Credentials in every container need 12.75."
    /// </summary>
    public static string OldExifTool(string version, IReadOnlyList<Core.ExifTool.ExifToolFeature> missing) =>
        $"Your ExifTool {version} can't read every AI field. " + string.Join(" ", missing.Select(f => string.Create(CultureInfo.InvariantCulture, $"{f.Name} need {f.MinVersion:0.00}.")));

    public static string ThemeName(AppTheme theme) => theme switch
    {
        AppTheme.Light => "Light",
        AppTheme.Dark => "Dark",
        _ => "System",
    };


    /// <summary>
    /// DS 1.3 without its em dash (owner at G4 review): "Theme: System (click for Light)".
    /// </summary>
    public static string ThemeTooltip(AppTheme current, AppTheme next) => $"Theme: {ThemeName(current)} (click for {ThemeName(next)})";

    #endregion // Public methods
}
