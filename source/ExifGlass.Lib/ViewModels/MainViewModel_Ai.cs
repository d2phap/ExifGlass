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
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExifGlass.Core.Ai;
using ExifGlass.Core.Export;
using ExifGlass.Core.ExifTool;
using ExifGlass.Core.Metadata;
using ExifGlass.Core.Query;
using ExifGlass.UI.Controls;
using System.Text;

namespace ExifGlass.ViewModels;

/// <summary>
/// What Export saves from the AI data (P7): the A1111 text, the normalized JSON, the workflow as embedded.
/// </summary>
public enum AiExportKind
{
    Parameters,
    Json,
    Workflow,
}


public sealed partial class MainViewModel
{
    #region Fields and constants

    // a typical AI read takes a few ms (S9); the skeleton shows only for a slow one, so it never flashes
    private static readonly TimeSpan AI_LOADING_DELAY = TimeSpan.FromMilliseconds(150);

    private static readonly UTF8Encoding UTF8_NO_BOM = new(false);

    private DispatcherTimer? _aiLoadingTimer;

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// The AI read is slow for a file whose table shows AI tags: the AI card pulses until it ends (AI addendum state-ai-loading).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowAiCard))]
    public partial bool IsAiLoading { get; private set; }

    /// <summary>
    /// The selected AI field card (AI addendum question 4): the AI row the table's selection is on.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFocusedAiField))]
    public partial AiFieldView? FocusedAiField { get; private set; }

    public bool HasFocusedAiField => FocusedAiField is not null;

    public bool ShowAiCard => HasAi || IsAiLoading;

    /// <summary>
    /// An LLM character card: its own neutral card, not AI generation (AI addendum question 9).
    /// </summary>
    public AiSourceInfo? CharacterSource => Document?.Ai?.Sources.FirstOrDefault(s => s.Source == AiSourceKind.CharacterCard);

    public bool HasCharacterCard => CharacterSource is not null;

    /// <summary>
    /// The rail file card's AI badge (question 13): the generator, "Character card", or nothing.
    /// </summary>
    public string? AiBadge => AiInfo is { } ai ? AiCard.GeneratorLabel(ai) : HasCharacterCard ? AiGenerationInfo.KindLabel(AiKind.CharacterCard) : null;

    public AiKind AiBadgeKind => AiInfo?.Kind ?? (HasCharacterCard ? AiKind.CharacterCard : AiKind.None);

    public string? AiBadgeTip => AiBadge is null ? null : $"{AiGenerationInfo.KindLabel(AiBadgeKind)} · {AiInfo?.Generator ?? AiBadge}\n{UI.UiText.JUMP_TO_AI}";

    /// <summary>
    /// R19: a custom ExifTool older than the AI read needs, said in the AI card; the bundled one always reads everything.
    /// </summary>
    public string? ExifToolNote
    {
        get
        {
            if (_metadata?.Location is not { Source: not ExifToolSource.Bundled } || Document?.ExifToolVersion is not { } version) return null;

            var missing = ExifToolVersion.MissingAiFeatures(version);
            return missing.Count == 0 ? null : AppText.OldExifTool(version.Trim(), missing);
        }
    }

    /// <summary>
    /// The AI part of the table summary, in Accent2 (question 2): "· 23 AI fields", "· 1 of 23 AI fields".
    /// </summary>
    public string? AiSummary => Rows.AiSummary.Length == 0 ? null : "· " + Rows.AiSummary;

    public bool HasAiSummary => AiSummary is not null;

    public bool HasWorkflow => AiCard.SavedWorkflow(Document?.Ai) is not null;

    public bool HasGenerationParameters => AiInfo?.Primary is { IsGeneration: true };

    #endregion // Properties


    #region Events

    /// <summary>
    /// Asks the table to glide to a row and select its value: Show source tag.
    /// </summary>
    public event EventHandler<string>? RowRevealRequested;

    #endregion // Events


    #region Public methods

