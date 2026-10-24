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
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using ExifGlass.Core.Ai;
using ExifGlass.Core.Export;
using ExifGlass.Core.Metadata;
using ExifGlass.Core.Query;
using ExifGlass.UI.Controls;
using ExifGlass.ViewModels;
using ExifGlass.Views;
using Xunit;

namespace ExifGlass.UI.Tests;

/// <summary>
/// The AI experience (P7): the extended AI card, the selected AI field, the table's AI rows and links, the rail's AI chips and badge.
/// </summary>
public sealed class AiTests
{
    #region Tests

    [AvaloniaFact]
    public void TheAiCardShowsEveryPartOfAnA1111File()
    {
        var card = new AiCard { Width = 304, Info = TestDocuments.Ai() };
        var window = Show(card);

        Assert.Equal("Generated", card.KindText);
        Assert.Equal("PNG:Parameters", card.SourceKey);
        Assert.Equal(["XMP:XMP-iptcExt:DigitalSourceType"], card.OtherSources);
        Assert.Contains("<lora:add_detail:0.8>", card.Prompt, StringComparison.Ordinal);
        Assert.Equal("lowres, easynegative", card.NegativePrompt);
        Assert.Contains("easynegative", card.Embeddings);

        // the first eight in the grid, model and VAE in the models list, the rest under All parameters
        Assert.Equal(8, card.Parameters.Count);
        Assert.DoesNotContain(card.Parameters, p => p.Label is "Model" or "VAE");
        Assert.True(Part<Control>(card, "PART_AllParameters").IsVisible);
        Assert.Equal((card.Parameters.Count + card.MoreParameters.Count).ToString(System.Globalization.CultureInfo.InvariantCulture), card.ParameterCount);

        Assert.Equal(["sd_xl_base_1.0", "sdxl_vae", "add_detail"], card.Models.Select(m => m.Name));
        Assert.Equal(["4x-UltraSharp", "easynegative"], card.MoreModels.Select(m => m.Name).Order(StringComparer.Ordinal));
        Assert.Equal("0.8", card.Models.Single(m => m.Name == "add_detail").Weight);
        Assert.True(Part<Control>(card, "PART_MoreModels").IsVisible);

        Assert.NotNull(card.Iptc);
        Assert.Null(card.Credentials);
        Assert.Null(card.Workflow);
        Assert.Null(card.PassesView);
        Assert.False(Part<Control>(card, "PART_ProvenanceNote").IsVisible);

        // a section without content is never built (R24): no workflow buttons, no credentials badge
        Assert.DoesNotContain(card.GetVisualDescendants().OfType<Button>(), b => Equals(b.Tag, AiAction.SaveWorkflow));
        Assert.Single(card.GetVisualDescendants().OfType<SourceLink>(), l => l.SourceKey == "XMP:XMP-iptcExt:DigitalSourceType");
        window.Close();
    }


    [AvaloniaFact]
    public void ContentCredentialsAreShownNotVerifiedWithTheirIngredients()
    {
        var active = new C2paManifest("urn:a", 2, "ChatGPT", null, "image.png", null,
            [new C2paAction("c2pa.opened", null, null, null, null)], [new C2paIngredient(null, "parentOf", null, "urn:b")], [], true, false);
        var created = new C2paManifest("urn:b", 2, "ChatGPT", null, "image.png", null,
            [new C2paAction("c2pa.created", "GPT-4o", null, "http://cv.iptc.org/newscodes/digitalsourcetype/trainedAlgorithmicMedia", null)], [], [], false, false);
        var info = new AiGenerationInfo([new AiSourceInfo
        {
            Source = AiSourceKind.C2pa,
            Generator = "ChatGPT",
            Kind = AiKind.Generated,
            C2pa = new C2paInfo([active, created], null),
            SourceKeys = ["JUMBF:CBOR:Actions"],
        }]);

        var card = new AiCard { Width = 304, Info = info };
        var window = Show(card);

        Assert.Contains(card.Credentials!.Rows, r => r is { Key: "Claimed by", Value: "ChatGPT" });
        Assert.Equal(new AiIngredientView("image.png", "Created by GPT-4o · Trained algorithmic media"), card.Credentials.Ingredients.Single());
        Assert.Contains(card.GetVisualDescendants().OfType<Badge>(), b => b.Text == UiText.NOT_VERIFIED);
        Assert.True(Part<Control>(card, "PART_ProvenanceNote").IsVisible);
        Assert.Equal(AiAction.CopyJson, Part<Button>(card, "PART_MainAction").Tag);
        window.Close();
    }


