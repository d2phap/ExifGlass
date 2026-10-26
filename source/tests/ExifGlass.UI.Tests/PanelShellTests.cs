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
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using ExifGlass.Core.Ai;
using ExifGlass.Core.Editing;
using ExifGlass.Core.Export;
using ExifGlass.Core.Query;
using ExifGlass.Core.Settings;
using ExifGlass.Services;
using ExifGlass.UI.Controls;
using ExifGlass.ViewModels;
using ExifGlass.Views;
using Xunit;

namespace ExifGlass.UI.Tests;

/// <summary>
/// The rail and the inspector in the main window (P6): groups, quick filters, the jump, the selected-tag card, staged edits and the AI card.
/// </summary>
public class PanelShellTests
{
    #region Tests

    [AvaloniaFact]
    public async Task ThePanelsAreMadeWithTheFirstFile()
    {
        using var shell = TestShell.Open(1400);
        Assert.Empty(shell.Window.RailContent.Children);
        Assert.Empty(shell.Window.InspectorContent.Children);

        await shell.ViewModel.OpenFileAsync("a.jpg");
        await shell.WaitForAsync(() => shell.ViewModel.HasDocument);
        Assert.IsType<RailPanel>(Assert.Single(shell.Window.RailContent.Children));
        Assert.IsType<InspectorPanel>(Assert.Single(shell.Window.InspectorContent.Children));
    }


    [AvaloniaFact]
    public async Task TheRailListsEveryGroupAndShowsOrHidesThem()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;

        Assert.Equal(vm.Rows.Groups.Select(g => g.Name), vm.RailGroups.Select(g => g.Name));
        Assert.Equal("7", vm.GroupCountLabel);
        Assert.Equal(AppText.HIDE_ALL, vm.AllGroupsLabel);
        Assert.Equal(7, vm.Segments.Count);

        // a click on the checkbox area hides the group; the same item stays, so its row keeps its state
        var exif = vm.RailGroups.Single(g => g.Name == "EXIF");
        var row = Row(shell, "EXIF");
        var at = row.TranslatePoint(new Point(10, 16), shell.Window)!.Value;
        shell.Window.MouseDown(at, MouseButton.Left);
        shell.Window.MouseUp(at, MouseButton.Left);
        await shell.WaitForAsync(() => !exif.IsShown);
        Assert.Same(exif, vm.RailGroups.Single(g => g.Name == "EXIF"));
        Assert.Equal("6/7", vm.GroupCountLabel);
        Assert.Equal(AppText.SHOW_ALL, vm.AllGroupsLabel);
        Assert.False(vm.Segments.Single(s => s.Group == "EXIF").IsShown);
        Assert.DoesNotContain(vm.Rows.RowsInView, r => r.Group == "EXIF");

        vm.ToggleAllGroupsCommand.Execute(null);
        await shell.WaitForAsync(() => vm.RailGroups.All(g => g.IsShown));

