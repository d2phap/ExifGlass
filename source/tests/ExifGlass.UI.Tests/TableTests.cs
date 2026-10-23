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
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExifGlass.Core.Editing;
using ExifGlass.Core.Export;
using ExifGlass.Core.Metadata;
using ExifGlass.Core.Query;
using ExifGlass.UI.Controls;
using ExifGlass.UI.Gallery;
using ExifGlass.UI.Tokens;
using ExifGlass.UI.Windows;
using Xunit;

namespace ExifGlass.UI.Tests;

/// <summary>
/// The metadata table on its own (P5): rows, headers, selection, keyboard, columns, states and motion choices, on documents made in memory.
/// </summary>
public class TableTests
{
    #region Rows and themes

    [AvaloniaFact]
    public void TheTableShowsHeadersAndRowsOnItsOwnThemes()
    {
        using var host = TableHost.Open(TestDocuments.Make("a.jpg", 200));

        var rows = host.Table.RealizedRows().ToList();
        Assert.True(rows.Count > 10, $"realized {rows.Count}");
        Assert.Contains(rows, r => r.Classes.Contains("group"));

        // DS 6.9: header rows 36, rows at least 36
        Assert.All(rows, r => Assert.True(r.Bounds.Height >= 35.5, $"row {r.Bounds.Height}"));
        Assert.All(rows.Where(r => r.Classes.Contains("group")), r => Assert.Equal(36, r.Bounds.Height, 1));

        var audit = TemplateAudit.Run(host.Window);
        Assert.True(audit.Passed, string.Join(", ", audit.Failures));
    }


    [AvaloniaFact]
    public void TheFirstColumnCarriesTheInsetAndGrows()
    {
        using var host = TableHost.Open(TestDocuments.Make("a.jpg", 40));
        var id = host.Column(TableItem.COLUMN_ID);
        var name = host.Column(TableItem.COLUMN_NAME);

        // Tag ID is first by default (Appendix A item 24): 88 + 22, the cells 34 px in
        Assert.Equal(110, id.ActualWidth, 1);
        Assert.Equal(34, host.CellOf(host.FirstRow(), id).Padding.Left);
        Assert.Equal(12, host.CellOf(host.FirstRow(), name).Padding.Left);

        // without # and Tag ID, Tag name leads: 36 % + 22
        host.Table.HidesIdColumns = true;
        host.Settle(500);
        Assert.False(id.IsVisible);
        Assert.Equal(Math.Round(host.Table.Bounds.Width * 0.36) + 22, name.ActualWidth, 1);
        Assert.Equal(34, host.CellOf(host.FirstRow(), name).Padding.Left);

        // below 460 px: 28 px in, 44 %, no group descriptions
        host.Table.IsNarrow = true;
        host.Settle(500);
        Assert.Equal(28, host.CellOf(host.FirstRow(), name).Padding.Left);
        Assert.Equal(Math.Max(130, Math.Round(host.Table.Bounds.Width * 0.44)) + 16, name.ActualWidth, 1);
    }


    [AvaloniaFact]
    public void AtLeastOneColumnStays()
    {
        using var host = TableHost.Open(TestDocuments.Make("a.jpg", 40));

        host.Table.Columns = TableColumns.Index;
        host.Table.HidesIdColumns = true;
        host.Settle(500);

        Assert.Equal(TableColumns.TagName, host.Table.ShownColumns);
        Assert.True(host.Column(TableItem.COLUMN_NAME).IsVisible);
        Assert.False(host.Column(TableItem.COLUMN_VALUE).IsVisible);
    }


    [AvaloniaFact]
    public void AColumnAtZeroWidthTakesNoRoom()
    {
        using var host = TableHost.Open(TestDocuments.Make("a.jpg", 40));
        var id = host.Column(TableItem.COLUMN_ID);

        // a hiding column's last glide frame: with the grid's 20 px floor the next column jumped when it hid
        id.Width = new DataGridLength(0);
        host.Settle();

        var row = host.FirstRow();
        Assert.Equal(0, id.ActualWidth, 1);
        Assert.Equal(0, host.CellOf(row, host.Column(TableItem.COLUMN_NAME)).TranslatePoint(default, row)!.Value.X, 1);
    }


