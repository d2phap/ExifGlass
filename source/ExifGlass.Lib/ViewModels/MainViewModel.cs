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
    /// Shows the open picker (images, then All files); null when cancelled.
    /// </summary>
    Task<string?> PickFileAsync();

    void ShowToast(string title, string? subtitle = null, ToastKind kind = ToastKind.Success);

    void FocusSearch();

    void ShowSettings();

    void ShowAbout();

    Task CopyTextAsync(string text);

    #endregion // Methods
}


/// <summary>
/// The main window's state (plan 4.6): the document and its load, search, the panels at the current width, theme and pin.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    #region Fields and constants

    private readonly SettingsStore _settings;
    private readonly MetadataService? _metadata;
    private readonly ArgumentAnalysis _arguments;
    private int _queryGeneration;

    #endregion // Fields and constants


    #region Constructors

    /// <summary>
    /// The app passes its MetadataService as both <paramref name="reader"/> and <paramref name="metadata"/>; tests pass a reader alone.
    /// </summary>
    public MainViewModel(SettingsStore settings, IMetadataReader reader, MetadataService? metadata = null)
    {
        _settings = settings;
        _metadata = metadata;
        _arguments = ArgumentPolicy.Analyze(settings.Current.ExifToolArguments);

        Loader = new DocumentLoader(reader);
        Loader.Changed += Loader_Changed;
        Loader.Failed += Loader_Failed;

        Theme = settings.Current.Theme;
        IsPinned = settings.Current.AlwaysOnTop;
        RailOpen = settings.Current.RailOpen;
        InspectorOpen = settings.Current.InspectorOpen;

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
    [NotifyPropertyChangedFor(nameof(IsLoading), nameof(ShowEmptyCard), nameof(Summary))]
    public partial LoadPhase Phase { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDocument), nameof(WindowTitle), nameof(SearchPlaceholder), nameof(ShowEmptyCard), nameof(Summary), nameof(SummaryDetail), nameof(ShowSummaryDetail))]
    public partial MetadataDocument? Document { get; private set; }

    /// <summary>
    /// The query's result for the table: group headers and rows (S2's flat model).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary), nameof(SearchPlaceholder))]
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
    [NotifyPropertyChangedFor(nameof(ShowSecondaryButtons), nameof(OpenLabel), nameof(ShowSearchHint))]
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
    public partial bool IsRailOverlayOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInspectorShown), nameof(InspectorTooltip))]
    public partial bool IsInspectorOverlayOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSummaryDetail))]
    public partial bool GroupCountFits { get; private set; } = true;

    public bool HasDocument => Document is not null;

    public bool IsLoading => Phase == LoadPhase.Loading;

    public bool ShowEmptyCard => Document is null && Phase != LoadPhase.Loading;

    public bool HasExifToolProblem => ExifToolProblem is not null;

    public bool HasCommand => CommandTokens.Count > 0;

    public string WindowTitle => AppText.WindowTitle(Document?.FileName);

    // the view's total: the CopyN duplicates stay out unless -a is on, as in the summary
    public string SearchPlaceholder => Document is null ? AppText.SEARCH_NO_FILE : AppText.SearchTags(Rows.Total);

    public string Summary => Document is null ? (IsLoading ? AppText.SUMMARY_READING : AppText.SUMMARY_NO_FILE) : Rows.Summary;

    public string SummaryDetail => Document is null ? string.Empty : AppText.InGroups(Document.Groups.Count);

    public bool ShowSummaryDetail => Document is not null && GroupCountFits;

    public IconId ThemeIcon => Theme switch
    {
        AppTheme.Light => IconId.WeatherSunny,
        AppTheme.Dark => IconId.WeatherMoon,
        _ => IconId.DarkTheme,
    };

    public string ThemeTooltip => AppText.ThemeTooltip(Theme, ThemeService.Next(Theme));

    public string PinTooltip => IsPinned ? AppText.UNPIN : AppText.PIN;

    public bool IsRailDocked => RailMode == PanelMode.Docked && RailOpen;

    public bool IsRailShown => RailMode == PanelMode.Docked ? RailOpen : IsRailOverlayOpen;

    public string RailTooltip => IsRailShown ? AppText.HIDE_RAIL : AppText.SHOW_RAIL;

    public bool IsInspectorDocked => InspectorMode == PanelMode.Docked && InspectorOpen;

    public bool IsInspectorShown => InspectorMode == PanelMode.Docked ? InspectorOpen : IsInspectorOverlayOpen;

    public string InspectorTooltip => IsInspectorShown ? AppText.HIDE_INSPECTOR : AppText.SHOW_INSPECTOR;

    public bool ShowSecondaryButtons => Toolbar == ToolbarMode.Full;

    /// <summary>
    /// The Open button's label: hidden below 620 px, where the toolbar is icon-only.
    /// </summary>
    public string? OpenLabel => Toolbar == ToolbarMode.IconOnly ? null : AppText.OPEN;

    public bool ShowSearchHint => Toolbar != ToolbarMode.IconOnly;

    #endregion // Properties


    #region Public methods

    public Task OpenFileAsync(string path) => Loader.OpenAsync(path);


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
    /// The part of the Esc chain the window owns, after edits, menus and dialogs: overlays, then search; true when it did something.
    /// </summary>
    public bool Escape()
    {
        if (IsRailOverlayOpen || IsInspectorOverlayOpen)
        {
            IsRailOverlayOpen = false;
            IsInspectorOverlayOpen = false;
            return true;
        }

        if (SearchText.Length > 0)
        {
            SearchText = string.Empty;
            return true;
        }

        return false;
    }


    /// <summary>
    /// Places the DS 2.1 bands for the window's width; an overlay whose panel docks closes.
    /// </summary>
    public void UpdateWindowWidth(double width)
    {
        if (!Layout.Update(width)) return;

        if (Layout.Rail == PanelMode.Docked) IsRailOverlayOpen = false;
        if (Layout.Inspector == PanelMode.Docked) IsInspectorOverlayOpen = false;
        RailMode = Layout.Rail;
        InspectorMode = Layout.Inspector;
        Toolbar = Layout.Toolbar;
    }


    public void UpdateTableWidth(double width)
    {
        if (Table.Update(width)) GroupCountFits = Table.GroupCountFits;
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


    [RelayCommand]
    private void ToggleRail()
    {
        if (RailMode == PanelMode.Overlay)
        {
            IsRailOverlayOpen = !IsRailOverlayOpen;
            if (IsRailOverlayOpen) IsInspectorOverlayOpen = false;
            return;
        }

        RailOpen = !RailOpen;
        _settings.Update(s => s.RailOpen = RailOpen);
    }


    [RelayCommand]
    private void ToggleInspector()
    {
        if (InspectorMode == PanelMode.Overlay)
        {
            IsInspectorOverlayOpen = !IsInspectorOverlayOpen;
            if (IsInspectorOverlayOpen) IsRailOverlayOpen = false;
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


    partial void OnDocumentChanged(MetadataDocument? value) => UpdateCommandTokens();


    // the query runs off the UI thread; only the newest result is applied, as one reset (plan 4.2), with its document and phase
    private async Task RunQueryAsync(MetadataDocument? document, LoadPhase? phase = null)
    {
        var generation = ++_queryGeneration;
        var query = new ViewQuery
        {
            Search = SearchText,
            ShowDuplicates = _arguments.ShowDuplicates,
            ShowRawValues = _arguments.ShowRawValues,
            ShowTagNames = _arguments.ShowTagNames,
        };

        var result = document is null ? ViewResult.Empty : await Task.Run(() => QueryEngine.Run(document, query));
        if (generation != _queryGeneration) return;

        if (phase is { } p) Phase = p;
        Document = document;
        Rows = result;
    }


    private void UpdateCommandTokens() => CommandTokens = _metadata?.ReadCommand(Document?.SourcePath ?? Loader.PendingPath) ?? [];


    private void UpdateExifToolState()
    {
        ExifToolProblem = _metadata?.Status is ExifToolStatus.Missing or ExifToolStatus.Failed ? _metadata.Problem ?? AppText.EXIFTOOL_MISSING_TITLE : null;
        UpdateCommandTokens();
    }

    #endregion // Private methods


    #region Event handlers

    private void Loader_Changed(object? sender, EventArgs e)
    {
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

    #endregion // Event handlers
}
