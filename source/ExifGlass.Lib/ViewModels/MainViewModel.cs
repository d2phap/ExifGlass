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
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExifGlass.Core.Editing;
using ExifGlass.Core.Export;
using ExifGlass.Core.ExifTool;
using ExifGlass.Core.Metadata;
using ExifGlass.Core.Query;
using ExifGlass.Core.Settings;
using ExifGlass.Services;
using ExifGlass.UI.Controls;

namespace ExifGlass.ViewModels;

/// <summary>
/// What the view model asks of the main window: pickers, toasts, focus and the other windows.
/// </summary>
public interface IShellView
{
    #region Methods

    /// <summary>
    /// Shows the open picker (All files, then images); null when cancelled.
    /// </summary>
    Task<string?> PickFileAsync();

    void ShowToast(string title, string? subtitle = null, ToastKind kind = ToastKind.Success);

    void FocusSearch();

    void ShowSettings();

    void ShowAbout();

    Task CopyTextAsync(string text);

    /// <summary>
    /// Shows the save picker for an export; null when cancelled.
    /// </summary>
    Task<SaveTarget?> PickSaveFileAsync(string suggestedName, ExportFormat format);

    /// <summary>
    /// Opens a web link in the system's browser (the Details card's map).
    /// </summary>
    Task OpenUriAsync(string uri) => Task.CompletedTask;

    /// <summary>
    /// Shows the save picker for a file of any type (Save binary data); null when cancelled.
    /// </summary>
    Task<SaveTarget?> PickSaveFileAsync(string suggestedName, string extension, string typeName) => Task.FromResult<SaveTarget?>(null);

    /// <summary>
    /// Asks in the window's dialog (D8); Cancel when there is none.
    /// </summary>
    Task<DialogAnswer> AskAsync(DialogRequest request) => Task.FromResult(DialogAnswer.Cancel);

    #endregion // Methods
}


/// <summary>
/// A file the user chose to save to: its stream and the name the toast shows.
/// </summary>
public sealed class SaveTarget(Stream stream, string name) : IAsyncDisposable
{
    #region Properties

    public Stream Stream { get; } = stream;

    public string Name { get; } = name;

    #endregion // Properties


    #region Public methods

    public ValueTask DisposeAsync() => Stream.DisposeAsync();

    #endregion // Public methods
}