    [AvaloniaFact]
    public void StagedEditsShowTheirBadges()
    {
        var doc = TestDocuments.Make("a.jpg", 40);
        var edited = doc.Tags[3];
        var deleted = doc.Tags[4];
        var edits = EditSet.Empty
            .Set(edited.Key, edited.WriteName, "new value", edited.Value)
            .Delete(deleted.Key, deleted.WriteName, deleted.Value)
            .Add(new AddedTag("EXIF", "Artist", "Dương Diệu Pháp"));
        using var host = TableHost.Open(doc);
        host.Query(ViewQuery.Default with { Edits = edits });

        Assert.Equal((BadgeKind.Edited, "Edited"), host.BadgeOf(edited.Key));
        Assert.Equal((BadgeKind.Delete, "Delete"), host.BadgeOf(deleted.Key));
        Assert.Equal((BadgeKind.New, "New"), host.BadgeOf("New:EXIF:Artist"));
        Assert.Contains("deleted", host.ValueOf(deleted.Key).Classes);
        Assert.Equal("+", host.IndexOf("New:EXIF:Artist").Text);
        Assert.Null(host.BadgeOf(doc.Tags[5].Key));
    }

    #endregion // Rows and themes


    #region Selection and keys

    [AvaloniaFact]
    public void APlainClickSelectsOneCellUnderTheRing()
    {
        using var host = TableHost.Open(TestDocuments.Make("a.jpg", 40));
        var row = host.FirstRow();
        var key = ((TableItem)row.DataContext!).Key;

        host.Click(host.CellCentre(row, TableItem.COLUMN_VALUE));

        Assert.Equal(key, host.Selection.CurrentKey);
        Assert.Equal(TableColumns.Value, host.Selection.CurrentColumn);
        Assert.True(host.Selection.HasCell);
        Assert.Equal(key, host.Selection.FocusedKey);

        // one squircle ring over the cell (O1)
        var ring = host.Ring;
        var cell = host.CellOf(row, host.Column(TableItem.COLUMN_VALUE));
        var cellAt = cell.TranslatePoint(default, host.Window)!.Value;
        var ringAt = ring.TranslatePoint(default, host.Window)!.Value;
        Assert.True(ring.IsVisible);
        Assert.Equal(cellAt.X, ringAt.X, 1);
        Assert.Equal(cellAt.Y, ringAt.Y, 1);
        Assert.Equal(cell.Bounds.Width, ring.Bounds.Width, 1);
    }


    [AvaloniaFact]
    public void CtrlTogglesRowsAndShiftSelectsARange()
    {
        using var host = TableHost.Open(TestDocuments.Make("a.jpg", 40));
        var data = host.DataRows().ToList();

        host.Click(host.CellCentre(data[0], TableItem.COLUMN_NAME), RawInputModifiers.Control);
        host.Click(host.CellCentre(data[2], TableItem.COLUMN_NAME), RawInputModifiers.Control);
        Assert.Equal(2, host.Selection.RowCount);
        Assert.False(host.Selection.HasCell);
        Assert.False(host.Ring.IsVisible);
        Assert.Contains("selected", data[0].Classes);

        // a range runs from the anchor in view order and steps over the group header between
        host.Click(host.CellCentre(data[5], TableItem.COLUMN_NAME), RawInputModifiers.Shift);
        Assert.Equal(4, host.Selection.RowCount);
        Assert.DoesNotContain(host.Table.Items.Where(i => i.IsHeader).Select(i => i.Key), host.Selection.Rows.Contains);

        // Ctrl again on a selected row takes it out
        host.Click(host.CellCentre(data[5], TableItem.COLUMN_NAME), RawInputModifiers.Control);
        Assert.Equal(3, host.Selection.RowCount);
        Assert.DoesNotContain("selected", data[5].Classes);
    }


    [AvaloniaFact]
    public void CtrlASelectsTheRowsInViewButNotHeadersOrCollapsedGroups()
    {
        var doc = TestDocuments.Make("a.jpg", 40);
        using var host = TableHost.Open(doc);
        var view = host.Query(ViewQuery.Default with { CollapsedGroups = new HashSet<string>(["EXIF"]) });

        host.Click(host.CellCentre(host.FirstRow(), TableItem.COLUMN_NAME));
        host.Key(Key.A, RawInputModifiers.Control);

        var expected = view.Items.OfType<RowItem>().Select(r => r.Row.Key).ToHashSet();
        Assert.Equal(expected.Count, host.Selection.RowCount);
        Assert.All(host.Selection.Rows, k => Assert.Contains(k, expected));
        Assert.DoesNotContain(host.Selection.Rows, k => doc.GetByKey(k)?.Group0 == "EXIF");
    }


