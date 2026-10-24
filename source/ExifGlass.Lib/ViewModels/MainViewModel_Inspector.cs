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
using CommunityToolkit.Mvvm.Input;
using ExifGlass.Core.Ai;
using ExifGlass.Core.Editing;
using ExifGlass.Core.Metadata;
using ExifGlass.Core.Query;
using ExifGlass.Services;
using ExifGlass.UI;
using ExifGlass.UI.Controls;
using System.Globalization;

namespace ExifGlass.ViewModels;

public sealed partial class MainViewModel
{
    #region Fields and constants

    // staged in memory only until P8 brings the pending bar and Save
    private EditSet _edits = EditSet.Empty;

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// The tag the selected-tag card shows: the current cell's row, or the last row Ctrl or Shift selected (DS 6.11).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFocusedTag))]
    [NotifyCanExecuteChangedFor(nameof(StartEditCommand), nameof(ToggleDeleteCommand))]
    public partial SelectedTagView? FocusedTag { get; private set; }

    [ObservableProperty]
    public partial bool IsEditing { get; private set; }

    /// <summary>
    /// The edit mode's text, bound both ways to the card's editor.
    /// </summary>
    [ObservableProperty]
    public partial string? EditText { get; set; }

    public bool HasFocusedTag => FocusedTag is not null;

    /// <summary>
    /// The AI card's data: shown only for a file made or edited with AI (DS 7.5; provenance alone and character cards are P7's).
    /// </summary>
    public AiGenerationInfo? AiInfo => Document?.Ai is { IsAi: true } ai ? ai : null;

    public bool HasAi => AiInfo is not null;

    public FrameInfo? Frame => Document?.Derived.Frame;

    public IReadOnlyList<ExposureItem>? Exposure => Document?.Derived.Exposure;

    /// <summary>
    /// Whether the Preview card shows: an empty card hides (owner at G7), but it stays while a preview that should come is decoding, so the cards below don't jump.
    /// </summary>
    public bool ShowPreviewCard => Document is { } doc && (Loader.Preview is not null || (Loader.IsPreviewPending && PreviewService.MayHavePreview(doc)));

    public bool ShowExposureCard => ExposureCard.HasReadout(Exposure);

    public bool ShowDetailsCard => DetailsCard.HasRows(Details);

    /// <summary>
    /// The Details card's rows; an AI-training signal on a file not made with AI is a row of its own (AI addendum question 10).
    /// </summary>
    public IReadOnlyList<DetailItem>? Details
    {
        get
        {
            if (Document?.Derived.Details is not { } details) return null;
            if (Document.Ai is not { IsAi: false, Training: { } training }) return details;

            var signals = new List<string>(2);
            if (training.DataMining is not null) signals.Add(AppText.PLUS_DATA_MINING);
            if (training.LearningOptOutIn is not null) signals.Add(AppText.EXIF_OPT_OUT);
            var value = training.IsOptOut ? AppText.NOT_ALLOWED : training.DataMining ?? training.LearningOptOutIn ?? string.Empty;
            return [.. details, new DetailItem(AppText.AI_TRAINING, value, string.Join(" · ", signals))];
        }
    }

    /// <summary>
    /// Staged changes, for the table's badges until P8's pending bar shows them.
    /// </summary>
    public EditSet Edits => _edits;

    #endregion // Properties


    #region Commands

    /// <summary>
    /// The card's Close: the inspector lets go of the tag; the table keeps its cell.
    /// </summary>
    [RelayCommand]
    private void CloseFocus()
    {
        IsEditing = false;
        Selection.Unfocus();
    }


    [RelayCommand]
    private Task CopyFocusValueAsync() => FocusedTag is { } tag ? CopyAndToastAsync(tag.Value, AppText.TOAST_COPIED_VALUE, tag.Name) : Task.CompletedTask;


    // "(same as value)" stands for the value itself, so that is what it copies
    [RelayCommand]
    private Task CopyFocusRawAsync() => FocusedTag is { } tag
        ? CopyAndToastAsync(tag.IsRawSame ? tag.Value : tag.Raw, AppText.TOAST_COPIED_RAW, AppText.Clip(tag.IsRawSame ? tag.Value : tag.Raw))
        : Task.CompletedTask;


    [RelayCommand]
    private Task CopyFocusFormatAsync() => FocusedTag is { } tag && tag.Format != TagIdRule.NONE
        ? CopyAndToastAsync(tag.Format, AppText.TOAST_COPIED_FORMAT, tag.Format)
        : Task.CompletedTask;


    [RelayCommand]
    private Task CopyFocusKeyAsync() => FocusedTag is { IsAi: false } tag ? CopyAndToastAsync(tag.ArgumentKey, AppText.TOAST_COPIED_KEY, tag.ArgumentKey) : Task.CompletedTask;


    [RelayCommand(CanExecute = nameof(CanEditFocus))]
    private void StartEdit()
    {
        if (FocusedTag is not { } tag) return;

        EditText = tag.Value;
        IsEditing = true;
    }


