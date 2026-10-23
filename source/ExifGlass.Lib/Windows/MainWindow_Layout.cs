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
using ExifGlass.Core.Query;
using ExifGlass.ViewModels;
using ExifGlass.Views;
using System.ComponentModel;

namespace ExifGlass.Windows;

public partial class MainWindow
{
    #region Fields and constants

    // until the window has its first real size, band changes apply at once: a restored maximize shouldn't play the panel motion
    private bool _layoutSettled;

    // the panels can't show without a file, so they are made with the first one: the idle heap stays as it was (plan 8)
    private RailPanel? _rail;
    private InspectorPanel? _inspector;

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
        ViewModel.GroupJumpRequested += ViewModel_GroupJumpRequested;
        RailSlot.Closed += RailSlot_Closed;
        InspectorSlot.Closed += InspectorSlot_Closed;
        Table.SizeChanged += (_, e) => ViewModel.UpdateTableWidth(e.NewSize.Width);
        RailSplitter.ResizeRequested += (_, width) => ViewModel.ResizeRail(width);
        RailSplitter.ResizeCompleted += (_, _) => ViewModel.CommitPanelWidths();
        RailSplitter.ResetRequested += (_, _) => ViewModel.ResetRailWidth();
        InspectorSplitter.ResizeRequested += (_, width) => ViewModel.ResizeInspector(width);
        InspectorSplitter.ResizeCompleted += (_, _) => ViewModel.CommitPanelWidths();
        InspectorSplitter.ResetRequested += (_, _) => ViewModel.ResetInspectorWidth();
        Table.Table.RowActivated += Table_RowActivated;
        ScalingChanged += (_, _) => ViewModel.Loader.PreviewScaling = RenderScaling;
        ViewModel.Loader.PreviewScaling = RenderScaling;
    }


    private void EnsurePanels()
    {
        if (_rail is not null || !ViewModel.HasDocument) return;

        _rail = new RailPanel();
        _inspector = new InspectorPanel();
        RailContent.Children.Add(_rail);
        InspectorContent.Children.Add(_inspector);
    }


    // the rail lives in its column while docked and in the push pane while pushed; it moves only when it has to
    private void PlaceRail()
    {
        if (ViewModel.RailMode == PanelMode.Docked) Move(RailContent, RailDockFrame);
        else if (ViewModel.IsRailPushed || (!RailSlot.IsOpen && RailSlot.Openness <= 0)) Move(RailContent, RailPushFrame);
    }


    private void PlaceInspector()
    {
        if (ViewModel.InspectorMode == PanelMode.Docked) Move(InspectorContent, InspectorDockFrame);
        else if (ViewModel.IsInspectorPushed || (!InspectorSlot.IsOpen && InspectorSlot.Openness <= 0)) Move(InspectorContent, InspectorPushFrame);
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
            case nameof(MainViewModel.IsRailPushed):
                PlaceRail();
                break;

            case nameof(MainViewModel.InspectorMode):
            case nameof(MainViewModel.IsInspectorPushed):
                PlaceInspector();
                break;

            case nameof(MainViewModel.IsRailDocked):
                if (!_layoutSettled) RailSlot.Snap(ViewModel.IsRailDocked);
                break;

            case nameof(MainViewModel.IsInspectorDocked):
                if (!_layoutSettled) InspectorSlot.Snap(ViewModel.IsInspectorDocked);
                break;

            case nameof(MainViewModel.HasDocument):
                EnsurePanels();
                break;
        }
    }


    // the rows of a group the jump showed or expanded are on screen once this view is applied
    private void ViewModel_GroupJumpRequested(object? sender, string group) =>
        Dispatcher.UIThread.Post(() => _ = Table.Table.JumpToGroupAsync(group), DispatcherPriority.Loaded);


    // Enter on a row (Appendix B item 10): the inspector shows, and keyboard focus moves into its card
    private void Table_RowActivated(object? sender, ViewRow e)
    {
        if (!ViewModel.IsInspectorShown) ViewModel.ToggleInspectorCommand.Execute(null);
        _inspector?.FocusSelectedTag();
    }


    private void RailSlot_Closed(object? sender, EventArgs e) => PlaceRail();


    private void InspectorSlot_Closed(object? sender, EventArgs e) => PlaceInspector();

    #endregion // Event handlers
}
