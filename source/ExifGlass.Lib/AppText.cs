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

    // editing, the pending bar, Save and the unsaved-changes dialog (DS 7.3, 6.13; S5; D8; Appendix B items 3 and 16)
    public const string EDIT_STAGED = "Already staged";
    public const string EDIT_AI_NONE = "No AI generation data in this file";
    public const string EDIT_GPS_MANIFEST = "Only in the Content Credentials, which ExifTool can't edit";
    public const string NOTE_MANIFEST_GPS = "The location in the Content Credentials stays: Strip all metadata removes it with the manifest";
    public const string TOAST_TAG_STAGED = "Tag staged";
    public const string TOAST_NOTHING_SAVED = "Nothing to change";
    public const string TOAST_NOTHING_SAVED_SUB = "The file already had these values";
    public const string TOAST_SAVED_BINARY = "Saved binary data";
    public const string TOAST_BINARY_FAILED = "Couldn't read the binary data";
    public const string SAVE = "Save";
    public const string DISCARD = "Discard";
    public const string SAVE_UNAVAILABLE = "ExifTool isn't available";
    public const string BINARY_FILES = "Binary data";

    // the Details card's AI-training row (AI addendum question 10)
    public const string AI_TRAINING = "AI training";
    public const string NOT_ALLOWED = "Not allowed";
    public const string PLUS_DATA_MINING = "PLUS DataMining";
    public const string EXIF_OPT_OUT = "Exif 3.1 LearningOptOutIn";

    // Settings (DS 6.15, Proto)
    public const string GENERAL_OVERLINE = "GENERAL";
    public const string EXIFTOOL_OVERLINE = "EXIFTOOL";
    public const string PREVIEW_OVERLINE = "COMMAND PREVIEW";
    public const string APP_THEME = "App theme";
    public const string THEME_SYSTEM = "System";
    public const string THEME_LIGHT = "Light";
    public const string THEME_DARK = "Dark";
    public const string APP_THEME_DESC = "System follows your OS light or dark setting.";
    public const string ALWAYS_ON_TOP = "Keep window always on top";
    public const string ALWAYS_ON_TOP_DESC = "Float above other apps while you compare files.";
    public const string CHECK_UPDATES = "Check for updates on startup";
    public const string CHECK_UPDATES_DESC = "A quiet check against GitHub releases.";
    public const string EXECUTABLE_PATH = "Executable path";
    public const string BROWSE = "Browse…";
    public const string BROWSE_TIP = "Choose the exiftool to run";
    public const string ARGUMENTS = "Arguments";
    public const string ARGUMENTS_KEYS = "Enter to add · Backspace to remove";
    public const string ARGUMENTS_HINT = "Hover an argument to see what it does.";
    public const string RESET = "Reset";
    public const string RESET_TIP = "Back to the default settings";
    public const string OK = "OK";
    public const string CANCEL = "Cancel";
    public const string BUNDLED_EXIFTOOL = "Bundled ExifTool";
    public const string PATH_NOT_FOUND = "No file at this path. ExifGlass uses its bundled ExifTool instead.";
    public const string NO_EXIFTOOL = "No ExifTool was found. Choose the exiftool to run.";
    public const string USE_BUNDLED = "Clear the path to use the bundled ExifTool.";
    public const string SANDBOX_NOTE = "This version runs its bundled ExifTool and keeps the _original backups in its own container.";
    public const string PICKER_EXIFTOOL = "Choose ExifTool";
    public const string PICKER_PROGRAMS = "Programs";
    public const string TOAST_SETTINGS_SAVED = "Settings saved";

    // About (FewerPhotos's About, owner at the P9 review; D10, D11)
    public const string NOTICES_OVERLINE = "THIRD-PARTY NOTICES";
    public const string BACK = "Back";
    public const string BACK_TIP = "Back (Esc)";
    public const string COPYRIGHT = "Copyright © 2023 - 2026 Duong Dieu Phap";
    public const string COPY_VERSION_INFO = "Copy version info";
    public const string TOAST_VERSION_COPIED = "Version info copied";
    public const string UPDATES = "Updates";
    public const string NEW_BADGE = "NEW";
    public const string CHECK_NOW = "Check now";
    public const string NOT_CHECKED = "Not checked yet";
    public const string CHECKING_UPDATES = "Checking for updates…";
    public const string UPDATE_ERROR = "Couldn't check for updates. Check your connection and try again.";
    public const string DOWNLOAD = "Download";
    public const string WEBSITE = "Website";
    public const string VISIT = "Visit";
    public const string HOMEPAGE_TEXT = "github.com/d2phap/ExifGlass";
    public const string STORE_TITLE = "Microsoft Store";
    public const string STORE_SUB = "Support development · automatic updates";
    public const string STORE_GET = "Get ExifGlass";
    public const string RELEASES_TITLE = "Downloads";
    public const string RELEASES_SUB = "Every release on GitHub";
    public const string SHOW_FEWER = "Show fewer";
    public const string STORE_URL = "https://apps.microsoft.com/detail/9MX8S9HZ57W8";
    public const string TOAST_UPDATE_SUB = "Download it from About";

    // the macOS menu bar (Appendix B item 5)
    public const string MENU_FILE = "File";
    public const string MENU_EDIT = "Edit";
    public const string MENU_VIEW = "View";
    public const string MENU_WINDOW = "Window";
    public const string MENU_SETTINGS = "Settings…";
    public const string MENU_QUIT = "Quit ExifGlass";
    public const string MENU_OPEN = "Open…";
    public const string MENU_COPY = "Copy";
    public const string MENU_SELECT_ALL = "Select All";
    public const string MENU_FIND = "Find";
    public const string MENU_GROUPS = "Groups Panel";
    public const string MENU_INSPECTOR = "Inspector";
    public const string MENU_COMMAND_BAR = "Command Bar";
    public const string MENU_MINIMIZE = "Minimize";
    public const string MENU_ZOOM = "Zoom";

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

    public static string GpsTags(int count) => string.Create(CultureInfo.InvariantCulture, $"{count:N0} location {(count == 1 ? "tag" : "tags")} · {Core.Editing.WritePlan.REMOVE_GPS}");

    /// <summary>
    /// The pending bar's title (S5): "3 pending changes".
    /// </summary>
    public static string PendingChanges(int count) => count == 1 ? "1 pending change" : string.Create(CultureInfo.InvariantCulture, $"{count:N0} pending changes");

    /// <summary>
    /// The pending bar's line (DS 6.13): "Save writes DSCF4127.JPG · backup kept as DSCF4127.JPG_original".
    /// </summary>
    public static string SaveWrites(string file) => $"Save writes {file} · backup kept as {file}_original";

    public static string TagStaged(string group, string name) => $"{group}:{name}";

    public static string Saved(int count) => count == 1 ? "Saved 1 change" : string.Create(CultureInfo.InvariantCulture, $"Saved {count:N0} changes");

    public static string BackupKept(string file) => $"Backup kept as {file}_original";

    public static string SaveFailed(string file) => $"Couldn't save {file}";

    /// <summary>
    /// D8's question when another file is asked for or the window closes with edits staged.
    /// </summary>
    public static string UnsavedTitle(string file) => $"Save changes to {file}?";

    public static string UnsavedBody(int count) => $"{PendingChanges(count)} not written yet. Discard drops them.";

    /// <summary>
    /// The Add-tag card's hint for a name ExifTool has no writable tag for: "ExifTool has no EXIF tag it can write named Foo".
    /// </summary>
    public static string NotWritable(string group, string name) => $"ExifTool has no {group} tag it can write named {name}";

    /// <summary>
    /// A note from the AI redaction, with how many more there are: "PNG:Comment can't be removed (+2 more)".
    /// </summary>
    public static string FirstOf(IReadOnlyList<string> notes) => notes.Count switch
    {
        0 => string.Empty,
        1 => notes[0],
        _ => string.Create(CultureInfo.InvariantCulture, $"{notes[0]} (+{notes.Count - 1} more)"),
    };

    public static string SavedBinary(string file, int bytes) => string.Create(CultureInfo.InvariantCulture, $"{file} · {bytes / 1024.0:0.0} kB");

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

    /// <summary>
    /// The "Settings saved" toast's line (Proto): "8 arguments · bundled ExifTool".
    /// </summary>
    public static string SettingsSavedSub(int arguments, bool custom) =>
        string.Create(CultureInfo.InvariantCulture, $"{arguments:N0} {(arguments == 1 ? "argument" : "arguments")} · {(custom ? "custom" : "bundled")} ExifTool");

    /// <summary>
    /// The path field's placeholder (plan 4.4): "Bundled · exiftool 13.59".
    /// </summary>
    public static string ExifToolPlaceholder(Core.ExifTool.ExifToolSource? source, string? version) => (source, version) switch
    {
        (Core.ExifTool.ExifToolSource.SystemPath, { } v) => $"On PATH · exiftool {v}",
        (_, { } v) => $"Bundled · exiftool {v}",
        _ => BUNDLED_EXIFTOOL,
    };

    /// <summary>
    /// A refused argument under the field: "-o: Writes or changes files".
    /// </summary>
    public static string Refused(string argument, string reason) => $"{argument}: {reason}";

    /// <summary>
    /// About's version line (FewerPhotos): "Version 3.0.0.1020-alpha (2026.10.25) · Windows · x64".
    /// </summary>
    public static string VersionLine(string version, string buildDate, string os, string architecture) => $"Version {version} ({buildDate}) · {os} · {architecture}";

    public static string UpToDate(string version, string time) => $"You're up to date. Version {version} is the latest · checked at {time}.";

    public static string UpdateReady(string version, string released) =>
        string.IsNullOrEmpty(released) ? $"Version {version} is available. Download it from its page when you're ready." : $"Version {version} is available (released {released}). Download it from its page when you're ready.";

    /// <summary>
    /// D10's startup toast: "ExifGlass 3.0.1 is available".
    /// </summary>
    public static string UpdateAvailable(string version) => $"{AppInfo.PRODUCT_NAME} {version} is available";

    public static string ThemeName(AppTheme theme) => theme switch
    {
        AppTheme.Light => THEME_LIGHT,
        AppTheme.Dark => THEME_DARK,
        _ => THEME_SYSTEM,
    };


    /// <summary>
    /// DS 1.3 without its em dash (owner at G4 review): "Theme: System (click for Light)".
    /// </summary>
    public static string ThemeTooltip(AppTheme current, AppTheme next) => $"Theme: {ThemeName(current)} (click for {ThemeName(next)})";

    #endregion // Public methods
}
