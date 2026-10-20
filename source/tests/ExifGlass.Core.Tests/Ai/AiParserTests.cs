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
using ExifGlass.Core.Ai.Parsers;
using Xunit;

namespace ExifGlass.Core.Tests.Ai;

/// <summary>
/// Parser cases the fixtures don't cover: graph shapes, version strings, collisions, malformed data.
/// </summary>
public class AiParserTests
{
    #region Tests

    [Fact]
    public void ComfyKeepsSamplersApartAndTracesConditioningThroughControlNetAndRegions()
    {
        const string prompt = """
            {
              "1": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": "sdxl.safetensors"}},
              "2": {"class_type": "CLIPTextEncodeSDXL", "inputs": {"text_g": "a castle", "text_l": "detailed", "clip": ["1", 1]}},
              "3": {"class_type": "CLIPTextEncode", "inputs": {"text": "blurry", "clip": ["1", 1]}},
              "4": {"class_type": "CLIPTextEncode", "inputs": {"text": "a dragon", "clip": ["1", 1]}},
              "5": {"class_type": "ConditioningSetArea", "_meta": {"title": "Left half"}, "inputs": {"conditioning": ["4", 0], "x": 0}},
              "6": {"class_type": "ConditioningCombine", "inputs": {"conditioning_1": ["2", 0], "conditioning_2": ["5", 0]}},
              "7": {"class_type": "ControlNetApplyAdvanced", "inputs": {"positive": ["6", 0], "negative": ["3", 0], "control_net": ["8", 0]}},
              "8": {"class_type": "ControlNetLoader", "inputs": {"control_net_name": "canny.safetensors"}},
              "9": {"class_type": "EmptyLatentImage", "inputs": {"width": 1024, "height": 768}},
              "10": {"class_type": "KSampler", "_meta": {"title": "Base"}, "inputs": {"seed": 7, "steps": 30, "cfg": 6.5, "sampler_name": "dpmpp_2m", "scheduler": "karras", "denoise": 1,
                     "model": ["1", 0], "positive": ["7", 0], "negative": ["7", 1], "latent_image": ["9", 0]}},
              "11": {"class_type": "LatentUpscale", "inputs": {"samples": ["10", 0]}},
              "12": {"class_type": "KSamplerAdvanced", "_meta": {"title": "Hires"}, "inputs": {"noise_seed": 8, "steps": 15, "cfg": 5, "sampler_name": "euler", "scheduler": "normal",
                     "start_at_step": 10, "model": ["1", 0], "positive": ["2", 0], "negative": ["3", 0], "latent_image": ["11", 0]}},
              "13": {"class_type": "UpscaleModelLoader", "inputs": {"model_name": "4x.pth"}},
              "14": {"class_type": "StringConcatenate", "inputs": {"string_a": "x", "string_b": "y", "delimiter": "-"}},
              "15": {"class_type": "CLIPTextEncode", "inputs": {"text": ["14", 0]}},
              "16": {"class_type": "SaveImage", "inputs": {"images": ["11", 0]}}
            }
            """;

        var s = Single(("PNG:Main:Prompt", prompt));

        Assert.Equal("ComfyUI", s.Generator);
        Assert.Equal(["a castle", "detailed", "a dragon", "blurry"], s.Prompts.Select(p => p.Text));
        Assert.Equal(["G", "L", "Left half", ""], s.Prompts.Select(p => p.Label));
        Assert.Equal(AiPromptRole.Negative, s.Prompts[3].Role);
        Assert.Equal("7", s.Parameter("seed")!.Value);
        Assert.Equal("1024x768", s.Parameter("size")!.Value);
        Assert.Equal("sdxl.safetensors", s.Parameter("model")!.Value);

        var hires = Assert.Single(s.Passes);
        Assert.Equal("Hires", hires.Title);
        Assert.Contains(hires.Parameters, p => p.Id == "seed" && p.Value == "8");
        Assert.Contains(hires.Parameters, p => p.Label == "Start at step" && p.Value == "10");
        Assert.Equal(["a castle", "detailed", "blurry"], hires.Prompts.Select(p => p.Text));

        Assert.Contains(s.Models, m => m is { Kind: AiModelKind.ControlNet, Name: "canny.safetensors" });
        Assert.Contains(s.Models, m => m is { Kind: AiModelKind.Upscaler, Name: "4x.pth" });
        Assert.Equal(16, s.Workflows.Single().NodeCount);
    }