    [AvaloniaFact]
    public void TheCardsSayWhatTheyCopyAndShow()
    {
        var requests = new List<AiActionRequest>();
        var command = new RelayCommand<AiActionRequest>(r => requests.Add(r!));
        var card = new AiCard { Width = 304, Info = TestDocuments.Ai(), ActionCommand = command };
        var field = new AiFieldCard
        {
            Width = 304,
            View = new AiFieldView("AI:seed", "Seed", "2841937305", "PNG:Parameters", "A1111 infotext", false, []),
            ActionCommand = command,
        };
        var window = Show(new StackPanel { Children = { card, field } });

        Click(Part<Button>(card, "PART_MainAction"));
        Click(card.GetVisualDescendants().OfType<SourceLink>().First(l => l.IsEffectivelyVisible));
        Click(Part<Button>(field, "PART_ShowSource"));
        Click(field.GetVisualDescendants().OfType<Button>().First(b => Equals(b.Tag, AiAction.CopyText)));

        Assert.Equal(
            [
                new AiActionRequest(AiAction.CopyGenerationParameters),
                new AiActionRequest(AiAction.ShowSourceTag, Key: "PNG:Parameters"),
                new AiActionRequest(AiAction.ShowSourceTag, Key: "PNG:Parameters"),
                new AiActionRequest(AiAction.CopyText, "2841937305", "Seed"),
            ],
            requests);
        window.Close();
    }


    [AvaloniaFact]
    public void ASelectedPromptReadsInTheUiFont()
    {
        var field = new AiFieldCard { Width = 304, View = new AiFieldView("AI:prompt", "Prompt", "a cat (fog:1.2)", "PNG:Parameters", "A1111 infotext", true, []) };
        var window = Show(field);
        Assert.True(Part<Control>(field, "PART_Prompt").IsVisible);
        Assert.False(Part<Control>(field, "PART_Value").IsVisible);
        Assert.Equal("-PNG:Parameters", field.View!.SourceArgument);

        field.View = new AiFieldView("AI:seed", "Seed", "5", null, null, false, []);
        Assert.False(Part<Control>(field, "PART_Prompt").IsVisible);
        Assert.True(Part<Control>(field, "PART_Value").IsVisible);
        Assert.False(Part<Control>(field, "PART_ShowSource").IsVisible);
        window.Close();
    }


    [AvaloniaFact]
    public async Task AnAiFieldHasItsOwnCardAndShowSourceTagSelectsTheTag()
    {
        using var shell = await OpenAiAsync();
        var vm = shell.ViewModel;

        var prompt = vm.Rows.RowsInView.Single(r => r is { IsAi: true, Name: "Prompt" });
        vm.Selection.SelectCell(prompt.Key, TableColumns.Value);
        shell.Settle();
        Assert.Equal("Prompt", vm.FocusedAiField?.Name);
        Assert.True(vm.FocusedAiField!.IsPrompt);
        Assert.Null(vm.FocusedTag);

        // the selected row's card is on top of every card, the AI card included (owner at G7)
        var inspector = Inspector(shell);
        Assert.True(inspector.AiFieldCard.IsVisible);
        Assert.Same(inspector.AiFieldCard, FirstShownCard(inspector));

        await vm.ShowSourceTagAsync("PNG:Parameters");
        shell.Settle(600);
        Assert.Equal(("PNG:Parameters", TableColumns.Value), (vm.Selection.CurrentKey, vm.Selection.CurrentColumn));
        Assert.Null(vm.FocusedAiField);
        Assert.Equal(vm.Rows.RowsInView.Count(r => r is { IsAi: true, SourceKey: "PNG:Parameters" }), vm.FocusedTag?.AiFields);
        Assert.Equal("(same as value)", vm.FocusedTag!.Raw);
        Assert.Same(inspector.TagCard, FirstShownCard(inspector));
        Assert.True(inspector.AiCard.IsVisible);

        // Raw and Format copy as Tag key does; "(same as value)" copies the value (owner after G7)
        var recorder = new RecordingShell();
        vm.Shell = recorder;
        await vm.CopyFocusRawCommand.ExecuteAsync(null);
        Assert.Equal(vm.FocusedTag.Value, recorder.Copied[^1]);
        Assert.Equal("Copied raw value", recorder.Toasts[^1].Title);
        // a tag without a format ("—") has nothing to copy
        Assert.Equal(TagIdRule.NONE, vm.FocusedTag.Format);
        await vm.CopyFocusFormatCommand.ExecuteAsync(null);
        Assert.Single(recorder.Copied);
    }


