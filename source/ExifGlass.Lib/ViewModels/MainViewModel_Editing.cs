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
using ExifGlass.Core.ExifTool;
using ExifGlass.Core.Metadata;
using ExifGlass.UI.Controls;

namespace ExifGlass.ViewModels;

public sealed partial class MainViewModel
{
    #region Fields and constants

    private const int SUGGESTIONS = 8;

    private Task<bool>? _saving;
    private string? _reloadPath;

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// Whether the Add-tag card shows (DS 6.11: first in the inspector, only while adding).
    /// </summary>
    [ObservableProperty]
    public partial bool IsAddingTag { get; private set; }

    [ObservableProperty]
    public partial string? AddGroup { get; set; }

    [ObservableProperty]
    public partial string? AddName { get; set; }

    [ObservableProperty]
    public partial string? AddValue { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<string> AddSuggestions { get; private set; } = [];

    /// <summary>
    /// Why Add tag is off for the name typed; null while it can be added.
    /// </summary>
    [ObservableProperty]
    public partial string? AddHint { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingSubtitle))]
    public partial bool IsSaving { get; private set; }

    /// <summary>
    /// The groups the Add-tag card offers for this file: IPTC only where ExifTool writes it (plan 4.4).
    /// </summary>
    public IReadOnlyList<string> AddGroups => TagWriteRules.AddableGroupsFor(Document?.Find("File", "FileType")?.Value);

    public int PendingCount => _edits.PendingCount;

    public bool HasPending => !_edits.IsEmpty && Document is not null;

    public string PendingTitle => AppText.PendingChanges(PendingCount);

    public string PendingSubtitle => Document is null ? string.Empty : AppText.SaveWrites(Document.FileName);

    /// <summary>
    /// What Save can't do: a location in the Content Credentials, a tag the AI redaction can't remove (plan P8); null when nothing.
    /// </summary>
    public string? PendingNote
    {
        get
        {
            if (Document is not { } doc) return null;

            var notes = new List<string>(2);
            if (_edits.RemoveGps && !_edits.StripAll && EditTargets.HasManifestGps(doc)) notes.Add(AppText.NOTE_MANIFEST_GPS);
            if (_edits.AiRedaction is { Warnings.Count: > 0 } plan && !_edits.StripAll) notes.AddRange(plan.Warnings);
            return notes.Count == 0 ? null : AppText.FirstOf(notes);
        }
    }

    public bool CanRemoveGps => Document is { } doc && !_edits.RemoveGps && !_edits.StripAll && EditTargets.GpsCount(doc) > 0;

    public string GpsDescription => Document is not { } doc ? AppText.EDIT_GPS_NONE
        : _edits.RemoveGps ? AppText.EDIT_STAGED
        : EditTargets.GpsCount(doc) is var gps and > 0 ? AppText.GpsTags(gps)
        : EditTargets.HasManifestGps(doc) ? AppText.EDIT_GPS_MANIFEST
        : AppText.EDIT_GPS_NONE;

    public bool CanRemoveAi => !_edits.RemoveAi && !_edits.StripAll && RedactionPlan() is { IsEmpty: false };

    public string AiRemovalDescription => _edits.RemoveAi ? AppText.EDIT_STAGED : RedactionPlan() is { IsEmpty: false } ? AppText.EDIT_AI_DESC : AppText.EDIT_AI_NONE;

    public bool CanStripAll => Document is not null && !_edits.StripAll;

    public string StripDescription => _edits.StripAll ? AppText.EDIT_STAGED : AppText.EDIT_STRIP_DESC;

    /// <summary>
    /// The command bar shows the write Save runs (DS 6.12 Write chip).
    /// </summary>
    public bool IsCommandWrite => HasPending && Phase != Services.LoadPhase.Loading;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// D8: before another file opens or the window closes with edits staged, asks Save, Discard or Cancel; true when it may go on.
    /// </summary>
    public async Task<bool> ConfirmLeaveAsync()
    {
        if (_saving is { IsCompleted: false } saving) await saving;
        if (!HasPending || Shell is null || Document is not { } doc) return true;

        var answer = await Shell.AskAsync(new DialogRequest(AppText.UnsavedTitle(doc.FileName), AppText.UnsavedBody(PendingCount),
            AppText.SAVE, AppText.DISCARD, Icon: IconId.Save));

        switch (answer)
        {
            case DialogAnswer.Primary:
                return await SaveAsync(reload: false);

            case DialogAnswer.Secondary:
                DiscardEdits();
                return true;

            default:
                return false;
        }
    }


    /// <summary>
    /// Save: one ExifTool write, then the file read again keeping the view; a failure keeps every staged edit (Appendix B item 3).
    /// </summary>
    public Task<bool> SaveAsync(bool reload = true)
    {
        if (_saving is { IsCompleted: false } running) return running;
        if (!HasPending || Document is null) return Task.FromResult(false);

        // a save that ends before it returns (a write that fails at once) leaves a finished task, which the checks above skip
        return _saving = SaveCoreAsync(reload);
    }

    #endregion // Public methods


    #region Commands

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void OpenAddTag()
    {
        var groups = AddGroups;
        if (AddGroup is null || !groups.Contains(AddGroup)) AddGroup = groups[0];
        AddName = string.Empty;
        AddValue = string.Empty;
        IsEditing = false;
        IsAddingTag = true;
        UpdateAddSuggestions();
        ShowInspector();
    }


    [RelayCommand]
    private void CancelAddTag() => IsAddingTag = false;


    /// <summary>
    /// Add tag: the new row staged as "New", selected and shown (S5).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAddTag))]
    private void AddTag()
    {
        if (AddGroup is not { } group || TagNameToAdd() is not { } name) return;

        var added = new AddedTag(group, name, AddValue ?? string.Empty);
        IsAddingTag = false;
        _hiddenGroups.Remove(group);
        _collapsed.Remove(group);
        Stage(_edits.Add(added));
        Shell?.ShowToast(AppText.TOAST_TAG_STAGED, AppText.TagStaged(group, name));
        _ = RevealAddedAsync("New:" + added.Key);
    }


    [RelayCommand(CanExecute = nameof(CanRemoveGps))]
    private void RemoveGps() => Stage(_edits.WithRemoveGps(true));


    [RelayCommand(CanExecute = nameof(CanRemoveAi))]
    private void RemoveAi()
    {
        if (RedactionPlan() is { IsEmpty: false } plan) Stage(_edits.WithRemoveAi(plan));
    }


    [RelayCommand(CanExecute = nameof(CanStripAll))]
    private void StripAll() => Stage(_edits.WithStripAll(true));


    [RelayCommand]
    private void Save() => _ = SaveAsync();


    [RelayCommand]
    private void Discard() => DiscardEdits();


    /// <summary>
    /// Save binary data… (D12): the tag's bytes through a save dialog, named by what they are.
    /// </summary>
    [RelayCommand]
    private async Task SaveBinaryAsync()
    {
        if (Shell is null || Document is not { } doc || FocusedTag is not { IsBinary: true } view || doc.GetByKey(view.Key) is not { } tag) return;

        byte[]? data;
        try
        {
            data = await _reader.ReadBinaryAsync(doc.SourcePath, tag.WriteName, CancellationToken.None);
        }
        catch (Exception ex) when (IsExifToolFailure(ex))
        {
            data = null;
        }

        if (data is not { Length: > 0 })
        {
            Shell.ShowToast(AppText.TOAST_BINARY_FAILED, tag.ArgumentKey, ToastKind.Danger);
            return;
        }

        var name = BinaryData.SuggestedName(doc.SourcePath, tag.Name, data);
        try
        {
            await using var target = await Shell.PickSaveFileAsync(name, BinaryData.Extension(data), AppText.BINARY_FILES);
            if (target is null) return;

            await target.Stream.WriteAsync(data);
            Shell.ShowToast(AppText.TOAST_SAVED_BINARY, AppText.SavedBinary(target.Name, data.Length));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Shell.ShowToast(AppText.TOAST_EXPORT_FAILED, ex.Message, ToastKind.Danger);
        }
    }

    #endregion // Commands


    #region Private methods

    // every staged change goes through here, so the pending bar, the Edit menu and the command bar follow
    private void Stage(EditSet edits)
    {
        SetEdits(edits);
        Requery();
    }


    private void SetEdits(EditSet edits)
    {
        if (ReferenceEquals(_edits, edits)) return;

        _edits = edits;
        NotifyEdits();
    }


    private void NotifyEdits()
    {
        OnPropertyChanged(nameof(Edits));
        OnPropertyChanged(nameof(PendingCount));
        OnPropertyChanged(nameof(HasPending));
        OnPropertyChanged(nameof(PendingTitle));
        OnPropertyChanged(nameof(PendingSubtitle));
        OnPropertyChanged(nameof(PendingNote));
        OnPropertyChanged(nameof(CanRemoveGps));
        OnPropertyChanged(nameof(GpsDescription));
        OnPropertyChanged(nameof(CanRemoveAi));
        OnPropertyChanged(nameof(AiRemovalDescription));
        OnPropertyChanged(nameof(CanStripAll));
        OnPropertyChanged(nameof(StripDescription));
        OnPropertyChanged(nameof(IsCommandWrite));
        OnPropertyChanged(nameof(AddGroups));
        RemoveGpsCommand.NotifyCanExecuteChanged();
        RemoveAiCommand.NotifyCanExecuteChanged();
        StripAllCommand.NotifyCanExecuteChanged();
        UpdateCommandTokens();
    }


    private void DiscardEdits()
    {
        IsEditing = false;
        Stage(EditSet.Empty);
    }


    private async Task<bool> SaveCoreAsync(bool reload)
    {
        var doc = Document!;
        var edits = _edits;
        var focus = Selection.FocusedKey;
        IsEditing = false;
        IsSaving = true;

        try
        {
            SaveResult result;
            try
            {
                result = await _reader.WriteAsync(doc.SourcePath, edits, CancellationToken.None);
            }
            catch (Exception ex) when (IsExifToolFailure(ex))
            {
                result = new SaveResult(SaveOutcome.Failed, [ex.Message], [], null);
            }

            if (result.Outcome == SaveOutcome.Failed)
            {
                Shell?.ShowToast(AppText.SaveFailed(doc.FileName), result.Errors.FirstOrDefault(), ToastKind.Danger);
                return false;
            }

            if (result.Outcome == SaveOutcome.Unchanged) Shell?.ShowToast(AppText.TOAST_NOTHING_SAVED, AppText.TOAST_NOTHING_SAVED_SUB, ToastKind.Info);
            else Shell?.ShowToast(AppText.Saved(edits.PendingCount), SaveNote(result) ?? AppText.BackupKept(doc.FileName));

            // the read that follows keeps the groups, filters and selection, and drops what was staged
            if (reload)
            {
                _reloadPath = doc.SourcePath;
                await Loader.OpenAsync(doc.SourcePath);
                _reloadPath = null;
                if (focus is not null && focus.StartsWith("New:", StringComparison.Ordinal)) SelectSavedTag(edits, focus);
            }

            if (ReferenceEquals(_edits, edits)) Stage(EditSet.Empty);
            return true;
        }
        finally
        {
            IsSaving = false;
        }
    }


    // ExifTool's "[minor]" warnings are notes about fixes it made; the first other one replaces the backup line (Appendix B item 3)
    private static string? SaveNote(SaveResult result) => result.Warnings.FirstOrDefault(w => !w.StartsWith("[minor]", StringComparison.OrdinalIgnoreCase));


    // a saved "New:XMP:Title" row is the file's own XMP Title tag now
    private void SelectSavedTag(EditSet saved, string newKey)
    {
        if (Document is not { } doc || saved.Added.FirstOrDefault(a => "New:" + a.Key == newKey) is not { } added) return;

        var tag = doc.Tags.FirstOrDefault(t => t.Group0.Equals(added.Group, StringComparison.OrdinalIgnoreCase) && t.Name.Equals(added.Name, StringComparison.OrdinalIgnoreCase));
        if (tag is not null) RowRevealRequested?.Invoke(this, tag.Key);
    }


    private async Task RevealAddedAsync(string key)
    {
        await RunQueryAsync(Loader.Document);
        RowRevealRequested?.Invoke(this, key);
    }


    // the Add-tag card is in the inspector, so adding shows it: docked open, or pushed in below the docking width
    private void ShowInspector()
    {
        if (InspectorMode == PanelMode.Push)
        {
            IsRailPushed = false;
            IsInspectorPushed = true;
        }
        else if (!InspectorOpen)
        {
            InspectorOpen = true;
            _settings.Update(s => s.InspectorOpen = true);
        }
    }


    private AiRedactionPlan? RedactionPlan() => Document?.Ai is { } ai ? AiRedaction.Plan(ai, Document.TagDb) : null;


    private bool CanAddTag() => TagNameToAdd() is not null;


    // the name as ExifTool spells it; without a tag table any name goes, and ExifTool's warning tells
    private string? TagNameToAdd()
    {
        var name = AddName?.Trim();
        if (string.IsNullOrEmpty(name) || AddGroup is not { } group) return null;
        if (Document?.TagDb is not { } tagDb) return name;

        return tagDb.TryFindWritable(group, name, out var exact) ? exact : null;
    }


    private void UpdateAddSuggestions()
    {
        var group = AddGroup ?? TagWriteRules.AddableGroups[0];
        var name = AddName?.Trim() ?? string.Empty;
        var tagDb = Document?.TagDb;

        AddSuggestions = name.Length == 0 || tagDb is null ? TagWriteRules.CommonNames(group) : tagDb.Suggest(group, name, SUGGESTIONS);
        AddHint = name.Length > 0 && tagDb is not null && TagNameToAdd() is null ? AppText.NotWritable(group, name) : null;
        AddTagCommand.NotifyCanExecuteChanged();
    }


    private static bool IsExifToolFailure(Exception ex) => ex is ExifToolException or Services.ExifToolUnavailableException or IOException
        or TimeoutException or UnauthorizedAccessException or InvalidOperationException or ObjectDisposedException;


    partial void OnAddGroupChanged(string? value) => UpdateAddSuggestions();


    partial void OnAddNameChanged(string? value) => UpdateAddSuggestions();

    #endregion // Private methods
}
