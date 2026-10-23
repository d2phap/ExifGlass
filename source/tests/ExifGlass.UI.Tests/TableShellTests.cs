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
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using ExifGlass.Core.Export;
using ExifGlass.Core.Query;
using ExifGlass.Core.Settings;
using ExifGlass.UI.Controls;
using ExifGlass.ViewModels;
using Xunit;

namespace ExifGlass.UI.Tests;

/// <summary>
/// The table inside the main window (P5): toolbar, sort, groups, columns, chips, the Esc chain and the window's Ctrl+A and Ctrl+C.
/// </summary>
public class TableShellTests
{
    #region Tests

    [AvaloniaFact]
    public async Task TheHeaderCyclesAscDescAndFileOrder()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;

        vm.CycleSort(SortColumn.TagName);
        await shell.WaitForAsync(() => vm.Rows.Query.SortDirection == SortDirection.Ascending);
        Assert.Equal(SortColumn.TagName, vm.SortColumn);
        AssertSortedWithinGroups(vm.Rows, descending: false);

        vm.CycleSort(SortColumn.TagName);
        await shell.WaitForAsync(() => vm.Rows.Query.SortDirection == SortDirection.Descending);
        AssertSortedWithinGroups(vm.Rows, descending: true);

        vm.CycleSort(SortColumn.TagName);
        await shell.WaitForAsync(() => vm.Rows.Query.SortDirection == SortDirection.None);
        Assert.Equal(SortColumn.None, vm.SortColumn);