    [AvaloniaFact]
    public async Task ShowSourceTagClearsTheFiltersThatHideIt()
    {
        using var shell = await OpenAiAsync();
        var vm = shell.ViewModel;
        vm.ToggleFilter(QuickFilter.AiPrompts);
        await shell.WaitForAsync(() => vm.Rows.Query.Filters.Count == 1);
        Assert.DoesNotContain(vm.Rows.RowsInView, r => r.Key == "PNG:Parameters");

        await vm.ShowSourceTagAsync("PNG:Parameters");
        shell.Settle(600);
        Assert.Contains(vm.Rows.RowsInView, r => r.Key == "PNG:Parameters");
        Assert.Equal("PNG:Parameters", vm.Selection.CurrentKey);
    }


    [AvaloniaFact]
    public async Task TheTableCountsAiFieldsApartAndLinksOnlyOtherSources()
    {
        using var shell = await OpenAiAsync();
        var vm = shell.ViewModel;

        Assert.Equal("4 tags", vm.Summary);
        Assert.Equal($"· {vm.Rows.AiTotal} AI fields", vm.AiSummary);
        Assert.Equal($"EXPORTS THE 4 TAGS AND {vm.Rows.AiTotal} AI FIELDS IN VIEW", vm.ExportHeader);
        Assert.Equal("PNG:Parameters", vm.Rows.Groups[0].SourceKey);

        // the source type comes from IPTC, not the group's PNG:Parameters: only its row names its tag; the tag carries the AI sparkle
        vm.ToggleFilter(QuickFilter.AiProvenance);
        await shell.WaitForAsync(() => vm.Rows.Query.Filters.Count == 1);
        shell.Settle();
        Assert.Equal(["XMP:XMP-iptcExt:DigitalSourceType"], Links(shell).Distinct());
        Assert.Contains("Digital Source Type", Marked(shell));

        vm.ToggleFilter(QuickFilter.AiProvenance);
        vm.ToggleGroup(GroupInfo.AI_GROUP);
        await shell.WaitForAsync(() => vm.Rows.Groups[0].IsCollapsed);
        shell.Settle();
        Assert.Empty(Links(shell));
        Assert.Contains("Parameters", Marked(shell));
    }


    [AvaloniaFact]
    public async Task TheRailShowsTheAiBadgeAndAiChipsOnlyForAFileWithAi()
    {
        using var shell = TestShell.Open(1400);
        var vm = shell.ViewModel;
        await vm.OpenFileAsync("a.jpg");
        await shell.WaitForAsync(() => vm.HasDocument);
        shell.Settle();
        Assert.Null(vm.AiBadge);
        Assert.Equal([QuickFilter.Ai], vm.ShownAiFilters.Select(f => f.Filter));

        shell.Reader.Maker = TestDocuments.MakeAi;
        shell.Reader.Ai = TestDocuments.Ai();
        await vm.OpenFileAsync("b.png");
        await shell.WaitForAsync(() => vm.HasAi);
        shell.Settle();

        Assert.Equal("AUTOMATIC1111 1.10.1", vm.AiBadge);
        Assert.Equal(AiKind.Generated, vm.AiBadgeKind);
        Assert.Equal(vm.AiQuickFilters, vm.ShownAiFilters);
        Assert.True(vm.AiQuickFilters.Single(f => f.Filter == QuickFilter.AiPrompts).IsAvailable);
        Assert.True(vm.AiQuickFilters.Single(f => f.Filter == QuickFilter.AiProvenance).IsAvailable);
        Assert.False(vm.AiQuickFilters.Single(f => f.Filter == QuickFilter.AiModels).IsOn);

        vm.JumpToAiCommand.Execute(null);
        shell.Settle(600);
        Assert.Equal(GroupInfo.AI_GROUP, shell.Window.Table.Table.ActiveGroup);
    }


