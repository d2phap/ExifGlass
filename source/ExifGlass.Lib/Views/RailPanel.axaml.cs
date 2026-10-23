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
using Avalonia.Threading;
using ExifGlass.Core.Metadata;
using ExifGlass.UI.Controls;
using ExifGlass.UI.Motion;
using ExifGlass.UI.Tokens;
using ExifGlass.ViewModels;
using System.ComponentModel;

namespace ExifGlass.Views;

/// <summary>
/// The rail (DS 6.10): file card, groups (show or hide, jump), quick filters and the drop target; made when the first file opens.
/// </summary>
public partial class RailPanel : UserControl
{
    #region Fields and constants

    // Proto: rows fade in and slide 12 px from the left, staggered 120 + i * 50 ms
    private const double ROW_SHIFT = -12;
    private static readonly TimeSpan ROW_STAGGER_START = TimeSpan.FromMilliseconds(120);
    private static readonly TimeSpan ROW_STAGGER_STEP = TimeSpan.FromMilliseconds(50);

    private MainViewModel? _viewModel;
    private MetadataDocument? _shown;

    #endregion // Fields and constants


    #region Constructors

    public RailPanel()
    {
        InitializeComponent();

        GroupList.AddHandler(RailGroupRow.ToggleRequestedEvent, Group_ToggleRequested);
        GroupList.AddHandler(RailGroupRow.JumpRequestedEvent, Group_JumpRequested);
        FilterList.AddHandler(Button.ClickEvent, Filter_Click);
        Scroller.PropertyChanged += Scroller_PropertyChanged;
    }

    #endregion // Constructors


    #region Protected methods

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null) _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel = DataContext as MainViewModel;
        if (_viewModel is null) return;

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        Reveal(_viewModel.Rows.Document);
    }

    #endregion // Protected methods


    #region Private methods

    // a new read plays the rail's entry: rows staggered in, the bar grown from nothing (DS 5.2)
    private void Reveal(MetadataDocument? document)
    {
        if (document is null || document.IsSameRead(_shown)) return;

        _shown = document;
        File.Replay();
        Dispatcher.UIThread.Post(() =>
        {
            var i = 0;
            foreach (var row in GroupList.GetRealizedContainers())
            {
                var delay = ROW_STAGGER_START + ROW_STAGGER_STEP * i++;
                CompositionMotion.Fade(row, 0, 1, MotionTokens.RevealFade, MotionTokens.RevealFadeEasing, delay);
                CompositionMotion.Move(row, new Vector(ROW_SHIFT, 0), default, MotionTokens.RevealRise, MotionTokens.RevealRiseEasing, delay);
            }
        }, DispatcherPriority.Loaded);
    }

    #endregion // Private methods


    #region Event handlers

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Rows)) Reveal(_viewModel?.Rows.Document);
    }


    // Proto: the drop target sits at the foot of the rail until the content outgrows it
    private void Scroller_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.ViewportProperty) Sections.MinHeight = Math.Max(0, Scroller.Viewport.Height - Sections.Margin.Top - Sections.Margin.Bottom);
    }


    private void Group_ToggleRequested(object? sender, RoutedEventArgs e)
    {
        if (e.Source is RailGroupRow { DataContext: RailGroupItem item }) _viewModel?.ToggleGroupShownCommand.Execute(item);
    }


    private void Group_JumpRequested(object? sender, RoutedEventArgs e)
    {
        if (e.Source is RailGroupRow { DataContext: RailGroupItem item }) _viewModel?.JumpToGroupCommand.Execute(item);
    }


    private void Filter_Click(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Chip { DataContext: QuickFilterItem item }) _viewModel?.ToggleQuickFilterCommand.Execute(item);
    }

    #endregion // Event handlers
}
