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
/// The -p:Key=Value command-line overrides: applied for this run only, never saved.
/// </summary>
public static class SettingsOverrides
{
    #region Public methods

    /// <summary>
    /// Applies the overrides it knows and returns their keys; unknown keys and bad values are ignored.
    /// </summary>
    public static IReadOnlySet<string> Apply(AppSettings settings, IReadOnlyDictionary<string, string> overrides)
    {
        var applied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in overrides)
        {
            if (TryApply(settings, key, value)) applied.Add(Canonical(key));
        }

        return applied;
    }


    /// <summary>
    /// Puts back the file values of overridden keys before a save.
    /// </summary>
    public static void Restore(AppSettings target, AppSettings fileValues, IReadOnlySet<string> keys)
    {
        foreach (var key in keys)
        {
            switch (key)
            {
                case nameof(AppSettings.Theme): target.Theme = fileValues.Theme; break;
                case nameof(AppSettings.Accent): target.Accent = fileValues.Accent; break;
                case nameof(AppSettings.AlwaysOnTop): target.AlwaysOnTop = fileValues.AlwaysOnTop; break;
                case nameof(AppSettings.CheckForUpdates): target.CheckForUpdates = fileValues.CheckForUpdates; break;
                case nameof(AppSettings.ExifToolPath): target.ExifToolPath = fileValues.ExifToolPath; break;
                case nameof(AppSettings.ExifToolArguments): target.ExifToolArguments = fileValues.ExifToolArguments; break;
                case nameof(AppSettings.VisibleColumns): target.VisibleColumns = [.. fileValues.VisibleColumns]; break;
                case nameof(AppSettings.CommandBarExpanded): target.CommandBarExpanded = fileValues.CommandBarExpanded; break;
                default: break;
            }
        }
    }

    #endregion // Public methods


    #region Private methods

    private static string Canonical(string key) => key.ToLowerInvariant() switch
    {
        "theme" => nameof(AppSettings.Theme),
        "accent" => nameof(AppSettings.Accent),
        "alwaysontop" => nameof(AppSettings.AlwaysOnTop),
        "checkforupdates" => nameof(AppSettings.CheckForUpdates),
        "exiftoolpath" => nameof(AppSettings.ExifToolPath),
        "exiftoolarguments" => nameof(AppSettings.ExifToolArguments),
        "visiblecolumns" => nameof(AppSettings.VisibleColumns),
        "commandbarexpanded" => nameof(AppSettings.CommandBarExpanded),
        _ => key,
    };


    private static bool TryApply(AppSettings s, string key, string value)
    {
        switch (Canonical(key))
        {
            case nameof(AppSettings.Theme):
                var theme = value.Trim().ToLowerInvariant() switch
                {
                    "system" or "default" => AppTheme.System,
                    "light" => AppTheme.Light,
                    "dark" => AppTheme.Dark,
                    _ => (AppTheme?)null,
                };
                if (theme is null) return false;
                s.Theme = theme.Value;
                return true;

            case nameof(AppSettings.Accent):
                if (value.Trim().Length == 0) return false;
                s.Accent = value.Trim();
                return true;

            case nameof(AppSettings.AlwaysOnTop):
                if (!TryBool(value, out var top)) return false;
                s.AlwaysOnTop = top;
                return true;

            case nameof(AppSettings.CheckForUpdates):
                if (!TryBool(value, out var check)) return false;
                s.CheckForUpdates = check;
                return true;

            case nameof(AppSettings.ExifToolPath):
                s.ExifToolPath = value;
                return true;

            case nameof(AppSettings.ExifToolArguments):
                s.ExifToolArguments = value;
                return true;

            case nameof(AppSettings.VisibleColumns):
                var columns = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                if (columns.Count == 0) return false;
                s.VisibleColumns = columns;
                return true;

            case nameof(AppSettings.CommandBarExpanded):
                if (!TryBool(value, out var expanded)) return false;
                s.CommandBarExpanded = expanded;
                return true;

            default:
                return false;
        }
    }


    private static bool TryBool(string value, out bool result)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "true" or "1" or "yes" or "on":
                result = true;
                return true;
            case "false" or "0" or "no" or "off":
                result = false;
                return true;
            default:
                result = false;
                return false;
        }
    }

    #endregion // Private methods
}
