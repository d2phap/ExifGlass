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
using ExifGlass.Core.Editing;
using ExifGlass.Core.Platform;
using ExifGlass.Core.Settings;
using ExifGlass.Core.Update;
using ExifGlass.Diagnostics;
using ExifGlass.Integration;
using ExifGlass.Services;
using ExifGlass.ViewModels;
using ExifGlass.Windows;

namespace ExifGlass;

/// <summary>
/// The composition root (plan 4.1): the command line, the settings, the channel, the one ExifTool, the update check and the main window's view model.
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

    private AppServices(StartupOptions options, SettingsStore settings, MetadataService metadata, ChannelPolicy channel)
    {
        Options = options;
        Settings = settings;
        Metadata = metadata;
        Channel = channel;
        Placements = new SettingsPlacementStore(settings);
        Updates = new UpdateService(settings, channel, AppInfo.BuildInfo?.Version ?? "0.0.0");
        ViewModel = new MainViewModel(settings, metadata, metadata) { Channel = channel, Updates = Updates };
    }

    #endregion // Constructors


    #region Properties

    /// <summary>
    /// A head's own host for a mode only it knows (the Windows head: ImageGlass 9); null leaves the choice to the Lib.
    /// </summary>
    public static Func<StartupOptions, IImageSourceHost?>? HostFactory { get; set; }

    public StartupOptions Options { get; }

    public SettingsStore Settings { get; }

    public MetadataService Metadata { get; }

    public ChannelPolicy Channel { get; }

    public UpdateService Updates { get; }

    public SettingsPlacementStore Placements { get; }

    public MainViewModel ViewModel { get; }

    /// <summary>
    /// Where file requests come from, once the main window shows.
    /// </summary>
    public HostSession? Session { get; private set; }

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

        var channel = DetectChannel();
        var metadata = new MetadataService(() => settings.Current)
        {
            Staging = channel.BackupInContainer ? new SandboxStaging(SandboxStaging.DefaultStageDirectory(), Path.Combine(settings.DirectoryPath, SandboxStaging.BACKUP_FOLDER)) : null,
            CustomExifTool = channel.CustomExifTool,
        };
        metadata.Start();

        return new AppServices(options, settings, metadata, channel);
    }


    /// <summary>
    /// The channel this build came from (plan 4.5): the build flavor, the package identity, the environment.
    /// </summary>
    public static ChannelPolicy DetectChannel()
    {
        var build = AppInfo.BuildInfo;
        var platform = build?.PlatformId ?? (OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "mac" : "linux");
        return ChannelPolicy.Detect(platform, build?.Channel ?? "Direct", PlatformServices.Shell?.PackageFamilyName);
    }


    public MainWindow CreateMainWindow() => new(ViewModel, Placements);


    /// <summary>
    /// The session for this run's host (P10): the command line, ImageGlass 10, or the head's ImageGlass 9. Not started yet.
    /// </summary>
    public HostSession CreateSession(MainWindow window) => Session = new HostSession(CreateHost(), ViewModel, window, Metadata.DisposeAsync);


    /// <summary>
    /// The startup update check (D10), a moment after the window shows; says so with a toast when a newer release is out.
    /// </summary>
    public async Task CheckForUpdateOnStartupAsync()
    {
        if (!Updates.IsAvailable || !Settings.Current.CheckForUpdates) return;

        await Task.Delay(UpdateService.StartupDelay);
        var result = await Updates.CheckOnStartupAsync();
        if (result.Status == UpdateStatus.Available && result.Info is { } info) ViewModel.ShowUpdateToast(info.Version);
    }


    /// <summary>
    /// At exit: lets go of the host, writes pending settings and stops ExifTool (-stay_open False, killed after 500 ms; R11).
    /// </summary>
    public void Shutdown()
    {
        Session?.Dispose();
        Settings.FlushAsync().GetAwaiter().GetResult();
        Metadata.DisposeAsync().AsTask().GetAwaiter().GetResult();
        IpcLog.Write("EXIT");
    }

    #endregion // Public methods


    #region Private methods

    private IImageSourceHost CreateHost() => HostFactory?.Invoke(Options) ?? Options switch
    {
#if !EG_MAS
        { Mode: AppMode.ImageGlass10, PipeName: { } pipe } => new ImageGlass10Host(pipe),
#endif
        _ => new StandaloneHost(Options.File),
    };

    #endregion // Private methods
}
