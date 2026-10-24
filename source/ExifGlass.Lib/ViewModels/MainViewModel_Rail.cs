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
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ExifGlass.Core.Metadata;
using ExifGlass.Core.Query;
using ExifGlass.UI;
using ExifGlass.UI.Controls;
using System.Globalization;

namespace ExifGlass.ViewModels;

/// <summary>
/// One group in the rail (DS 6.10); kept across queries so a row under the pointer keeps its state.
/// </summary>
public sealed partial class RailGroupItem : ObservableObject
{
    #region Constructors

    public RailGroupItem(string name)
    {
        Name = name;
        Label = IsAi ? UiText.AI_GROUP : name;
    }

    #endregion // Constructors


    #region Properties

    public string Name { get; }

    public string Label { get; }

    public bool IsAi => Name == GroupInfo.AI_GROUP;

    /// <summary>
    /// "55", or "12/55" while a search or quick filter runs (Proto).
    /// </summary>
    [ObservableProperty]
    public partial string Count { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleTip))]
    public partial bool IsShown { get; set; } = true;

    public string ToggleTip => IsShown ? AppText.HideGroup(Label) : AppText.ShowGroup(Label);

    public string JumpTip => AppText.JumpTo(Label);

    #endregion // Properties
}


/// <summary>
/// One rail quick filter (DS 6.6): its chip, live count and state.
/// </summary>
public sealed partial class QuickFilterItem(QuickFilter filter, string label, IconId icon, string? tip = null) : ObservableObject
{
    #region Properties

    public QuickFilter Filter { get; } = filter;

    public string Label { get; } = label;

    public IconId Icon { get; } = icon;

    public string? Tip { get; } = tip;

    /// <summary>
    /// The AI chips take Accent2 when on (AI addendum).
    /// </summary>
    public bool IsAi => Filter is QuickFilter.Ai or QuickFilter.AiPrompts or QuickFilter.AiModels or QuickFilter.AiProvenance;

    /// <summary>
    /// Prompts, Models and Provenance show only for a file with AI data (question 3).
    /// </summary>
    public bool IsOnlyForAi => Filter is QuickFilter.AiPrompts or QuickFilter.AiModels or QuickFilter.AiProvenance;

    [ObservableProperty]
    public partial string? Count { get; set; }

    [ObservableProperty]
    public partial bool IsOn { get; set; }

    /// <summary>
    /// Off without a file, and for an AI chip with nothing to show.
    /// </summary>
    [ObservableProperty]
    public partial bool IsAvailable { get; set; }

    #endregion // Properties
}


public sealed partial class MainViewModel
{
    #region Properties

    /// <summary>
    /// The rail's groups in table order (the AI group first); a new list only when the groups change.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<RailGroupItem> RailGroups { get; private set; } = [];