    [Fact]
    public void ComfyReadsPromptsWithoutASamplerAndToleratesNonFiniteNumbers()
    {
        var s = Single(("PNG:Main:Prompt", """{"1": {"class_type": "CLIPTextEncode", "inputs": {"text": "only text", "strength": NaN, "x": -Infinity}}, "2": {"class_type": "Other", "inputs": {"v": Infinity}}}"""));

        Assert.Equal("only text", Assert.Single(s.Prompts).Text);
        Assert.Empty(s.Passes);
    }


    [Fact]
    public void ComfyWarnsAboutAWorkflowItCantRead()
    {
        var info = Parse(("PNG:Main:Workflow", "{\"nodes\": [ broken"), ("PNG:Main:Prompt", "{\"1\": {\"class_type\": \"KSampler\", \"inputs\": {\"seed\": 1, \"positive\": [\"9\", 0]}}}"));

        // the broken workflow isn't a workflow; the prompt still parses and the workflow text stays Unknown AI
        Assert.Equal("ComfyUI", info!.Generator);
        Assert.Contains(info.Sources, s => s.Source == AiSourceKind.UnknownAi);
    }


    [Fact]
    public void ComfyReadsTheExifPrefixesOfAnimatedWebp()
    {
        var s = Single(("EXIF:IFD0:Main:Make", "workflow:{\"nodes\": [{\"id\": 1, \"type\": \"KSampler\"}, {\"id\": 2, \"type\": \"CheckpointLoaderSimple\"}], \"links\": [], \"version\": 0.4}"),
            ("EXIF:IFD0:Main:Model", "prompt:{\"1\": {\"class_type\": \"CLIPTextEncode\", \"inputs\": {\"text\": \"hi\"}}}"));

        Assert.Equal(["EXIF:IFD0:Model", "EXIF:IFD0:Make"], s.SourceKeys);
        Assert.Equal("ComfyUI workflow 0.4", s.Workflows[0].FormatLabel);
        Assert.Equal((2, 1, 1), (s.Workflows[0].NodeCount, s.Workflows[0].SamplerCount, s.Workflows[0].ModelCount));
    }


    [Fact]
    public void EasyDiffusionKeepsItsPlainTextPromptFromComfy()
    {
        var s = Single(("PNG:Main:Prompt", "a horse"), ("PNG:Main:Use_stable_diffusion_model", "sd-v1-5"), ("PNG:Main:Width", "512"), ("PNG:Main:Height", "768"),
            ("PNG:Main:Use_lora_model", "detail"), ("PNG:Main:Lora_alpha", "0.5"));

        Assert.Equal("Easy Diffusion", s.Generator);
        Assert.Equal("a horse", s.Prompts.Single().Text);
        Assert.Equal("512x768", s.Parameter("size")!.Value);
        Assert.Contains(s.Models, m => m is { Kind: AiModelKind.Lora, Name: "detail", Weight: "0.5" });
    }


    [Theory]
    [InlineData("v1.10.1", "AUTOMATIC1111", "1.10.1")]
    [InlineData("v1.10.1-96-gabcdef0", "AUTOMATIC1111", "1.10.1")]
    [InlineData("1.10.1", "AUTOMATIC1111", "1.10.1")]
    [InlineData("f2.0.1v1.10.1-previous-669-gdfdcbab6", "Forge", "2.0.1")]
    [InlineData("1.0.0v2-v1.10.1RC", "reForge", "1.0.0")]
    [InlineData("classic", "Forge Classic", null)]
    [InlineData("neo-2.30.1", "Forge Neo", "2.30.1")]
    [InlineData("Fooocus v2.5.5", "Fooocus", "2.5.5")]
    [InlineData("something", "A1111 format", "something")]
    public void NamesTheA1111FamilyFromItsVersion(string version, string generator, string? number)
    {
        var info = A1111Infotext.Parse($"p\nSteps: 1, Sampler: x, CFG scale: 2, Version: {version}")!;

        Assert.Equal((generator, number), A1111Parser.DetectGenerator(info));
    }


    [Fact]
    public void ReadsTheSidecarWhenTheImageHasNoInfotext()
    {
        using var tags = AiTagSet.Empty;
        var info = AiEngine.Parse(tags, "a cat\nSteps: 20, Sampler: Euler, CFG scale: 7, Seed: 3", TestContext.Current.CancellationToken)!;

        var s = Assert.Single(info.Sources);
        Assert.Equal("3", s.Parameter("seed")!.Value);
        Assert.Contains(s.Warnings, w => w.Contains(".txt", StringComparison.Ordinal));
    }


