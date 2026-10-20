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
/// Product facts shared by every layer.
/// </summary>
public static class AppInfo
{
    #region Constants

    public const string PRODUCT_NAME = "ExifGlass";
    public const string HOMEPAGE_URL = "https://github.com/d2phap/ExifGlass";

    #endregion // Constants


    #region Properties

    /// <summary>
    /// Set once by the app head, before anything else runs.
    /// </summary>
    public static IAppBuildInfo? BuildInfo { get; set; }

    #endregion // Properties
}