    [AvaloniaFact]
    public void ArrowsMoveTheCellAndAreTogglesOnHeaders()
    {
        using var host = TableHost.Open(TestDocuments.Make("a.jpg", 40));
        var toggled = new List<string>();
        host.Table.GroupToggleRequested += (_, g) => toggled.Add(g);
        var items = host.Table.Items;

        host.Click(host.CellCentre(host.FirstRow(), TableItem.COLUMN_NAME));
        var first = host.Selection.CurrentKey!;
        var at = items.ToList().FindIndex(i => i.Key == first);

        host.Key(Key.Right);
        Assert.Equal(TableColumns.Value, host.Selection.CurrentColumn);
        host.Key(Key.Home);
        Assert.Equal(TableColumns.TagId, host.Selection.CurrentColumn);
        host.Key(Key.End);
        Assert.Equal(TableColumns.Value, host.Selection.CurrentColumn);

        // down past the group's last row lands on the next header, where Left and Right collapse and expand
        var header = items.Skip(at).First(i => i.IsHeader);
        while (host.Selection.CurrentKey != header.Key) host.Key(Key.Down);
        Assert.Equal(TableColumns.None, host.Selection.CurrentColumn);
        Assert.True(host.Ring.IsVisible);
        host.Key(Key.Right);
        Assert.Empty(toggled);
        host.Key(Key.Left);
        host.Key(Key.Enter);
        Assert.Equal([header.Group!.Name, header.Group!.Name], toggled);

        // the next row takes the column the keyboard left
        host.Key(Key.Down);
        Assert.Equal(TableColumns.Value, host.Selection.CurrentColumn);
        host.Key(Key.Home, RawInputModifiers.Control);
        Assert.Equal(items[0].Key, host.Selection.CurrentKey);
    }


    [AvaloniaFact]
    public void ShiftArrowsExtendARowSelection()
    {
        using var host = TableHost.Open(TestDocuments.Make("a.jpg", 40));
        var data = host.DataRows().ToList();

        host.Click(host.CellCentre(data[1], TableItem.COLUMN_NAME));
        host.Key(Key.Down, RawInputModifiers.Shift);
        host.Key(Key.Down, RawInputModifiers.Shift);

        Assert.True(host.Selection.RowCount >= 3);
        Assert.True(host.Selection.IsSelected(((TableItem)data[1].DataContext!).Key));
    }


    [AvaloniaFact]
    public void HeaderClicksAndDoubleClicksAskTheApp()
    {
        using var host = TableHost.Open(TestDocuments.Make("a.jpg", 40));
        var toggled = new List<string>();
        var copied = new List<string>();
        var sorts = new List<SortColumn>();
        host.Table.GroupToggleRequested += (_, g) => toggled.Add(g);
        host.Table.ValueCopyRequested += (_, r) => copied.Add(r.Key);
        host.Table.SortRequested += (_, c) => sorts.Add(c);

        var header = host.Table.RealizedRows().First(r => r.Classes.Contains("group"));
        host.Click(header.TranslatePoint(new Point(200, 18), host.Window)!.Value);
        Assert.Equal(["ExifTool"], toggled);

        var row = host.FirstRow();
        var point = host.CellCentre(row, TableItem.COLUMN_VALUE);
        host.Click(point);
        host.Click(point);
        var key = ((TableItem)row.DataContext!).Key;
        Assert.Equal([key], copied);
        Assert.True(host.Table.IsCopied(key));

        var nameHeader = host.Window.GetVisualDescendants().OfType<DataGridColumnHeader>().First(h => Equals(h.Content, UiText.COLUMN_TAG_NAME));
        host.Click(nameHeader.TranslatePoint(new Point(30, 16), host.Window)!.Value);
        Assert.Equal([SortColumn.TagName], sorts);
    }


    [AvaloniaFact]
    public void SortGlyphsFollowTheSort()
    {
        using var host = TableHost.Open(TestDocuments.Make("a.jpg", 40));
        var name = host.Column(TableItem.COLUMN_NAME);

        host.Table.SortColumn = SortColumn.TagName;
        host.Table.SortDirection = SortDirection.Ascending;
        Assert.Contains("asc", name.HeaderStyleClasses);
        host.Table.SortDirection = SortDirection.Descending;
        Assert.Contains("desc", name.HeaderStyleClasses);
        Assert.DoesNotContain("asc", name.HeaderStyleClasses);
        host.Table.SortDirection = SortDirection.None;
        Assert.DoesNotContain("desc", name.HeaderStyleClasses);
    }


