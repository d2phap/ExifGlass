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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExifGlass.Core.Editing;
using ExifGlass.Core.ExifTool;
using ExifGlass.Core.Settings;
using ExifGlass.Services;
using ExifGlass.UI.Controls;
using System.ComponentModel;

namespace ExifGlass.ViewModels;

/// <summary>
/// The Settings window (DS 6.15): a draft of the settings that OK saves; the theme and the accent apply at once and Cancel restores them (Appendix B item 18).
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    #region Fields and constants

    private readonly MainViewModel _main;
    private int _pathGeneration;
    private bool _committed;
    private bool _following;
    private bool _refreshingSwatches;

    #endregion // Fields and constants


    #region Constructors

    public SettingsViewModel(MainViewModel main)
    {
        _main = main;

        var current = main.Settings.Current;
        Theme = main.Theme;
        Accent = main.Accent;
        AlwaysOnTop = main.IsPinned;
        CheckForUpdates = current.CheckForUpdates;
        ExifToolPath = current.ExifToolPath;
        Arguments = ArgumentPolicy.Split(current.ExifToolArguments);

        main.PropertyChanged += Main_PropertyChanged;
        if (main.Metadata is { } metadata) metadata.StatusChanged += Metadata_StatusChanged;
        AccentService.SystemAccentChanged += AccentService_SystemAccentChanged;
        UpdateArguments();
        _ = UpdatePathHintAsync();
    }

    #endregion // Constructors


    #region Properties

    /// <summary>
    /// The update switch: only where this build checks (hidden in the Store, Mac App Store and Flatpak builds).
    /// </summary>
    public bool ShowsUpdates => _main.Channel.UpdateCheck;

    /// <summary>
    /// The ExifTool path row: not in the sandboxed Mac App Store build, which shows where its backups go instead (D7).
    /// </summary>
    public bool ShowsExifToolPath => _main.Channel.CustomExifTool;

    public bool ShowsSandboxNote => _main.Channel.BackupInContainer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeIndex))]
    public partial AppTheme Theme { get; set; }

    /// <summary>
    /// The segmented control's position: System, Light, Dark.
    /// </summary>
    public int ThemeIndex
    {
        get => (int)Theme;
        set
        {
            if (value is >= 0 and <= 2) Theme = (AppTheme)value;
        }
    }

    /// <summary>
    /// The accent row's swatches: System in the OS accent, then the presets (Appendix A item 84).
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<SwatchItem> AccentChoices { get; private set; } = AccentService.Swatches();

    /// <summary>
    /// The accent setting: System, a preset's id, or a #RRGGBB the file holds.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccentIndex))]
    public partial string Accent { get; set; } = AppSettings.ACCENT_SYSTEM;

    public int AccentIndex
    {
        get => AccentService.IndexOf(Accent);
        set
        {
            // new swatches reset the list's selection before the binding puts it back
            if (!_refreshingSwatches && value >= 0) Accent = AccentService.IdAt(value);
        }
    }

    [ObservableProperty]
    public partial bool AlwaysOnTop { get; set; }

    [ObservableProperty]
    public partial bool CheckForUpdates { get; set; }

    [ObservableProperty]
    public partial string ExifToolPath { get; set; } = string.Empty;

    /// <summary>
    /// The argument field's tokens in order (D1).
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> Arguments { get; set; } = [];

    /// <summary>
    /// The line under the argument field: a refused argument, a warning, or how hover helps.
    /// </summary>
    [ObservableProperty]
    public partial string ArgumentHint { get; private set; } = AppText.ARGUMENTS_HINT;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ArgumentHintIcon))]
    public partial NoteKind ArgumentHintKind { get; private set; }

    public IconId ArgumentHintIcon => IconFor(ArgumentHintKind);

    /// <summary>
    /// What the path row says when it matters: a missing file, an ExifTool too old for AI data (R19), none at all.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPathHint))]
    public partial string? PathHint { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PathHintIcon))]
    public partial NoteKind PathHintKind { get; private set; }

    public IconId PathHintIcon => IconFor(PathHintKind);

    public bool HasPathHint => PathHint is not null;

    [ObservableProperty]
    public partial IReadOnlyList<CommandToken> PreviewTokens { get; private set; } = [];

    /// <summary>
    /// The path field's placeholder: the ExifTool in use when the field is empty (plan 4.4).
    /// </summary>
    public string ExifToolPlaceholder => AppText.ExifToolPlaceholder(_main.Metadata?.Location?.Source, _main.Metadata?.Version);

    /// <summary>
    /// Gets the suggestions under the field (DS 6.15).
    /// </summary>
    public IReadOnlyList<string> Suggestions { get; } = [.. ArgumentPolicy.SUGGESTIONS.Select(s => s.Flag)];

    #endregion // Properties


    #region Events

    /// <summary>
    /// Occurs when OK or Cancel is done with the window.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Occurs when Settings says something in its own toast: "Command copied".
    /// </summary>
    public event EventHandler<(string Title, string? Subtitle)>? ToastRequested;

    #endregion // Events


    #region Public methods

    /// <summary>
    /// What a chip of the argument field says (D1); a value belongs to the flag before it.
    /// </summary>
    public static TokenNote NoteFor(IReadOnlyList<string> tokens, int index)
    {
        var chip = ArgumentPolicy.Chips(tokens)[index];
        return new TokenNote(chip.Description, chip.Reason, chip.IsLocked);
    }


    public static string? HintFor(string suggestion)
    {
        foreach (var (flag, label) in ArgumentPolicy.SUGGESTIONS)
        {
            if (flag == suggestion) return label;
        }

        return null;
    }


    /// <summary>
    /// The window is closing: without OK, the theme and the accent go back to the saved ones.
    /// </summary>
    public void Close()
    {
        _main.PropertyChanged -= Main_PropertyChanged;
        if (_main.Metadata is { } metadata) metadata.StatusChanged -= Metadata_StatusChanged;
        AccentService.SystemAccentChanged -= AccentService_SystemAccentChanged;
        if (_committed) return;

        var saved = _main.Settings.Current;
        if (_main.Theme != saved.Theme) _main.PreviewTheme(saved.Theme);
        if (_main.Accent != saved.Accent) _main.PreviewAccent(saved.Accent);
    }

    #endregion // Public methods


    #region Commands

    [RelayCommand]
    private void Reset()
    {
        var defaults = new AppSettings();
        Theme = defaults.Theme;
        Accent = defaults.Accent;
        AlwaysOnTop = defaults.AlwaysOnTop;
        CheckForUpdates = defaults.CheckForUpdates;
        ExifToolPath = defaults.ExifToolPath;
        Arguments = ArgumentPolicy.Split(defaults.ExifToolArguments);
    }


    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task OkAsync()
    {
        _committed = true;
        var apply = _main.ApplySettingsAsync(new SettingsChange(Theme, Accent, AlwaysOnTop, CheckForUpdates, ExifToolPath.Trim().Trim('"'), ArgumentPolicy.Join(Arguments)));

        // the window goes at once; ExifTool restarts and the file is read again behind it
        CloseRequested?.Invoke(this, EventArgs.Empty);
        await apply;
    }


    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);


    [RelayCommand]
    private async Task CopyCommandLineAsync()
    {
        if (_main.Shell is not { } shell) return;

        await shell.CopyTextAsync(WritePlan.ToCommandLine(PreviewTokens));
        ToastRequested?.Invoke(this, (AppText.TOAST_COMMAND_COPIED, AppText.TOAST_COMMAND_COPIED_SUB));
    }

    #endregion // Commands


    #region Private methods

    private bool CanSave() => ArgumentHintKind != NoteKind.Danger;


    private static IconId IconFor(NoteKind kind) => kind switch
    {
        NoteKind.Danger => IconId.ErrorCircle,
        NoteKind.Warning => IconId.Warning,
        _ => IconId.Info,
    };


    partial void OnThemeChanged(AppTheme value)
    {
        if (!_following) _main.PreviewTheme(value);
    }


    partial void OnAccentChanged(string value) => _main.PreviewAccent(value);


    partial void OnArgumentsChanged(IReadOnlyList<string> value) => UpdateArguments();


    partial void OnExifToolPathChanged(string value)
    {
        UpdatePreview();
        _ = UpdatePathHintAsync();
    }


    private void UpdateArguments()
    {
        var analysis = ArgumentPolicy.Analyze(Arguments);
        if (analysis.Refused.FirstOrDefault() is { } refused)
        {
            ArgumentHint = AppText.Refused(refused.Text, refused.Reason ?? string.Empty);
            ArgumentHintKind = NoteKind.Danger;
        }
        else if (analysis.Warnings.Count > 0)
        {
            ArgumentHint = analysis.Warnings[0];
            ArgumentHintKind = NoteKind.Warning;
        }
        else
        {
            ArgumentHint = AppText.ARGUMENTS_HINT;
            ArgumentHintKind = NoteKind.Plain;
        }

        OkCommand.NotifyCanExecuteChanged();
        UpdatePreview();
    }


    // the command the table read would match in a terminal, with the open file or <file>
    private void UpdatePreview()
    {
        var location = _main.Metadata?.Location;
        var path = ExifToolPath.Trim().Trim('"');
        var executable = path.Length > 0 ? path : location?.Path ?? (OperatingSystem.IsWindows() ? "exiftool.exe" : "exiftool");
        PreviewTokens = WritePlan.ReadTokens(executable, location?.ConfigPath, Arguments, _main.Document?.SourcePath);
    }


    // a path on a slow share must not stall typing, so the file check runs off the UI thread
    private async Task UpdatePathHintAsync()
    {
        var generation = ++_pathGeneration;
        var path = ExifToolPath.Trim().Trim('"');
        var exists = path.Length == 0 || await Task.Run(() => File.Exists(path));
        if (generation != _pathGeneration) return;

        var metadata = _main.Metadata;
        var running = metadata?.Location;
        if (!exists)
        {
            (PathHint, PathHintKind) = (AppText.PATH_NOT_FOUND, NoteKind.Danger);
        }
        else if (path.Length == 0 && metadata?.Status == ExifToolStatus.Missing)
        {
            (PathHint, PathHintKind) = (AppText.NO_EXIFTOOL, NoteKind.Danger);
        }
        else if (path.Length > 0 && running is { Source: ExifToolSource.Custom } && SamePath(running.Path, path)
            && metadata?.Version is { } version && ExifToolVersion.MissingAiFeatures(version) is { Count: > 0 } missing)
        {
            (PathHint, PathHintKind) = ($"{AppText.OldExifTool(version, missing)} {AppText.USE_BUNDLED}", NoteKind.Warning);
        }
        else
        {
            PathHint = null;
        }
    }


    private static bool SamePath(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    #endregion // Private methods


    #region Event handlers

    // the toolbar's Theme and Pin buttons still work while Settings is open; the rows follow them
    private void Main_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.Theme) when Theme != _main.Theme:
                _following = true;
                Theme = _main.Theme;
                _following = false;
                break;

            case nameof(MainViewModel.IsPinned):
                AlwaysOnTop = _main.IsPinned;
                break;

            case nameof(MainViewModel.Document):
                UpdatePreview();
                break;
        }
    }


    private void AccentService_SystemAccentChanged(object? sender, EventArgs e)
    {
        _refreshingSwatches = true;
        AccentChoices = AccentService.Swatches();
        _refreshingSwatches = false;
        OnPropertyChanged(nameof(AccentIndex));
    }


    private void Metadata_StatusChanged(object? sender, EventArgs e) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
    {
        OnPropertyChanged(nameof(ExifToolPlaceholder));
        UpdatePreview();
        _ = UpdatePathHintAsync();
    });

    #endregion // Event handlers
}