    [Fact]
    public void ReadsASidecarFileNextToTheImageOnlyWhenItExists()
    {
        var dir = Directory.CreateTempSubdirectory("exifglass-sidecar-").FullName;
        try
        {
            var image = Path.Combine(dir, "a.png");
            Assert.Null(AiEngine.ReadSidecar(image));

            File.WriteAllText(Path.Combine(dir, "a.txt"), "x\nSteps: 1, Sampler: y, CFG scale: 2");
            Assert.StartsWith("x\n", AiEngine.ReadSidecar(image));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }


    [Fact]
    public void NovelAiNeedsItsSoftwareTag()
    {
        Assert.Null(Parse(("PNG:Main:Comment", "{\"prompt\": \"x\", \"uc\": \"y\", \"steps\": 28}")));

        var s = Single(("PNG:Main:Software", "NovelAI"), ("PNG:Main:Description", "1girl"), ("PNG:Main:Source", "Stable Diffusion XL"));
        Assert.Equal("1girl", s.Prompts.Single().Text);
        Assert.Equal("Stable Diffusion XL", s.Models.Single().Name);
        Assert.Null(s.Models.Single().Hash);
    }


    [Fact]
    public void MidjourneyReadsFlagsJobIdAndTheNoParameter()
    {
        var s = Single(("PNG:Main:Description", "a fox --niji 6 --ar 2:3 --no text, watermark --tile Job ID: 0123abcd-4567-89ef"));

        Assert.Equal("Niji 6", s.GeneratorVersion);
        Assert.Equal("a fox", s.Prompts[0].Text);
        Assert.Equal("text, watermark", s.Prompts.Single(p => p.Role == AiPromptRole.Negative).Text);
        Assert.Equal("on", s.Parameters.Single(p => p.Label == "Tile").Value);
        Assert.Equal("0123abcd-4567-89ef", s.Parameters.Single(p => p.Label == "Job ID").Value);
        Assert.Null(Parse(("PNG:Main:Description", "slides about prompt engineering")));
    }


    [Theory]
    [InlineData("1", "AI-generated", AiKind.Generated)]
    [InlineData("2", "Possibly AI-generated", AiKind.UnknownAi)]
    [InlineData("3", "Suspected AI-generated", AiKind.UnknownAi)]
    [InlineData("9", "Unknown label 9", AiKind.UnknownAi)]
    public void ReadsTheAigcLabel(string label, string meaning, AiKind kind)
    {
        var s = Single(("QuickTime:Keys:Main:AIGC", $"{{\"Label\": \"{label}\", \"ProduceID\": \"doubao-1\"}}"));

        Assert.Equal((meaning, kind, "Doubao"), (s.Aigc!.Meaning, s.Kind, s.Generator));
    }


    [Fact]
    public void ReadsIptcCreditsAndAiSystemsWithoutASourceType()
    {
        Assert.Equal(AiKind.EditedWithAi, Single(("XMP:XMP-photoshop:Main:Credit", "Edited with Google AI")).Kind);
        Assert.Equal(AiKind.Generated, Single(("XMP:XMP-photoshop:Main:Credit", "Made by Google AI")).Kind);
        Assert.Null(Parse(("XMP:XMP-photoshop:Main:Credit", "Reuters")));

        var s = Single(("XMP:XMP-iptcExt:Main:AISystemUsed", "Some Model"));
        Assert.Equal((AiKind.UnknownAi, "Some Model"), (s.Kind, s.Generator));
    }


    [Fact]
    public void NamesAiToolsOnlyWhenNoParserFoundData()
    {
        var s = Single(("EXIF:IFD0:Main:Software", "Image Playground 1.0"));
        Assert.Equal(("Image Playground", AiKind.UnknownAi), (s.Generator, s.Kind));

        Assert.Null(Parse(("EXIF:IFD0:Main:Software", "Adobe Lightroom Classic 14.0")));

        var info = Parse(("EXIF:IFD0:Main:Software", "NovelAI"), ("PNG:Main:Parameters", "a\nSteps: 1, Sampler: x, CFG scale: 2"))!;
        Assert.DoesNotContain(info.Sources, x => x.Source == AiSourceKind.ToolName);
    }


    [Fact]
    public void ShowsUnparsedAiChunksAsUnknownAi()
    {
        var s = Single(("PNG:Main:Parameters", "something no parser knows"), ("PNG:Main:Dream", new string('z', 300)));

        Assert.Equal(AiKind.UnknownAi, s.Kind);
        Assert.Contains("something no parser knows", s.RawText);
        Assert.EndsWith("…", s.Parameters.Single(p => p.RawKey == "Dream").Value);
    }


    [Fact]
    public void ReadsCharacterCardsEvenWhenTheyDontDecode()
    {
        var s = Single(("PNG:Main:Chara", "not base64!"));

        Assert.Equal(AiKind.CharacterCard, s.Kind);
        Assert.Null(s.CharacterName);
    }


    [Fact]
    public void InvokeGraphOnlyImagesGetPromptRolesFromEdges()
    {
        var s = Single(("PNG:Main:Invokeai_graph", """
            {"id": "g", "nodes": {"pos": {"type": "compel", "prompt": "a lake"}, "neg": {"type": "compel", "prompt": "fog"}, "d": {"type": "denoise_latents"}},
             "edges": [{"source": {"node_id": "pos"}, "destination": {"node_id": "d", "field": "positive_conditioning"}},
                       {"source": {"node_id": "neg"}, "destination": {"node_id": "d", "field": "negative_conditioning"}}]}
            """));

        Assert.Equal([("a lake", AiPromptRole.Positive), ("fog", AiPromptRole.Negative)], s.Prompts.Select(p => (p.Text, p.Role)));
        Assert.Equal((3, 2), (s.Workflows[0].NodeCount, s.Workflows[0].LinkCount));
    }


    [Fact]
    public void InvokeLegacyReadsNegativesInBrackets()
    {
        var s = Single(("PNG:Main:Dream", "\"a castle [fog, rain]\" -s 50 -S 42 -W 640 -H 480 -C 7.5 -A k_lms -f 0.6"));

        Assert.Equal("a castle", s.Prompts[0].Text);
        Assert.Equal("fog, rain", s.Prompts[1].Text);
        Assert.Equal(("640x480", "0.6"), (s.Parameter("size")!.Value, s.Parameter("denoise")!.Value));
    }


    [Fact]
    public void FooocusJsonIsReadWithoutTheSchemeWhenTheVersionSaysSo()
    {
        var s = Single(("PNG:Main:Parameters", "{\"prompt\": \"a cat\", \"version\": \"Fooocus v2.5.0\", \"resolution\": \"(896, 1152)\", \"vae\": \"sdxl_vae.safetensors\"}"));

        Assert.Equal(("Fooocus", "2.5.0", "896x1152"), (s.Generator, s.GeneratorVersion, s.Parameter("size")!.Value));
        Assert.Contains(s.Models, m => m is { Kind: AiModelKind.Vae, Name: "sdxl_vae.safetensors" });

        // JSON without a Fooocus marker isn't Fooocus's
        Assert.NotEqual(AiSourceKind.Fooocus, Single(("PNG:Main:Parameters", "{\"prompt\": \"a cat\"}")).Source);
    }


    [Fact]
    public void DrawThingsIsFoundByItsJsonEvenWithoutCreatorTool()
    {
        var s = Single(("XMP:XMP-exif:Main:UserComment", "{\"c\": \"a cabin\", \"uc\": \"\", \"steps\": 8}"));

        Assert.Equal("Draw Things", s.Generator);
        Assert.Equal("8", s.Parameter("steps")!.Value);
    }


    [Fact]
    public void C2paReadsManifestsActionsIngredientsAndRemoteUrls()
    {
        // -struct values are JSON objects and arrays, as the real AI read gives them
        using var tags = AiTagSet.Parse("""
            [{
              "JUMBF:Doc1:JUMDLabel": "c2pa",
              "JUMBF:Doc1-1:JUMDLabel": "urn:c2pa:aaa",
              "JUMBF:Doc1-1-1-1:JUMDLabel": "c2pa.actions.v2",
              "JUMBF:CBOR:Doc1-1-1-1:Actions": {"action": "c2pa.created", "digitalSourceType": "http://cv.iptc.org/newscodes/digitalsourcetype/digitalCapture"},
              "JUMBF:Doc1-1-1-2:JUMDLabel": "stds.exif",
              "JUMBF:Doc1-1-2:JUMDLabel": "c2pa.claim.v2",
              "JUMBF:CBOR:Doc1-1-2:Claim_generator_info": [{"name": "Camera"}],
              "JUMBF:Doc1-2:JUMDLabel": "urn:c2pa:bbb",
              "JUMBF:Doc1-2-1-1:JUMDLabel": "c2pa.actions.v2",
              "JUMBF:CBOR:Doc1-2-1-1:Actions": [{"action": "c2pa.edited", "softwareAgent": {"name": "Retoucher", "version": "2"},
                "digitalSourceType": "http://cv.iptc.org/newscodes/digitalsourcetype/algorithmicallyEnhanced"}],
              "JUMBF:Doc1-2-1-2:JUMDLabel": "c2pa.ingredient.v3",
              "JUMBF:CBOR:Doc1-2-1-2:Relationship": "parentOf",
              "JUMBF:CBOR:Doc1-2-1-2:ActiveManifest": {"url": "self#jumbf=/c2pa/urn:c2pa:aaa"},
              "JUMBF:Doc1-2-1-3:JUMDLabel": "stds.schema-org.CreativeWork",
              "JUMBF:JSON:Doc1-2-1-3:Author": [{"name": "Jane"}, "Joe"],
              "JUMBF:Doc1-2-2:JUMDLabel": "c2pa.claim",
              "JUMBF:CBOR:Doc1-2-2:Claim_generator": "Photo_Editor/1.0 c2pa-rs/0.1",
              "XMP:XMP-dcterms:Main:Provenance": "https://example.com/manifest"
            }]
            """);
        var s = Assert.Single(AiEngine.Parse(tags, null, TestContext.Current.CancellationToken)!.Sources);

        Assert.Equal((AiKind.EditedWithAi, "Photo Editor/1.0"), (s.Kind, s.Generator));
        Assert.Equal(["urn:c2pa:bbb", "urn:c2pa:aaa"], s.C2pa!.Manifests.Select(m => m.Label));
        Assert.True(s.C2pa.Active!.IsActive);
        Assert.Equal(1, s.C2pa.Active.ClaimVersion);
        Assert.Equal(["Jane", "Joe"], s.C2pa.Active.Authors);
        Assert.Equal("urn:c2pa:aaa", s.C2pa.Active.Ingredients.Single().ManifestLabel);
        Assert.Equal(("Retoucher", "2"), (s.C2pa.Active.Actions[0].SoftwareAgent, s.C2pa.Active.Actions[0].SoftwareAgentVersion));
        Assert.Equal("Algorithmically enhanced", s.C2pa.Active.Actions[0].DigitalSourceLabel);
        Assert.Equal("https://example.com/manifest", s.C2pa.RemoteManifestUrl);
        Assert.True(s.C2pa.HasExifAssertion);
        Assert.False(s.C2pa.IsVerified);
        Assert.Contains(s.Warnings, w => w.Contains("GPS", StringComparison.Ordinal));
        Assert.Equal("Retoucher 2", s.Parameters.Single(p => p.Label == "AI system").Value);
    }


    [Fact]
    public void C2paNamesAiByAgentWhenThereIsNoSourceType()
    {
        Assert.Equal(AiKind.Generated, C2paParser_Kind(("c2pa.created", "Azure OpenAI DALL-E", null, null)));
        Assert.Equal(AiKind.EditedWithAi, C2paParser_Kind(("c2pa.edited", null, null, "Generative Fill")));
        Assert.Equal(AiKind.None, C2paParser_Kind(("c2pa.edited", "Lightroom", null, null)));
        Assert.Equal(AiKind.Captured, C2paParser_Kind(("c2pa.created", null, "http://cv.iptc.org/newscodes/digitalsourcetype/digitalCapture", null)));
    }


    [Fact]
    public void TheEngineTurnsParserFailuresIntoWarnings()
    {
        // 300 levels of nesting: over LenientJson's depth cap, so the parser reports instead of throwing
        var deep = new string('[', 300) + new string(']', 300);
        var info = Parse(("PNG:Main:Prompt", "{\"a\": " + deep + "}"), ("PNG:Main:Parameters", "a\nSteps: 1, Sampler: x, CFG scale: 2"));

        Assert.NotNull(info);
        Assert.Equal("A1111 format", info.Generator);
    }

    #endregion // Tests


    #region Private methods

    private static AiGenerationInfo? Parse(params (string Key, string Value)[] pairs)
    {
        using var tags = AiTagSet.FromPairs(pairs);
        return AiEngine.Parse(tags, null, TestContext.Current.CancellationToken);
    }


    private static AiSourceInfo Single(params (string Key, string Value)[] pairs)
    {
        var info = Parse(pairs);
        Assert.NotNull(info);
        return Assert.Single(info.Sources);
    }


    private static AiKind C2paParser_Kind((string Action, string? Agent, string? SourceType, string? Description) a) =>
        C2paParser.Kind([new C2paManifest("m", 2, null, null, null, null, [new C2paAction(a.Action, a.Agent, null, a.SourceType, a.Description)], [], [], true, false)]);

    #endregion // Private methods
}
