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
using ExifGlass.Core.ExifTool;
using ExifGlass.Core.Metadata;
using System.Buffers;
using System.Text;

namespace ExifGlass.Benchmarks;

/// <summary>
/// Inputs the benchmarks share: synthetic table reads of a given size, the source tree and the bundled ExifTool.
/// </summary>
internal static class BenchData
{
    #region Properties

    public static string SourceDir { get; } = FindSourceDir();

    public static ExifToolLocation ExifTool => new(
        Path.Combine(SourceDir, "__assets", "exiftool", OperatingSystem.IsWindows() ? Path.Combine("win", "exiftool.exe") : Path.Combine("unix", "exiftool")),
        ExifToolSource.Bundled,
        Path.Combine(SourceDir, "__assets", "exiftool", ExifToolLocator.CONFIG_FILE_NAME));

    #endregion // Properties


    #region Public methods

    public static string Fixture(string name) => Path.Combine(SourceDir, "tests", "fixtures", "ai", name);


    /// <summary>
    /// A -j -l -G0:1:4 -H read with this many tags in 12 groups, shaped like a RAW's.
    /// </summary>
    public static byte[] TableJson(int tags)
    {
        var sb = new StringBuilder("[{\"SourceFile\": \"C:/bench.cr2\"");
        for (var i = 0; i < tags; i++)
        {
            var group = i % 12;
            sb.Append($",\n\"Group{group}:Sub{group}:Tag{i}\": {{\"id\": \"0x{i:x4}\", \"desc\": \"Tag number {i % 400}\", ");
            if (i % 3 == 0) sb.Append($"\"num\": {i * 0.5}, ");
            sb.Append($"\"val\": \"{i} units of value {i % 17} and some longer text to wrap\"}}");
        }

        return Encoding.UTF8.GetBytes(sb.Append("}]").ToString());
    }


    public static MetadataDocument Document(int tags) =>
        MetadataDocument.Create("C:/bench.cr2", TableReader.Read(new ReadOnlySequence<byte>(TableJson(tags)), new StringPool()));

    #endregion // Public methods


    #region Private methods

    private static string FindSourceDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("source/global.json not found");
    }

    #endregion // Private methods
}