    /// <summary>
    /// Show source tag (D22): the tag an AI field came from, shown and selected in its group, the filters that hid it cleared.
    /// </summary>
    public async Task ShowSourceTagAsync(string sourceKey)
    {
        if (Loader.Document?.FindSourceTag(sourceKey) is not { } tag) return;

        var changed = _hiddenGroups.Remove(tag.Group0) | _collapsed.Remove(tag.Group0);
        if (FindRow(tag.Key) is null)
        {
            changed |= _filters.Count > 0;
            _filters.Clear();
            if (SearchText.Length > 0) SearchText = string.Empty;
        }

        IsRailPushed = false;
        if (changed) await RunQueryAsync(Loader.Document);

        RowRevealRequested?.Invoke(this, tag.Key);
    }

    #endregion // Public methods


    #region Commands

    [RelayCommand]
    private Task JumpToAi() => JumpToGroupAsync(GroupInfo.AI_GROUP);


    /// <summary>
    /// Every copy, save and jump of the AI surfaces, each with its toast (Appendix B item 16).
    /// </summary>
    [RelayCommand]
    private async Task AiActionAsync(AiActionRequest? request)
    {
        if (request is null || Shell is null) return;

        var ai = Document?.Ai;
        switch (request.Action)
        {
            case AiAction.CopyPrompt when (request.Text ?? AiInfo?.Prompt?.Text) is { } prompt:
                await CopyAndToastAsync(prompt, AppText.TOAST_COPIED_PROMPT, AppText.Characters(prompt.Length));
                break;

            case AiAction.CopyNegativePrompt when (request.Text ?? AiInfo?.NegativePrompt?.Text) is { } negative:
                await CopyAndToastAsync(negative, AppText.TOAST_COPIED_NEGATIVE, AppText.Characters(negative.Length));
                break;

            case AiAction.CopyText when request.Text is { } text:
                await CopyAndToastAsync(text, AppText.CopiedField(request.Label), AppText.Clip(text));
                break;

            case AiAction.CopyTagKey when request.Text is { } key:
                await CopyAndToastAsync(key, AppText.TOAST_COPIED_KEY, key);
                break;

            case AiAction.CopyGenerationParameters:
                await CopyGenerationParametersAsync();
                break;

            case AiAction.CopyJson when ai is not null:
                var json = await Task.Run(() => AiExport.ToJson(ai));
                await CopyAndToastAsync(json, AppText.TOAST_COPIED_AI_JSON, AppText.AiFields(Rows.AiTotal));
                break;

            case AiAction.CopyWorkflowJson when AiCard.SavedWorkflow(ai) is { } workflow:
                await CopyAndToastAsync(workflow.Json, AppText.TOAST_COPIED_WORKFLOW, $"{workflow.FormatLabel} · {AiRows.FormatSize(workflow.Size)}");
                break;

            case AiAction.SaveWorkflow:
                await ExportAiAsync(AiExportKind.Workflow);
                break;

            case AiAction.ShowSourceTag when request.Key is { } sourceKey:
                await ShowSourceTagAsync(sourceKey);
                break;

            // the AI chip shows the AI fields and every tag they were read from
            case AiAction.ShowSourceTags:
                if (_filters.Add(QuickFilter.Ai)) Requery();
                break;

            case AiAction.JumpToAiGroup:
                await JumpToGroupAsync(GroupInfo.AI_GROUP);
                break;

            case AiAction.UseBundledExifTool:
                await UseBundledExifToolAsync();
                break;

            case AiAction.OpenSettings:
                Shell.ShowSettings();
                break;

            case AiAction.CopyCharacterJson when CharacterSource?.Character is { } card:
                await CopyAndToastAsync(card.Json, AppText.TOAST_COPIED_CHARACTER, card.Name ?? AppText.Characters(card.Json.Length));
                break;

            case AiAction.SaveCharacterJson when CharacterSource?.Character is { } card:
                await SaveTextAsync(FileStem() + "_character.json", card.Json, AppText.TOAST_SAVED_CHARACTER);
                break;
        }
    }


