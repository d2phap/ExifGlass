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
using ExifGlass.Core.Common;
using ExifGlass.Core.Settings;
using ExifGlass.Core.Update;

namespace ExifGlass.Services;

/// <summary>
/// Where the update check stands, for About.
/// </summary>
public enum UpdateState
{
    Idle,
    Checking,
    UpToDate,
    Available,
    Failed,
}


/// <summary>
/// The notify-only update check (D10): once at startup in direct channels, throttled and silent on errors, and from About at any time.
/// </summary>
public sealed partial class UpdateService : ObservableObject
{
    #region Fields and constants

    public const string RELEASES_URL = AppInfo.HOMEPAGE_URL + "/releases";

    /// <summary>
    /// How long after launch the startup check waits, so it never competes with ExifTool's warm-up or the first file.
    /// </summary>
    public static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(3);

    private readonly SettingsStore _settings;
    private readonly UpdateChecker _checker;
    private readonly Func<DateTimeOffset> _now;
    private Task<UpdateCheckResult>? _running;

    #endregion // Fields and constants


    #region Constructors

    /// <summary>
    /// Tests pass their own feed and clock; the app reads the real feed (UpdateFeed).
    /// </summary>
    public UpdateService(SettingsStore settings, ChannelPolicy channel, string currentVersion, Func<CancellationToken, Task<string>>? fetchFeed = null, Func<DateTimeOffset>? now = null)
    {
        _settings = settings;
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _checker = new UpdateChecker(fetchFeed ?? (ct => UpdateFeed.FetchAsync(currentVersion, ct)));
        Channel = channel;
        CurrentVersion = currentVersion;
    }

    #endregion // Constructors


    #region Properties

    public ChannelPolicy Channel { get; }

    public string CurrentVersion { get; }

    /// <summary>
    /// Gets whether this build checks for updates at all: stores update their own apps (Appendix B item 13).
    /// </summary>
    public bool IsAvailable => Channel.UpdateCheck;

    [ObservableProperty]
    public partial UpdateState State { get; private set; }

    /// <summary>
    /// The release the feed offers when it is newer than this build.
    /// </summary>
    [ObservableProperty]
    public partial UpdateInfo? Latest { get; private set; }

    [ObservableProperty]
    public partial string? Error { get; private set; }

    /// <summary>
    /// When the feed last answered this session, for About's "checked at".
    /// </summary>
    [ObservableProperty]
    public partial DateTimeOffset? CheckedAt { get; private set; }

    /// <summary>
    /// Where Download goes: the feed's link when it is https, the releases page otherwise.
    /// </summary>
    public string DownloadUrl => Latest?.DownloadUrl is { } url && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? url : RELEASES_URL;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// The startup check: only when switched on and due (5 days); errors stay quiet. Call on the UI thread.
    /// </summary>
    public Task<UpdateCheckResult> CheckOnStartupAsync(CancellationToken cancellationToken = default) => RunAsync(force: false, cancellationToken);


    /// <summary>
    /// About's Check for update: any time; a check already running is the same check. Call on the UI thread.
    /// </summary>
    public Task<UpdateCheckResult> CheckNowAsync(CancellationToken cancellationToken = default) => RunAsync(force: true, cancellationToken);

    #endregion // Public methods


    #region Private methods

    private Task<UpdateCheckResult> RunAsync(bool force, CancellationToken cancellationToken)
    {
        if (!IsAvailable) return Task.FromResult(new UpdateCheckResult(UpdateStatus.Skipped));
        if (_running is { IsCompleted: false } running) return running;

        return _running = CheckCoreAsync(force, cancellationToken);
    }


    private async Task<UpdateCheckResult> CheckCoreAsync(bool force, CancellationToken cancellationToken)
    {
        var settings = _settings.Current;
        if (!force && (!settings.CheckForUpdates || !UpdateChecker.ShouldCheck(settings.LastUpdateCheck, _now(), UpdateChecker.THROTTLE)))
        {
            return new UpdateCheckResult(UpdateStatus.Skipped);
        }

        var before = State;
        State = UpdateState.Checking;

        UpdateCheckResult result;
        try
        {
            result = await _checker.CheckAsync(CurrentVersion, force: true, enabled: true, settings.LastUpdateCheck, _now(), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            State = before;
            throw;
        }

        // a check the server answered starts the 5 days again; a failed one is tried at the next launch
        if (result.Status is UpdateStatus.UpToDate or UpdateStatus.Available)
        {
            var now = _now();
            var stamp = UpdateChecker.Stamp(now);
            _settings.Update(s => s.LastUpdateCheck = stamp);
            CheckedAt = now;
        }

        switch (result.Status)
        {
            case UpdateStatus.Available:
                Latest = result.Info;
                Error = null;
                State = UpdateState.Available;
                break;

            case UpdateStatus.UpToDate:
                Latest = null;
                Error = null;
                State = UpdateState.UpToDate;
                break;

            default:
                // the startup check is silent: About keeps what it showed before
                if (force)
                {
                    Error = result.Error;
                    State = UpdateState.Failed;
                }
                else State = before;
                break;
        }

        return result;
    }


    partial void OnLatestChanged(UpdateInfo? value) => OnPropertyChanged(nameof(DownloadUrl));

    #endregion // Private methods
}
