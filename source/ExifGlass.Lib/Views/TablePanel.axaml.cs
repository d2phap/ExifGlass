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
using Avalonia.Interactivity;
using ExifGlass.UI.Controls;
using ExifGlass.UI.Motion;
using ExifGlass.UI.Tokens;
using ExifGlass.ViewModels;
using System.ComponentModel;

namespace ExifGlass.Views;

/// <summary>
/// The table panel (DS 6.9): its toolbar (summary or selection, Collapse all, Edit, Columns, Export) over the metadata table.
/// </summary>
public partial class TablePanel : UserControl
{
    #region Fields and constants

    // Proto checkBar: labels come back once the toolbar is this much wider than where they clipped
    private const double LABEL_HYSTERESIS = 80;

    private MainViewModel? _viewModel;
    private double _clippedAt;

    #endregion // Fields and constants


    #region Constructors

    public TablePanel()
    {
        InitializeComponent();

        Grid.SortRequested += Grid_SortRequested;
        Grid.GroupToggleRequested += Grid_GroupToggleRequested;
        Grid.ValueCopyRequested += Grid_ValueCopyRequested;
        Grid.CopyRequested += Grid_CopyRequested;
        Grid.PropertyChanged += Grid_PropertyChanged;
        Chips.AddHandler(FilterChip.RemovedEvent, Chip_Removed);
        ToolbarGrid.LayoutUpdated += ToolbarGrid_LayoutUpdated;

        foreach (var button in new[] { EditButton, ColumnsButton, ExportButton })
        {
            if (button.Flyout is Avalonia.Controls.Primitives.PopupFlyoutBase flyout) MenuMotion.Attach(flyout);
        }
    }

    #endregion // Constructors


    #region Properties

    public EmptyState EmptyCard => Empty;

    public MetadataTable Table => Grid;

    #endregion // Properties


    #region Protected methods

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null) _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel = DataContext as MainViewModel;
        Grid.Selection = _viewModel?.Selection;
        if (_viewModel is null) return;

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        SyncColumnItems();
    }

    #endregion // Protected methods


    #region Private methods

    private void SyncColumnItems()
    {
        if (_viewModel is null) return;

        var items = new[] { ColumnIndexItem, ColumnIdItem, ColumnNameItem, ColumnValueItem };
        var choices = _viewModel.ColumnChoices;
        for (var i = 0; i < items.Length && i < choices.Count; i++)
        {
            items[i].IsChecked = choices[i].IsChecked;
            items[i].IsEnabled = !choices[i].IsLocked;
            items[i].Hint = choices[i].Hint;
        }
    }


    // the left side truncates first; once the buttons themselves don't fit, their labels go (P4 rules)
    private void CheckLabels()
    {
        if (_viewModel is null) return;

        var room = ToolbarGrid.Bounds.Width;
        if (room <= 0) return;

        if (!_viewModel.LabelsClipped)
        {
            if (_viewModel.ShowTableLabels && TableButtons.DesiredSize.Width > room)
            {
                _clippedAt = room;
                _viewModel.LabelsClipped = true;
            }
        }
        else if (room > _clippedAt + LABEL_HYSTERESIS) _viewModel.LabelsClipped = false;
    }

    #endregion // Private methods


    #region Event handlers

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.ColumnChoices):
                SyncColumnItems();
                break;

            // Proto egRise: the no-match state rises 12 px into place as it fades in
            case nameof(MainViewModel.ShowNoMatch) when _viewModel?.ShowNoMatch == true:
                CompositionMotion.Fade(NoMatch, 0, 1, MotionTokens.Rise, MotionTokens.RiseEasing);
                CompositionMotion.Move(NoMatch, new Vector(0, 12), default, MotionTokens.Rise, MotionTokens.RiseEasing);
                break;
        }
    }


    private void ToolbarGrid_LayoutUpdated(object? sender, EventArgs e) => CheckLabels();


    private void Grid_SortRequested(object? sender, Core.Query.SortColumn e) => _viewModel?.CycleSort(e);


    private void Grid_GroupToggleRequested(object? sender, string e) => _viewModel?.ToggleGroup(e);


    private void Grid_ValueCopyRequested(object? sender, Core.Query.ViewRow e)
    {
        if (_viewModel is not null) _ = _viewModel.CopyValueAsync(e);
    }


    private void Grid_CopyRequested(object? sender, EventArgs e) => _viewModel?.CopySelection();


    private void Grid_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == MetadataTable.ActiveGroupProperty && _viewModel is not null) _viewModel.ActiveGroup = Grid.ActiveGroup;
    }


    private void Chip_Removed(object? sender, RoutedEventArgs e)
    {
        if (e.Source is FilterChip { DataContext: FilterChipItem chip }) _viewModel?.RemoveChipCommand.Execute(chip);
    }

    #endregion // Event handlers
}
