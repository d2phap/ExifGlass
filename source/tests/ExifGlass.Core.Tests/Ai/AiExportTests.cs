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
    public async Task TheAiGroupShowsEachFieldOnceWithItsSourceTag()
    {
        var info = await Read("a1111.png");

        var rows = AiRows.Build(info);

        Assert.Equal("AI:generator", rows[0].Key);
        Assert.Equal("AUTOMATIC1111 1.10.1", rows[0].Value);
        Assert.Equal("Generated", rows[1].Value);
        Assert.Equal("PNG:Parameters", rows.Single(r => r.Label == "Prompt").SourceKey);
        Assert.Single(rows, r => r.Label == "Negative prompt");
        Assert.Contains(rows, r => r.Label == "Prompt (Hires)");
        Assert.Contains(rows, r => r is { Label: "LoRA", Value: "add_detail · 0.8 · 7c6bad76eb54" });
        Assert.DoesNotContain(rows, r => r.Name == "model");
        Assert.Equal(rows.Count, rows.Select(r => r.Key).Distinct().Count());
        Assert.Empty(AiRows.Build(null));
    }


    [Fact]
    public async Task TheAiGroupShowsProvenanceTrainingAndUnknownText()
    {
        var c2pa = AiRows.Build(await Read("c2pa_chatgpt.png"));
        Assert.Contains(c2pa, r => r is { Label: "Content Credentials", Value: "2 manifests · not verified" });
        Assert.Contains(c2pa, r => r is { Label: "Digital source type", Value: "Trained algorithmic media" });

        var training = AiRows.Build(await Read("ai_training_signals.jpg"));
        Assert.Contains(training, r => r.Label == "AI training opt-out");

        Assert.Contains(AiRows.Build(await Read("aigc.png")), r => r is { Label: "AIGC label", Value: "AI-generated" });
        Assert.Contains(AiRows.Build(await Read("charactercard.png")), r => r is { Label: "Character card", Value: "Test Character" });
        Assert.Contains(AiRows.Build(await Read("comfyui.png")), r => r.Label == "Workflow" && r.Value.EndsWith("1 nodes", StringComparison.Ordinal));
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
