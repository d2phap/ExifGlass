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
namespace ExifGlass.Core.Common;

/// <summary>
/// Build facts each app head bakes in at compile time (AppBuildInfo.g.cs).
/// </summary>
public interface IAppBuildInfo
{
    #region Properties

    /// <summary>
    /// Numeric version, such as 3.0.0.1020.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Version with release type, platform and architecture, such as 3.0.0.1020-alpha-win-x64.
    /// </summary>
    string FullVersion { get; }

    /// <summary>
    /// Empty for a stable release, otherwise alpha, beta or similar.
    /// </summary>
    string ReleaseType { get; }

    /// <summary>
    /// Direct or MacAppStore.
    /// </summary>
    string Channel { get; }

    /// <summary>
    /// win, mac or linux.
    /// </summary>
    string PlatformId { get; }

    /// <summary>
    /// x64 or arm64.
    /// </summary>
    string Architecture { get; }

    string BuildDate { get; }

    string Copyright { get; }

    #endregion // Properties
}