    /// <summary>
    /// The groups overline's badge: "8", or "5/8" while some are hidden (Proto).
    /// </summary>
    [ObservableProperty]
    public partial string GroupCountLabel { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string AllGroupsLabel { get; private set; } = AppText.HIDE_ALL;

    /// <summary>
    /// The composition bar: every real group by tag count, hidden ones at zero; the AI group is left out (P7 question 2).
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<GroupSegment> Segments { get; private set; } = [];

    /// <summary>
    /// The AI chips, first and on a line of their own (AI addendum question 3).
    /// </summary>
    public IReadOnlyList<QuickFilterItem> AiQuickFilters { get; } =
    [
        new(QuickFilter.Ai, AppText.FILTER_AI, IconId.Sparkle, AppText.FILTER_AI_TIP),
        new(QuickFilter.AiPrompts, AppText.FILTER_PROMPTS, IconId.AiPrompt, AppText.FILTER_PROMPTS_TIP),
        new(QuickFilter.AiModels, AppText.FILTER_MODELS, IconId.AiModel, AppText.FILTER_MODELS_TIP),
        new(QuickFilter.AiProvenance, AppText.FILTER_PROVENANCE, IconId.AiShieldCheck, AppText.FILTER_PROVENANCE_TIP),
    ];

    /// <summary>
    /// The AI chips the rail shows: Prompts, Models and Provenance only for a file with AI data; a hidden chip would still cost its heap (R24).
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<QuickFilterItem> ShownAiFilters { get; private set; } = [];

    public IReadOnlyList<QuickFilterItem> QuickFilters { get; } =
    [
        new(QuickFilter.DateTime, AppText.FILTER_DATES, IconId.Clock),
        new(QuickFilter.Gps, AppText.FILTER_GPS, IconId.Location),
        new(QuickFilter.Converted, AppText.FILTER_CONVERTED, IconId.ArrowSync),
        new(QuickFilter.HasTagId, AppText.FILTER_TAG_ID, IconId.Key),
    ];

    public string FileTitle => Document?.FileName ?? (IsLoading ? AppText.FILE_READING : AppText.FILE_NONE);

    public string FileMeta => Document?.Derived.MetaLine ?? (IsLoading ? AppText.FILE_READING_SUB : AppText.FILE_NONE_SUB);

    /// <summary>
    /// The thumbnail's label until the preview comes: the file's extension (Proto).
    /// </summary>
    public string? FileExtension
    {
        get
        {
            if (Document is not { } doc) return null;

            var ext = Path.GetExtension(doc.FileName).TrimStart('.');
            return (ext.Length > 0 ? ext : doc.Find("File", "FileType")?.Value ?? string.Empty).ToUpperInvariant();
        }
    }

    public Bitmap? Preview => Loader.Preview;

    #endregion // Properties


    #region Events

    /// <summary>
    /// Asks the table to glide to a group's header once its rows are on screen.
    /// </summary>
    public event EventHandler<string>? GroupJumpRequested;

    #endregion // Events


    #region Public methods

    /// <summary>
    /// The rail row's click: shows and expands the group, then the table glides to it; a pushed rail closes (DS 2.1).
    /// </summary>
    public async Task JumpToGroupAsync(string group)
    {
        var changed = _hiddenGroups.Remove(group) | _collapsed.Remove(group);
        IsRailPushed = false;
        if (changed) await RunQueryAsync(Loader.Document);

        GroupJumpRequested?.Invoke(this, group);
    }

    #endregion // Public methods


    #region Commands

    [RelayCommand]
    private void ToggleGroupShown(RailGroupItem item) => SetGroupHidden(item.Name, item.IsShown);


    [RelayCommand]
    private Task JumpToGroup(RailGroupItem item) => JumpToGroupAsync(item.Name);


    /// <summary>
    /// Hide all, or Show all while any group is hidden (Proto).
    /// </summary>
    [RelayCommand]
    private void ToggleAllGroups()
    {
        if (_hiddenGroups.Count > 0) _hiddenGroups.Clear();
        else
        {
            foreach (var group in Rows.Groups) _hiddenGroups.Add(group.Name);
        }

        Requery();
    }


    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void ToggleQuickFilter(QuickFilterItem item) => ToggleFilter(item.Filter);

    #endregion // Commands


    #region Private methods

    private void UpdateRail()
    {
        var groups = Rows.Groups;
        var same = groups.Count == RailGroups.Count;
        for (var i = 0; same && i < groups.Count; i++) same = groups[i].Name == RailGroups[i].Name;

        var items = same ? RailGroups : [.. groups.Select(g => new RailGroupItem(g.Name))];
        var searching = Rows.Query.Search.Trim().Length > 0 || Rows.Query.Filters.Count > 0;
        var segments = new List<GroupSegment>(groups.Count);
        var shown = 0;
        for (var i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            items[i].IsShown = !g.IsHidden;
            items[i].Count = searching
                ? string.Create(CultureInfo.InvariantCulture, $"{g.Visible}/{g.Total}")
                : g.Total.ToString(CultureInfo.InvariantCulture);
            if (!g.IsHidden) shown++;
            if (!items[i].IsAi) segments.Add(new GroupSegment(g.Name, g.Total, !g.IsHidden));
        }

        if (!same) RailGroups = items;
        GroupCountLabel = groups.Count == 0 ? string.Empty
            : shown < groups.Count ? string.Create(CultureInfo.InvariantCulture, $"{shown}/{groups.Count}")
            : groups.Count.ToString(CultureInfo.InvariantCulture);
        AllGroupsLabel = shown < groups.Count ? AppText.SHOW_ALL : AppText.HIDE_ALL;
        if (!segments.SequenceEqual(Segments)) Segments = segments;

        var filters = Rows.Query.Filters;
        var hasAi = Rows.AiTotal > 0;
        foreach (var chip in AiQuickFilters.Concat(QuickFilters))
        {
            // with a chip on, the others count within it, and one that would empty the view is off (owner after G7)
            var count = Rows.ChipCounts.GetValueOrDefault(chip.Filter);
            chip.IsOn = filters.Contains(chip.Filter);
            chip.Count = Document is null ? null : count.ToString(CultureInfo.InvariantCulture);
            chip.IsAvailable = Document is not null && (chip.IsOn || count > 0 || (filters.Count == 0 && !chip.IsOnlyForAi));
        }

        var aiChips = hasAi ? AiQuickFilters.Count : 1;
        if (ShownAiFilters.Count != aiChips) ShownAiFilters = [.. AiQuickFilters.Take(aiChips)];
    }

    #endregion // Private methods
}
