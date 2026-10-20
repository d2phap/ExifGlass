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
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExifGlass.Core.Update;

/// <summary>
/// The release feed (update.json), the same schema v2 published.
/// </summary>
public sealed record UpdateInfo
{
    #region Properties

    public int ApiVersion { get; init; }

    public string Version { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string ChangelogUrl { get; init; } = string.Empty;

    public string PublishedDate { get; init; } = string.Empty;

    public string DownloadUrl { get; init; } = string.Empty;

    #endregion // Properties
}


public enum UpdateStatus
{
    /// <summary>
    /// Not checked: switched off, not due, or not a direct channel.
    /// </summary>
    Skipped,
    UpToDate,
    Available,
    Failed,
}


public sealed record UpdateCheckResult(UpdateStatus Status, UpdateInfo? Info = null, string? Error = null);


/// <summary>
/// The notify-only update check: throttled for the startup check, any time for "Check for update" (D10).
/// </summary>
public sealed class UpdateChecker(Func<CancellationToken, Task<string>> fetchFeed)
{
    #region Fields and constants

    public const string FEED_URL = "https://raw.githubusercontent.com/d2phap/ExifGlass/main/update.json";

    public static readonly TimeSpan THROTTLE = TimeSpan.FromDays(5);

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Checks the feed; a startup check (force false) runs only when due and allowed.
    /// </summary>
    public async Task<UpdateCheckResult> CheckAsync(string currentVersion, bool force, bool enabled, string? lastCheck, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        if (!force && (!enabled || !ShouldCheck(lastCheck, now, THROTTLE))) return new UpdateCheckResult(UpdateStatus.Skipped);

        UpdateInfo? info;
        try
        {
            var json = await fetchFeed(cancellationToken).ConfigureAwait(false);
            info = JsonSerializer.Deserialize(json, UpdateJsonContext.Default.UpdateInfo);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException or OperationCanceledException or System.Net.Http.HttpRequestException)
        {
            return new UpdateCheckResult(UpdateStatus.Failed, Error: ex is JsonException ? "The update information couldn't be read" : "Couldn't reach the update server");
        }

        if (info is null || string.IsNullOrWhiteSpace(info.Version)) return new UpdateCheckResult(UpdateStatus.Failed, Error: "The update information couldn't be read");

        return IsNewer(info.Version, currentVersion)
            ? new UpdateCheckResult(UpdateStatus.Available, info)
            : new UpdateCheckResult(UpdateStatus.UpToDate, info);
    }


    /// <summary>
    /// True when the last check is missing, unreadable (fail open) or at least the interval old.
    /// </summary>
    public static bool ShouldCheck(string? lastCheck, DateTimeOffset now, TimeSpan interval)
    {
        if (string.IsNullOrWhiteSpace(lastCheck)) return true;
        if (!DateTimeOffset.TryParse(lastCheck, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var last)) return true;

        return now - last >= interval;
    }


    /// <summary>
    /// The time stamp a check stores: UTC, round-trip format.
    /// </summary>
    public static string Stamp(DateTimeOffset now) => now.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);


    /// <summary>
    /// Compares dotted versions, ignoring "-beta" and "+build" suffixes and padding missing parts with zero.
    /// </summary>
    public static int CompareVersions(string? left, string? right)
    {
        var a = Parts(left);
        var b = Parts(right);
        for (var i = 0; i < Math.Max(a.Length, b.Length); i++)
        {
            var x = i < a.Length ? a[i] : 0;
            var y = i < b.Length ? b[i] : 0;
            if (x != y) return x.CompareTo(y);
        }

        return 0;
    }


    public static bool IsNewer(string? candidate, string? current) => CompareVersions(candidate, current) > 0;

    #endregion // Public methods


    #region Private methods

    private static long[] Parts(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return [];

        var span = version.AsSpan().Trim().TrimStart('v');
        var cut = span.IndexOfAny('-', '+');
        if (cut >= 0) span = span[..cut];

        var parts = span.ToString().Split('.', StringSplitOptions.RemoveEmptyEntries);
        var result = new long[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            var end = 0;
            while (end < p.Length && char.IsAsciiDigit(p[end])) end++;
            result[i] = end > 0 && long.TryParse(p.AsSpan(0, end), CultureInfo.InvariantCulture, out var n) ? n : 0;
        }

        return result;
    }

    #endregion // Private methods
}


[JsonSerializable(typeof(UpdateInfo))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
internal sealed partial class UpdateJsonContext : JsonSerializerContext;