        // the header shows it too
        vm.CycleSort(SortColumn.Value);
        shell.Settle();
        var value = shell.Window.Table.Table.Grid.Columns.First(c => Equals(c.ColumnKey, TableItem.COLUMN_VALUE));
        Assert.Contains("asc", value.HeaderStyleClasses);
    }


    [AvaloniaFact]
    public async Task CollapseAllTurnsIntoExpandAll()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;
        var button = shell.Window.Table.CollapseAllButton;
        Assert.Equal(AppText.COLLAPSE_ALL, button.Content);

        vm.ToggleCollapseAllCommand.Execute(null);
        await shell.WaitForAsync(() => vm.IsAllCollapsed);
        shell.Settle();
        Assert.Equal(AppText.EXPAND_ALL, button.Content);
        Assert.All(vm.Rows.Items, i => Assert.IsType<GroupHeaderItem>(i));
        Assert.Equal(vm.Rows.Total, vm.Rows.RowsInView.Count);

        vm.ToggleCollapseAllCommand.Execute(null);
        await shell.WaitForAsync(() => !vm.IsAllCollapsed);
        Assert.Contains(vm.Rows.Items, i => i is RowItem);

        // one group by its header
        vm.ToggleGroup("EXIF");
        await shell.WaitForAsync(() => vm.Rows.Groups.Single(g => g.Name == "EXIF").IsCollapsed);
        Assert.DoesNotContain(vm.Rows.Items.OfType<RowItem>(), r => r.Row.Group == "EXIF");
    }


    [AvaloniaFact]
    public async Task EscClearsTheSearchThenTheCellThenTheRows()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;
        var keys = MetadataTable.RowKeys(vm.Rows);

        vm.SearchText = "tag";
        await shell.WaitForAsync(() => vm.Rows.IsFiltering);
        vm.Selection.SelectCell(MetadataTable.RowKeys(vm.Rows)[0], TableColumns.Value);

        Esc(shell);
        Assert.Equal(string.Empty, vm.SearchText);
        Assert.True(vm.Selection.HasCell);

        Esc(shell);
        Assert.Null(vm.Selection.CurrentKey);

        vm.Selection.SelectAll(keys);
        Esc(shell);
        Assert.Equal(0, vm.Selection.RowCount);
        Assert.False(vm.Escape());
    }


    [AvaloniaFact]
    public async Task CtrlAAndCtrlCActOnTheTableOutsideTheSearch()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;
        var panel = shell.Window.Table;

        shell.Window.Table.Table.FocusTable();
        shell.Window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, null);
        shell.Settle();
        Assert.Equal(vm.Rows.Total, vm.SelectedCount);

        // DS 6.9: "N selected · Copy · JSON · Clear", and the buttons lose their labels
        Assert.True(panel.SelectionRow.IsVisible);
        Assert.False(panel.SummaryRow.IsVisible);
        Assert.Equal(vm.Rows.Total.ToString(System.Globalization.CultureInfo.InvariantCulture), panel.SelectedCountText.Text);
        Assert.Null(panel.ExportButton.Content);

        shell.Window.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, null);
        shell.Settle();
        Assert.Equal(AppText.CopiedTags(vm.Rows.Total), shell.Window.Toasts.Title);

        vm.ClearSelectionCommand.Execute(null);
        shell.Settle();
        Assert.True(panel.SummaryRow.IsVisible);
        Assert.Equal(AppText.EXPORT, panel.ExportButton.Content);

        // a cell copies its text
        var key = MetadataTable.RowKeys(vm.Rows)[1];
        vm.Selection.SelectCell(key, TableColumns.TagName);
        Assert.True(vm.CopySelection());
        shell.Settle();
        Assert.Equal(AppText.TOAST_COPIED_CELL, shell.Window.Toasts.Title);
    }


    [AvaloniaFact]
    public async Task TheColumnsMenuTogglesKeepsOneAndRemembers()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;

        vm.ToggleColumnCommand.Execute(TableColumns.Index);
        shell.Settle(500);
        Assert.True(shell.Window.Table.Table.Grid.Columns.First(c => Equals(c.ColumnKey, TableItem.COLUMN_INDEX)).IsVisible);
        Assert.Contains("Index", shell.Settings.Current.VisibleColumns);

        foreach (var column in new[] { TableColumns.Index, TableColumns.TagId, TableColumns.TagName }) vm.ToggleColumnCommand.Execute(column);
        Assert.Equal(TableColumns.Value, vm.Columns);
        var value = vm.ColumnChoices.Single(c => c.Column == TableColumns.Value);
        Assert.True(value.IsLocked);
        Assert.Equal(AppText.AT_LEAST_ONE, value.Hint);

        vm.ToggleColumnCommand.Execute(TableColumns.Value);
        Assert.Equal(TableColumns.Value, vm.Columns);
        Assert.False(shell.Window.Table.ColumnValueItem.IsEnabled);
    }


    [AvaloniaFact]
    public async Task NarrowTablesHideTheIdColumnsAndSayWhy()
    {
        using var shell = await TestShell.OpenWithFileAsync(480);
        var vm = shell.ViewModel;

        Assert.True(vm.IdColumnsHidden);
        Assert.False(shell.Window.Table.Table.Grid.Columns.First(c => Equals(c.ColumnKey, TableItem.COLUMN_ID)).IsVisible);
        Assert.Equal(AppText.HIDDEN_AT_WIDTH, vm.ColumnChoices.Single(c => c.Column == TableColumns.TagId).Hint);

        // icon-only below 660, as the P4 rules say
        Assert.Null(shell.Window.Table.CollapseAllButton.Content);
        Assert.Null(shell.Window.Table.ExportButton.Content);
    }


    [AvaloniaFact]
    public async Task ChipsShowHiddenGroupsAndFiltersAndClear()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;

        vm.SetGroupHidden("EXIF", true);
        vm.ToggleFilter(QuickFilter.HasTagId);
        await shell.WaitForAsync(() => vm.FilterChips.Count == 2);
        Assert.Equal(["Hiding EXIF", AppText.FILTER_TAG_ID], vm.FilterChips.Select(c => c.Label));
        Assert.False(vm.ShowSummaryDetail);
        Assert.True(vm.Rows.IsFiltering);
        Assert.DoesNotContain(vm.Rows.RowsInView, r => r.Group == "EXIF");

        vm.RemoveChipCommand.Execute(vm.FilterChips[1]);
        await shell.WaitForAsync(() => vm.FilterChips.Count == 1);

        vm.ClearFiltersCommand.Execute(null);
        await shell.WaitForAsync(() => vm.FilterChips.Count == 0);
        Assert.True(vm.ShowSummaryDetail);
        Assert.False(vm.Rows.IsFiltering);
    }


    [AvaloniaFact]
    public async Task NoMatchOffersToClearTheFilters()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;

        vm.SearchText = "zzz";
        await shell.WaitForAsync(() => vm.ShowNoMatch);
        shell.Settle();
        Assert.True(shell.Window.Table.NoMatch.IsEffectivelyVisible);
        Assert.Equal("0 of 24 tags", vm.Summary);

        shell.Window.Table.ClearFiltersButton.Command!.Execute(null);
        await shell.WaitForAsync(() => !vm.ShowNoMatch);
        Assert.Equal(string.Empty, vm.SearchText);
    }


    [AvaloniaFact]
    public async Task TheFirstReadShowsSkeletonRows()
    {
        using var shell = TestShell.Open(1400);
        shell.Reader.Hold("slow.jpg");
        _ = shell.ViewModel.OpenFileAsync("slow.jpg");
        await shell.WaitForAsync(() => shell.ViewModel.IsLoading);
        shell.Settle();

        Assert.True(shell.ViewModel.ShowSkeleton);
        Assert.Contains(shell.Window.Table.Table.GetVisualDescendants().OfType<Skeleton>(), s => s.IsEffectivelyVisible && s.IsPulsing);
        Assert.True(shell.Window.Table.LoadLine.IsVisible);

        shell.Reader.Release("slow.jpg");
        await shell.WaitForAsync(() => shell.ViewModel.HasDocument);
        shell.Settle();
        Assert.False(shell.ViewModel.ShowSkeleton);
        Assert.DoesNotContain(shell.Window.Table.Table.GetVisualDescendants().OfType<Skeleton>(), s => s.IsEffectivelyVisible);
    }


    [AvaloniaFact]
    public async Task ANewFileStartsOpenAndUnselected()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;
        vm.ToggleGroup("EXIF");
        vm.Selection.SelectAll(MetadataTable.RowKeys(vm.Rows));

        await vm.OpenFileAsync("b.jpg");
        await shell.WaitForAsync(() => vm.Document?.FileName == "b.jpg");

        Assert.DoesNotContain(vm.Rows.Groups, g => g.IsCollapsed);
        Assert.Equal(0, vm.SelectedCount);
    }


    [AvaloniaFact]
    public async Task ExportWritesTheRowsInViewWithTheChosenColumns()
    {
        var folder = Directory.CreateTempSubdirectory("eg-export-").FullName;
        try
        {
            var shell = new RecordingShell();
            var vm = new MainViewModel(new SettingsStore(folder), new FakeReader()) { Shell = shell };
            await vm.OpenFileAsync("C:/photos/a.jpg");
            for (var i = 0; i < 100 && vm.Rows.Total == 0; i++) await Task.Delay(10);

            vm.SearchText = "Tag 1";
            for (var i = 0; i < 100 && !vm.Rows.IsFiltering; i++) await Task.Delay(10);
            await vm.ExportCommand.ExecuteAsync(ExportFormat.Csv);

            var csv = System.Text.Encoding.UTF8.GetString(shell.Saved.ToArray());
            Assert.StartsWith("\uFEFF\"Group\",\"Tag ID\",\"Tag name\",\"Value\"", csv);
            Assert.Equal(vm.Rows.RowsInView.Count + 1, csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
            Assert.Equal(("Exported a.csv", AppText.ExportedSub(vm.Rows.TagsInView, vm.Rows.AiInView, shell.Saved.Length)), shell.Toasts[^1]);

            await vm.CopyAsJsonCommand.ExecuteAsync(null);
            Assert.StartsWith("[", shell.Copied[^1]);
            Assert.Equal((AppText.TOAST_COPIED_AS_JSON, AppText.TagsInView(vm.Rows.TagsInView, vm.Rows.AiInView)), shell.Toasts[^1]);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    #endregion // Tests


    #region Private methods

    private static void Esc(TestShell shell)
    {
        shell.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        shell.Settle(40);
    }


    private static void AssertSortedWithinGroups(ViewResult view, bool descending)
    {
        foreach (var group in view.RowsInView.GroupBy(r => r.Group))
        {
            var names = group.Select(r => r.Name).ToList();
            var sorted = descending ? names.OrderByDescending(n => n, NaturalComparer.Instance).ToList() : names.OrderBy(n => n, NaturalComparer.Instance).ToList();
            Assert.Equal(sorted, names);
        }
    }

    #endregion // Private methods
}
