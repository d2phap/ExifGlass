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
using ExifGlass.Core.Common;
using ExifGlass.Core.Settings;
using ExifGlass.Services;
using ExifGlass.ViewModels;
using ExifGlass.Windows;

namespace ExifGlass;

/// <summary>
/// The composition root (plan 4.1): the command line, the settings, the one ExifTool and the main window's view model.
/// </summary>
public sealed class AppServices
{
    #region Fields and constants

    /// <summary>
    /// Points the settings at another folder, so test and gate runs leave the user's own alone.
    /// </summary>
    public const string SETTINGS_DIR_VARIABLE = "EG_SETTINGS_DIR";

    #endregion // Fields and constants


    #region Constructors

    private AppServices(StartupOptions options, SettingsStore settings, MetadataService metadata)
    {
        Options = options;
        Settings = settings;
        Metadata = metadata;
        Placements = new SettingsPlacementStore(settings);
        ViewModel = new MainViewModel(settings, metadata, metadata);
    }

    #endregion // Constructors


    #region Properties

    public StartupOptions Options { get; }

    public SettingsStore Settings { get; }

    public MetadataService Metadata { get; }

    public SettingsPlacementStore Placements { get; }

    public MainViewModel ViewModel { get; }

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Reads the command line and the settings, and starts ExifTool in the background.
    /// </summary>
    public static AppServices Create(IReadOnlyList<string> args, string? settingsDirectory = null)
    {
        var options = CommandLine.Parse(args);

        // --theme pins this run's theme like -p:Theme=, so it is never saved
        var overrides = new Dictionary<string, string>(options.Overrides, StringComparer.OrdinalIgnoreCase);
        if (options.Theme is { } theme) overrides[nameof(AppSettings.Theme)] = theme;

        settingsDirectory ??= Environment.GetEnvironmentVariable(SETTINGS_DIR_VARIABLE) is { Length: > 0 } dir ? dir : SettingsStore.DefaultDirectory();
        var settings = new SettingsStore(settingsDirectory);
        settings.Load(overrides);

        var metadata = new MetadataService(() => settings.Current);
        metadata.Start();

        return new AppServices(options, settings, metadata);
    }


    public MainWindow CreateMainWindow() => new(ViewModel, Placements);


    /// <summary>
    /// At exit: writes pending settings and stops ExifTool (-stay_open False, killed after 500 ms; R11).
    /// </summary>
    public void Shutdown()
    {
        Settings.FlushAsync().GetAwaiter().GetResult();
        Metadata.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    #endregion // Public methods
}