    /// <summary>
    /// Apply (Ctrl+Enter): stages the new value, or drops the edit when the value is the file's again (Proto).
    /// </summary>
    [RelayCommand]
    private void ApplyEdit()
    {
        if (!IsEditing || FocusedTag is not { } view || Document?.GetByKey(view.Key) is not { } tag) return;

        IsEditing = false;
        var value = EditText ?? string.Empty;
        var original = _arguments.ShowRawValues ? tag.RawOrValue : tag.Value;
        var unchanged = value == original;
        _edits = unchanged ? _edits.Revert(tag.Key) : _edits.Set(tag.Key, tag.WriteName, value, original, _arguments.ShowRawValues);
        Requery();

        Shell?.ShowToast(unchanged ? AppText.TOAST_NO_CHANGE : AppText.TOAST_EDIT_STAGED, view.Name, unchanged ? ToastKind.Info : ToastKind.Success);
    }


    [RelayCommand]
    private void CancelEdit() => IsEditing = false;


    /// <summary>
    /// Delete, or Undo a staged delete (DS 6.11).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEditFocus))]
    private void ToggleDelete()
    {
        if (FocusedTag is not { } view || Document?.GetByKey(view.Key) is not { } tag) return;

        IsEditing = false;
        _edits = view.IsDeleted ? _edits.Revert(tag.Key) : _edits.Delete(tag.Key, tag.WriteName, _arguments.ShowRawValues ? tag.RawOrValue : tag.Value);
        Requery();
    }


    [RelayCommand]
    private void RevertFocus()
    {
        if (FocusedTag is not { } view) return;

        _edits = _edits.Revert(view.Key);
        Requery();
    }


    /// <summary>
    /// Copy generation parameters: A1111 infotext, as A1111 and Forge read it back (G7 checks the round trip).
    /// </summary>
    [RelayCommand]
    private Task CopyGenerationParametersAsync() => AiInfo is { Primary: { IsGeneration: true } source } info
        ? CopyAndToastAsync(AiExport.ToA1111(source), AppText.TOAST_COPIED_PARAMETERS, AppText.A1111Text(AiCard.GeneratorLabel(info)))
        : Task.CompletedTask;


    [RelayCommand]
    private Task OpenLinkAsync(string? uri) => uri is null || Shell is null ? Task.CompletedTask : Shell.OpenUriAsync(uri);

    #endregion // Commands


    #region Private methods

    private bool CanEditFocus() => FocusedTag is { IsReadOnly: false };


    // the card follows the selection and the rows it is made from (an edit restyles it); an AI row has a card of its own
    private void UpdateFocus()
    {
        var row = Selection.FocusedKey is { } key ? FindRow(key) : null;
        UpdateAiFocus(row);
        if (row is null || row.IsAi || row.EditState == EditState.Added)
        {
            IsEditing = false;
            FocusedTag = null;
            return;
        }

        if (FocusedTag?.Key != row.Key) IsEditing = false;

        var tag = row.Tag;
        var view = new SelectedTagView(
            row.Key,
            row.IsAi ? UiText.AI_GROUP : row.Group,
            row.DisplayId,
            row.Name,
            row.Value,
            tag?.Raw is { } raw && raw != row.Value ? raw : UI.UiText.SAME_AS_VALUE,
            tag is not null && Document is { } doc ? doc.FormatOf(tag) : TagIdRule.NONE,
            tag?.ArgumentKey ?? string.Empty,
            row.IsAi || tag is null || tag.IsReadOnly || TagWriteRules.IsReadOnly(tag, null),
            row.EditState,
            row.OriginalValue,
            row.IsAi,
            tag?.IsAiSource == true ? AiFieldsFrom(tag) : 0);

        if (view != FocusedTag) FocusedTag = view;
    }


    // the AI fields read from this tag: "Parsed into 23 AI fields" on its card
    private int AiFieldsFrom(TagRecord tag)
    {
        var count = 0;
        foreach (var row in Rows.RowsInView)
        {
            if (row is { IsAi: true, SourceKey: { } key } && (key == tag.Key || new AiSourceMatcher([key]).Matches(tag))) count++;
        }

        return count;
    }


    private void NotifyInspector()
    {
        OnPropertyChanged(nameof(AiInfo));
        OnPropertyChanged(nameof(HasAi));
        OnPropertyChanged(nameof(Frame));
        OnPropertyChanged(nameof(Exposure));
        OnPropertyChanged(nameof(Details));
        OnPropertyChanged(nameof(ShowPreviewCard));
        OnPropertyChanged(nameof(ShowExposureCard));
        OnPropertyChanged(nameof(ShowDetailsCard));
        OnPropertyChanged(nameof(FileTitle));
        OnPropertyChanged(nameof(FileMeta));
        OnPropertyChanged(nameof(FileExtension));
    }

    #endregion // Private methods
}
