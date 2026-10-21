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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ExifGlass.ViewModels;
using System.ComponentModel;

namespace ExifGlass.Windows;

public partial class MainWindow
{
    #region Fields and constants

    // until the window has its first real size, band changes apply at once: a restored maximize shouldn't play the panel motion
    private bool _layoutSettled;

    #endregion // Fields and constants


    #region Protected methods

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ClientSizeProperty && DataContext is MainViewModel viewModel) viewModel.UpdateWindowWidth(ClientSize.Width);
    }


    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // placement maximizes in its own Opened handler; the resize it causes lands before Background work runs
        Dispatcher.UIThread.Post(() => _layoutSettled = true, DispatcherPriority.Background);
    }

    #endregion // Protected methods


    #region Private methods

    private void InitializeLayout()
    {
        ViewModel.UpdateWindowWidth(Width);
        PlaceRail();
        PlaceInspector();

        ViewModel.PropertyChanged += ViewModel_LayoutChanged;
        RailSlot.Closed += RailSlot_Closed;
        InspectorSlot.Closed += InspectorSlot_Closed;
        Table.SizeChanged += (_, e) => ViewModel.UpdateTableWidth(e.NewSize.Width);
    }


    // the rail lives in its column while docked and in the overlay pane while one is shown; it moves only when it has to
    private void PlaceRail()
    {
        if (ViewModel.RailMode == PanelMode.Docked) Move(RailContent, RailDockFrame);
        else if (ViewModel.IsRailOverlayOpen || (!RailSlot.IsOpen && RailSlot.Openness <= 0)) Move(RailContent, RailOverlayFrame);
    }


    private void PlaceInspector()
    {
        if (ViewModel.InspectorMode == PanelMode.Docked) Move(InspectorContent, InspectorDockFrame);
        else if (ViewModel.IsInspectorOverlayOpen || (!InspectorSlot.IsOpen && InspectorSlot.Openness <= 0)) Move(InspectorContent, InspectorOverlayFrame);
    }


    private static void Move(Control content, Decorator target)
    {
        if (ReferenceEquals(content.Parent, target)) return;

        if (content.Parent is Decorator from) from.Child = null;
        target.Child = content;
    }

    #endregion // Private methods


    #region Event handlers

    private void ViewModel_LayoutChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.RailMode):
            case nameof(MainViewModel.IsRailOverlayOpen):
                PlaceRail();
                break;

            case nameof(MainViewModel.InspectorMode):
            case nameof(MainViewModel.IsInspectorOverlayOpen):
                PlaceInspector();
                break;

            case nameof(MainViewModel.IsRailDocked):
                if (!_layoutSettled) RailSlot.Snap(ViewModel.IsRailDocked);
                break;

            case nameof(MainViewModel.IsInspectorDocked):
                if (!_layoutSettled) InspectorSlot.Snap(ViewModel.IsInspectorDocked);
                break;
        }
    }


    private void RailSlot_Closed(object? sender, EventArgs e) => PlaceRail();


    private void InspectorSlot_Closed(object? sender, EventArgs e) => PlaceInspector();

    #endregion // Event handlers
}
