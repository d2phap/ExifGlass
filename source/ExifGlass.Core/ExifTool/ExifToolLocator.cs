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
/// Where the ExifTool in use came from.
/// </summary>
public enum ExifToolSource
{
    Custom,
    Bundled,
    SystemPath,
}


/// <summary>
/// The ExifTool to run, its source, and ExifGlass's config next to the app.
/// </summary>
public sealed record ExifToolLocation(string Path, ExifToolSource Source, string? ConfigPath)
{
    #region Properties

    /// <summary>
    /// True when a custom path was set but doesn't exist, so another ExifTool is used.
    /// </summary>
    public bool CustomPathMissing { get; init; }

    /// <summary>
    /// The Perl that runs the script, when the app carries its own (the Mac App Store build); else the script runs by itself.
    /// </summary>
    public string? Interpreter { get; init; }

    #endregion // Properties
}


/// <summary>
/// Finds ExifTool: the Settings path, then the bundled copy, then PATH.
/// </summary>
public static class ExifToolLocator
{
    #region Fields and constants

    public const string CONFIG_FILE_NAME = "exifglass.config";

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Returns the ExifTool to use, or null when there is none at all.
    /// </summary>
    public static ExifToolLocation? Locate(string? customPath, string? baseDirectory = null, string? pathVariable = null)
    {
        baseDirectory ??= AppContext.BaseDirectory;
        var config = Path.Combine(baseDirectory, CONFIG_FILE_NAME);
        var configPath = File.Exists(config) ? config : null;

        var customMissing = false;
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            var trimmed = customPath.Trim().Trim('"');
            if (File.Exists(trimmed)) return new ExifToolLocation(Path.GetFullPath(trimmed), ExifToolSource.Custom, configPath);
            customMissing = true;
        }

        BundledExifTool.EnsureExecutable(baseDirectory);
        if (BundledExifTool.FindPath(baseDirectory) is { } bundled)
        {
            return new ExifToolLocation(bundled, ExifToolSource.Bundled, configPath) { CustomPathMissing = customMissing, Interpreter = BundledPerl(baseDirectory) };
        }

        if (FindOnPath(pathVariable ?? Environment.GetEnvironmentVariable("PATH")) is { } onPath)
        {
            return new ExifToolLocation(onPath, ExifToolSource.SystemPath, configPath) { CustomPathMissing = customMissing };
        }

        return null;
    }


    /// <summary>
    /// The Perl in Contents/Resources/perl of a Mac App Store bundle: App Review takes /usr/bin/perl for a deprecated technology (plan 3, S7).
    /// </summary>
    public static string? BundledPerl(string baseDirectory)
    {
        var perl = Path.GetFullPath(Path.Combine(baseDirectory, "..", "Resources", "perl", "bin", "perl"));
        return File.Exists(perl) ? perl : null;
    }


    /// <summary>
    /// Finds exiftool (exiftool.exe on Windows) in a PATH-style list of folders.
    /// </summary>
    public static string? FindOnPath(string? pathVariable)
    {
        if (string.IsNullOrEmpty(pathVariable)) return null;

        var name = OperatingSystem.IsWindows() ? "exiftool.exe" : "exiftool";
        foreach (var dir in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim('"'), name);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException)
            {
                // a malformed PATH entry
            }
        }

        return null;
    }

    #endregion // Public methods
}
