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
using ExifGlass.Core.ExifTool;
using ExifGlass.Core.Localization;
using ExifGlass.Core.Metadata;
using ExifGlass.Core.Settings;
using ExifGlass.Core.Update;
using ExifGlass.Services;
using ExifGlass.UI.Controls;

namespace ExifGlass.ViewModels;

/// <summary>
/// What OK in Settings saves (DS 6.15).
/// </summary>
public sealed record SettingsChange(AppTheme Theme, string Accent, bool AlwaysOnTop, bool EnableMultiInstances, bool CheckForUpdates, string ExifToolPath, string Arguments, string Language);


public sealed partial class MainViewModel
{
    #region Fields and constants

    private MetadataDocument? _relocalizedFor;
    private DerivedInfo? _relocalized;

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// The channel this build came from (plan 4.5): which Settings rows and About parts show.
    /// </summary>
    public ChannelPolicy Channel { get; init; } = AppServices.DetectChannel();

    /// <summary>
    /// The update check; null in tests that don't need one.
    /// </summary>
    public UpdateService? Updates { get; init; }

    /// <summary>
    /// The app's ExifTool, for Settings' status line and command preview; null in tests.
    /// </summary>
    public MetadataService? Metadata => _metadata;

    public SettingsStore Settings => _settings;

    /// <summary>
    /// The inspector's readouts in the language on screen: a file read in another one is described again.
    /// </summary>
    private DerivedInfo? Derived
    {
        get
        {
            if (Document is not { } doc) return null;
            if (ReferenceEquals(doc.Derived.Language, Lang.Current)) return doc.Derived;
            if (_relocalized is { } derived && ReferenceEquals(_relocalizedFor, doc) && ReferenceEquals(derived.Language, Lang.Current)) return derived;

            _relocalizedFor = doc;
            return _relocalized = DerivedInfo.From(doc);
        }
    }

    /// <summary>
    /// The accent on screen, which Settings may be previewing (Appendix A item 84).
    /// </summary>
    public string Accent { get; private set; } = AppSettings.ACCENT_SYSTEM;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Shows a theme without saving it: Settings applies its choice at once and Cancel restores the saved one (Appendix B item 18).
    /// </summary>
    public void PreviewTheme(AppTheme theme)
    {
        Theme = theme;
        ThemeService.Apply(theme);
    }


    /// <summary>
    /// Shows an accent without saving it, as PreviewTheme does.
    /// </summary>
    public void PreviewAccent(string accent)
    {
        Accent = accent;
        AccentService.Apply(accent);
    }


    /// <summary>
    /// Shows a language in every window without saving it: Settings applies its choice at once and Cancel restores the saved one, as PreviewTheme does.
    /// </summary>
    public void PreviewLanguage(Lang language) => Lang.Current = language;


    /// <summary>
    /// Saves Settings' OK: a new ExifTool restarts it, new arguments apply to the file on screen, then the "Settings saved" toast.
    /// </summary>
    public async Task ApplySettingsAsync(SettingsChange change)
    {
        var before = _settings.Current;
        var pathChanged = !string.Equals(before.ExifToolPath.Trim(), change.ExifToolPath.Trim(), StringComparison.Ordinal);
        var argumentsChanged = !string.Equals(before.ExifToolArguments, change.Arguments, StringComparison.Ordinal);
        var languageChanged = !string.Equals(before.Language, change.Language, StringComparison.OrdinalIgnoreCase);
        var oldArguments = _arguments;

        _settings.Update(s =>
        {
            s.Theme = change.Theme;
            s.Accent = change.Accent;
            s.AlwaysOnTop = change.AlwaysOnTop;
            s.EnableMultiInstances = change.EnableMultiInstances;
            s.CheckForUpdates = change.CheckForUpdates;
            s.ExifToolPath = change.ExifToolPath.Trim();
            s.ExifToolArguments = change.Arguments;
            s.Language = change.Language;
        });

        // Settings shows its choice already; any other caller's language is read here, before the toast below speaks
        if (languageChanged && !Lang.Current.SettingValue.Equals(change.Language, StringComparison.OrdinalIgnoreCase)) Lang.Current = Lang.FromSetting(change.Language);

        PreviewTheme(change.Theme);
        PreviewAccent(change.Accent);
        IsPinned = change.AlwaysOnTop;
        if (argumentsChanged) _arguments = ArgumentPolicy.Analyze(change.Arguments);

        Shell?.ShowToast(Lang.Current[LangId.Toast_SettingsSaved], AppText.SettingsSavedSub(ArgumentPolicy.Split(change.Arguments).Count, change.ExifToolPath.Trim().Length > 0));

        if (pathChanged && _metadata is not null) await _metadata.RestartAsync();

        // with edits staged the file isn't read again: the new arguments reach the read after Save
        var reread = pathChanged || (argumentsChanged && !oldArguments.PassThrough.SequenceEqual(_arguments.PassThrough));
        if (reread && !HasPending && Document?.SourcePath is { } path)
        {
            _reloadPath = path;
            await Loader.OpenAsync(path);
            _reloadPath = null;
        }
        else if (argumentsChanged) Requery();

        UpdateCommandTokens();
    }


    /// <summary>
    /// Reads again every text the main window set from code or computes, after a language change.
    /// </summary>
    public void RefreshText()
    {
        foreach (var item in RailGroups) item.RefreshText();
        foreach (var chip in AiQuickFilters.Concat(QuickFilters)) chip.RefreshText();
        UpdateRail();
        UpdateChips();
        UpdateExifToolState();
        UpdateAddSuggestions();

        // the view's summary and group descriptions are made with the rows
        Requery();
        OnPropertyChanged(string.Empty);
    }


    /// <summary>
    /// D10's startup toast when the feed has a newer release.
    /// </summary>
    public void ShowUpdateToast(string version) => Shell?.ShowToast(AppText.UpdateAvailable(version), Lang.Current[LangId.Toast_UpdateSub], ToastKind.Info);

    #endregion // Public methods
}