    [AvaloniaFact]
    public void ANewViewKeepsTheSelectionItStillShows()
    {
        using var host = TableHost.Open(TestDocuments.Make("a.jpg", 40));
        var data = host.DataRows().ToList();
        var kept = ((TableItem)data[0].DataContext!).Key;
        var dropped = ((TableItem)data[1].DataContext!).Key;
        host.Selection.ToggleRow(kept);
        host.Selection.ToggleRow(dropped);

        var keptName = ((TableItem)data[0].DataContext!).Row!.Name;
        host.Query(ViewQuery.Default with { Search = keptName });

        Assert.True(host.Selection.IsSelected(kept));
        Assert.False(host.Selection.IsSelected(dropped));
    }

    #endregion // Selection and keys


    #region States and motion

    [AvaloniaFact]
    public void NoMatchShowsThePlaceholderUnderTheHeader()
    {
        using var host = TableHost.Open(TestDocuments.Make("a.jpg", 40));
        var placeholder = new TextBlock { Text = "No tags match" };
        host.Table.Placeholder = placeholder;

        host.Query(ViewQuery.Default with { Search = "zzz" });
        Assert.True(placeholder.IsEffectivelyVisible);
        Assert.True(placeholder.TranslatePoint(default, host.Window)!.Value.Y >= MetadataTable.COLUMN_HEADER_HEIGHT);

        host.Query(ViewQuery.Default);
        Assert.Null(TopLevel.GetTopLevel(placeholder));
    }


    [AvaloniaFact]
    public void LoadingShowsSkeletonRowsLinedUpWithTheColumns()
    {
        using var host = TableHost.Open(TestDocuments.Make("a.jpg", 40));
        host.Table.View = null;
        host.Table.IsLoading = true;
        host.Settle();

        var bars = host.Window.GetVisualDescendants().OfType<Skeleton>().Where(s => s.IsEffectivelyVisible && s.Bounds.Width > 0).ToList();
        Assert.Equal(14 * 3, bars.Count);
        Assert.All(bars, b => Assert.True(b.IsPulsing));

        // the name bars start at the Tag name column's padding
        var name = host.Column(TableItem.COLUMN_NAME);
        var id = host.Column(TableItem.COLUMN_ID);
        Assert.All(bars.Where(b => b.Classes.Contains("name")), b => Assert.Equal(id.ActualWidth + 12, b.Bounds.X, 1));

        host.Table.IsLoading = false;
        host.Settle();
        Assert.DoesNotContain(host.Window.GetVisualDescendants().OfType<Skeleton>(), s => s.IsEffectivelyVisible);
    }


    [AvaloniaFact]
    public void TheMotionFollowsWhatChanged()
    {
        var doc = TestDocuments.Make("a.jpg", 40);
        var view = QueryEngine.Run(doc, ViewQuery.Default);

        Assert.Equal(ViewMotionKind.Reveal, TableMotionDecide(null, view).Kind);
        Assert.Equal(ViewMotionKind.Reveal, TableMotionDecide(view, QueryEngine.Run(TestDocuments.Make("a.jpg", 40), ViewQuery.Default)).Kind);
        Assert.Equal(ViewMotionKind.Flip, TableMotionDecide(view, QueryEngine.Run(doc, ViewQuery.Default with { Search = "tag" })).Kind);
        Assert.Equal(ViewMotionKind.Flip, TableMotionDecide(view, QueryEngine.Run(doc, ViewQuery.Default with { SortColumn = SortColumn.TagName, SortDirection = SortDirection.Ascending })).Kind);

        var collapsed = QueryEngine.Run(doc, ViewQuery.Default with { CollapsedGroups = new HashSet<string>(["EXIF"]) });
        Assert.Equal(new ViewMotion(ViewMotionKind.Collapse, "EXIF"), TableMotionDecide(view, collapsed));
        Assert.Equal(new ViewMotion(ViewMotionKind.Expand, "EXIF"), TableMotionDecide(collapsed, view));

        // Collapse all glides as one FLIP; the AI data arriving is the same read
        var all = QueryEngine.Run(doc, ViewQuery.Default with { CollapsedGroups = new HashSet<string>(["EXIF", "File"]) });
        Assert.Equal(ViewMotionKind.Flip, TableMotionDecide(view, all).Kind);
        var withAi = QueryEngine.Run(doc.WithAi(null), ViewQuery.Default);
        Assert.Equal(ViewMotionKind.Flip, TableMotionDecide(view, withAi).Kind);
    }


