/*
ExifGlass - EXIF metadata viewer
Copyright (C) 2023-2024 DUONG DIEU PHAP
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
using Avalonia.Controls;
using Avalonia.Styling;
using ImageGlass.Tools;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace ExifGlass;

public class Config
{
    public const string DATETIME_FORMAT = "yyyy/MM/dd HH:mm:ss";
    public const string DATE_FORMAT = "yyyy/MM/dd";
    public const string MS_APPSTORE_ID = "9MX8S9HZ57W8";


    private static string ConfigFileName => "exifglass.config.json";

    public static string ConfigDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
    private static string ConfigFilePath => Path.Combine(ConfigDir, ConfigFileName);


    /// <summary>
    /// Gets app name.
    /// </summary>
    public static string AppName => Process.GetCurrentProcess().MainModule?.FileVersionInfo.ProductName ?? "[ExifGlass]";

    /// <summary>
    /// Gets app version.
    /// </summary>
    public static Version AppVersion => new(Process.GetCurrentProcess().MainModule?.FileVersionInfo.FileVersion ?? "1.0.0.0");


    // User settings
    #region User settings

    /// <summary>
    /// Gets, sets the last time to check for update. Set it to "0" to disable auto-update.
    /// </summary>
    public static string AutoUpdate { get; set; } = "2023/04/01 01:01:01";

    /// <summary>
    /// Gets, sets 'Left' position of main window
    /// </summary>
    public static int WindowPositionX { get; set; } = 200;

    /// <summary>
    /// Gets, sets 'Top' position of main window
    /// </summary>
    public static int WindowPositionY { get; set; } = 200;

    /// <summary>
    /// Gets, sets width of main window
    /// </summary>
    public static int WindowWidth { get; set; } = 600;

    /// <summary>
    /// Gets, sets height of main window
    /// </summary>
    public static int WindowHeight { get; set; } = 800;

    /// <summary>
    /// Gets, sets the state of main window.
    /// </summary>
    public static WindowState WindowState { get; set; } = WindowState.Normal;

    /// <summary>
    /// Gets, sets value indicating that main window is always on top or not.
    /// </summary>
    public static bool EnableWindowTopMost { get; set; } = false;

    /// <summary>
    /// Gets, sets value of theme color mode.
    /// </summary>
    public static ThemeMode ThemeMode { get; set; } = ThemeMode.Default;

    /// <summary>
    /// Gets, sets the executable path of ExifTool.
    /// </summary>
    public static string ExifToolExecutable { get; set; } = string.Empty;

    /// <summary>
    /// Gets, sets the command-line arguments of ExifTool.
    /// </summary>
    public static string ExifToolArguments { get; set; } = string.Empty;

    #endregion


    // Public methods
    #region Public methods

    /// <summary>
    /// Loads user configs from file.
    /// </summary>
    public static void Load()
    {
        Directory.CreateDirectory(ConfigDir);
        if (LoadUserConfigs() is not IConfiguration items) return;

        WindowPositionX = items.GetValue(nameof(WindowPositionX), WindowPositionX);
        WindowPositionY = items.GetValue(nameof(WindowPositionY), WindowPositionY);
        WindowWidth = items.GetValue(nameof(WindowWidth), WindowWidth);
        WindowHeight = items.GetValue(nameof(WindowHeight), WindowHeight);
        WindowState = items.GetValue(nameof(WindowState), WindowState);

        if (WindowPositionX < 0) WindowPositionX = 0;
        if (WindowPositionY < 0) WindowPositionY = 0;
        if (WindowWidth < 10) WindowWidth = 600;
        if (WindowHeight < 10) WindowHeight = 800;
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;


        AutoUpdate = items.GetValue(nameof(AutoUpdate), AutoUpdate);
        EnableWindowTopMost = items.GetValue(nameof(EnableWindowTopMost), EnableWindowTopMost);
        ThemeMode = items.GetValue(nameof(ThemeMode), ThemeMode);
        ExifToolExecutable = items.GetValue(nameof(ExifToolExecutable), ExifToolExecutable);
        ExifToolArguments = items.GetValue(nameof(ExifToolArguments), ExifToolArguments);
    }


    /// <summary>
    /// Write user configs to file.
    /// </summary>
    public static async Task SaveAsync()
    {
        Directory.CreateDirectory(ConfigDir);

        // metadata
        var metadata = new ExpandoObject();
        _ = metadata.TryAdd("Description", "ExifGlass configuration file");
        _ = metadata.TryAdd("Version", "1.0");

        var settings = new ExpandoObject();
        _ = settings.TryAdd("_Metadata", metadata);


        // user configs
        _ = settings.TryAdd(nameof(WindowPositionX), WindowPositionX);
        _ = settings.TryAdd(nameof(WindowPositionY), WindowPositionY);
        _ = settings.TryAdd(nameof(WindowWidth), WindowWidth);
        _ = settings.TryAdd(nameof(WindowHeight), WindowHeight);
        _ = settings.TryAdd(nameof(WindowState), WindowState);

        _ = settings.TryAdd(nameof(AutoUpdate), AutoUpdate);
        _ = settings.TryAdd(nameof(EnableWindowTopMost), EnableWindowTopMost);
        _ = settings.TryAdd(nameof(ThemeMode), ThemeMode);
        _ = settings.TryAdd(nameof(ExifToolExecutable), ExifToolExecutable);
        _ = settings.TryAdd(nameof(ExifToolArguments), ExifToolArguments);


        await JsonEx.WriteJsonAsync(ConfigFilePath, settings);
    }


    /// <summary>
    /// Open URL in the default browser.
    /// </summary>
    public static void OpenUrl(string? url, string campaign = "app_unknown")
    {
        if (string.IsNullOrEmpty(url)) return;

        try
        {
            var ub = new UriBuilder(url);
            var queries = HttpUtility.ParseQueryString(ub.Query);
            queries["utm_source"] = "exifglass_" + AppVersion;
            queries["utm_medium"] = "app_click";
            queries["utm_campaign"] = campaign;

            ub.Query = queries.ToString();

            Process.Start(new ProcessStartInfo(ub.Uri.AbsoluteUri)
            {
                UseShellExecute = true,
            });
        }
        catch { }
    }


    /// <summary>
    /// Opens ExifGlass site om Microsoft Store.
    /// </summary>
    public static void OpenExifGlassMsStore()
    {
        var campaignId = $"InAppBadgeV{AppVersion}";
        var source = "AboutWindow";

        try
        {
            var url = $"ms-windows-store://pdp/?productid={MS_APPSTORE_ID}&cid={campaignId}&referrer=appbadge&source={source}";

            OpenUrl(url);
        }
        catch
        {
            try
            {
                var url = $"https://www.microsoft.com/store/productId/{MS_APPSTORE_ID}?cid={campaignId}&referrer=appbadge&source={source}";

                OpenUrl(url);
            }
            catch { }
        }
    }


    /// <summary>
    /// Applies theme mode to the app.
    /// </summary>
    public static void ApplyThemeMode(ThemeMode mode)
    {
        if (Application.Current == null) return;

        var themeVariant = ThemeVariant.Default;
        if (mode == ThemeMode.Dark)
        {
            themeVariant = ThemeVariant.Dark;
        }
        else if (mode == ThemeMode.Light)
        {
            themeVariant = ThemeVariant.Light;
        }

        Application.Current.RequestedThemeVariant = themeVariant;
    }

    #endregion // Public methods


    // Private methods
    #region Private methods

    /// <summary>
    /// Loads all config files: default, user, command-lines, admin;
    /// then unify configs.
    /// </summary>
    private static IConfigurationRoot? LoadUserConfigs()
    {
        // filter the command lines begin with '/'
        // example: ExifGlass.exe /WindowWidth=900
        var args = Environment.GetCommandLineArgs()
            .Where(cmd => cmd.StartsWith("/") && !cmd.StartsWith(ImageGlassTool.PIPE_CODE_CMD_LINE))
            .Select(cmd => cmd[1..]) // trim '/' from the command
            .ToArray();

        try
        {
            // build with command lines by default
            return new ConfigurationBuilder()
                .SetBasePath(ConfigDir)
                .AddJsonFile(ConfigFileName, optional: true)
                .AddCommandLine(args)
                .Build();
        }
        catch (FormatException)
        {
            // build without command lines
            return new ConfigurationBuilder()
                .SetBasePath(ConfigDir)
                .AddJsonFile(ConfigFileName, optional: true)
                .Build();
        }
        catch { }


        return null;
    }

    #endregion // Private methods

}


public enum ThemeMode : int
{
    Default = 0,
    Dark = 1,
    Light = 2,
}
