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
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using ExifGlass.UI;
using ExifGlass.UI.Controls;
using ExifGlass.UI.Motion;
using ExifGlass.UI.Tokens;
using ExifGlass.ViewModels;
using System.ComponentModel;

namespace ExifGlass.Views;

/// <summary>
/// The empty table's content (DS 6.9): drop a file or open one; with no ExifTool it says so and offers Settings and Retry.
/// </summary>
public partial class EmptyState : UserControl
{
    #region Fields and constants

    private const double DROP_SCALE = 1.04;

    public static readonly StyledProperty<bool> IsDragOverProperty =
        AvaloniaProperty.Register<EmptyState, bool>(nameof(IsDragOver));

    private static readonly AvaloniaList<double> DASHES = [4, 3];

    private MainViewModel? _viewModel;

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// Gets or sets whether a file is over the window: the content grows inside an accent dashed border, and the logo's ring pulses.
    /// </summary>
    public bool IsDragOver
    {
        get => GetValue(IsDragOverProperty);
        set => SetValue(IsDragOverProperty, value);
    }

    #endregion // Properties


    #region Constructors

    public EmptyState()
    {
        InitializeComponent();
        OpenHint.Text = UiText.Shortcut("O");
    }

    #endregion // Constructors


    #region Protected methods

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null) _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null) _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        UpdateText();
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != IsDragOverProperty) return;

        var over = IsDragOver;
        UpdateText();
        Ring.IsPulsing = over;
        if (over) Card[!SquircleBorder.BorderBrushProperty] = this.GetResourceObservable(TokenKeys.ACCENT_BRUSH).ToBinding();
        else Card.BorderBrush = Brushes.Transparent;
        SquircleBorder.SetBorderDashArray(Card, over ? DASHES : null);
        CompositionMotion.Scale(Card, over ? 1 : DROP_SCALE, over ? DROP_SCALE : 1, MotionTokens.DropScale, MotionTokens.DropScaleEasing);
    }

    #endregion // Protected methods


    #region Private methods

    private void UpdateText()
    {
        if (_viewModel?.ExifToolProblem is { } problem)
        {
            TitleText.Text = AppText.EXIFTOOL_MISSING_TITLE;
            CaptionText.Text = $"{problem.TrimEnd('.')}. {AppText.EXIFTOOL_MISSING_CAPTION}";
            return;
        }

        TitleText.Text = IsDragOver ? AppText.RELEASE_TO_INSPECT : AppText.EMPTY_TITLE;
        CaptionText.Text = AppText.EMPTY_CAPTION;
    }

    #endregion // Private methods


    #region Event handlers

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.ExifToolProblem)) UpdateText();
    }

    #endregion // Event handlers
}
