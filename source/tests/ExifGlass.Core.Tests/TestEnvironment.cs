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
using Xunit;

namespace ExifGlass.Core.Tests;

/// <summary>
/// Where the tests find the source tree, the bundled ExifTool and the fixtures.
/// </summary>
public static class TestEnvironment
{
    #region Properties

    /// <summary>
    /// The source folder, found by walking up from the test binaries to global.json.
    /// </summary>
    public static string SourceDir { get; } = FindSourceDir();

    public static string AiFixtures => Path.Combine(SourceDir, "tests", "fixtures", "ai");

    public static string AiGolden => Path.Combine(SourceDir, "tests", "fixtures", "ai-golden");

    public static string ConfigPath => Path.Combine(SourceDir, "__assets", "exiftool", ExifToolLocator.CONFIG_FILE_NAME);

    /// <summary>
    /// The bundled ExifTool of this OS, or null when the git-ignored builds aren't there.
    /// </summary>
    public static string? ExifToolPath
    {
        get
        {
            var path = Path.Combine(SourceDir, "__assets", "exiftool", OperatingSystem.IsWindows() ? Path.Combine("win", "exiftool.exe") : Path.Combine("unix", "exiftool"));
            return File.Exists(path) ? path : null;
        }
    }

    public static ExifToolLocation? Location => ExifToolPath is { } p ? new ExifToolLocation(p, ExifToolSource.Bundled, ConfigPath) : null;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Skips the calling test when ExifTool isn't in source/__assets/exiftool.
    /// </summary>
    public static ExifToolLocation RequireExifTool()
    {
        if (Location is { } location)
        {
            if (!OperatingSystem.IsWindows()) BundledExifTool.EnsureExecutable(Path.GetDirectoryName(location.Path));
            return location;
        }

        Assert.Skip("The bundled ExifTool isn't in source/__assets/exiftool (see its README)");
        return null!;
    }


    public static string Fixture(string name) => Path.Combine(AiFixtures, name);


    /// <summary>
    /// A fresh copy of a fixture in a temp folder, for write tests.
    /// </summary>
    public static string CopyToTemp(string fixture, string? name = null)
    {
        var dir = Directory.CreateTempSubdirectory("exifglass-tests-").FullName;
        var target = Path.Combine(dir, name ?? Path.GetFileName(fixture));
        File.Copy(fixture, target);
        return target;
    }

    #endregion // Public methods


    #region Private methods

    private static string FindSourceDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "global.json")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("source/global.json not found above the test binaries");
    }

    #endregion // Private methods
}
