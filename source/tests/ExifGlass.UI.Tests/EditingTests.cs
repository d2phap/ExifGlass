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
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ExifGlass.Core.Editing;
using ExifGlass.Core.Export;
using ExifGlass.Core.Metadata;
using ExifGlass.UI.Controls;
using ExifGlass.UI.Tokens;
using ExifGlass.ViewModels;
using ExifGlass.Views;
using System.Buffers;
using System.Text;
using Xunit;

namespace ExifGlass.UI.Tests;

/// <summary>
/// P8: the Edit menu, Add tag, the pending bar, Save, the unsaved-changes dialog (D8), Save binary data, the command bar.
/// </summary>
public class EditingTests
{
    #region Tests

    [AvaloniaFact]
    public async Task SaveWritesTheStagedEditsAndReadsTheFileAgainKeepingTheView()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;
        var table = shell.Window.Table.Table;
        var viewport = table.Scroller!.Viewport.Height;
        var key = KeyOf(vm, "Tag 2");
        vm.Selection.SelectCell(key, TableColumns.Value);

        vm.StartEditCommand.Execute(null);
        vm.EditText = "Changed";
        vm.ApplyEditCommand.Execute(null);
        await shell.WaitForAsync(() => vm.HasPending);
        shell.Settle();

        // DS 6.13: the bar floats in with what Save writes; the table makes 76 of room under its last row
        var bar = shell.Window.Table.PendingBar;
        Assert.True(bar.IsVisible);
        Assert.Equal("1 pending change", bar.Title);
        Assert.Equal("Save writes a.jpg · backup kept as a.jpg_original", bar.Subtitle);
        Assert.Null(bar.Note);
        Assert.Equal(76, table.BottomInset);
        Assert.Equal(viewport - 76, table.Scroller.Viewport.Height, 0.5);

        var recorder = new RecordingShell();
        vm.Shell = recorder;
        var reads = shell.Reader.Reads;
        Assert.True(await vm.SaveAsync());

        var (path, edits) = Assert.Single(shell.Reader.Writes);
        Assert.Equal("a.jpg", path);
        Assert.Equal("Changed", edits.Edits[key].Value);
        Assert.Equal(reads + 1, shell.Reader.Reads);
        Assert.Equal(("Saved 1 change", "Backup kept as a.jpg_original"), recorder.Toasts[^1]);

