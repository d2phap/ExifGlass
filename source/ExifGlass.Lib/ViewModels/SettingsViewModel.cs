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
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExifGlass.Core.Editing;
using ExifGlass.Core.ExifTool;
using ExifGlass.Core.Localization;
using ExifGlass.Core.Settings;
using ExifGlass.Services;
using ExifGlass.UI.Controls;
using System.ComponentModel;

namespace ExifGlass.ViewModels;

/// <summary>
/// A toast in the Settings window: "Command copied", "Installed 1 language pack".
/// </summary>
public sealed record SettingsToast(string Title, string? Subtitle, ToastKind Kind = ToastKind.Success);


/// <summary>
/// One language in the Settings list: its own name first, then the English one when they differ.
/// </summary>
public sealed class LanguageChoice(Lang pack)
{
    #region Properties

    public Lang Pack { get; } = pack;

    public string Name => Pack.Metadata.LocalName;

    public string? EnglishName => string.Equals(Pack.Metadata.EnglishName, Name, StringComparison.OrdinalIgnoreCase) ? null : Pack.Metadata.EnglishName;

    #endregion // Properties
}


/// <summary>
/// The Settings window (DS 6.15): a draft of the settings that OK saves; the theme, the accent and the language apply at once and Cancel restores them (Appendix B item 18).
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    #region Fields and constants

    private readonly MainViewModel _main;
    private readonly Lang _savedLanguage = Lang.Current;
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
        Languages = [new LanguageChoice(Lang.Current)];
        SelectedLanguage = Languages[0];

        main.PropertyChanged += Main_PropertyChanged;
        if (main.Metadata is { } metadata) metadata.StatusChanged += Metadata_StatusChanged;
        AccentService.SystemAccentChanged += AccentService_SystemAccentChanged;
        UpdateArguments();
        _ = UpdatePathHintAsync();
        _ = LoadLanguagesAsync();
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

    /// <summary>
    /// The built-in English and every installed pack; OK applies the chosen one to every window.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<LanguageChoice> Languages { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LanguageContributors), nameof(HasLanguageContributors))]
    public partial LanguageChoice? SelectedLanguage { get; set; }

    /// <summary>
    /// The Language row's line: who translated the chosen language.
    /// </summary>
    public string LanguageContributors => SelectedLanguage is { } choice ? AppText.Contributors(choice.Pack.Metadata.Contributors) : string.Empty;

    public bool HasLanguageContributors => LanguageContributors.Length > 0;

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
    public partial string ArgumentHint { get; private set; } = Lang.Current[LangId.Settings_ArgumentsHint];

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
    public event EventHandler<SettingsToast>? ToastRequested;

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
            if (flag == suggestion) return Lang.Current[label];
        }

        return null;
    }


    /// <summary>
    /// Lists the languages again and picks <paramref name="select"/>, a Language setting, or keeps the choice.
    /// </summary>
    public async Task LoadLanguagesAsync(string? select = null)
    {
        var packs = await Task.Run(Lang.LoadAll);

        // the language on screen keeps its own instance, so listing again doesn't count as choosing it
        var shown = Lang.Current;
        var wanted = select ?? SelectedLanguage?.Pack.SettingValue ?? shown.SettingValue;
        Languages = [.. packs.Select(p => new LanguageChoice(p.SettingValue.Equals(shown.SettingValue, StringComparison.OrdinalIgnoreCase) ? shown : p))];
        SelectedLanguage = Languages.FirstOrDefault(c => c.Pack.SettingValue.Equals(wanted, StringComparison.OrdinalIgnoreCase)) ?? Languages[0];
    }


    /// <summary>
    /// Reads again the lines it builds, after the language it previews changed them.
    /// </summary>
    public void RefreshText()
    {
        UpdateArguments();
        _ = UpdatePathHintAsync();
        RefreshSwatches();
        OnPropertyChanged(nameof(ExifToolPlaceholder));
        OnPropertyChanged(nameof(LanguageContributors));
    }


    /// <summary>
    /// Copies the picked packs into the user folder and selects the first one; OK still applies it.
    /// </summary>
    public async Task InstallLanguagesAsync(IReadOnlyList<string> files)
    {
        if (files.Count == 0) return;

        var result = await Task.Run(() => Lang.Install(files));
        await LoadLanguagesAsync(result.Installed.FirstOrDefault());

        if (result.Incompatible.Count > 0) ToastRequested?.Invoke(this, new SettingsToast(Lang.Current[LangId.Toast_LanguageIncompatible], string.Join(", ", result.Incompatible), ToastKind.Danger));
        else ToastRequested?.Invoke(this, new SettingsToast(AppText.LanguagesInstalled(result.Installed.Count), SelectedLanguage?.Name));
    }


    /// <summary>
    /// Writes the chosen language as a pack to translate from: every key, English where the pack has no text.
    /// </summary>
    public void ExportLanguage(Stream stream, string fileName)
    {
        if (SelectedLanguage is not { } choice) return;

        choice.Pack.Export(stream);
        ToastRequested?.Invoke(this, new SettingsToast(AppText.Exported(fileName), choice.Name));
    }


    /// <summary>
    /// The file name Export suggests: the pack's own, or English.eglang.json for the template.
    /// </summary>
    public string ExportFileName => SelectedLanguage is { Pack.IsBuiltIn: false } choice ? choice.Pack.FileName : Lang.BUILT_IN + Lang.FILE_EXTENSION;


    /// <summary>
    /// The window is closing: without OK, the theme, the accent and the language go back to the saved ones.
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
        if (!ReferenceEquals(Lang.Current, _savedLanguage)) _main.PreviewLanguage(_savedLanguage);
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
        SelectedLanguage = Languages.FirstOrDefault(c => c.Pack.IsBuiltIn) ?? SelectedLanguage;
    }


    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task OkAsync()
    {
        _committed = true;
        var apply = _main.ApplySettingsAsync(new SettingsChange(Theme, Accent, AlwaysOnTop, CheckForUpdates, ExifToolPath.Trim().Trim('"'), ArgumentPolicy.Join(Arguments),
            SelectedLanguage?.Pack.SettingValue ?? _main.Settings.Current.Language));

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
        ToastRequested?.Invoke(this, new SettingsToast(Lang.Current[LangId.Toast_CommandCopied], Lang.Current[LangId.Toast_CommandCopiedSub]));
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


    partial void OnSelectedLanguageChanged(LanguageChoice? value)
    {
        if (value is not null && !ReferenceEquals(value.Pack, Lang.Current)) _main.PreviewLanguage(value.Pack);
    }


    private void RefreshSwatches()
    {
        _refreshingSwatches = true;
        AccentChoices = AccentService.Swatches();
        _refreshingSwatches = false;
        OnPropertyChanged(nameof(AccentIndex));
    }


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
            ArgumentHint = Lang.Current[LangId.Settings_ArgumentsHint];
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
            (PathHint, PathHintKind) = (Lang.Current[LangId.Settings_PathNotFound], NoteKind.Danger);
        }
        else if (path.Length == 0 && metadata?.Status == ExifToolStatus.Missing)
        {
            (PathHint, PathHintKind) = (Lang.Current[LangId.Settings_NoExifTool], NoteKind.Danger);
        }
        else if (path.Length > 0 && running is { Source: ExifToolSource.Custom } && SamePath(running.Path, path)
            && metadata?.Version is { } version && ExifToolVersion.MissingAiFeatures(version) is { Count: > 0 } missing)
        {
            (PathHint, PathHintKind) = ($"{AppText.OldExifTool(version, missing)} {Lang.Current[LangId.Settings_UseBundled]}", NoteKind.Warning);
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


    private void AccentService_SystemAccentChanged(object? sender, EventArgs e) => RefreshSwatches();


    private void Metadata_StatusChanged(object? sender, EventArgs e) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
    {
        OnPropertyChanged(nameof(ExifToolPlaceholder));
        UpdatePreview();
        _ = UpdatePathHintAsync();
    });

    #endregion // Event handlers
}