    [AvaloniaFact]
    public async Task ASlowAiReadShowsTheSkeletonUntilItEnds()
    {
        using var shell = TestShell.Open(1400);
        var vm = shell.ViewModel;
        var hold = new TaskCompletionSource<AiGenerationInfo?>(TaskCreationOptions.RunContinuationsAsynchronously);
        shell.Reader.Maker = TestDocuments.MakeAi;
        shell.Reader.AiHold = hold;

        _ = vm.OpenFileAsync("slow.png");
        await shell.WaitForAsync(() => vm.HasDocument);
        Assert.False(vm.IsAiLoading);
        await shell.WaitForAsync(() => vm.IsAiLoading);
        shell.Settle();
        Assert.True(Inspector(shell).AiCard.IsVisible);
        Assert.True(Part<Control>(Inspector(shell).AiCard, "PART_Loading").IsVisible);

        hold.SetResult(TestDocuments.Ai());
        await shell.WaitForAsync(() => vm.HasAi);
        shell.Settle();
        Assert.False(vm.IsAiLoading);
        Assert.False(Part<Control>(Inspector(shell).AiCard, "PART_Loading").IsVisible);
    }


    [AvaloniaFact]
    public async Task AnAiTrainingSignalAloneIsARowInTheDetailsCard()
    {
        using var shell = TestShell.Open(1400);
        var vm = shell.ViewModel;
        shell.Reader.Ai = new AiGenerationInfo([new AiSourceInfo
        {
            Source = AiSourceKind.TrainingSignals,
            Generator = "AI-training signals",
            Kind = AiKind.None,
            Training = new TrainingSignals("Prohibited for AI/ML training", null),
            SourceKeys = ["XMP:XMP-plus:DataMining"],
        }]);
        await vm.OpenFileAsync("cam.jpg");
        await shell.WaitForAsync(() => vm.Document?.Ai is not null);

        Assert.False(vm.HasAi);
        Assert.False(vm.ShowAiCard);
        Assert.Null(vm.AiBadge);
        Assert.Equal(0, vm.Rows.AiTotal);
        Assert.Contains(vm.Details!, d => d is { Label: "AI training", Value: "Not allowed", Sub: "PLUS DataMining" });
    }

    [AvaloniaFact]
    public async Task TheAiDataIsCopiedAndSavedAsTheGeneratorsReadIt()
    {
        using var shell = TestShell.Open(1400);
        var vm = shell.ViewModel;
        var recorder = new RecordingShell();
        const string WORKFLOW = "{\"nodes\": [{\"type\": \"KSampler\", \"title\": \"日本の風景\"}]}";
        var info = TestDocuments.Ai();
        var comfy = new AiSourceInfo
        {
            Source = AiSourceKind.ComfyUi,
            Generator = "ComfyUI",
            Kind = AiKind.Generated,
            Prompts = [new AiPrompt("a cat", AiPromptRole.Positive)],
            Workflows = [new AiWorkflowSummary(AiWorkflowFormat.ComfyUiWorkflow, "ComfyUI workflow 0.4", 1, 0, 1, 0, 0, System.Text.Encoding.UTF8.GetByteCount(WORKFLOW), WORKFLOW, "PNG:Workflow")],
            SourceKeys = ["PNG:Workflow"],
        };
        shell.Reader.Maker = TestDocuments.MakeAi;
        shell.Reader.Ai = new AiGenerationInfo([.. info.Sources, comfy]);
        await vm.OpenFileAsync("cat.png");
        await shell.WaitForAsync(() => vm.HasAi);
        vm.Shell = recorder;

        // Copy generation parameters gives the infotext back as A1111 wrote it, so "Read generation parameters" fills the same settings
        await vm.AiActionCommand.ExecuteAsync(new AiActionRequest(AiAction.CopyGenerationParameters));
        Assert.Equal(TestDocuments.A1111, recorder.Copied[^1]);
        Assert.Equal(("Copied generation parameters", "AUTOMATIC1111 1.10.1 · A1111 text"), recorder.Toasts[^1]);

        await vm.AiActionCommand.ExecuteAsync(new AiActionRequest(AiAction.CopyPrompt));
        Assert.Equal(("Copied prompt", $"{recorder.Copied[^1].Length} characters"), recorder.Toasts[^1]);

        await vm.AiActionCommand.ExecuteAsync(new AiActionRequest(AiAction.CopyText, "2841937305", "Seed"));
        Assert.Equal(("Copied seed", "2841937305"), recorder.Toasts[^1]);

        // the workflow is saved exactly as embedded: UTF-8, no byte order mark, so ComfyUI loads it back
        await vm.AiActionCommand.ExecuteAsync(new AiActionRequest(AiAction.SaveWorkflow));
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes(WORKFLOW), recorder.Saved.ToArray());
        Assert.Equal("Saved workflow", recorder.Toasts[^1].Title);