        // the read after the save keeps the cell; what was staged is in the file now
        Assert.True(vm.Edits.IsEmpty);
        Assert.Equal(key, vm.Selection.CurrentKey);
        Assert.False(vm.IsSaving);
        shell.Settle(200);
        Assert.False(bar.IsVisible);
        Assert.Equal(0, table.BottomInset);
    }


    [AvaloniaFact]
    public async Task SavingShowsTheSpinnerAndAFailedSaveKeepsEveryEdit()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;
        var recorder = new RecordingShell();
        vm.Shell = recorder;
        Delete(vm, "Tag 2");
        await shell.WaitForAsync(() => vm.HasPending);

        // while ExifTool writes, Save turns and a second Save is the same one
        shell.Reader.WriteHold = new TaskCompletionSource<SaveResult>();
        var save = vm.SaveAsync();
        Assert.True(vm.IsSaving);
        Assert.Same(save, vm.SaveAsync());
        shell.Settle();
        Assert.True(shell.Window.Table.PendingBar.IsSaving);

        shell.Reader.WriteHold.SetResult(new SaveResult(SaveOutcome.Failed, ["File is read-only"], [], null));
        Assert.False(await save);
        Assert.Equal(("Couldn't save a.jpg", "File is read-only"), recorder.Toasts[^1]);
        Assert.Equal(1, vm.PendingCount);
        Assert.False(vm.IsSaving);
        Assert.Single(shell.Reader.Writes);
    }


    [AvaloniaFact]
    public async Task AnotherFileAsksFirstAndCancelKeepsTheFileAndItsEdits()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;
        var recorder = new RecordingShell();
        vm.Shell = recorder;
        Delete(vm, "Tag 2");
        await shell.WaitForAsync(() => vm.HasPending);

        await vm.OpenFileAsync("b.jpg");
        var asked = Assert.Single(recorder.Asked);
        Assert.Equal("Save changes to a.jpg?", asked.Title);
        Assert.Equal("1 pending change not written yet. Discard drops them.", asked.Body);
        Assert.Equal(("Save", "Discard"), (asked.PrimaryText, asked.SecondaryText));
        Assert.Equal("a.jpg", vm.Document!.FileName);
        Assert.Equal(1, vm.PendingCount);

        // Save writes the file and opens the next one without reading this one again
        recorder.Answer = DialogAnswer.Primary;
        var reads = shell.Reader.Reads;
        await vm.OpenFileAsync("b.jpg");
        await shell.WaitForAsync(() => vm.Document?.FileName == "b.jpg");
        Assert.Single(shell.Reader.Writes);
        Assert.Equal(reads + 1, shell.Reader.Reads);
        Assert.True(vm.Edits.IsEmpty);

        // nothing staged, nothing asked
        await vm.OpenFileAsync("c.jpg");
        Assert.Equal(2, recorder.Asked.Count);
    }


    [AvaloniaFact]
    public async Task AFailedSaveInTheDialogKeepsTheWindowOnTheFile()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;
        vm.Shell = new RecordingShell { Answer = DialogAnswer.Primary };
        shell.Reader.WriteResult = new SaveResult(SaveOutcome.Failed, ["Disk full"], [], null);
        Delete(vm, "Tag 2");
        await shell.WaitForAsync(() => vm.HasPending);

        Assert.False(await vm.ConfirmLeaveAsync());
        await vm.OpenFileAsync("b.jpg");
        Assert.Equal("a.jpg", vm.Document!.FileName);
        Assert.Equal(1, vm.PendingCount);
    }


    [AvaloniaFact]
    public async Task TheEditMenuStagesEachBulkActionOnceAndTheRowsShowWhatGoes()
    {
        using var shell = TestShell.Open(1400);
        var vm = shell.ViewModel;
        shell.Reader.Maker = WithGps;
        await vm.OpenFileAsync("gps.jpg");
        await shell.WaitForAsync(() => vm.HasDocument);

        Assert.True(vm.RemoveGpsCommand.CanExecute(null));
        Assert.Equal("2 location tags · -gps*=", vm.GpsDescription);
        Assert.False(vm.RemoveAiCommand.CanExecute(null));
        Assert.Equal("No AI generation data in this file", vm.AiRemovalDescription);

        vm.RemoveGpsCommand.Execute(null);
        await shell.WaitForAsync(() => vm.Rows.RowsInView.Any(r => r.EditState == EditState.Removed));
        Assert.Equal(EditState.Removed, Row(vm, "GPS Latitude").EditState);
        Assert.Equal(EditState.Removed, Row(vm, "GPS Longitude").EditState);
        Assert.Equal(EditState.Removed, Row(vm, "GPS Position").EditState);
        Assert.Equal(EditState.None, Row(vm, "Make").EditState);
        Assert.False(vm.RemoveGpsCommand.CanExecute(null));
        Assert.Equal("Already staged", vm.GpsDescription);
        Assert.Equal(1, vm.PendingCount);

        // a removed row's card has no Undo of its own
        vm.Selection.SelectCell(Row(vm, "GPS Latitude").Key, TableColumns.Value);
        Assert.True(vm.FocusedTag!.IsRemoved);

        vm.StripAllCommand.Execute(null);
        await shell.WaitForAsync(() => Row(vm, "Make").EditState == EditState.Removed);
        Assert.False(vm.StripAllCommand.CanExecute(null));
        Assert.Equal(2, vm.PendingCount);
        Assert.Equal(EditState.None, Row(vm, "File Type").EditState);

        vm.DiscardCommand.Execute(null);
        await shell.WaitForAsync(() => Row(vm, "Make").EditState == EditState.None);
        Assert.True(vm.RemoveGpsCommand.CanExecute(null));
        Assert.Equal(0, vm.PendingCount);
    }


    [AvaloniaFact]
    public async Task AGpsOnlyInTheContentCredentialsCantBeRemovedAndTheBarSaysSo()
    {
        using var shell = TestShell.Open(1400);
        var vm = shell.ViewModel;
        shell.Reader.Maker = p => Doc(p, """
            , "File:FileType": {"id": "FileType", "desc": "File Type", "val": "JPEG"}
            , "EXIF:GPS:GPSLatitude": {"id": "0x0002", "desc": "GPS Latitude", "val": "43 deg 9' 7.54\""}
            , "JUMBF:JSON:EXIF_GPSLatitude": {"id": "EXIF_GPSLatitude", "desc": "EXIF GPS Latitude", "val": 43.1520939}
            """);
        await vm.OpenFileAsync("c2pa.jpg");
        await shell.WaitForAsync(() => vm.HasDocument);

        vm.RemoveGpsCommand.Execute(null);
        await shell.WaitForAsync(() => vm.HasPending);
        Assert.Equal("The location in the Content Credentials stays: Strip all metadata removes it with the manifest", vm.PendingNote);
        Assert.Equal(EditState.None, Row(vm, "EXIF GPS Latitude").EditState);
        shell.Settle();
        Assert.Equal(vm.PendingNote, shell.Window.Table.PendingBar.Note);

        // a file with the location only there has nothing ExifTool can remove
        shell.Reader.Maker = p => Doc(p, """, "JUMBF:JSON:EXIF_GPSLatitude": {"id": "EXIF_GPSLatitude", "desc": "EXIF GPS Latitude", "val": 43.15}""");
        vm.Shell = new RecordingShell { Answer = DialogAnswer.Secondary };
        await vm.OpenFileAsync("only.jpg");
        await shell.WaitForAsync(() => vm.Document?.FileName == "only.jpg");
        Assert.False(vm.RemoveGpsCommand.CanExecute(null));
        Assert.Equal("Only in the Content Credentials, which ExifTool can't edit", vm.GpsDescription);
    }


    [AvaloniaFact]
    public async Task RemoveAiGenerationDataKeepsTheProvenance()
    {
        using var shell = TestShell.Open(1400);
        var vm = shell.ViewModel;
        shell.Reader.Maker = TestDocuments.MakeAi;
        shell.Reader.Ai = TestDocuments.Ai();
        await vm.OpenFileAsync("ai.png");
        await shell.WaitForAsync(() => vm.HasAi);

        Assert.True(vm.RemoveAiCommand.CanExecute(null));
        Assert.Equal("Prompts, workflows and parameters · provenance kept", vm.AiRemovalDescription);
        vm.RemoveAiCommand.Execute(null);
        await shell.WaitForAsync(() => vm.Rows.RowsInView.Any(r => r.EditState == EditState.Removed));

        Assert.Equal(["-PNG:Parameters="], vm.Edits.AiRedaction!.Arguments);
        Assert.Equal(EditState.Removed, vm.Rows.RowsInView.Single(r => r.Key == "PNG:Parameters").EditState);
        Assert.Equal(EditState.None, vm.Rows.RowsInView.Single(r => r.TagName == "DigitalSourceType" && !r.IsAi).EditState);
        Assert.Contains(vm.Rows.RowsInView, r => r is { IsAi: true, EditState: EditState.Removed });
        Assert.Contains(vm.Rows.RowsInView, r => r is { IsAi: true, EditState: EditState.None, Ai.Kind: Core.Ai.AiFieldKind.Provenance });
        Assert.Equal("Already staged", vm.AiRemovalDescription);
    }


    [AvaloniaFact]
    public async Task AddTagStagesANewRowWhichCanBeEditedAndDropped()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;
        var recorder = new RecordingShell();
        vm.Shell = recorder;

        vm.OpenAddTagCommand.Execute(null);
        shell.Settle();
        var card = Inspector(shell).AddTagCard;
        Assert.True(vm.IsAddingTag);
        Assert.True(card.IsEffectivelyVisible);
        Assert.Equal(["EXIF", "XMP", "IPTC"], vm.AddGroups);
        Assert.Equal("EXIF", vm.AddGroup);
        Assert.Equal("Artist", vm.AddSuggestions[0]);
        Assert.False(vm.AddTagCommand.CanExecute(null));

        vm.AddGroup = "XMP";
        Assert.Equal("Title", vm.AddSuggestions[0]);
        vm.AddName = "Title";
        vm.AddValue = "Lanterns";
        Assert.True(vm.AddTagCommand.CanExecute(null));
        vm.AddTagCommand.Execute(null);

        await shell.WaitForAsync(() => vm.FocusedTag?.Key == "New:XMP:Title");
        Assert.False(vm.IsAddingTag);
        Assert.Equal(("Tag staged", "XMP:Title"), recorder.Toasts[^1]);
        Assert.Equal(EditState.Added, vm.FocusedTag!.State);
        Assert.Equal("-XMP:Title", vm.FocusedTag.ArgumentKey);
        Assert.False(vm.FocusedTag.IsReadOnly);

        vm.StartEditCommand.Execute(null);
        vm.EditText = "Lantern";
        vm.ApplyEditCommand.Execute(null);
        await shell.WaitForAsync(() => vm.FocusedTag?.Value == "Lantern");
        Assert.Equal("Lantern", Assert.Single(vm.Edits.Added).Value);

        vm.ToggleDeleteCommand.Execute(null);
        await shell.WaitForAsync(() => vm.Rows.RowsInView.All(r => r.Key != "New:XMP:Title"));
        Assert.True(vm.Edits.IsEmpty);

        // Esc closes the card before the window's chain goes on
        vm.OpenAddTagCommand.Execute(null);
        Assert.True(vm.Escape());
        Assert.False(vm.IsAddingTag);
    }


    [AvaloniaFact]
    public async Task AddTagTakesOnlyNamesExifToolCanWriteAndSpellsThemItsWay()
    {
        using var shell = TestShell.Open(1400);
        var vm = shell.ViewModel;
        var tagDb = TagDb.LoadEmbedded();
        shell.Reader.Maker = p => Doc(p, """, "File:FileType": {"id": "FileType", "desc": "File Type", "val": "HEIC"}""", tagDb);
        await vm.OpenFileAsync("a.heic");
        await shell.WaitForAsync(() => vm.HasDocument);

        // HEIC takes no IPTC (S5)
        vm.OpenAddTagCommand.Execute(null);
        Assert.Equal(["EXIF", "XMP"], vm.AddGroups);

        vm.AddName = "NotATag";
        Assert.Equal("ExifTool has no EXIF tag it can write named NotATag", vm.AddHint);
        Assert.False(vm.AddTagCommand.CanExecute(null));

        vm.AddName = "art";
        Assert.Contains("Artist", vm.AddSuggestions);
        Assert.Equal("ExifTool has no EXIF tag it can write named art", vm.AddHint);

        vm.AddName = "artist";
        Assert.Null(vm.AddHint);
        vm.AddTagCommand.Execute(null);
        Assert.Equal("Artist", Assert.Single(vm.Edits.Added).Name);
    }


    [AvaloniaFact]
    public async Task AddTagPushesTheInspectorInBelowItsDockingWidth()
    {
        using var shell = await TestShell.OpenWithFileAsync(700);
        var vm = shell.ViewModel;
        Assert.False(vm.IsInspectorShown);

        vm.OpenAddTagCommand.Execute(null);
        Assert.True(vm.IsInspectorPushed);
        Assert.False(vm.IsRailPushed);
    }


    [AvaloniaFact]
    public async Task SaveBinaryDataWritesTheTagsBytesNamedByWhatTheyAre()
    {
        using var shell = TestShell.Open(1400);
        var vm = shell.ViewModel;
        shell.Reader.Maker = TestDocuments.MakeRaw;
        shell.Reader.Binary = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];
        await vm.OpenFileAsync("x.arw");
        await shell.WaitForAsync(() => vm.HasDocument);

        vm.Selection.SelectCell(Row(vm, "Thumbnail Image").Key, TableColumns.Value);
        shell.Settle();
        Assert.True(vm.FocusedTag!.IsBinary);
        Assert.True(vm.FocusedTag.IsReadOnly);
        var card = Inspector(shell).TagCard;
        Assert.True(Part<GlassButton>(card, "PART_SaveBinary").IsVisible);
        Assert.False(Part<GlassButton>(card, "PART_Edit").IsVisible);

        var recorder = new RecordingShell();
        vm.Shell = recorder;
        await vm.SaveBinaryCommand.ExecuteAsync(null);
        Assert.Equal(shell.Reader.Binary, recorder.Saved.ToArray());
        Assert.Equal(("Saved binary data", "x_ThumbnailImage.jpg · 0.0 kB"), recorder.Toasts[^1]);

        shell.Reader.Binary = null;
        await vm.SaveBinaryCommand.ExecuteAsync(null);
        Assert.Equal("Couldn't read the binary data", recorder.Toasts[^1].Title);
    }


    [AvaloniaFact]
    public async Task AValueWellShowsTheRingWhileItsTextHasFocus()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;
        vm.Selection.SelectCell(KeyOf(vm, "Tag 2"), TableColumns.Value);
        shell.Settle();

        var card = Inspector(shell).TagCard;
        var ring = Part<SquircleBorder>(card, "PART_WellRing");
        var value = Part<SelectableTextBlock>(card, "PART_Value");
        Assert.Null(value.FocusAdorner);
        Assert.Equal(0, ring.Opacity);

        value.Focus(NavigationMethod.Tab);
        shell.Settle(250);
        Assert.Equal(1, ring.Opacity);

        Part<GlassButton>(card, "PART_Close").Focus(NavigationMethod.Tab);
        shell.Settle(250);
        Assert.Equal(0, ring.Opacity);
    }


    [AvaloniaFact]
    public async Task TheCommandBarShowsTheWriteSaveRunsAndRemembersExpanded()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;
        var panel = shell.Window.CommandBarView.Bar;
        Assert.False(panel.IsWrite);

        Delete(vm, "Tag 2");
        await shell.WaitForAsync(() => vm.IsCommandWrite);
        Assert.True(panel.IsWrite);

        panel.IsExpanded = true;
        Assert.True(vm.IsCommandExpanded);
        Assert.True(shell.Settings.Current.CommandBarExpanded);
    }


    [AvaloniaFact]
    public void TheCommandBarExpandsWrapsAndCollapsesToOneLine()
    {
        MotionTokens.IsFrozen = true;
        try
        {
            var path = "C:/Users/someone/Pictures/" + string.Concat(Enumerable.Repeat("long folder name/", 12)) + "DSCF4127.JPG";
            var panel = new CommandPanel
            {
                Tokens = [new("exiftool", CommandTokenKind.Executable), new("-fast", CommandTokenKind.Flag), new("-charset", CommandTokenKind.Flag), new("UTF8", CommandTokenKind.Value), new(path, CommandTokenKind.Path)],
            };
            panel.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            var window = Show(panel, 480);
            var host = Part<Panel>(panel, "PART_TextHost");
            var text = Part<SelectableTextBlock>(panel, "PART_Text");
            var copy = Part<GlassButton>(panel, "PART_Copy");
            var buttons = Part<Panel>(panel, "PART_Buttons");

            // DS 6.12: one line of 20 with an ellipsis, Copy beside Expand
            Assert.Equal(20, host.Height);
            Assert.Equal(TextWrapping.NoWrap, text.TextWrapping);
            Assert.Equal(66, buttons.Width);
            Assert.Equal(new Vector(-34, 0), Translation(copy));
            Assert.Equal(40, panel.Bounds.Height, 0.5);
            Assert.Equal(WritePlan.ToCommandLine(panel.Tokens), panel.CommandLine);

            // wrapped, as tall as the text up to 140, Copy stacked above Expand
            // the buttons glide with XAML transitions, which run even with motion frozen
            panel.IsExpanded = true;
            Wait(450);
            Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
            Assert.InRange(host.Height, 40, 140);
            Assert.Equal(32, buttons.Width);
            Assert.Equal(new Vector(0, -34), Translation(copy));
            Assert.Equal(UiText.COLLAPSE_COMMAND, panel.ToggleTip);

            panel.IsExpanded = false;
            Wait(450);
            Assert.Equal(20, host.Height);
            Assert.Equal(TextWrapping.NoWrap, text.TextWrapping);

            // the Write chip while edits are staged; a value's line break stays on the one line
            panel.IsWrite = true;
            panel.Tokens = [new("exiftool", CommandTokenKind.Executable), new("\"-XMP:Title=a\nb\"", CommandTokenKind.Write), new("a.jpg", CommandTokenKind.Path)];
            Dispatcher.UIThread.RunJobs();
            Assert.True(Part<Badge>(panel, "PART_Write").IsVisible);
            Assert.Equal("exiftool \"-XMP:Title=a↵b\" a.jpg", string.Concat(text.Inlines!.OfType<Run>().Select(r => r.Text)));
            window.Close();
        }
        finally
        {
            MotionTokens.IsFrozen = false;
        }
    }


    [AvaloniaFact]
    public void TheCommandTypesItselfInWhileAFileIsRead()
    {
        var panel = new CommandPanel { Tokens = [new("exiftool", CommandTokenKind.Executable), new("-fast", CommandTokenKind.Flag), new("C:/photos/DSCF4127.JPG", CommandTokenKind.Path)] };
        var window = Show(panel, 480);
        var text = Part<SelectableTextBlock>(panel, "PART_Text");
        string Shown() => string.Concat(text.Inlines!.OfType<Run>().Select(r => r.Text));

        panel.IsTyping = true;
        Assert.Equal(string.Empty, Shown());
        Wait(40);
        var partial = Shown();
        Assert.InRange(partial.Length, 1, panel.CommandLine!.Length - 1);
        Assert.StartsWith(partial, panel.CommandLine);

        // the read ending shows it whole
        panel.IsTyping = false;
        Assert.Equal(panel.CommandLine, Shown());
        window.Close();
    }


    [AvaloniaFact]
    public void ThePendingBarSlidesInAndShowsItsNoteAndSpinner()
    {
        MotionTokens.IsFrozen = true;
        try
        {
            var bar = new PendingBar { Title = "3 pending changes", Subtitle = "Save writes a.jpg · backup kept as a.jpg_original", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
            var window = Show(bar, 520);
            Assert.False(bar.IsVisible);

            bar.IsOpen = true;
            Dispatcher.UIThread.RunJobs();
            Assert.True(bar.IsVisible);
            Assert.False(Part<DockPanel>(bar, "PART_Note").IsVisible);
            Assert.InRange(bar.Bounds.Height, 50, 50.5);

            bar.Note = "The location in the Content Credentials stays";
            bar.IsSaving = true;
            Dispatcher.UIThread.RunJobs();
            Assert.True(Part<DockPanel>(bar, "PART_Note").IsVisible);
            Assert.True(Part<TextBlock>(bar, "PART_SavingText").IsVisible);
            Assert.True(bar.GetVisualDescendants().OfType<SpinningIcon>().Single().IsSpinning);

            bar.IsOpen = false;
            Wait(20);
            Assert.False(bar.IsVisible);
            window.Close();
        }
        finally
        {
            MotionTokens.IsFrozen = false;
        }
    }


    [AvaloniaFact]
    public void TheAddTagCardOffersNamesAndTakesTheHighlightedOne()
    {
        var card = new AddTagCard { Groups = ["EXIF", "XMP", "IPTC"], Group = "EXIF", Suggestions = ["Artist", "ArtworkTitle"] };
        var window = Show(card, 320);
        var name = Part<TextBox>(card, "PART_Name");
        var popup = Part<Avalonia.Controls.Primitives.Popup>(card, "PART_Suggestions");

        name.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.True(popup.IsOpen);

        window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
        window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("ArtworkTitle", card.TagName);
        Assert.False(popup.IsOpen);

        // Esc closes the list first, then cancels the card
        var cancelled = false;
        card.CancelCommand = new Relay(() => cancelled = true);
        name.Text = "Art";
        Dispatcher.UIThread.RunJobs();
        Assert.True(popup.IsOpen);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.False(popup.IsOpen);
        Assert.False(cancelled);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.True(cancelled);
        window.Close();
    }

    // the whole pipeline on the bundled ExifTool: the menu and the card stage, the command bar shows the write, Save writes a Unicode path and reads it back
    [AvaloniaFact]
    public async Task TheRealExifToolWritesWhatWasStagedAndTheTableReadsItBack()
    {
        var exe = Path.Combine(Render.SourceDir, "__assets", "exiftool", OperatingSystem.IsWindows() ? Path.Combine("win", "exiftool.exe") : Path.Combine("unix", "exiftool"));
        Assert.SkipUnless(File.Exists(exe), "no bundled ExifTool");

        var folder = Directory.CreateTempSubdirectory("eg-save-").FullName;
        var file = Path.Combine(folder, "ảnh lưu.jpg");
        File.Copy(Path.Combine(Render.SourceDir, "tests", "fixtures", "ai", "negative_camera.jpg"), file);
        var settings = new Core.Settings.SettingsStore(folder);
        settings.Update(s => s.ExifToolPath = exe);
        var metadata = new Services.MetadataService(() => settings.Current);
        try
        {
            metadata.Start();
            var recorder = new RecordingShell();
            var vm = new MainViewModel(settings, metadata, metadata) { Shell = recorder };
            await vm.OpenFileAsync(file);
            await Until(() => vm.HasDocument && !vm.IsLoading);

            var make = vm.Rows.RowsInView.Single(r => r.TagName == "Make" && r.Group == "EXIF");
            vm.Selection.SelectCell(make.Key, TableColumns.Value);
            vm.StartEditCommand.Execute(null);
            vm.EditText = "Fujifilm";
            vm.ApplyEditCommand.Execute(null);
            vm.OpenAddTagCommand.Execute(null);
            vm.AddGroup = "XMP";
            vm.AddName = "title";
            vm.AddValue = "Đèn lồng";
            vm.AddTagCommand.Execute(null);
            await Until(() => vm.PendingCount == 2);

            // the command bar shows the write Save runs, the name spelled as ExifTool spells it
            var command = WritePlan.ToCommandLine(vm.CommandTokens);
            Assert.True(vm.IsCommandWrite);
            Assert.Contains("-XMP:Title=Đèn lồng", command, StringComparison.Ordinal);
            Assert.Contains("-IFD0:Make=Fujifilm", command, StringComparison.Ordinal);

            Assert.True(await vm.SaveAsync());
            await Until(() => vm.Document?.Find("XMP", "Title") is not null);
            Assert.Equal("Fujifilm", vm.Document!.Find("EXIF", "Make")!.Value);
            Assert.Equal("Đèn lồng", vm.Document.Find("XMP", "Title")!.Value);
            Assert.True(File.Exists(file + "_original"));
            Assert.Equal(("Saved 2 changes", "Backup kept as ảnh lưu.jpg_original"), recorder.Toasts[^1]);
            Assert.True(vm.Edits.IsEmpty);
            Assert.Equal(make.Key, vm.Selection.CurrentKey);
        }
        finally
        {
            await metadata.DisposeAsync();
            await settings.FlushAsync();
            Directory.Delete(folder, recursive: true);
        }
    }

    #endregion // Tests


    #region Private methods

    // no window here: the dispatcher runs the posted work while the reads finish on the thread pool
    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 600 && !condition(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Assert.True(condition());
    }


    private static string KeyOf(MainViewModel vm, string name) => Row(vm, name).Key;


    private static Core.Query.ViewRow Row(MainViewModel vm, string name) => vm.Rows.RowsInView.Single(r => r.Name == name);


    private static void Delete(MainViewModel vm, string name)
    {
        vm.Selection.SelectCell(KeyOf(vm, name), TableColumns.Value);
        vm.ToggleDeleteCommand.Execute(null);
    }


    private static InspectorPanel Inspector(TestShell shell) => (InspectorPanel)shell.Window.InspectorContent.Children[0];


    // a file with a location in EXIF, in XMP and the Composite position made from them
    private static MetadataDocument WithGps(string path) => Doc(path, """
        , "File:FileType": {"id": "FileType", "desc": "File Type", "val": "JPEG"}
        , "EXIF:IFD0:Make": {"id": "0x010f", "desc": "Make", "val": "FUJIFILM"}
        , "EXIF:GPS:GPSLatitude": {"id": "0x0002", "desc": "GPS Latitude", "val": "22 deg 16' 33.24\""}
        , "XMP:XMP-exif:GPSLongitude": {"id": "GPSLongitude", "desc": "GPS Longitude", "val": "114 deg 8' 43.80\" E"}
        , "Composite:GPSPosition": {"id": "GPSPosition", "desc": "GPS Position", "val": "22 deg 16' 33.24\" N, 114 deg 8' 43.80\" E"}
        """);


    private static MetadataDocument Doc(string path, string tags, TagDb? tagDb = null)
    {
        var json = $"[{{\"SourceFile\": \"{path}\", \"ExifTool:ExifTool:ExifToolVersion\": {{\"id\": \"ExifToolVersion\", \"desc\": \"ExifTool Version Number\", \"val\": 13.59}}{tags}}}]";
        var read = TableReader.Read(new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(json)), new StringPool());
        return MetadataDocument.Create(path, read, tagDb);
    }


    private static Window Show(Control content, double width)
    {
        var window = new Window { Width = width, Height = 400, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }


    private static T Part<T>(Control control, string name) where T : Control
    {
        Dispatcher.UIThread.RunJobs();
        return control.GetVisualDescendants().OfType<T>().First(c => c.Name == name);
    }


    private static Vector Translation(Visual visual) => visual.RenderTransform?.Value is { } m ? new Vector(m.M31, m.M32) : default;


    private static void Wait(int milliseconds)
    {
        var frame = new DispatcherFrame();
        DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(milliseconds));
        Dispatcher.UIThread.PushFrame(frame);
    }

    #endregion // Private methods


    #region Nested types

    private sealed class Relay(Action action) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => action();
    }

    #endregion // Nested types
}
