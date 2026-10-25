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
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExifGlass.Core.Common;
using ExifGlass.Core.Update;
using ExifGlass.Services;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;

namespace ExifGlass.ViewModels;

/// <summary>
/// One third-party notice in About: its text file, the component and its licence (__assets/notices/gen-notices.py).
/// </summary>
public sealed record NoticeView(string File, string Name, string Licence);


/// <summary>
/// The About window, laid out as FewerPhotos's (owner at the P9 review): the version, Updates (D10), Website, the store row (D11), the third-party notices and their texts.
/// </summary>
public sealed partial class AboutViewModel : ObservableObject
{
    #region Fields and constants

    // FewerPhotos shows five notices before Show all
    public const int NOTICES_SHOWN = 5;

    private const string NOTICES = "avares://ExifGlass.Lib/Assets/Notices/";

    private static readonly Lazy<IReadOnlyList<NoticeView>> _notices = new(ReadNotices);

    private readonly MainViewModel _main;
    private readonly UpdateService? _updates;

    #endregion // Fields and constants


    #region Constructors

    public AboutViewModel(MainViewModel main)
    {
        _main = main;
        _updates = main.Updates;
        if (_updates is not null) _updates.PropertyChanged += Updates_PropertyChanged;
    }

    #endregion // Constructors


    #region Properties

    public string Tagline => AppInfo.TAGLINE;

    public static string AppVersion => AppInfo.BuildInfo is { } build
        ? (string.IsNullOrEmpty(build.ReleaseType) ? build.Version : $"{build.Version}-{build.ReleaseType}")
        : "0.0.0";

    // no build info only in tests and the gallery: a fixed word keeps their renders the same every day
    public static string BuildDate => AppInfo.BuildInfo?.BuildDate ?? "dev";

    public static string OsName => OperatingSystem.IsWindows() ? "Windows" : OperatingSystem.IsMacOS() ? "macOS" : "Linux";

    public static string Architecture => AppInfo.BuildInfo?.Architecture ?? RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();

    public string VersionLine => AppText.VersionLine(AppVersion, BuildDate, OsName, Architecture);

    /// <summary>
    /// What Copy version info puts on the clipboard, for a bug report: the app, the OS, the runtime and the ExifTool in use.
    /// </summary>
    public string VersionInfo
    {
        get
        {
            var exifTool = _main.Metadata?.Version is { } version ? $" · ExifTool {version}" : string.Empty;
            return $"{AppInfo.PRODUCT_NAME} {AppVersion} ({BuildDate}) · {RuntimeInformation.OSDescription} · {Architecture} · .NET {Environment.Version}{exifTool}";
        }
    }

    public string Copyright => AppInfo.BuildInfo?.Copyright ?? AppText.COPYRIGHT;

    /// <summary>
    /// The Updates row: only where this build checks (Appendix B item 13).
    /// </summary>
    public bool ShowsUpdates => _updates?.IsAvailable == true;

    public UpdateState UpdateState => _updates?.State ?? UpdateState.Idle;

    public bool IsChecking => UpdateState == UpdateState.Checking;

    public bool IsUpdateAvailable => UpdateState == UpdateState.Available;

    public bool IsUpdateError => UpdateState == UpdateState.Failed;

    public bool CanCheck => UpdateState is not (UpdateState.Checking or UpdateState.Available);