/// <summary>
/// The main window's state (plan 4.6): the document and its load, search, the panels at the current width, theme and pin.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    #region Fields and constants

    private readonly SettingsStore _settings;
    private readonly IMetadataReader _reader;
    private readonly MetadataService? _metadata;
    private ArgumentAnalysis _arguments;
    private int _queryGeneration;
    private double _windowWidth;

    #endregion // Fields and constants


    #region Constructors

    /// <summary>
    /// The app passes its MetadataService as both <paramref name="reader"/> and <paramref name="metadata"/>; tests pass a reader alone.
    /// </summary>
    public MainViewModel(SettingsStore settings, IMetadataReader reader, MetadataService? metadata = null)
    {
        _settings = settings;
        _reader = reader;
        _metadata = metadata;
        _arguments = ArgumentPolicy.Analyze(settings.Current.ExifToolArguments);

        Loader = new DocumentLoader(reader);
        Loader.Changed += Loader_Changed;
        Loader.Failed += Loader_Failed;
        Loader.PreviewChanged += Loader_PreviewChanged;

        Theme = settings.Current.Theme;
        IsPinned = settings.Current.AlwaysOnTop;
        RailOpen = settings.Current.RailOpen;
        InspectorOpen = settings.Current.InspectorOpen;
        RestorePanelWidths(settings.Current);
        Columns = ParseColumns(settings.Current.VisibleColumns);
        RestoreViewChoices(settings.Current);
        IsCommandExpanded = settings.Current.CommandBarExpanded;
        Selection.Changed += Selection_Changed;

        if (metadata is not null) metadata.StatusChanged += Metadata_StatusChanged;
        UpdateExifToolState();
    }

    #endregion // Constructors


    #region Properties

    public IShellView? Shell { get; set; }

    public DocumentLoader Loader { get; }

    public ShellLayout Layout { get; } = new();

    public TableLayout Table { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading), nameof(ShowEmptyCard), nameof(Summary), nameof(ShowSkeleton), nameof(FileTitle), nameof(FileMeta))]
    [NotifyPropertyChangedFor(nameof(IsCommandWrite))]
    public partial LoadPhase Phase { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDocument), nameof(WindowTitle), nameof(ShowEmptyCard), nameof(Summary), nameof(SummaryDetail), nameof(ShowSummaryDetail))]
    [NotifyPropertyChangedFor(nameof(IsRailDocked), nameof(IsRailShown), nameof(RailTooltip), nameof(IsInspectorDocked), nameof(IsInspectorShown), nameof(InspectorTooltip))]
    [NotifyPropertyChangedFor(nameof(ShowSkeleton), nameof(ShowNoMatch))]
    [NotifyCanExecuteChangedFor(nameof(ToggleRailCommand), nameof(ToggleInspectorCommand), nameof(ToggleCollapseAllCommand), nameof(ExportCommand), nameof(CopyAsJsonCommand), nameof(ToggleQuickFilterCommand))]
    public partial MetadataDocument? Document { get; private set; }

    /// <summary>
    /// The query's result for the table: group headers and rows (S2's flat model).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    public partial ViewResult Rows { get; private set; } = ViewResult.Empty;

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>
    /// Why ExifTool can't read, shown on the empty card; null while it can (Appendix B item 3).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExifToolProblem))]
    public partial string? ExifToolProblem { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCommand))]
    public partial IReadOnlyList<CommandToken> CommandTokens { get; private set; } = [];

    /// <summary>
    /// The command bar's expanded state, kept in the settings (plan P8).
    /// </summary>
    [ObservableProperty]
    public partial bool IsCommandExpanded { get; set; }

    /// <summary>
    /// The command types itself in while a file is read (DS 7.1).
    /// </summary>
    [ObservableProperty]
    public partial bool IsCommandTyping { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeIcon), nameof(ThemeTooltip))]
    public partial AppTheme Theme { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PinTooltip))]
    public partial bool IsPinned { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRailDocked), nameof(IsRailShown), nameof(RailTooltip))]
    public partial PanelMode RailMode { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInspectorDocked), nameof(IsInspectorShown), nameof(InspectorTooltip))]
    public partial PanelMode InspectorMode { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSecondaryButtons), nameof(OpenLabel), nameof(ShowSearchHint), nameof(SearchFillsGap))]
    public partial ToolbarMode Toolbar { get; private set; }

    /// <summary>
    /// The docked rail's open state, kept in the settings.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRailDocked), nameof(IsRailShown), nameof(RailTooltip))]
    public partial bool RailOpen { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInspectorDocked), nameof(IsInspectorShown), nameof(InspectorTooltip))]
    public partial bool InspectorOpen { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRailShown), nameof(RailTooltip))]
    public partial bool IsRailPushed { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInspectorShown), nameof(InspectorTooltip))]
    public partial bool IsInspectorPushed { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSummaryDetail))]
    public partial bool GroupCountFits { get; private set; } = true;

    public bool HasDocument => Document is not null;

    public bool IsLoading => Phase == LoadPhase.Loading;

    public bool ShowEmptyCard => Document is null && Phase != LoadPhase.Loading;

    public bool HasExifToolProblem => ExifToolProblem is not null;

    public bool HasCommand => CommandTokens.Count > 0;

    public string WindowTitle => AppText.WindowTitle(Document?.FileName);

    public string Summary => Document is null ? (IsLoading ? AppText.SUMMARY_READING : AppText.SUMMARY_NO_FILE) : Rows.Summary;

    public string SummaryDetail => Document is null ? string.Empty : AppText.InGroups(Document.Groups.Count);

    // Proto: "in 8 groups" makes room for the filter chips
    public bool ShowSummaryDetail => Document is not null && GroupCountFits && !HasFilterChips;

    public IconId ThemeIcon => Theme switch
    {
        AppTheme.Light => IconId.WeatherSunny,
        AppTheme.Dark => IconId.WeatherMoon,
        _ => IconId.DarkTheme,
    };

    public string ThemeTooltip => AppText.ThemeTooltip(Theme, ThemeService.Next(Theme));

    public string PinTooltip => IsPinned ? AppText.UNPIN : AppText.PIN;

    // with no file the panels stay closed and their toggles off, whatever the settings say (owner at G4 review)
    public bool IsRailDocked => HasDocument && RailMode == PanelMode.Docked && RailOpen;

    public bool IsRailShown => HasDocument && (RailMode == PanelMode.Docked ? RailOpen : IsRailPushed);

    public string RailTooltip => IsRailShown ? AppText.HIDE_RAIL : AppText.SHOW_RAIL;

    public bool IsInspectorDocked => HasDocument && InspectorMode == PanelMode.Docked && InspectorOpen;

    public bool IsInspectorShown => HasDocument && (InspectorMode == PanelMode.Docked ? InspectorOpen : IsInspectorPushed);

    public string InspectorTooltip => IsInspectorShown ? AppText.HIDE_INSPECTOR : AppText.SHOW_INSPECTOR;

    public bool ShowSecondaryButtons => Toolbar == ToolbarMode.Full;

    /// <summary>
    /// The Open button's label: hidden below 620 px, where the toolbar is icon-only.
    /// </summary>
    public string? OpenLabel => Toolbar == ToolbarMode.IconOnly ? null : AppText.OPEN;

    public bool ShowSearchHint => Toolbar != ToolbarMode.IconOnly;

    public bool SearchFillsGap => Toolbar == ToolbarMode.IconOnly;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Opens a file; with edits staged it asks first (D8), whatever asked for it: the picker, a drop, the command line, ImageGlass.
    /// </summary>
    public async Task OpenFileAsync(string path)
    {
        if (!await ConfirmLeaveAsync()) return;

        IsAddingTag = false;
        await Loader.OpenAsync(path);
    }


    /// <summary>
    /// Opens what was dropped: one file at a time, folders ignored (Appendix B item 6).
    /// </summary>
    public void OpenDropped(IReadOnlyList<string> files, int folderCount)
    {
        if (folderCount > 0) Shell?.ShowToast(AppText.TOAST_FOLDER, AppText.TOAST_FOLDER_SUB, ToastKind.Info);
        if (files.Count == 0) return;

        _ = OpenFileAsync(files[0]);
        if (files.Count > 1) Shell?.ShowToast(AppText.OpenedOneOf(files.Count), Path.GetFileName(files[0]), ToastKind.Info);
    }


    /// <summary>
    /// The part of the Esc chain the window owns, after menus and dialogs: an edit, a pushed panel, the search, then the selection (cell, then rows); true when it did something.
    /// </summary>
    public bool Escape()
    {
        if (IsEditing)
        {
            IsEditing = false;
            return true;
        }

        if (IsAddingTag)
        {
            IsAddingTag = false;
            return true;
        }

        if (IsRailPushed || IsInspectorPushed)
        {
            IsRailPushed = false;
            IsInspectorPushed = false;
            return true;
        }

        if (SearchText.Length > 0)
        {
            SearchText = string.Empty;
            return true;
        }

        return Selection.ClearStep();
    }


    /// <summary>
    /// Places the panels and the toolbar for the window's width; a pushed panel that docks stops being pushed. A fresh width, before the window has its first real size, ignores the bands' hysteresis.
    /// </summary>
    public void UpdateWindowWidth(double width, bool fresh = false)
    {
        _windowWidth = width;
        UpdatePushWidths();
        if (!Layout.Update(width, RailOpen, fresh)) return;

        if (Layout.Rail == PanelMode.Docked) IsRailPushed = false;
        if (Layout.Inspector == PanelMode.Docked) IsInspectorPushed = false;
        RailMode = Layout.Rail;
        InspectorMode = Layout.Inspector;
        Toolbar = Layout.Toolbar;
    }


    public void UpdateTableWidth(double width)
    {
        if (Table.Update(width)) OnTableLayoutChanged();
    }

    #endregion // Public methods


    #region Commands

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (Shell is null || await Shell.PickFileAsync() is not { } path) return;
        await OpenFileAsync(path);
    }


    [RelayCommand]
    private void CycleTheme()
    {
        Theme = ThemeService.Next(Theme);
        _settings.Update(s => s.Theme = Theme);
        ThemeService.Apply(Theme);
    }


    [RelayCommand]
    private void TogglePin()
    {
        IsPinned = !IsPinned;
        _settings.Update(s => s.AlwaysOnTop = IsPinned);

        if (IsPinned) Shell?.ShowToast(AppText.TOAST_PINNED, AppText.TOAST_PINNED_SUB);
        else Shell?.ShowToast(AppText.TOAST_UNPINNED, AppText.TOAST_UNPINNED_SUB);
    }


    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void ToggleRail()
    {
        if (RailMode == PanelMode.Push)
        {
            IsRailPushed = !IsRailPushed;
            if (IsRailPushed) IsInspectorPushed = false;
            return;
        }

        RailOpen = !RailOpen;
        _settings.Update(s => s.RailOpen = RailOpen);

        // the inspector docks in the room the rail leaves, or makes way for it
        UpdateWindowWidth(_windowWidth);
    }


    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void ToggleInspector()
    {
        if (InspectorMode == PanelMode.Push)
        {
            IsInspectorPushed = !IsInspectorPushed;
            if (IsInspectorPushed) IsRailPushed = false;
            return;
        }

        InspectorOpen = !InspectorOpen;
        _settings.Update(s => s.InspectorOpen = InspectorOpen);
    }


    [RelayCommand]
    private void OpenSettings() => Shell?.ShowSettings();


    [RelayCommand]
    private void OpenAbout() => Shell?.ShowAbout();


    [RelayCommand]
    private void FocusSearch() => Shell?.FocusSearch();


    [RelayCommand]
    private async Task RetryExifToolAsync()
    {
        if (_metadata is null) return;

        ExifToolProblem = null;
        await _metadata.RestartAsync();
    }


    [RelayCommand]
    private async Task CopyCommandAsync()
    {
        if (Shell is null) return;

        await Shell.CopyTextAsync(WritePlan.ToCommandLine(CommandTokens));
        Shell.ShowToast(AppText.TOAST_COMMAND_COPIED, AppText.TOAST_COMMAND_COPIED_SUB);
    }

    #endregion // Commands


    #region Private methods

    // the loader's document, not the one on screen: a search typed while a new file's rows are on their way must not undo it
    partial void OnSearchTextChanged(string value) => _ = RunQueryAsync(Loader.Document, Loader.Phase);


    partial void OnDocumentChanged(MetadataDocument? value)
    {
        if (value is null)
        {
            IsRailPushed = false;
            IsInspectorPushed = false;
        }

        NotifyEdits();
        NotifyInspector();
        NotifyAi();
    }


    partial void OnIsCommandExpandedChanged(bool value) => _settings.Update(s => s.CommandBarExpanded = value);


    // the query runs off the UI thread; only the newest result is applied, as one reset (plan 4.2), with its document and phase
    private async Task RunQueryAsync(MetadataDocument? document, LoadPhase? phase = null)
    {
        var generation = ++_queryGeneration;

        // a new read starts with every group open, nothing selected and nothing staged (Proto; D8 asked first), but the read after a save keeps the view; the AI data arriving is the same read
        if (document is not null && !document.IsSameRead(Document))
        {
            if (document.SourcePath != _reloadPath)
            {
                _collapsed.Clear();
                Selection.Clear();
            }

            SetEdits(EditSet.Empty);
            IsEditing = false;
        }

        RememberViewChoices();
        var query = BuildQuery();
        var result = document is null ? ViewResult.Empty : await Task.Run(() => QueryEngine.Run(document, query));
        if (generation != _queryGeneration) return;

        if (phase is { } p) Phase = p;
        Document = document;
        Rows = result;
    }


    // the read command, typed in while a file is read, or the write Save runs while edits are staged (DS 6.12, 7.1, 7.3)
    private void UpdateCommandTokens()
    {
        var loading = Loader.Phase == LoadPhase.Loading && Loader.PendingPath is not null;
        IsCommandTyping = loading && _reloadPath is null;
        if (_metadata is null)
        {
            CommandTokens = [];
            return;
        }

        CommandTokens = !loading && HasPending && Document is { } doc
            ? _metadata.WriteCommand(doc.SourcePath, _edits)
            : _metadata.ReadCommand(loading ? Loader.PendingPath : Document?.SourcePath);
    }


    private void UpdateExifToolState()
    {
        ExifToolProblem = _metadata?.Status is ExifToolStatus.Missing or ExifToolStatus.Failed ? _metadata.Problem ?? AppText.EXIFTOOL_MISSING_TITLE : null;
        UpdateCommandTokens();
    }

    #endregion // Private methods


    #region Event handlers

    private void Loader_Changed(object? sender, EventArgs e)
    {
        UpdateAiLoading();
        if (ReferenceEquals(Loader.Document, Document))
        {
            Phase = Loader.Phase;
            UpdateCommandTokens();
            return;
        }

        // a new document waits for its rows, so the summary and the empty card never show the gap between them
        _ = RunQueryAsync(Loader.Document, Loader.Phase);
    }


    private void Loader_Failed(object? sender, LoadFailedEventArgs e) => Shell?.ShowToast(AppText.TOAST_READ_FAILED, e.Message, ToastKind.Danger);


    private void Metadata_StatusChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(UpdateExifToolState);


    private void Loader_PreviewChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(Preview));
        OnPropertyChanged(nameof(ShowPreviewCard));
    }

    #endregion // Event handlers
}
