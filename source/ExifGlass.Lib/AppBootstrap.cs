/*
ExifGlass - AI & EXIF metadata viewer
Copyright (C) 2023 - 2027 DUONG DIEU PHAP
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
using Avalonia;
using Avalonia.Styling;
using ExifGlass.UI.Tokens;
using ExifGlass.Core.Common;
using ExifGlass.Core.Diagnostics;
using ExifGlass.Core.Integration;
using ExifGlass.Core.Platform;
using ExifGlass.Core.Settings;
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
    public const string GALLERY_ARG = "--gallery";

    // S1: Avalonia's 28 MB default evicts squircle masks in motion; 96 MB covers the 72 MB stress peak and stays bounded
    public const long SKIA_GPU_BUDGET = 96L * 1024 * 1024;

    #endregion // Constants


    #region Public methods

    public static bool IsSelfTest(string[] args) => args.Contains(SELFTEST_ARG, StringComparer.OrdinalIgnoreCase);


    public static bool IsGallery(string[]? args) => args?.Contains(GALLERY_ARG, StringComparer.OrdinalIgnoreCase) == true;


    public static bool IsSoftwareRendering(string[] args) => CommandLine.Parse(args).SoftwareRendering;


    /// <summary>
    /// Single instance: a plain launch hands its file to the window already open and returns true, so the head exits; a first one keeps the slot in <see cref="AppServices.Instance"/>.
    /// </summary>
    public static bool HandOffToRunningInstance(string[] args)
    {
        var options = CommandLine.Parse(args);
        if (!JoinsSingleInstance(options, args)) return false;

        var directory = AppServices.SettingsDirectory();
        var instance = new AppInstance(directory);
        if (!instance.TryClaim())
        {
            if (!SettingsStore.Peek(directory).EnableMultiInstances)
            {
                PlatformServices.Shell?.AllowForegroundActivation();
                if (instance.Send(HandOffArgs(options))) return true;
            }

            // no answer: the open window may have closed since, so its slot can be free
            if (!instance.TryClaim()) return false;
        }

        instance.Listen();
        AppServices.Instance = instance;
        return false;
    }


    /// <summary>
    /// Only a launch with nothing but a file joins: ImageGlass tools own their process (it must be the one they started), and test or styling flags describe a run of their own.
    /// </summary>
    public static bool JoinsSingleInstance(StartupOptions options, IReadOnlyList<string> args) => options is
    {
        Mode: AppMode.Standalone,
        SelfTest: false,
        Gallery: false,
        SoftwareRendering: false,
        Theme: null,
        Overrides.Count: 0,
    } && !PerfRun.IsPerf(args);


    /// <summary>
    /// What every head adds to its builder: the fonts and their size rule (plan 5.3) and the Skia GPU budget (plan 5.4).
    /// </summary>
    public static AppBuilder WithExifGlassDefaults(this AppBuilder builder) => builder
        .With(new SkiaOptions { MaxGpuResourceSizeBytes = SKIA_GPU_BUDGET })
        .WithAppFonts()
        .WithPlatformFontSizes();


    /// <summary>
    /// Runs every self-test check without starting the UI; returns the process exit code.
    /// </summary>
    public static int RunSelfTest(string[] args)
    {
        // a redirected console on Windows uses the ANSI code page: the report scripts read UTF-8
        Console.OutputEncoding = new System.Text.UTF8Encoding(false);

        var options = CommandLine.Parse(args);
        var test = new SelfTest()
            .AddCoreChecks(options.FixturesDirectory)
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


    #region Private methods

    // the open window has its own working folder, so a relative path is resolved here
    private static string[] HandOffArgs(StartupOptions options)
    {
        if (string.IsNullOrEmpty(options.File)) return [];

        try
        {
            return [Path.GetFullPath(options.File)];
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return [options.File];
        }
    }

    #endregion // Private methods
}