    [AvaloniaFact]
    public void ACollapseLeavesAGhostOfTheRowsItHides()
    {
        using var host = TableHost.Open(TestDocuments.Make("a.jpg", 60));
        var hidden = host.Table.RealizedRows().Count(r => r.DataContext is TableItem { Row.Group: "File" });
        MotionTokens.IsFrozen = false;

        host.Query(ViewQuery.Default with { CollapsedGroups = new HashSet<string>(["File"]) });

        // copies made of the same cell controls, no snapshot: one per row the collapse hides
        var ghost = Assert.IsType<Canvas>(Assert.Single(host.Table.GhostLayer.Children));
        Assert.Equal(hidden, ghost.Children.Count);
        Assert.Contains(ghost.GetVisualDescendants().OfType<NameCell>(), c => c.Text is { Length: > 0 });

        host.Settle(600);
        Assert.Empty(host.Table.GhostLayer.Children);
    }


    [AvaloniaFact]
    public void AResetKeepsTheHoverUnderThePointer()
    {
        using var host = TableHost.Open(TestDocuments.Make("a.jpg", 40));
        var groups = host.Table.View!.Items.OfType<GroupHeaderItem>().Select(h => h.Group.Name).ToHashSet();
        var collapsed = ViewQuery.Default with { CollapsedGroups = groups };
        host.Query(collapsed);
        host.Table.GroupToggleRequested += (_, g) => host.Table.View = QueryEngine.Run(host.Document, collapsed with { CollapsedGroups = groups.Where(x => x != g).ToHashSet() });

        var last = host.Table.RealizedRows().OrderBy(r => r.Bounds.Y).Last();
        var key = ((TableItem)last.DataContext!).Key;
        var point = last.TranslatePoint(new Point(200, 18), host.Window)!.Value;

        // the press resets the rows and the grid hands the hovered row to another header; its pointer-over follows later
        host.Window.MouseMove(point);
        host.Window.MouseDown(point, MouseButton.Left);
        AssertHoverOnlyOn(host, key);

        host.Window.MouseUp(point, MouseButton.Left);
        host.Settle();
        AssertHoverOnlyOn(host, key);
    }


    [AvaloniaFact]
    public void TheStickyHeaderFollowsTheGroupAtTheTop()
    {
        using var host = TableHost.Open(TestDocuments.Make("a.jpg", 400));
        Assert.False(host.Table.Sticky.IsVisible);

        var scroller = host.Table.Grid.GetVisualDescendants().OfType<ScrollViewer>().First();
        scroller.Offset = new Vector(0, 300);
        host.Settle();

        var top = host.Table.RealizedRows().First(r => r.Bounds.Y <= 0.5 && r.Bounds.Bottom > 0.5);
        var item = (TableItem)top.DataContext!;
        var group = item.Group?.Name ?? item.Row!.Group;
        Assert.True(host.Table.Sticky.IsVisible);
        Assert.Equal(group, host.Table.Sticky.View.Group?.Name);
        Assert.Equal(group, host.Table.ActiveGroup);
    }


    [AvaloniaFact]
    public async Task AJumpBringsAGroupHeaderToTheTop()
    {
        using var host = TableHost.Open(TestDocuments.Make("a.jpg", 400));

        await host.Table.JumpToGroupAsync("XMP");
        host.Settle();

        var header = host.Table.RealizedRows().First(r => r.DataContext is TableItem { Group.Name: "XMP" });
        Assert.Equal(0, header.Bounds.Y, 1);
        Assert.Equal("XMP", host.Table.ActiveGroup);
    }

    #endregion // States and motion


    #region Private methods

    private static ViewMotion TableMotionDecide(ViewResult? old, ViewResult view) => TableMotion.Decide(old, view);


    private static void AssertHoverOnlyOn(TableHost host, string key)
    {
        foreach (var row in host.Table.RealizedRows())
        {
            var item = (TableItem)row.DataContext!;
            var overlay = row.GetVisualDescendants().OfType<InteractionOverlay>().First();
            var expected = item.Key == key ? overlay.HoverBrush : overlay.RestBrush;
            Assert.True(ReferenceEquals(expected, overlay.Background), $"{item.Key}: {overlay.Background}");
        }
    }

