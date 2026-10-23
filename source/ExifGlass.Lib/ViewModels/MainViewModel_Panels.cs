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
using CommunityToolkit.Mvvm.ComponentModel;
using ExifGlass.Core.Settings;

namespace ExifGlass.ViewModels;

public sealed partial class MainViewModel
{
    #region Properties

    /// <summary>
    /// The rail's width, set by a drag in the gap beside it and kept in the settings (owner at G7).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RailColumn))]
    public partial double RailWidth { get; private set; } = AppSettings.DEFAULT_RAIL_WIDTH;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InspectorColumn))]
    public partial double InspectorWidth { get; private set; } = AppSettings.DEFAULT_INSPECTOR_WIDTH;

    /// <summary>
    /// The docked rail's column: its width and the gap on the table's side.
    /// </summary>
    public double RailColumn => RailWidth + ShellLayout.PANEL_GAP;

    public double InspectorColumn => InspectorWidth + ShellLayout.PANEL_GAP;

    /// <summary>
    /// A pushed rail's width: its own, but never wider than the window's content.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RailPushTravel))]
    public partial double RailPushWidth { get; private set; } = AppSettings.DEFAULT_RAIL_WIDTH;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InspectorPushTravel))]
    public partial double InspectorPushWidth { get; private set; } = AppSettings.DEFAULT_INSPECTOR_WIDTH;

    public double RailPushTravel => RailPushWidth + ShellLayout.PANEL_GAP;

    public double InspectorPushTravel => InspectorPushWidth + ShellLayout.PANEL_GAP;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Sizes the docked rail for a drag: within its limits, and the table keeps its minimum width beside it.
    /// </summary>
    public void ResizeRail(double width) => RailWidth = Layout.FitRail(width, _windowWidth, IsInspectorDocked);


    public void ResizeInspector(double width) => InspectorWidth = Layout.FitInspector(width, _windowWidth, IsRailDocked);


    /// <summary>
    /// Keeps the widths once a drag ends; a narrower panel may leave room for the other to dock.
    /// </summary>
    public void CommitPanelWidths()
    {
        _settings.Update(s =>
        {
            s.RailWidth = RailWidth;
            s.InspectorWidth = InspectorWidth;
        });
        UpdateWindowWidth(_windowWidth);
    }


    public void ResetRailWidth()
    {
        ResizeRail(AppSettings.DEFAULT_RAIL_WIDTH);
        CommitPanelWidths();
    }


    public void ResetInspectorWidth()
    {
        ResizeInspector(AppSettings.DEFAULT_INSPECTOR_WIDTH);
        CommitPanelWidths();
    }

    #endregion // Public methods


    #region Private methods

    // a saved width comes back within the limits; the window's room isn't known yet, and docking follows from the widths
    private void RestorePanelWidths(AppSettings settings)
    {
        RailWidth = Layout.FitRail(settings.RailWidth, double.PositiveInfinity, inspectorDocked: false);
        InspectorWidth = Layout.FitInspector(settings.InspectorWidth, double.PositiveInfinity, railDocked: false);
    }


    private void UpdatePushWidths()
    {
        var room = Math.Max(0, _windowWidth - ShellLayout.CONTENT_PADDING);
        RailPushWidth = Math.Min(RailWidth, room);
        InspectorPushWidth = Math.Min(InspectorWidth, room);
    }


    partial void OnRailWidthChanged(double value)
    {
        Layout.RailColumn = RailColumn;
        UpdatePushWidths();
    }


    partial void OnInspectorWidthChanged(double value)
    {
        Layout.InspectorColumn = InspectorColumn;
        UpdatePushWidths();
    }

    #endregion // Private methods
}
