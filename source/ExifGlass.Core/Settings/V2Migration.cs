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
namespace ExifGlass.Core.Settings;

/// <summary>
/// v2's exifglass.config.json, read only to migrate it.
/// </summary>
public sealed class V2Settings
{
    #region Properties

    public string? Theme { get; set; }

    public bool AlwaysOnTop { get; set; }

    public string? ExifToolPath { get; set; }

    public string? ExifToolArguments { get; set; }

    public bool ShowCommandPreview { get; set; } = true;

    public bool ShowIndex { get; set; } = true;

    public bool ShowTagId { get; set; } = true;

    public bool ShowTagName { get; set; } = true;

    public bool ShowValue { get; set; } = true;

    public V2WindowBounds? Window { get; set; }

    public bool CheckForUpdates { get; set; } = true;

    public string? LastUpdateCheck { get; set; }

    #endregion // Properties
}


/// <summary>
/// v2's main-window bounds.
/// </summary>
public sealed class V2WindowBounds
{
    #region Properties

    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public bool Maximized { get; set; }

    #endregion // Properties
}


/// <summary>
/// Maps v2 settings to v3: Show* flags become the column set, Window the main window's placement; ShowCommandPreview is dropped.
/// </summary>
public static class V2Migration
{
    #region Fields and constants

    // v2 put its extra arguments between these and "-charset UTF8"
    private const string V2_BASE = "-fast -G -t -m -q -H";
    private const string V2_TAIL = "-charset UTF8";

    #endregion // Fields and constants


    #region Public methods

    public static AppSettings ToV3(V2Settings v2)
    {
        var columns = new List<string>();
        if (v2.ShowIndex) columns.Add("Index");
        if (v2.ShowTagId) columns.Add("TagId");
        if (v2.ShowTagName) columns.Add("TagName");
        if (v2.ShowValue) columns.Add("Value");

        // at least one column stays visible
        if (columns.Count == 0) columns.Add("Value");

        var settings = new AppSettings
        {
            Theme = v2.Theme?.Trim().ToLowerInvariant() switch
            {
                "dark" or "1" => AppTheme.Dark,
                "light" or "2" => AppTheme.Light,
                _ => AppTheme.System,
            },
            AlwaysOnTop = v2.AlwaysOnTop,
            CheckForUpdates = v2.CheckForUpdates,
            ExifToolPath = v2.ExifToolPath?.Trim() ?? string.Empty,
            ExifToolArguments = string.IsNullOrWhiteSpace(v2.ExifToolArguments) ? $"{V2_BASE} {V2_TAIL}" : $"{V2_BASE} {v2.ExifToolArguments.Trim()} {V2_TAIL}",
            VisibleColumns = columns,
            LastUpdateCheck = v2.LastUpdateCheck ?? string.Empty,
        };

        if (v2.Window is { Width: > 0, Height: > 0 } w)
        {
            settings.Windows[AppSettings.WINDOW_MAIN] = new WindowPlacementState
            {
                X = w.X,
                Y = w.Y,
                Width = w.Width,
                Height = w.Height,
                Maximized = w.Maximized,
            };
        }

        return settings;
    }

    #endregion // Public methods
}
