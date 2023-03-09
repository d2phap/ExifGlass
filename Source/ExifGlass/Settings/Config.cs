/*
ExifGlass - Standalone Exif tool for ImageGlass
Copyright (C) 2023 DUONG DIEU PHAP
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
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ExifGlass;

public class Config
{
    private static string ConfigFileName => "exifglass.config.json";
    private static string ConfigDir => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static string ConfigFilePath => Path.Combine(ConfigDir, ConfigFileName);


    // User settings
    #region User settings

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

    #endregion


    // Public methods
    #region Public methods

    /// <summary>
    /// Loads user configs from file.
    /// </summary>
    public static void Load()
    {
        if (LoadUserConfigs() is not IConfiguration items) return;

        WindowPositionX = items.GetValue(nameof(WindowPositionX), WindowPositionX);
        WindowPositionY = items.GetValue(nameof(WindowPositionY), WindowPositionY);
        WindowWidth = items.GetValue(nameof(WindowWidth), WindowWidth);
        WindowHeight = items.GetValue(nameof(WindowHeight), WindowHeight);
        WindowState = items.GetValue(nameof(WindowState), WindowState);
        EnableWindowTopMost = items.GetValue(nameof(EnableWindowTopMost), EnableWindowTopMost);
        ThemeMode = items.GetValue(nameof(ThemeMode), ThemeMode);
    }


    /// <summary>
    /// Write user configs to file.
    /// </summary>
    public static async Task SaveAsync()
    {
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
        _ = settings.TryAdd(nameof(EnableWindowTopMost), EnableWindowTopMost);
        _ = settings.TryAdd(nameof(ThemeMode), ThemeMode);


        await JsonEx.WriteJsonAsync(ConfigFilePath, settings);
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
        // filter the command lines begin with '-'
        // example: ExifGlass.exe -WindowWidth=900
        var args = Environment.GetCommandLineArgs()
            .Where(cmd => cmd.StartsWith("-"))
            .Select(cmd => cmd[1..]) // trim '-' from the command
            .ToArray();

        try
        {
            var userConfig = new ConfigurationBuilder()
                .SetBasePath(ConfigDir)
                .AddJsonFile(ConfigFileName, optional: true)
                .AddCommandLine(args)
                .Build();

            return userConfig;
        }
        catch { }


        return null;
    }

    #endregion // Private methods

}


public enum ThemeMode
{
    Default,
    Dark,
    Light,
}