        await vm.AiActionCommand.ExecuteAsync(new AiActionRequest(AiAction.CopyWorkflowJson));
        Assert.Equal(WORKFLOW, recorder.Copied[^1]);

        await vm.ExportAiCommand.ExecuteAsync(AiExportKind.Json);
        var json = System.Text.Encoding.UTF8.GetString(recorder.Saved.ToArray());
        Assert.Equal(AiExport.ToJson(vm.Document!.Ai!), json);
    }


    [AvaloniaFact]
    public void AHugeValueIsLaidOutOnlyInPart()
    {
        var huge = "{\"nodes\": [" + string.Concat(Enumerable.Repeat("{\"type\": \"KSampler\"}, ", 100_000)) + "]}";
        TextRange[] matches = [new(1, 5), new(ShownText.CELL + 10, 5)];

        // a cell lays out the first 2,048 characters, and drops the matches past them
        var cell = new HighlightTextBlock();
        cell.SetText(huge, matches);
        Assert.Equal(ShownText.CELL + 1, cell.Text!.Length);
        Assert.EndsWith("…", cell.Text, StringComparison.Ordinal);
        Assert.Equal([matches[0]], ShownText.Matches(matches, ShownText.CELL));

        // the value well shows 10,000 and says how many more there are; Copy still has them all
        var tag = new SelectedTagView("PNG:Workflow", "PNG", "Workflow", "Workflow", huge, UiText.SAME_AS_VALUE, "string", "-PNG:Workflow", false);
        Assert.StartsWith(huge[..ShownText.WELL], tag.ShownValue, StringComparison.Ordinal);
        Assert.EndsWith(UiText.MoreCharacters(huge.Length - ShownText.WELL), tag.ShownValue, StringComparison.Ordinal);
        Assert.Equal("short", ShownText.Well("short"));

        // a cut never splits a surrogate pair
        var emoji = new string('a', ShownText.CELL - 1) + "😀";
        Assert.Equal(new string('a', ShownText.CELL - 1) + "…", ShownText.Cut(emoji, ShownText.CELL));
    }

    #endregion // Tests


    #region Private methods

    private static async Task<TestShell> OpenAiAsync()
    {
        var shell = TestShell.Open(1400);
        shell.Reader.Maker = TestDocuments.MakeAi;
        shell.Reader.Ai = TestDocuments.Ai();
        await shell.ViewModel.OpenFileAsync("ai.png");
        await shell.WaitForAsync(() => shell.ViewModel.HasAi);
        shell.Settle();
        return shell;
    }


    private static InspectorPanel Inspector(TestShell shell) => (InspectorPanel)shell.Window.InspectorContent.Children[0];


    private static Control FirstShownCard(InspectorPanel inspector) => new Control[] { inspector.AddTagCard, inspector.AiFieldCard, inspector.TagCard }.Concat(inspector.Cards.Children)
        .Where(c => c.IsEffectivelyVisible).OrderBy(c => c.TranslatePoint(default, inspector)?.Y ?? 0).First();


    private static List<string?> Links(TestShell shell) => [.. shell.Window.Table.Table.GetVisualDescendants().OfType<NameCell>()
        .Where(c => c.IsEffectivelyVisible).SelectMany(c => c.GetVisualChildren().OfType<SourceLink>()).Where(l => l.IsVisible).Select(l => l.SourceKey)];


    private static List<string?> Marked(TestShell shell) => [.. shell.Window.Table.Table.GetVisualDescendants().OfType<NameCell>()
        .Where(c => c.IsEffectivelyVisible && c.GetVisualChildren().OfType<GlassButton>().Any(b => b is { IsVisible: true } and not SourceLink)).Select(c => c.Text)];


    private static Window Show(Control content, double width = 320)
    {
        var window = new Window { Width = width, Height = 900, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }


    private static T Part<T>(Control control, string name) where T : Control
    {
        Dispatcher.UIThread.RunJobs();
        return control.GetVisualDescendants().OfType<T>().First(c => c.Name == name);
    }


    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        Dispatcher.UIThread.RunJobs();
    }

    #endregion // Private methods
}
