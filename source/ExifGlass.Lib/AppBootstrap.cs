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
using Avalonia.Styling;
using ExifGlass.Core.Diagnostics;
using ExifGlass.Diagnostics;

namespace ExifGlass;

/// <summary>
/// Startup work shared by the app heads, before and around Avalonia.
/// </summary>
public static class AppBootstrap
{
    #region Constants

    public const string SELFTEST_ARG = "--selftest";
    public const string THEME_ARG = "--theme";

    #endregion // Constants


    #region Public methods

    public static bool IsSelfTest(string[] args) => args.Contains(SELFTEST_ARG, StringComparer.OrdinalIgnoreCase);


    /// <summary>
    /// Runs every self-test check without starting the UI; returns the process exit code.
    /// </summary>
    public static int RunSelfTest()
    {
        var test = new SelfTest()
            .AddCoreChecks()
            .AddLibChecks();

        // Nothing else runs yet, so waiting here blocks no UI.
        return test.RunAsync(Console.Out).GetAwaiter().GetResult();
    }


    /// <summary>
    /// Reads --theme dark|light; null keeps the system theme.
    /// </summary>
    public static ThemeVariant? ReadTheme(string[]? args)
    {
        if (args is null) return null;

        var index = Array.FindIndex(args, a => a.Equals(THEME_ARG, StringComparison.OrdinalIgnoreCase));
        if (index < 0 || index + 1 >= args.Length) return null;

        return args[index + 1].ToLowerInvariant() switch
        {
            "dark" => ThemeVariant.Dark,
            "light" => ThemeVariant.Light,
            _ => null,
        };
    }

    #endregion // Public methods
}
