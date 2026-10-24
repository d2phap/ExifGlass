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
using Avalonia.Controls;
using Avalonia.Threading;
using ExifGlass.ViewModels;
using System.ComponentModel;

namespace ExifGlass.Views;

/// <summary>
/// The inspector (DS 6.11): a stack of cards; the AI and selected-tag cards rise in as they appear; made when the first file opens.
/// </summary>
public partial class InspectorPanel : UserControl
{
    #region Fields and constants

    private MainViewModel? _viewModel;
    private string? _focusedKey;
    private string? _focusedAiKey;
    private bool _hadAi;
    private bool _hadCharacter;
    private bool _appeared;

    #endregion // Fields and constants


    #region Constructors

    public InspectorPanel()
    {
        InitializeComponent();

        // a card slides out showing what it showed; it lets go once its room has closed
        TagSlot.Closed += (_, _) =>
        {
            if (_viewModel?.FocusedTag is null) TagCard.View = null;
        };
        AiFieldSlot.Closed += (_, _) =>
        {
            if (_viewModel?.FocusedAiField is null) AiFieldCard.View = null;
        };
    }

    #endregion // Constructors


    #region Public methods

    /// <summary>
    /// Enter on a row (Appendix B item 10): keyboard focus moves into the selected-tag card.
    /// </summary>
    public void FocusSelectedTag() => Dispatcher.UIThread.Post(() =>
    {
        if (_viewModel?.HasFocusedAiField == true)
        {
            AiFieldCard.BringIntoView();
            AiFieldCard.FocusFirst();
            return;
        }

        TagCard.BringIntoView();
        TagCard.FocusFirst();
    }, DispatcherPriority.Loaded);

    #endregion // Public methods


    #region Protected methods

    // made with the first file, so the standing cards rise in with it (DS 5.2)
    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (_appeared) return;

        _appeared = true;
        UI.Controls.Card.PlayAppear(FrameCard);
        UI.Controls.Card.PlayAppear(ExposureCard);
        UI.Controls.Card.PlayAppear(DetailsCard);
    }


    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null) _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null) _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        TagCard.View = _viewModel?.FocusedTag;
        AiFieldCard.View = _viewModel?.FocusedAiField;
    }

    #endregion // Protected methods


    #region Event handlers

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null) return;

        switch (e.PropertyName)
        {
            // the Add-tag card opens its room on top with the caret in its name field
            case nameof(MainViewModel.IsAddingTag):
                if (!_viewModel.IsAddingTag) break;

                AddTagCard.FocusName();
                Dispatcher.UIThread.Post(() => AddTagCard.BringIntoView(), DispatcherPriority.Loaded);
                break;

            // the skeleton becomes the card in place; a card that wasn't there rises in
            case nameof(MainViewModel.ShowAiCard):
                if (_viewModel.ShowAiCard && !_hadAi) AiCard.Appear();
                _hadAi = _viewModel.ShowAiCard;
                break;

            case nameof(MainViewModel.HasCharacterCard):
                if (_viewModel.HasCharacterCard && !_hadCharacter) CharacterCard.Appear();
                _hadCharacter = _viewModel.HasCharacterCard;
                break;

            // a standing card that was hidden for an earlier file rises in when this one has something for it
            case nameof(MainViewModel.ShowPreviewCard):
                if (_viewModel.ShowPreviewCard && !FrameCard.IsVisible) UI.Controls.Card.PlayAppear(FrameCard);
                break;

            case nameof(MainViewModel.ShowExposureCard):
                if (_viewModel.ShowExposureCard && !ExposureCard.IsVisible) UI.Controls.Card.PlayAppear(ExposureCard);
                break;

            case nameof(MainViewModel.ShowDetailsCard):
                if (_viewModel.ShowDetailsCard && !DetailsCard.IsVisible) UI.Controls.Card.PlayAppear(DetailsCard);
                break;

            // on top of the cards (owner at G7): the inspector scrolls to it
            case nameof(MainViewModel.FocusedAiField):
                var aiKey = _viewModel.FocusedAiField?.Key;
                if (_viewModel.FocusedAiField is { } field) AiFieldCard.View = field;
                if (aiKey is not null && aiKey != _focusedAiKey) Dispatcher.UIThread.Post(() => AiFieldCard.BringIntoView(), DispatcherPriority.Loaded);
                _focusedAiKey = aiKey;
                break;

            case nameof(MainViewModel.FocusedTag):
                var key = _viewModel.FocusedTag?.Key;
                if (_viewModel.FocusedTag is { } tag) TagCard.View = tag;
                if (key is not null && key != _focusedKey) Dispatcher.UIThread.Post(() => TagCard.BringIntoView(), DispatcherPriority.Loaded);
                _focusedKey = key;
                break;
        }
    }

    #endregion // Event handlers
}
