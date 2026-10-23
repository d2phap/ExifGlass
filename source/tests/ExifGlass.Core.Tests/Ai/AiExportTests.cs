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
using ExifGlass.Core.Ai;
using ExifGlass.Core.Metadata;
using System.Text.Json;
using Xunit;

namespace ExifGlass.Core.Tests.Ai;

/// <summary>
/// The AI exports, the AI group rows and the redaction plan, on real fixtures where it matters.
/// </summary>
public class AiExportTests(ExifToolFixture exifTool)
{
    #region Tests

    [Theory]
    [InlineData("a1111.png")]
    [InlineData("forge.png")]
    [InlineData("sdnext.png")]
    [InlineData("civitai.jpg")]
    [InlineData("krita.png")]
    [InlineData("a1111_utf8.png")]
    public async Task CopyGenerationParametersGivesTheEmbeddedInfotextBack(string fixture)
    {
        var service = exifTool.Require();
        using var tags = await service.ReadAiTagsAsync(TestEnvironment.Fixture(fixture), TestContext.Current.CancellationToken);
        var embedded = tags.Tags.First(t => t.Name is "Parameters" or "UserComment").Text;

        var info = AiEngine.Parse(tags, null, TestContext.Current.CancellationToken)!;

        Assert.Equal(embedded, AiExport.ToA1111(info.Primary!));
    }


    [Fact]
    public async Task CopyGenerationParametersFromComfyUiUsesA1111Names()
    {
        var info = await Read("comfyui.png");

        var text = AiExport.ToA1111(info.Primary!);

        Assert.Equal("beautiful scenery, 日本の風景\nNegative prompt: text, watermark\nSteps: 20, Sampler: Euler, Schedule type: Normal, CFG scale: 8.0, Seed: 156680208700286, Size: 512x512, Model: v1-5-pruned-emaonly, Denoising strength: 1.0, Version: ComfyUI", text);

        // and A1111's own reader gets the same values back
        var back = A1111Infotext.Parse(text)!;
        Assert.Equal("156680208700286", back.Get("Seed")!.Value);
        Assert.Equal("text, watermark", back.Negative);
    }


    [Fact]
    public async Task CopyGenerationParametersAddsLoraHashesWhenKnown()
    {
        var info = await Read("swarmui.png");

        var text = AiExport.ToA1111(info.Primary!);

        Assert.Contains("Lora hashes: \"detail-tweaker-xl: 0x", text);
        Assert.EndsWith("Version: SwarmUI 0.9.6.2", text);
        Assert.Contains("Sampler: Euler", text);
    }


    [Fact]
    public async Task CopyAsJsonCarriesEverySourceAndMarksC2paUnverified()
    {
        var info = await Read("c2pa_gemini.png");

        using var doc = JsonDocument.Parse(AiExport.ToJson(info));
        var root = doc.RootElement;

        Assert.Equal("Google", root.GetProperty("generator").GetString());
        Assert.True(root.GetProperty("isAi").GetBoolean());
        var c2pa = root.GetProperty("sources")[0].GetProperty("c2pa");
        Assert.False(c2pa.GetProperty("verified").GetBoolean());
        Assert.StartsWith("urn:c2pa:", c2pa.GetProperty("manifests")[0].GetProperty("label").GetString());
        Assert.Equal("Made with Google AI", root.GetProperty("sources")[1].GetProperty("iptc").GetProperty("credit").GetString());
    }


