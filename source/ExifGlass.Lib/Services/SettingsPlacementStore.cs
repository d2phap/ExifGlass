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
using ExifGlass.Core.Settings;
using ExifGlass.UI.Windows;

namespace ExifGlass.Services;

/// <summary>
/// Window placements kept in the settings file (O15): AppSettings.Windows by key.
/// </summary>
public sealed class SettingsPlacementStore(SettingsStore settings) : IWindowPlacementStore
{
    #region Public methods

    public WindowPlacementState? Get(string key) => settings.Current.Windows.TryGetValue(key, out var placement) ? placement.Clone() : null;

    public void Set(string key, WindowPlacementState placement) => settings.Update(s => s.Windows[key] = placement.Clone());

    #endregion // Public methods
}