    /// <summary>
    /// Export menu's AI items (P7 Exports): the generation parameters as A1111 text, the AI data as JSON, the workflow exactly as embedded.
    /// </summary>
    [RelayCommand]
    private async Task ExportAiAsync(AiExportKind kind)
    {
        if (Document?.Ai is not { } ai) return;

        switch (kind)
        {
            case AiExportKind.Parameters when ai.Primary is { IsGeneration: true } primary:
                await SaveTextAsync(FileStem() + "_parameters.txt", AiExport.ToA1111(primary), AppText.TOAST_SAVED_PARAMETERS);
                break;

            case AiExportKind.Json:
                await SaveTextAsync(FileStem() + "_ai.json", await Task.Run(() => AiExport.ToJson(ai)), AppText.TOAST_SAVED_AI_JSON);
                break;

            case AiExportKind.Workflow when AiCard.SavedWorkflow(ai) is { } workflow:
                await SaveTextAsync(FileStem() + "_workflow.json", workflow.Json, AppText.TOAST_SAVED_WORKFLOW);
                break;
        }
    }

    #endregion // Commands


    #region Private methods

    // UTF-8 with no BOM: a workflow loads back into ComfyUI or InvokeAI byte for byte as embedded
    private async Task SaveTextAsync(string name, string text, string toast)
    {
        if (Shell is null) return;

        var format = name.EndsWith(".txt", StringComparison.Ordinal) ? ExportFormat.PlainText : ExportFormat.Json;
        try
        {
            await using var target = await Shell.PickSaveFileAsync(name, format);
            if (target is null) return;

            var bytes = await Task.Run(() => UTF8_NO_BOM.GetBytes(text));
            await target.Stream.WriteAsync(bytes);
            Shell.ShowToast(toast, target.Name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Shell.ShowToast(AppText.TOAST_EXPORT_FAILED, ex.Message, ToastKind.Danger);
        }
    }


    private string FileStem() => Path.GetFileNameWithoutExtension(Document?.FileName ?? "metadata");


    // the AI card's "Use the bundled ExifTool" (R19): the custom path goes, ExifTool restarts and the file is read again
    private async Task UseBundledExifToolAsync()
    {
        if (_metadata is null) return;

        _settings.Update(s => s.ExifToolPath = string.Empty);
        await _metadata.RestartAsync();
        if (Document?.SourcePath is { } path) await OpenFileAsync(path);
    }


    private void UpdateAiFocus(ViewRow? row)
    {
        if (row is not { IsAi: true, Ai: { } ai })
        {
            FocusedAiField = null;
            return;
        }

        var embeddings = Document?.Ai?.Models.Where(m => m.Kind == AiModelKind.Embedding).Select(m => m.Name).ToList() ?? [];
        var view = new AiFieldView(row.Key, ai.Label, ai.Value, ai.SourceKey, ai.ParsedAs, ai.Kind == AiFieldKind.Prompt, embeddings);
        if (view.Key != FocusedAiField?.Key || view.Value != FocusedAiField.Value) FocusedAiField = view;
    }


    // the skeleton waits a moment, so a fast AI read never shows it
    private void UpdateAiLoading()
    {
        var waiting = Loader.IsReadingAi && Loader.Document is { Ai: null, MayHoldAi: true };
        if (!waiting)
        {
            _aiLoadingTimer?.Stop();
            IsAiLoading = false;
            return;
        }

        if (IsAiLoading || _aiLoadingTimer?.IsEnabled == true) return;

        _aiLoadingTimer ??= new DispatcherTimer(AI_LOADING_DELAY, DispatcherPriority.Background, (_, _) =>
        {
            _aiLoadingTimer!.Stop();
            IsAiLoading = Loader.IsReadingAi && Loader.Document is { Ai: null, MayHoldAi: true };
        });
        _aiLoadingTimer.Start();
    }


    private void NotifyAi()
    {
        OnPropertyChanged(nameof(ShowAiCard));
        OnPropertyChanged(nameof(CharacterSource));
        OnPropertyChanged(nameof(HasCharacterCard));
        OnPropertyChanged(nameof(AiBadge));
        OnPropertyChanged(nameof(AiBadgeKind));
        OnPropertyChanged(nameof(AiBadgeTip));
        OnPropertyChanged(nameof(ExifToolNote));
        OnPropertyChanged(nameof(HasWorkflow));
        OnPropertyChanged(nameof(HasGenerationParameters));
    }

    #endregion // Private methods
}