    [Fact]
    public async Task TheWorkflowIsSavedExactlyAsEmbedded()
    {
        var service = exifTool.Require();
        using var tags = await service.ReadAiTagsAsync(TestEnvironment.Fixture("comfyui_flux_subgraph.png"), TestContext.Current.CancellationToken);
        var embedded = tags.Get("PNG", "Workflow")!.Text;

        var info = AiEngine.Parse(tags, null, TestContext.Current.CancellationToken)!;

        var workflow = info.Workflows.Single(w => w.Format == AiWorkflowFormat.ComfyUiWorkflow);
        Assert.Equal(embedded, workflow.Json);
        Assert.Equal((6, 1), (workflow.NodeCount, workflow.SubgraphCount));
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(embedded), workflow.Size);
    }


    [Fact]
    public async Task TheAiGroupShowsEachFieldOnceInTheAddendumOrder()
    {
        var set = AiRows.Build(await Read("a1111.png"));
        var rows = set.Rows;

        Assert.Equal("AI:generator", rows[0].Key);
        Assert.Equal("AUTOMATIC1111 1.10.1", rows[0].Value);
        Assert.Equal(("Generated", AiKind.Generated), (rows[1].Value, rows[1].KindChip!.Value));
        Assert.Equal(["Prompt", "Negative prompt", "Prompt · ADetailer", "Prompt · Hires"], rows.Where(r => r.Kind == AiFieldKind.Prompt).Select(r => r.Label));
        Assert.Equal(["Model", "VAE", "LoRA", "Embedding", "Upscaler", "ControlNet", "Detailer", "Detailer"], rows.Where(r => r.Kind == AiFieldKind.Model).Select(r => r.Label));
        Assert.Equal("Sampler", rows.First(r => r.Kind == AiFieldKind.Parameter).Label);
        Assert.DoesNotContain(rows, r => r.Kind == AiFieldKind.Parameter && r.Name is "model" or "modelHash" or "vae" or "vaeHash");
        Assert.Equal(rows.Count, rows.Select(r => r.Key).Distinct().Count());
        Assert.Empty(AiRows.Build(null).Rows);
    }


    [Fact]
    public async Task OneTagFeedingTheGroupIsNamedOnceInItsHeader()
    {
        var a1111 = AiRows.Build(await Read("a1111.png"));
        Assert.Equal("PNG:Parameters", a1111.SourceKey);
        Assert.All(a1111.Rows, r => Assert.False(r.ShowsSource));
        Assert.Equal("A1111 infotext", a1111.Rows.Single(r => r.Label == "Prompt").ParsedAs);

        // Easy Diffusion writes a chunk per field, so its rows keep their own links
        var easy = AiRows.Build(await Read("easydiffusion.png"));
        Assert.Contains(easy.Rows, r => r.ShowsSource && r.SourceKey != easy.SourceKey);
        Assert.Equal("PNG:Parameters", AiRows.SourceLabel("PNG:Parameters"));
        Assert.Equal("XMP-iptcExt:DigitalSourceType", AiRows.SourceLabel("XMP:XMP-iptcExt:DigitalSourceType"));
        Assert.Equal("JUMBF:Actions", AiRows.SourceLabel("JUMBF:CBOR:Copy1:Actions"));
    }


    [Fact]
    public async Task ChipsAfterAValueAreBlankedInWhatTheTableDraws()
    {
        var rows = AiRows.Build(await Read("a1111.png")).Rows;

        var lora = rows.Single(r => r.Label == "LoRA");
        Assert.Equal("add_detail · 0.8 · 7c6bad76eb54", lora.Value);
        Assert.Equal("add_detail   0.8   7c6bad76eb54", lora.Display);
        Assert.Equal([new AiSpan(13, 3, AiSpanKind.WeightChip), new AiSpan(19, 12, AiSpanKind.Chip)], lora.Spans);

        var prompt = rows.Single(r => r.Label == "Prompt");
        Assert.Equal(prompt.Value, prompt.Display);
        Assert.Contains(prompt.Spans, s => s.Kind == AiSpanKind.Lora && prompt.Value.Substring(s.Start, s.Length) == "<lora:add_detail:0.8>");
        Assert.Contains(prompt.Spans, s => s.Kind == AiSpanKind.Weight && prompt.Value.Substring(s.Start, s.Length) == "(detailed eyes:1.2)");
        Assert.Contains(prompt.Spans, s => s.Kind == AiSpanKind.Embedding && prompt.Value.Substring(s.Start, s.Length) == "easynegative");
        Assert.Contains(prompt.Spans, s => s.Kind == AiSpanKind.Keyword && prompt.Value.Substring(s.Start, s.Length) == "BREAK");
    }


    [Fact]
    public async Task TheAiGroupShowsProvenanceCharactersAndUnknownText()
    {
        var c2pa = AiRows.Build(await Read("c2pa_chatgpt.png")).Rows;
        Assert.Contains(c2pa, r => r is { Label: "Claim generator", Value: "ChatGPT", Kind: AiFieldKind.Provenance });
        Assert.Contains(c2pa, r => r is { Label: "Source type", Value: "Trained algorithmic media · trainedAlgorithmicMedia" });
        Assert.Contains(c2pa, r => r is { Label: "Ingredient 1" } && r.Value.Contains("Created by GPT-4o", StringComparison.Ordinal));
        Assert.Contains(c2pa, r => r is { Label: "Signature", Value: "Not verified · ExifGlass doesn't check C2PA signatures" });

        var aigc = AiRows.Build(await Read("aigc.png")).Rows;
        Assert.Contains(aigc, r => r is { Label: "AI label", Value: "AI-generated · GB 45438-2025 code 1" });

        var character = AiRows.Build(await Read("charactercard.png"));
        Assert.Contains(character.Rows, r => r is { Label: "Character", Value: "Test Character", Kind: AiFieldKind.Character });
        Assert.Equal(AiKind.CharacterCard, character.Rows[1].KindChip);

        var comfy = AiRows.Build(await Read("comfyui.png")).Rows;
        Assert.Contains(comfy, r => r is { Label: "Workflow", Kind: AiFieldKind.Workflow } && r.Value.StartsWith("1 node · 1 sampler · 0 models", StringComparison.Ordinal));
        Assert.Contains(comfy, r => r is { Label: "API prompt" } && r.Value.EndsWith("ComfyUI API prompt", StringComparison.Ordinal));

        var novelai = AiRows.Build(await Read("novelai.png")).Rows;
        Assert.Contains(novelai, r => r is { Label: "Character 1", Value: "girl, umbrella · at 0.5, 0.5" });
    }


    [Fact]
    public async Task ProvenanceAloneHasNoAiGroup()
    {
        Assert.Empty(AiRows.Build(await Read("ai_training_signals.jpg")).Rows);
        Assert.Empty(AiRows.Build(await Read("c2pa_capture.jpg")).Rows);
    }


    [Theory]
    [InlineData("a1111.png", new[] { "-PNG:Parameters=" })]
    [InlineData("comfyui.png", new[] { "-PNG:Prompt=", "-PNG:Workflow=" })]
    [InlineData("comfyui_anim.webp", new[] { "-IFD0:Model=", "-IFD0:Make=" })]
    [InlineData("novelai.png", new[] { "-PNG:Description=", "-PNG:Source=", "-PNG:GenerationTime=", "-PNG:Comment=" })]
    [InlineData("midjourney.png", new[] { "-PNG:Description=" })]
    [InlineData("iptc_ai_fields.jpg", new[] { "-XMP-iptcExt:AIPromptInformation=", "-XMP-iptcExt:AIPromptWriterName=" })]
    [InlineData("c2pa_chatgpt.png", new string[0])]
    [InlineData("fooocus.jpg", new[] { "-IFD0:UserComment=" })]
    public async Task PlansTheRedactionOfPromptsWorkflowsAndParameters(string fixture, string[] expected)
    {
        var plan = AiRedaction.Plan(await Read(fixture), TagDb.LoadEmbedded());

        Assert.Equal(expected, plan.Arguments);
        Assert.Equal(expected.Length == 0, plan.IsEmpty);
    }


    [Fact]
    public async Task WarnsAboutAiDataExifToolCantRemove()
    {
        var mp3 = AiRedaction.Plan(await Read("comfyui_audio.mp3"));
        Assert.True(mp3.IsEmpty);
        Assert.Equal(2, mp3.Warnings.Count);

        var apng = AiRedaction.Plan(await Read("comfyui_anim.png"));
        Assert.Contains(apng.Warnings, w => w.Contains("comf", StringComparison.Ordinal));
    }

    #endregion // Tests


    #region Private methods

    private async Task<AiGenerationInfo> Read(string fixture) =>
        (await exifTool.Require().ReadAiAsync(TestEnvironment.Fixture(fixture), TestContext.Current.CancellationToken))!;

    #endregion // Private methods
}