    #endregion // Private methods
}


/// <summary>
/// A window holding a table on Bg.Panel, with motion frozen; its views come from the real query engine.
/// </summary>
internal sealed class TableHost : IDisposable
{
    #region Constructors

    private TableHost(MetadataDocument document, double width, double height, ThemeVariant theme)
    {
        MotionTokens.IsFrozen = true;
        Document = document;
        Table = new MetadataTable { Selection = Selection };
        var panel = new Border { Child = Table };
        panel[!Border.BackgroundProperty] = panel.GetResourceObservable(TokenKeys.BG_PANEL_BRUSH).ToBinding();

        Window = new Window { Width = width, Height = height, RequestedThemeVariant = theme, Content = panel };
        AppWindow.Prepare(Window);
        Window.Show();
        Query(ViewQuery.Default);
    }

    #endregion // Constructors


    #region Properties

    public MetadataDocument Document { get; }

    public MetadataTable Table { get; }

    public TableSelection Selection { get; } = new();

    public Window Window { get; }

    public Control Ring => Window.GetVisualDescendants().OfType<Control>().First(c => c.Classes.Contains("table-ring"));

    #endregion // Properties


    #region Public methods

    public static TableHost Open(MetadataDocument document, double width = 900, double height = 600, ThemeVariant? theme = null) =>
        new(document, width, height, theme ?? ThemeVariant.Light);


    public ViewResult Query(ViewQuery query)
    {
        var view = QueryEngine.Run(Document, query);
        Table.View = view;
        Settle();
        return view;
    }


    public void Settle(int ms = 60)
    {
        var frame = new DispatcherFrame();
        var ticker = new DispatcherTimer(TimeSpan.FromMilliseconds(15), DispatcherPriority.Background, (_, _) => AvaloniaHeadlessPlatform.ForceRenderTimerTick());
        ticker.Start();
        DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(ms));
        Dispatcher.UIThread.PushFrame(frame);
        ticker.Stop();
    }


    public DataGridColumn Column(string key) => Table.Grid.Columns.First(c => Equals(c.ColumnKey, key));

    public DataGridRow FirstRow() => DataRows().First();

    public IEnumerable<DataGridRow> DataRows() => Table.RealizedRows().Where(r => !r.Classes.Contains("group")).OrderBy(r => r.Bounds.Y);


    public DataGridCell CellOf(DataGridRow row, DataGridColumn column) => (DataGridCell)column.GetCellContent(row)!.Parent!;


    public Point CellCentre(DataGridRow row, string column)
    {
        var cell = CellOf(row, Column(column));
        return cell.TranslatePoint(new Point(cell.Bounds.Width / 2, cell.Bounds.Height / 2), Window)!.Value;
    }


    public void Click(Point point, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.MouseMove(point, modifiers);
        Window.MouseDown(point, MouseButton.Left, modifiers);
        Window.MouseUp(point, MouseButton.Left, modifiers);
        Settle(30);
    }


    public void Key(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPress(key, modifiers, PhysicalKey.None, null);
        Window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        Settle(30);
    }


    public (BadgeKind, string?)? BadgeOf(string key)
    {
        var badge = ValueOf(key).Parent is ValueCell cell ? cell.GetVisualChildren().OfType<Badge>().FirstOrDefault(b => b.IsVisible) : null;
        return badge is null ? null : (badge.Kind, badge.Text);
    }


    public HighlightTextBlock ValueOf(string key) => RowContent<ValueCell>(key, TableItem.COLUMN_VALUE).Text;

    public IndexCell IndexOf(string key) => RowContent<IndexCell>(key, TableItem.COLUMN_INDEX);


    public void Dispose()
    {
        Window.Close();
        MotionTokens.IsFrozen = false;
    }

    #endregion // Public methods


    #region Private methods

    private T RowContent<T>(string key, string column) where T : Control
    {
        Table.JumpToGroupAsync(Table.Find(key)?.Row?.Group ?? "").GetAwaiter().GetResult();
        Settle();

        // the # column is hidden by default: show it for the lookup
        var col = Column(column);
        if (!col.IsVisible)
        {
            Table.Columns |= TableColumns.Index;
            Settle(500);
        }

        var row = Table.RealizedRows().First(r => r.DataContext is TableItem item && item.Key == key);
        return (T)col.GetCellContent(row)!;
    }

    #endregion // Private methods
}
