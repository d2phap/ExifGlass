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
namespace ExifGlass.Core.ExifTool;

/// <summary>
/// The argument lists of every command the app sends (§4.4, S5, S9).
/// </summary>
public static class ExifToolCommands
{
    #region Public methods

    /// <summary>
    /// The table read: -j -l -G0:1:4 -H, then the allowlisted user flags (D1).
    /// </summary>
    public static IReadOnlyList<string> TableRead(string path, IReadOnlyList<string> passThrough) =>
        ["-j", "-l", "-G0:1:4", "-H", .. passThrough, FullPath(path)];


    /// <summary>
    /// The AI read: fixed, never with a user flag (R18); family 3 separates C2PA manifests, family 4 duplicates.
    /// </summary>
    public static IReadOnlyList<string> AiRead(string path) =>
        ["-j", "-a", "-u", "-G0:1:3:4", "-struct", FullPath(path)];


    /// <summary>
    /// The embedded previews as base64 inside JSON, largest first.
    /// </summary>
    public static IReadOnlyList<string> PreviewRead(string path) =>
        ["-j", "-b", "-JpgFromRaw", "-PreviewImage", "-ThumbnailImage", FullPath(path)];


    /// <summary>
    /// One binary tag as base64 inside JSON (D12, Save binary data).
    /// </summary>
    public static IReadOnlyList<string> BinaryRead(string path, string writeName) =>
        ["-j", "-b", "-" + writeName, FullPath(path)];


    /// <summary>
    /// The tag table for TagDb: names, types, groups and flags (S5).
    /// </summary>
    public static IReadOnlyList<string> ListTags() => ["-listx", "-s", "-f"];


    /// <summary>
    /// A write of staged arguments; ExifTool keeps FILE_original unless the arguments say otherwise.
    /// </summary>
    public static IReadOnlyList<string> Write(string path, IReadOnlyList<string> writeArgs) =>
        ["-charset", "UTF8", .. writeArgs, FullPath(path)];

    #endregion // Public methods


    #region Private methods

    // never "--": it ends option parsing for the rest of the command, so -execute would read as a file (exiftool line 629)
    private static string FullPath(string path) => Path.GetFullPath(path);

    #endregion // Private methods
}
