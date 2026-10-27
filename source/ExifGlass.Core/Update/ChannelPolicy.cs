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
namespace ExifGlass.Core.Update;

/// <summary>
/// The distribution channel the running build came from (§3).
/// </summary>
public enum AppChannel
{
    /// <summary>
    /// A Linux build from source or a tarball.
    /// </summary>
    Development,
    WindowsSideload,
    WindowsZip,
    WindowsStore,
    MacDirect,
    MacAppStore,
    LinuxAppImage,
    LinuxFlatpak,
}


/// <summary>
/// What the About window's store badge shows (D11).
/// </summary>
public enum StoreBadgeKind
{
    Hidden,
    MicrosoftStore,
    GitHubReleases,
}


/// <summary>
/// What a channel allows: update check, store badge, ImageGlass IPC, where the backup goes (§4.5).
/// </summary>
public sealed record ChannelPolicy(AppChannel Channel)
{
    #region Properties

    /// <summary>
    /// Stores update their own apps; only direct downloads check the feed.
    /// </summary>
    public bool UpdateCheck => Channel is AppChannel.WindowsSideload or AppChannel.WindowsZip or AppChannel.MacDirect or AppChannel.LinuxAppImage;

    public StoreBadgeKind StoreBadge => Channel switch
    {
        AppChannel.WindowsSideload or AppChannel.WindowsZip => StoreBadgeKind.MicrosoftStore,
        AppChannel.MacDirect or AppChannel.LinuxAppImage => StoreBadgeKind.GitHubReleases,
        _ => StoreBadgeKind.Hidden,
    };

    /// <summary>
    /// ImageGlass 10 IPC: everywhere but the sandboxed Mac App Store build.
    /// </summary>
    public bool ImageGlass10 => Channel != AppChannel.MacAppStore;

    public bool ImageGlass9 => OperatingSystem.IsWindows();

    /// <summary>
    /// D7: the Mac App Store build edits a copy in its container and keeps the backup there.
    /// </summary>
    public bool BackupInContainer => Channel == AppChannel.MacAppStore;

    /// <summary>
    /// The Settings row for a custom ExifTool path (not in the sandboxed build).
    /// </summary>
    public bool CustomExifTool => Channel != AppChannel.MacAppStore;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Works out the channel: the build flavor for macOS, the package identity on Windows (none: the ZIP), the environment on Linux.
    /// </summary>
    public static ChannelPolicy Detect(string platformId, string buildChannel, string? packageFamilyName, Func<string, string?>? environment = null, string? processPath = null)
    {
        environment ??= Environment.GetEnvironmentVariable;
        processPath ??= Environment.ProcessPath;

        var channel = platformId switch
        {
            "win" when packageFamilyName is null => AppChannel.WindowsZip,
            "win" when packageFamilyName.StartsWith("9662DuongDieuPhap", StringComparison.OrdinalIgnoreCase) => AppChannel.WindowsStore,
            "win" => AppChannel.WindowsSideload,
            "mac" when buildChannel == "MacAppStore" => AppChannel.MacAppStore,
            "mac" => AppChannel.MacDirect,
            "linux" when !string.IsNullOrEmpty(environment("FLATPAK_ID")) => AppChannel.LinuxFlatpak,
            "linux" when RunsFromAppImage(environment, processPath) => AppChannel.LinuxAppImage,
            _ => AppChannel.Development,
        };

        return new ChannelPolicy(channel);
    }

    #endregion // Public methods


    #region Private methods

    /// <summary>
    /// An AppImage's runtime exports APPIMAGE and APPDIR to every process it starts, so ExifGlass started by
    /// another AppImage (ImageGlass's tools) inherits them: only a process inside APPDIR is ExifGlass's AppImage.
    /// </summary>
    private static bool RunsFromAppImage(Func<string, string?> environment, string? processPath)
    {
        var appDir = environment("APPDIR");
        if (string.IsNullOrEmpty(environment("APPIMAGE")) || string.IsNullOrEmpty(appDir) || string.IsNullOrEmpty(processPath)) return false;

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(appDir)) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(processPath).StartsWith(root, StringComparison.Ordinal);
    }

    #endregion // Private methods
}
