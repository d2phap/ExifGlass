/*
ExifGlass - AI & EXIF metadata viewer
Copyright (C) 2023 - 2027 DUONG DIEU PHAP
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
using ExifGlass.Core.Localization;
using ExifGlass.Core.Settings;
using ExifGlass.UI;
using ExifGlass.UI.Localization;

namespace ExifGlass;

/// <summary>
/// The app's text that is built from parts (plan 5.9): counts, file names and shortcuts; the words themselves are LangId keys.
/// </summary>
public static class AppText
{
    #region Constants

    // names, addresses and specification terms every language keeps
    public const string COPYRIGHT = "Copyright © 2023 - 2027 Duong Dieu Phap";
    public const string HOMEPAGE_TEXT = "imageglass.org/exifglass";
    public const string STORE_TITLE = "Microsoft Store";
    public const string STORE_URL = "https://apps.microsoft.com/detail/9MX8S9HZ57W8";
    public const string PLUS_DATA_MINING = "PLUS DataMining";
    public const string EXIF_OPT_OUT = "Exif 3.1 LearningOptOutIn";

    #endregion // Constants


    #region Public methods

    /// <summary>
    /// Gives the XAML tooltips their platform shortcuts: "Open file (Ctrl+O)".
    /// </summary>
    public static void RegisterResources()
    {
        LangResources.SetArguments(LangId.Toolbar_OpenFileTip, UiText.Shortcut("O"));
        LangResources.SetArguments(LangId.Toolbar_SettingsTip, UiText.Shortcut(","));
        LangResources.SetArguments(LangId.Toolbar_AboutTip, "F1");
        LangResources.SetArguments(LangId.Edit_AddTagTip, UiText.Shortcut("Enter"));
        LangResources.SetArguments(LangId.Inspector_ApplyTip, UiText.Shortcut("Enter"));
    }


    /// <summary>
    /// The window title (Appendix B item 14): "{file} – ExifGlass", or the app name alone.
    /// </summary>
    public static string WindowTitle(string? fileName) => string.IsNullOrEmpty(fileName) ? AppInfo.PRODUCT_NAME : $"{fileName} – {AppInfo.PRODUCT_NAME}";

    public static string InGroups(int count) => count == 1 ? Lang.Current[LangId.Table_InGroupsOne] : Lang.Current[LangId.Table_InGroups, count];

    public static string OpenedOneOf(int total) => Lang.Current[LangId.Toast_OpenedOneOf, total];

    public static string Tags(int count) => UiText.Tags(count);

    public static string CopiedTags(int count) => Lang.Current[LangId.Toast_CopiedTags, Tags(count)];

    public static string CopiedAsJson(int count) => Lang.Current[LangId.Toast_CopiedRowsAsJson, count];

    public static string TagsInView(int tags, int ai) => Lang.Current[LangId.Table_InView, TagsAndAi(tags, ai)];

    /// <summary>
    /// The Export menu's header (DS 8: be concrete), uppercase as a menu header (DS 6.8).
    /// </summary>
    public static string ExportsInView(int tags, int ai) => Lang.Current[LangId.Export_Header, TagsAndAi(tags, ai).ToUpperInvariant()];

    /// <summary>
    /// "22 tags and 15 AI fields": AI fields are counted apart from tags (AI addendum question 2).
    /// </summary>
    public static string TagsAndAi(int tags, int ai) => ai == 0 ? Tags(tags) : Lang.Current[LangId._TagsAndAi, Tags(tags), AiFields(ai)];

    public static string Exported(string file) => Lang.Current[LangId.Toast_Exported, file];

    public static string ExportedSub(int tags, int ai, long bytes) => Lang.Current[LangId.Toast_ExportedSub, TagsAndAi(tags, ai), bytes / 1024.0];

    public static string GpsTags(int count) => count == 1
        ? Lang.Current[LangId.Edit_GpsTagsOne, Core.Editing.WritePlan.REMOVE_GPS]
        : Lang.Current[LangId.Edit_GpsTags, count, Core.Editing.WritePlan.REMOVE_GPS];

    /// <summary>
    /// The pending bar's title (S5): "3 pending changes".
    /// </summary>
    public static string PendingChanges(int count) => count == 1 ? Lang.Current[LangId.Pending_ChangesOne] : Lang.Current[LangId.Pending_Changes, count];

    /// <summary>
    /// The pending bar's line (DS 6.13): "Save writes DSCF4127.JPG and keeps a backup as DSCF4127.JPG_original".
    /// </summary>
    public static string SaveWrites(string file) => Lang.Current[LangId.Pending_SaveWrites, file];

    /// <summary>
    /// The Mac App Store build's pending bar line (D7): the backup stays in the app's container.
    /// </summary>
    public static string SaveWritesInContainer(string file) => Lang.Current[LangId.Pending_SaveWritesInContainer, file];

    public static string TagStaged(string group, string name) => $"{group}:{name}";

    public static string Saved(int count) => count == 1 ? Lang.Current[LangId.Toast_SavedOne] : Lang.Current[LangId.Toast_Saved, count];

    public static string BackupKept(string file) => Lang.Current[LangId.Toast_BackupKept, file];

    public static string SaveFailed(string file) => Lang.Current[LangId.Toast_SaveFailed, file];

    /// <summary>
    /// D8's question when another file is asked for or the window closes with edits staged.
    /// </summary>
    public static string UnsavedTitle(string file) => Lang.Current[LangId.Dialog_UnsavedTitle, file];

    public static string UnsavedBody(int count) => Lang.Current[LangId.Dialog_UnsavedBody, PendingChanges(count)];

    /// <summary>
    /// Save's question when another app changed the file after ExifGlass read it (owner at G12).
    /// </summary>
    public static string ChangedTitle(string file) => Lang.Current[LangId.Dialog_ChangedTitle, file];

    public static string ChangedBody(int count) =>
        Lang.Current[LangId.Dialog_ChangedBody, count == 1 ? Lang.Current[LangId._ChangesOne] : Lang.Current[LangId._Changes, count]];

    /// <summary>
    /// D29's question at the start after ImageGlass closed the app with edits staged.
    /// </summary>
    public static string RecoveryTitle(string file) => Lang.Current[LangId.Dialog_RecoveryTitle, file];

    public static string RecoveryBody(int count, bool fileChanged)
    {
        var kept = count == 1 ? Lang.Current[LangId.Dialog_RecoveryKeptOne] : Lang.Current[LangId.Dialog_RecoveryKept, count];
        return Lang.Current[fileChanged ? LangId.Dialog_RecoveryBodyChanged : LangId.Dialog_RecoveryBody, kept];
    }

    public static string RecoveryGone(string file) => Lang.Current[LangId.Toast_RecoveryGoneSub, file];

    public static string Restored(int count) => count == 1 ? Lang.Current[LangId.Toast_RestoredOne] : Lang.Current[LangId.Toast_Restored, count];

    public static string RestoreDropped(int count) => count == 1 ? Lang.Current[LangId.Toast_RestoreDroppedOne] : Lang.Current[LangId.Toast_RestoreDropped, count];

    /// <summary>
    /// The Add-tag card's hint for a name ExifTool has no writable tag for: "ExifTool has no EXIF tag it can write named Foo".
    /// </summary>
    public static string NotWritable(string group, string name) => Lang.Current[LangId.Edit_NotWritable, group, name];

    /// <summary>
    /// A note from the AI redaction, with how many more there are: "PNG:Comment can't be removed (+2 more)".
    /// </summary>
    public static string FirstOf(IReadOnlyList<string> notes) => notes.Count switch
    {
        0 => string.Empty,
        1 => notes[0],
        _ => Lang.Current[LangId._FirstOfMore, notes[0], notes.Count - 1],
    };

    public static string SavedBinary(string file, int bytes) => Lang.Current[LangId.Toast_SavedBinarySub, file, bytes / 1024.0];

    /// <summary>
    /// The hidden-groups chip (Proto): "Hiding EXIF" or "Hiding 3 groups".
    /// </summary>
    public static string Hiding(IReadOnlyCollection<string> groups) => groups.Count == 1
        ? Lang.Current[LangId.Table_HidingOne, groups.First()]
        : Lang.Current[LangId.Table_Hiding, groups.Count];

    /// <summary>
    /// A copied cell's toast line: the text, cut at 40 characters (Proto).
    /// </summary>
    public static string Clip(string text) => text.Length > 40 ? string.Concat(text.AsSpan(0, 40), "…") : text;

    public static string HideGroup(string group) => Lang.Current[LangId.Rail_HideGroup, group];

    public static string ShowGroup(string group) => Lang.Current[LangId.Rail_ShowGroup, group];

    public static string JumpTo(string group) => Lang.Current[LangId.Rail_JumpTo, group];

    public static string Characters(int count) => UiText.Characters(count);

    public static string AiFields(int count) => count == 1 ? Lang.Current[LangId._AiFieldsOne] : Lang.Current[LangId._AiFields, count];

    public static string A1111Text(string generator) => Lang.Current[LangId.Toast_A1111Text, generator];

    /// <summary>
    /// A copied AI value's toast: "Copied seed", "Copied character 1"; "Copied value" without a label.
    /// </summary>
    public static string CopiedField(string? label) => string.IsNullOrEmpty(label) ? Lang.Current[LangId.Toast_CopiedValue]
        : Lang.Current[LangId.Toast_CopiedField, label.Length > 1 && char.IsUpper(label[1]) ? label : char.ToLowerInvariant(label[0]) + label[1..]];

    /// <summary>
    /// R19 in the AI card: "Your ExifTool 12.40 can't read every AI field. C2PA Content Credentials in every container need 12.75."
    /// </summary>
    public static string OldExifTool(string version, IReadOnlyList<Core.ExifTool.ExifToolFeature> missing) =>
        Lang.Current[LangId.Settings_OldExifTool, version] + " " + string.Join(" ", missing.Select(f => Lang.Current[LangId.Settings_OldExifToolNeeds, f.Name, f.MinVersion]));

    /// <summary>
    /// The "Settings saved" toast's line (Proto): "8 arguments, bundled ExifTool".
    /// </summary>
    public static string SettingsSavedSub(int arguments, bool custom)
    {
        var count = arguments == 1 ? Lang.Current[LangId.Toast_SettingsSavedArgumentsOne] : Lang.Current[LangId.Toast_SettingsSavedArguments, arguments];
        return Lang.Current[custom ? LangId.Toast_SettingsSavedCustom : LangId.Toast_SettingsSavedBundled, count];
    }

    /// <summary>
    /// The path field's placeholder (plan 4.4): "Bundled exiftool 13.59".
    /// </summary>
    public static string ExifToolPlaceholder(Core.ExifTool.ExifToolSource? source, string? version) => (source, version) switch
    {
        (Core.ExifTool.ExifToolSource.SystemPath, { } v) => Lang.Current[LangId.Settings_ExifToolOnPath, v],
        (_, { } v) => Lang.Current[LangId.Settings_ExifToolBundled, v],
        _ => Lang.Current[LangId.Settings_BundledExifTool],
    };

    /// <summary>
    /// A refused argument under the field: "-o: Writes or changes files".
    /// </summary>
    public static string Refused(string argument, string reason) => $"{argument}: {reason}";

    /// <summary>
    /// About's version line (FewerPhotos): "Version 3.0.0.1020-alpha (2026.10.25), Windows x64".
    /// </summary>
    public static string VersionLine(string version, string buildDate, string os, string architecture) => Lang.Current[LangId.About_VersionLine, version, buildDate, os, architecture];

    /// <summary>
    /// The Updates row's line, kept to one (owner at the P9 review): "You're up to date (checked at 12:17)".
    /// </summary>
    public static string UpToDate(string time) => Lang.Current[LangId.About_UpToDate, time];

    public static string UpdateReady(string version, string released) =>
        string.IsNullOrEmpty(released) ? Lang.Current[LangId.About_UpdateReady, version] : Lang.Current[LangId.About_UpdateReadyDate, version, released];

    /// <summary>
    /// D10's startup toast: "ExifGlass 3.0.1 is available".
    /// </summary>
    public static string UpdateAvailable(string version) => Lang.Current[LangId.Toast_UpdateAvailable, AppInfo.PRODUCT_NAME, version];

    public static string ThemeName(AppTheme theme) => theme switch
    {
        AppTheme.Light => Lang.Current[LangId.Settings_ThemeLight],
        AppTheme.Dark => Lang.Current[LangId.Settings_ThemeDark],
        _ => Lang.Current[LangId.Settings_ThemeSystem],
    };

    /// <summary>
    /// DS 1.3 without its em dash (owner at G4 review): "Theme: System (click for Light)".
    /// </summary>
    public static string ThemeTooltip(AppTheme current, AppTheme next) => Lang.Current[LangId.Toolbar_ThemeTip, ThemeName(current), ThemeName(next)];

    /// <summary>
    /// The Language row's line: "Contributors: Duong Dieu Phap"; empty for a pack that names no one.
    /// </summary>
    public static string Contributors(IReadOnlyList<string> names) => names.Count == 0 ? string.Empty : Lang.Current[LangId.Settings_LanguageContributors, string.Join(", ", names)];

    public static string LanguagesInstalled(int count) => count == 1 ? Lang.Current[LangId.Toast_LanguageInstalledOne] : Lang.Current[LangId.Toast_LanguageInstalled, count];

    #endregion // Public methods
}