    public string UpdateText => UpdateState switch
    {
        UpdateState.Checking => AppText.CHECKING_UPDATES,
        UpdateState.UpToDate => AppText.UpToDate(AppVersion, (_updates?.CheckedAt ?? DateTimeOffset.Now).ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)),
        UpdateState.Available => AppText.UpdateReady(_updates?.Latest?.Version ?? string.Empty, Released(_updates?.Latest)),
        UpdateState.Failed => AppText.UPDATE_ERROR,
        _ => AppText.NOT_CHECKED,
    };

    /// <summary>
    /// D11: the Microsoft Store row in the Windows direct builds, GitHub's releases in the direct macOS and AppImage builds, none in the stores and Flatpak.
    /// </summary>
    public bool ShowsStore => _main.Channel.StoreBadge != StoreBadgeKind.Hidden;

    public bool IsMicrosoftStore => _main.Channel.StoreBadge == StoreBadgeKind.MicrosoftStore;

    public bool IsReleases => _main.Channel.StoreBadge == StoreBadgeKind.GitHubReleases;

    public string StoreTitle => IsMicrosoftStore ? AppText.STORE_TITLE : AppText.RELEASES_TITLE;

    public string StoreText => IsMicrosoftStore ? AppText.STORE_SUB : AppText.RELEASES_SUB;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShownNotices), nameof(NoticesToggleText))]
    public partial bool AllNoticesShown { get; set; }

    public static IReadOnlyList<NoticeView> Notices => _notices.Value;

    public IReadOnlyList<NoticeView> ShownNotices => AllNoticesShown ? Notices : [.. Notices.Take(NOTICES_SHOWN)];

    public bool ShowsNoticesToggle => Notices.Count > NOTICES_SHOWN;

    public string NoticesToggleText => AllNoticesShown ? AppText.SHOW_FEWER : AppText.SHOW_ALL;

    /// <summary>
    /// The notice whose full text shows in place of the main page, with Back (FewerPhotos).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNoticeOpen), nameof(IsMainPage), nameof(NoticeParagraphs))]
    public partial NoticeView? OpenNotice { get; private set; }

    public bool IsNoticeOpen => OpenNotice is not null;

    public bool IsMainPage => OpenNotice is null;

    /// <summary>
    /// The open notice's paragraphs, read when it opens; the list virtualizes them (.NET's runs to 900 lines).
    /// </summary>
    public IReadOnlyList<string> NoticeParagraphs => OpenNotice is { } notice ? Read(notice.File + ".txt").Split("\n\n", StringSplitOptions.RemoveEmptyEntries) : [];

    #endregion // Properties


    #region Events

    /// <summary>
    /// Occurs when About says something in its own toast: "Version info copied".
    /// </summary>
    public event EventHandler<string>? ToastRequested;

    #endregion // Events


    #region Public methods

    public void Close()
    {
        if (_updates is not null) _updates.PropertyChanged -= Updates_PropertyChanged;
    }

    #endregion // Public methods


    #region Commands

    [RelayCommand]
    private async Task CheckAsync()
    {
        if (_updates is null || !CanCheck) return;
        await _updates.CheckNowAsync();
    }


    [RelayCommand]
    private Task DownloadAsync() => Open(_updates?.DownloadUrl ?? UpdateService.RELEASES_URL);


    [RelayCommand]
    private Task OpenHomepageAsync() => Open(AppInfo.HOMEPAGE_URL);


    [RelayCommand]
    private Task OpenStoreAsync() => Open(IsMicrosoftStore ? AppText.STORE_URL : UpdateService.RELEASES_URL);


    [RelayCommand]
    private async Task CopyInfoAsync()
    {
        if (_main.Shell is not { } shell) return;

        await shell.CopyTextAsync(VersionInfo);
        ToastRequested?.Invoke(this, AppText.TOAST_VERSION_COPIED);
    }


    [RelayCommand]
    private void ToggleNotices() => AllNoticesShown = !AllNoticesShown;


    [RelayCommand]
    private void ShowNotice(NoticeView? notice) => OpenNotice = notice;


    [RelayCommand]
    private void CloseNotice() => OpenNotice = null;

    #endregion // Commands


    #region Private methods

    private Task Open(string url) => _main.Shell?.OpenUriAsync(url) ?? Task.CompletedTask;


    private static IReadOnlyList<NoticeView> ReadNotices() => [.. Read("index.tsv").Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(line => line.Split('\t'))
        .Where(parts => parts.Length == 3)
        .Select(parts => new NoticeView(parts[0], parts[1], parts[2]))];


    private static string Read(string file)
    {
        using var reader = new StreamReader(AssetLoader.Open(new Uri(NOTICES + file)));
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }


    private static string Released(UpdateInfo? info) =>
        info is not null && DateTimeOffset.TryParse(info.PublishedDate, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date.ToString("d MMM yyyy", CultureInfo.InvariantCulture)
            : string.Empty;

    #endregion // Private methods


    #region Event handlers

    private void Updates_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(UpdateState));
        OnPropertyChanged(nameof(IsChecking));
        OnPropertyChanged(nameof(IsUpdateAvailable));
        OnPropertyChanged(nameof(IsUpdateError));
        OnPropertyChanged(nameof(CanCheck));
        OnPropertyChanged(nameof(UpdateText));
    }

    #endregion // Event handlers
}
