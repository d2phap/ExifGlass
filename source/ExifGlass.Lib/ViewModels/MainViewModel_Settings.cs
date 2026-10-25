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
using ExifGlass.Core.ExifTool;
using ExifGlass.Core.Settings;
using ExifGlass.Core.Update;
using ExifGlass.Services;
using ExifGlass.UI.Controls;

namespace ExifGlass.ViewModels;

/// <summary>
/// What OK in Settings saves (DS 6.15).
/// </summary>
public sealed record SettingsChange(AppTheme Theme, bool AlwaysOnTop, bool CheckForUpdates, string ExifToolPath, string Arguments);


public sealed partial class MainViewModel
{
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
    /// Saves Settings' OK: a new ExifTool restarts it, new arguments apply to the file on screen, then the "Settings saved" toast.
    /// </summary>
    public async Task ApplySettingsAsync(SettingsChange change)
    {
        var before = _settings.Current;
        var pathChanged = !string.Equals(before.ExifToolPath.Trim(), change.ExifToolPath.Trim(), StringComparison.Ordinal);
        var argumentsChanged = !string.Equals(before.ExifToolArguments, change.Arguments, StringComparison.Ordinal);
        var oldArguments = _arguments;

        _settings.Update(s =>
        {
            s.Theme = change.Theme;
            s.AlwaysOnTop = change.AlwaysOnTop;
            s.CheckForUpdates = change.CheckForUpdates;
            s.ExifToolPath = change.ExifToolPath.Trim();
            s.ExifToolArguments = change.Arguments;
        });

        PreviewTheme(change.Theme);
        IsPinned = change.AlwaysOnTop;
        if (argumentsChanged) _arguments = ArgumentPolicy.Analyze(change.Arguments);

        Shell?.ShowToast(AppText.TOAST_SETTINGS_SAVED, AppText.SettingsSavedSub(ArgumentPolicy.Split(change.Arguments).Count, change.ExifToolPath.Trim().Length > 0));

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
    /// D10's startup toast when the feed has a newer release.
    /// </summary>
    public void ShowUpdateToast(string version) => Shell?.ShowToast(AppText.UpdateAvailable(version), AppText.TOAST_UPDATE_SUB, ToastKind.Info);

    #endregion // Public methods
}
