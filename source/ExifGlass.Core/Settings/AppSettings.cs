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
using System.Text.Json.Serialization;

namespace ExifGlass.Core.Settings;

/// <summary>
/// App theme: System follows the OS.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AppTheme>))]
public enum AppTheme
{
    System,
    Light,
    Dark,
}


/// <summary>
/// A window's saved placement (O15): normal bounds, state, and the screen it was on (S4).
/// </summary>
public sealed class WindowPlacementState
{
    #region Properties

    public int X { get; set; }

    public int Y { get; set; }

    /// <summary>
    /// Client width in DIPs while the window is normal.
    /// </summary>
    public double Width { get; set; }

    public double Height { get; set; }

    /// <summary>
    /// Minimized is never restored, so only normal or maximized is kept.
    /// </summary>
    public bool Maximized { get; set; }

    public int ScreenX { get; set; }

    public int ScreenY { get; set; }

    public int ScreenWidth { get; set; }

    public int ScreenHeight { get; set; }

    public double Scaling { get; set; } = 1;

    /// <summary>
    /// The OS frame around the client area in DIPs, so the off-screen clamp counts it (S4).
    /// </summary>
    public double FrameWidth { get; set; }

    public double FrameHeight { get; set; }

    #endregion // Properties


    #region Public methods

    public WindowPlacementState Clone() => (WindowPlacementState)MemberwiseClone();

    #endregion // Public methods
}


/// <summary>
/// Every setting the app persists; a plain mutable class so source-generated JSON reads and writes it.
/// </summary>
public sealed class AppSettings
{
    #region Fields and constants

    /// <summary>
    /// Bumped when a setting changes meaning; older files are migrated, newer ones read as far as they go.
    /// </summary>
    public const int CURRENT_VERSION = 3;

    public const string WINDOW_MAIN = "Main";
    public const double DEFAULT_RAIL_WIDTH = 236;
    public const double DEFAULT_INSPECTOR_WIDTH = 304;

    public const string WINDOW_SETTINGS = "Settings";
    public const string WINDOW_ABOUT = "About";

    #endregion // Fields and constants


    #region Properties

    public int Version { get; set; } = CURRENT_VERSION;

    public AppTheme Theme { get; set; } = AppTheme.System;

    public bool AlwaysOnTop { get; set; }

    public bool CheckForUpdates { get; set; } = true;

    /// <summary>
    /// A custom ExifTool; empty uses the bundled one.
    /// </summary>
    public string ExifToolPath { get; set; } = string.Empty;

    /// <summary>
    /// The terminal-equivalent arguments of the Settings token field (D1).
    /// </summary>
    public string ExifToolArguments { get; set; } = ArgumentPolicy.DEFAULT_ARGUMENTS;

    /// <summary>
    /// Visible table columns: Index, TagId, TagName, Value.
    /// </summary>
    public List<string> VisibleColumns { get; set; } = ["TagId", "TagName", "Value"];

    public bool RailOpen { get; set; } = true;

    public bool InspectorOpen { get; set; } = true;

    /// <summary>
    /// The rail's width as the user dragged it (owner at G7); the shell keeps it within its limits.
    /// </summary>
    public double RailWidth { get; set; } = DEFAULT_RAIL_WIDTH;

    public double InspectorWidth { get; set; } = DEFAULT_INSPECTOR_WIDTH;

    public bool CommandBarExpanded { get; set; }

    /// <summary>
    /// Placement of every window by key: Main, Settings, About (O15).
    /// </summary>
    public Dictionary<string, WindowPlacementState> Windows { get; set; } = [];

    /// <summary>
    /// UTC time of the last update check, round-trip format; empty when never.
    /// </summary>
    public string LastUpdateCheck { get; set; } = string.Empty;

    #endregion // Properties


    #region Public methods

    public AppSettings Clone()
    {
        var copy = (AppSettings)MemberwiseClone();
        copy.VisibleColumns = [.. VisibleColumns];
        copy.Windows = Windows.ToDictionary(kv => kv.Key, kv => kv.Value.Clone(), StringComparer.Ordinal);
        return copy;
    }

    #endregion // Public methods
}


[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(V2Settings))]
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true)]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;