        vm.ToggleAllGroupsCommand.Execute(null);
        await shell.WaitForAsync(() => vm.RailGroups.All(g => !g.IsShown));
        Assert.Equal("0/7", vm.GroupCountLabel);
        Assert.True(vm.ShowNoMatch);
    }


    [AvaloniaFact]
    public async Task QuickFiltersCountAndCombineWithTheGroupCounts()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;
        var ids = vm.QuickFilters.Single(f => f.Filter == QuickFilter.HasTagId);
        Assert.Equal(vm.Rows.FilterCounts[QuickFilter.HasTagId].ToString(), ids.Count);

        vm.ToggleQuickFilterCommand.Execute(ids);
        await shell.WaitForAsync(() => ids.IsOn);
        Assert.Contains(QuickFilter.HasTagId, vm.Rows.Query.Filters);
        var exif = vm.Rows.Groups.Single(g => g.Name == "EXIF");
        Assert.Equal($"{exif.Visible}/{exif.Total}", vm.RailGroups.Single(g => g.Name == "EXIF").Count);

        // with a chip on, a chip that would empty the view is off (owner after G7); the test file has no dates or GPS
        var gps = vm.QuickFilters.Single(f => f.Filter == QuickFilter.Gps);
        Assert.Equal("0", gps.Count);
        Assert.False(gps.IsAvailable);

        vm.ToggleQuickFilterCommand.Execute(ids);
        await shell.WaitForAsync(() => !ids.IsOn);
        Assert.Equal(exif.Total.ToString(), vm.RailGroups.Single(g => g.Name == "EXIF").Count);
        Assert.True(gps.IsAvailable);
    }


    [AvaloniaFact]
    public async Task QuickFiltersCountWithinTheShownGroups()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;
        var ids = vm.QuickFilters.Single(f => f.Filter == QuickFilter.HasTagId);
        var gps = vm.QuickFilters.Single(f => f.Filter == QuickFilter.Gps);
        var total = vm.Rows.FilterCounts[QuickFilter.HasTagId];
        int IdsIn(string group) => vm.Rows.RowsInView.Count(r => r.Group == group && QueryEngine.Passes(r, QuickFilter.HasTagId));
        var most = vm.RailGroups.Select(g => g.Name).MaxBy(IdsIn)!;
        var left = total - IdsIn(most);
        Assert.True(IdsIn(most) > 0);
        Assert.True(gps.IsAvailable);

        // a hidden group narrows the view as a chip does: the chips count what's left, and one at 0 is off (item 86)
        vm.SetGroupHidden(most, true);
        await shell.WaitForAsync(() => ids.Count == left.ToString());
        Assert.Equal(left > 0, ids.IsAvailable);
        Assert.False(gps.IsAvailable);
        Assert.Equal(total, vm.Rows.FilterCounts[QuickFilter.HasTagId]);

        foreach (var group in vm.RailGroups.ToList()) vm.SetGroupHidden(group.Name, true);
        await shell.WaitForAsync(() => vm.RailGroups.All(g => !g.IsShown));
        Assert.All(vm.QuickFilters, f => Assert.Equal("0", f.Count));
        Assert.DoesNotContain(vm.QuickFilters, f => f.IsAvailable);

        vm.ToggleAllGroupsCommand.Execute(null);
        await shell.WaitForAsync(() => ids.Count == total.ToString());
        Assert.True(ids.IsAvailable);
        Assert.True(gps.IsAvailable);
    }


    [AvaloniaFact]
    public async Task HiddenGroupsAndQuickFiltersStayForTheNextLaunch()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;
        vm.SetGroupHidden("XMP", true);
        vm.ToggleFilter(QuickFilter.HasTagId);
        await shell.WaitForAsync(() => vm.Rows.Query.Filters.Contains(QuickFilter.HasTagId) && vm.Rows.Query.HiddenGroups.Contains("XMP"));
        await shell.Settings.FlushAsync();

        var reloaded = new SettingsStore(shell.Folder);
        reloaded.Load();
        Assert.Equal(["XMP"], reloaded.Current.HiddenGroups);
        Assert.Equal(["HasTagId"], reloaded.Current.QuickFilters);

        var next = new MainViewModel(reloaded, shell.Reader);
        await next.OpenFileAsync("a.jpg");
        await shell.WaitForAsync(() => next.HasDocument);
        Assert.Contains("XMP", next.Rows.Query.HiddenGroups);
        Assert.True(next.QuickFilters.Single(f => f.Filter == QuickFilter.HasTagId).IsOn);
        Assert.False(next.RailGroups.Single(g => g.Name == "XMP").IsShown);

        // Clear forgets them too
        next.ClearFiltersCommand.Execute(null);
        await shell.WaitForAsync(() => next.Rows.Query.Filters.Count == 0);
        Assert.Empty(reloaded.Current.HiddenGroups);
        Assert.Empty(reloaded.Current.QuickFilters);
        await reloaded.FlushAsync();
    }


    [AvaloniaFact]
    public async Task AJumpShowsAndExpandsTheGroupAndClosesThePushedRail()
    {
        using var shell = await TestShell.OpenWithFileAsync(480);
        var vm = shell.ViewModel;
        vm.SetGroupHidden("XMP", true);
        vm.ToggleGroup("XMP");
        vm.ToggleRailCommand.Execute(null);
        await shell.WaitForAsync(() => vm.IsRailPushed && vm.Rows.Groups.Single(g => g.Name == "XMP").IsHidden);

        await vm.JumpToGroupAsync("XMP");
        shell.Settle(200);

        var xmp = vm.Rows.Groups.Single(g => g.Name == "XMP");
        Assert.False(xmp.IsHidden);
        Assert.False(xmp.IsCollapsed);
        Assert.False(vm.IsRailPushed);
        Assert.Equal("XMP", shell.Window.Table.Table.ActiveGroup);
    }


    [AvaloniaFact]
    public async Task TheInspectorFollowsTheSelectionAndCloseLetsGo()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;
        var key = Key(vm, "Tag 2");

        vm.Selection.SelectCell(key, TableColumns.Value);
        Assert.NotNull(vm.FocusedTag);
        Assert.Equal("Tag 2", vm.FocusedTag.Name);
        Assert.Equal("EXIF", vm.FocusedTag.Group);
        Assert.Equal("-IFD0:Tag2", vm.FocusedTag.ArgumentKey);
        Assert.False(vm.FocusedTag.IsReadOnly);
        shell.Settle();
        Assert.True(Inspector(shell).TagCard.IsVisible);

        // Ctrl-click a row: the card follows the last row selected
        var other = Key(vm, "Tag 3");
        vm.Selection.ToggleRow(other);
        Assert.Equal(other, vm.FocusedTag?.Key);

        vm.Selection.SelectCell(key, TableColumns.Value);
        vm.CloseFocusCommand.Execute(null);
        Assert.Null(vm.FocusedTag);
        Assert.True(vm.Selection.HasCell);
    }


    [AvaloniaFact]
    public async Task EditsAreStagedRevertedAndEscCancelsTheEditFirst()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;
        var key = Key(vm, "Tag 2");
        vm.Selection.SelectCell(key, TableColumns.Value);

        vm.StartEditCommand.Execute(null);
        Assert.True(vm.IsEditing);
        Assert.Equal("Value 2", vm.EditText);

        // Esc leaves the edit before the window's chain reaches the cell
        Assert.True(vm.Escape());
        Assert.False(vm.IsEditing);
        Assert.True(vm.Selection.HasCell);

        vm.StartEditCommand.Execute(null);
        vm.EditText = "Changed";
        vm.ApplyEditCommand.Execute(null);
        await shell.WaitForAsync(() => vm.FocusedTag?.IsEdited == true);
        Assert.False(vm.IsEditing);
        Assert.Equal("Changed", vm.FocusedTag!.Value);
        Assert.Equal("Value 2", vm.FocusedTag.OriginalValue);
        Assert.Equal(EditState.Edited, vm.Rows.RowsInView.Single(r => r.Key == key).EditState);

        vm.RevertFocusCommand.Execute(null);
        await shell.WaitForAsync(() => vm.FocusedTag?.IsEdited == false);
        Assert.True(vm.Edits.IsEmpty);

        // applying the file's own value stages nothing
        vm.StartEditCommand.Execute(null);
        vm.ApplyEditCommand.Execute(null);
        shell.Settle();
        Assert.True(vm.Edits.IsEmpty);
    }


    [AvaloniaFact]
    public async Task DeleteTurnsIntoUndoAndANewReadDropsWhatWasStaged()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;
        vm.Selection.SelectCell(Key(vm, "Tag 2"), TableColumns.Value);

        vm.ToggleDeleteCommand.Execute(null);
        await shell.WaitForAsync(() => vm.FocusedTag?.IsDeleted == true);

        vm.ToggleDeleteCommand.Execute(null);
        await shell.WaitForAsync(() => vm.FocusedTag?.IsDeleted == false);

        vm.ToggleDeleteCommand.Execute(null);
        await shell.WaitForAsync(() => vm.FocusedTag?.IsDeleted == true);

        // another file asks first (D8); the window's own dialog, answered with Discard
        var open = vm.OpenFileAsync("b.jpg");
        await shell.WaitForAsync(() => shell.Window.Dialogs.IsOpen);
        shell.Settle();
        var discard = shell.Window.Dialogs.GetVisualDescendants().OfType<Avalonia.Controls.Button>().Single(b => b.Name == "PART_Secondary");
        Assert.Equal("Discard", discard.Content);
        discard.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
        await open;
        await shell.WaitForAsync(() => vm.Document?.FileName == "b.jpg");
        Assert.True(vm.Edits.IsEmpty);
        Assert.Null(vm.FocusedTag);
    }


    [AvaloniaFact]
    public async Task ReadOnlyGroupsCannotBeEditedOrDeleted()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;
        vm.Selection.SelectCell(Key(vm, "Tag 1"), TableColumns.Value);

        Assert.Equal("File", vm.FocusedTag?.Group);
        Assert.True(vm.FocusedTag?.IsReadOnly);
        Assert.False(vm.StartEditCommand.CanExecute(null));
        Assert.False(vm.ToggleDeleteCommand.CanExecute(null));
    }


    [AvaloniaFact]
    public async Task TheAiCardShowsOnlyForAFileMadeWithAi()
    {
        using var shell = TestShell.Open(1400);
        var vm = shell.ViewModel;
        await vm.OpenFileAsync("a.png");
        await shell.WaitForAsync(() => vm.HasDocument);
        Assert.False(vm.HasAi);

        shell.Reader.Ai = new AiGenerationInfo([new AiSourceInfo
        {
            Source = AiSourceKind.A1111,
            Generator = "AUTOMATIC1111",
            GeneratorVersion = "1.10.1",
            Kind = AiKind.Generated,
            Prompts = [new AiPrompt("a lighthouse", AiPromptRole.Positive)],
        }]);
        await vm.OpenFileAsync("b.png");
        await shell.WaitForAsync(() => vm.HasAi);
        shell.Settle();

        var card = Inspector(shell).AiCard;
        Assert.True(card.IsVisible);
        Assert.Equal("AUTOMATIC1111 1.10.1", card.Generator);
        Assert.Equal("a lighthouse", card.Prompt);
        Assert.Contains(vm.RailGroups, g => g.IsAi);
        Assert.DoesNotContain(vm.Segments, s => s.Group == Core.Metadata.GroupInfo.AI_GROUP);
    }


    [AvaloniaFact]
    public async Task ACardWithNothingForTheFileHides()
    {
        using var shell = TestShell.Open(1400);
        var vm = shell.ViewModel;
        var ai = new TaskCompletionSource<AiGenerationInfo?>(TaskCreationOptions.RunContinuationsAsynchronously);
        shell.Reader.Maker = TestDocuments.MakeRaw;
        shell.Reader.AiHold = ai;
        var open = vm.OpenFileAsync("a.arw");
        await shell.WaitForAsync(() => vm.HasDocument);
        shell.Settle();

        // its embedded preview waits behind the AI read: the Preview card holds its place meanwhile
        var inspector = Inspector(shell);
        Assert.True(PreviewService.MayHavePreview(vm.Document!));
        Assert.True(inspector.FrameCard.IsVisible);
        Assert.False(inspector.ExposureCard.IsVisible);
        Assert.False(inspector.DetailsCard.IsVisible);

        // none came, so the empty card goes (owner at G7)
        ai.SetResult(null);
        await open;
        shell.Settle();
        Assert.False(vm.ShowPreviewCard);
        Assert.False(inspector.FrameCard.IsVisible);
    }


    [AvaloniaFact]
    public async Task EnterOnARowShowsTheInspectorAndFocusesItsCard()
    {
        using var shell = await TestShell.OpenWithFileAsync(900);
        var vm = shell.ViewModel;
        Assert.False(vm.IsInspectorShown);

        var table = shell.Window.Table.Table;
        table.FocusTable();
        vm.Selection.SelectCell(Key(vm, "Tag 2"), TableColumns.Value);
        shell.Window.KeyPress(Avalonia.Input.Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        shell.Settle(300);

        Assert.True(vm.IsInspectorPushed);
        var focused = shell.Window.FocusManager?.GetFocusedElement() as Avalonia.Visual;
        Assert.NotNull(focused);
        Assert.Contains(Inspector(shell).TagCard, focused.GetVisualAncestors());
    }

    #endregion // Tests


    #region Private methods

    private static string Key(MainViewModel vm, string name) => vm.Rows.RowsInView.Single(r => r.Name == name).Key;


    private static InspectorPanel Inspector(TestShell shell) => (InspectorPanel)shell.Window.InspectorContent.Children[0];


    private static RailGroupRow Row(TestShell shell, string group)
    {
        shell.Settle();
        return shell.Window.RailContent.GetVisualDescendants().OfType<RailGroupRow>().Single(r => r.Group == group);
    }

    #endregion // Private methods
}
