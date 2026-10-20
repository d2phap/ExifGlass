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
using System.Text.Json;
using Xunit;

namespace ExifGlass.Core.Tests.Ai;

/// <summary>
/// The helpers under the parsers: lenient JSON, text repair, source types, parameter names, the merged view.
/// </summary>
public class AiSupportTests
{
    #region Tests

    [Theory]
    [InlineData("{\"a\": NaN, \"b\": Infinity, \"c\": -Infinity}", "{\"a\": null, \"b\": null, \"c\": null}")]
    [InlineData("{\"s\": \"NaN and Infinity stay in strings\", \"e\": \"\\\"NaN\"}", "{\"s\": \"NaN and Infinity stay in strings\", \"e\": \"\\\"NaN\"}")]
    [InlineData("{\"x\": 1}\0\0", "{\"x\": 1}")]
    [InlineData("  [1, 2]  ", "[1, 2]")]
    public void SanitizesPythonJson(string input, string expected) => Assert.Equal(expected, LenientJson.Sanitize(input));


    [Fact]
    public void ParsesLenientlyAndReportsWhatItCant()
    {
        using var ok = LenientJson.TryParse("{\"a\": NaN, // comment\n \"b\": [1, 2,],}", out var none);
        Assert.NotNull(ok);
        Assert.Null(none);
        Assert.Equal(JsonValueKind.Null, ok.RootElement.GetProperty("a").ValueKind);

        Assert.Null(LenientJson.TryParse("{broken", out var warning));
        Assert.StartsWith("Embedded JSON couldn't be read", warning);
        Assert.Null(LenientJson.TryParse("   ", out _));
        Assert.Null(LenientJson.TryParse(new string('x', LenientJson.MAX_LENGTH + 1), out var big));
        Assert.Contains("isn't parsed", big);

        Assert.True(LenientJson.LooksLikeObject("  {\"a\": 1}"));
        Assert.False(LenientJson.LooksLikeObject("[1]"));
        Assert.False(LenientJson.LooksLikeObject(null));
    }


    [Fact]
    public void ReadsPropertiesAsText()
    {
        using var doc = JsonDocument.Parse("{\"s\": \"x\", \"n\": 1.50, \"t\": true, \"f\": false, \"z\": null, \"a\": [1, \"b\"], \"o\": {\"k\": 1}, \"e\": \"\"}");
        var root = doc.RootElement;

        Assert.Equal("x", LenientJson.Str(root, "s"));
        Assert.Equal("1.50", LenientJson.Str(root, "n"));
        Assert.Equal("True", LenientJson.Str(root, "t"));
        Assert.Equal("False", LenientJson.Str(root, "f"));
        Assert.Null(LenientJson.Str(root, "z"));
        Assert.Equal("1, b", LenientJson.Str(root, "a"));
        Assert.Equal("{\"k\": 1}", LenientJson.Str(root, "o"));
        Assert.Null(LenientJson.Str(root, "e"));
        Assert.Null(LenientJson.Str(root, "missing"));
        Assert.Null(LenientJson.Prop(root, "z"));
        Assert.Null(LenientJson.Str(root.GetProperty("a"), "x"));

        Assert.Equal(1.5, AiTagSet.ToNumber(root.GetProperty("n")));
        Assert.Null(AiTagSet.ToNumber(root.GetProperty("t")));
        Assert.Equal(2.0, AiTagSet.ToNumber(JsonDocument.Parse("\"2\"").RootElement));
    }


    [Theory]
    [InlineData("çŒ«ã\u0081®å°‘å¥³ cafÃ©", "猫の少女 café")]
    [InlineData("â˜€ sun", "☀ sun")]
    public void RepairsUtf8ReadAsCp1252(string mojibake, string expected) => Assert.Equal(expected, TextRepair.TryRepairMojibake(mojibake));


    [Theory]
    [InlineData("plain ascii")]
    [InlineData("café")]
    [InlineData("naïve façade")]
    [InlineData("日本語")]
    [InlineData("€ alone")]
    public void LeavesRealTextAlone(string text) => Assert.Null(TextRepair.TryRepairMojibake(text));


    [Theory]
    [InlineData("http://cv.iptc.org/newscodes/digitalsourcetype/trainedAlgorithmicMedia", "Trained algorithmic media", AiKind.Generated)]
    [InlineData("https://cv.iptc.org/newscodes/digitalsourcetype/trainedAlgorithmicMedia/", "Trained algorithmic media", AiKind.Generated)]
    [InlineData("http://c2pa.org/digitalsourcetype/trainedAlgorithmicData", "Trained algorithmic data", AiKind.Generated)]
    [InlineData("http://cv.iptc.org/newscodes/digitalsourcetype/compositeSynthetic", "Composite of synthetic elements", AiKind.Composite)]
    [InlineData("http://cv.iptc.org/newscodes/digitalsourcetype/minorHumanEdits", "Minor human edits", AiKind.Captured)]
    [InlineData("http://c2pa.org/digitalsourcetype/empty", "Empty", AiKind.None)]
    [InlineData("http://example.com/somethingNew", "somethingNew", AiKind.None)]
    [InlineData("digitalCapture", "Digital capture", AiKind.Captured)]
    public void LabelsDigitalSourceTypes(string uri, string label, AiKind kind)
    {
        Assert.Equal(label, DigitalSourceTypes.Label(uri));
        Assert.Equal(kind, DigitalSourceTypes.Kind(uri));
    }


    [Theory]
    [InlineData("CFG scale", "cfg", "CFG")]
    [InlineData("cfgscale", "cfg", "CFG")]
    [InlineData("Schedule type", "scheduler", "Scheduler")]
    [InlineData("noise_seed", "seed", "Seed")]
    [InlineData("Hires upscaler", "hiresUpscaler", "Hires upscaler")]
    [InlineData("CFG rescale", "cfgRescale", "CFG rescale")]
    [InlineData("cfg_rescale", "cfgRescale", "Cfg rescale")]
    [InlineData("noiseSchedule2", "noiseSchedule2", "Noise schedule2")]
    [InlineData("aspectratio", "aspectratio", "Aspect ratio")]
    [InlineData("guidance", "guidance", "Guidance")]
    [InlineData("_", "_", " ")]
    public void NormalizesParameterNames(string raw, string id, string label) => Assert.Equal((id, label), AiParameterNames.Normalize(raw));


    [Fact]
    public void OrdersParametersAndDropsEmptyAndRepeatedOnes()
    {
        var fields = AiParameterNames.Build([("Extra", "1", null), ("Seed", "5", "k"), ("steps", "20", null), ("cfg_scale", "7", null), ("CFG scale", "8", null), ("Model", "m", null), ("Empty", " ", null)]);

        Assert.Equal(["model", "steps", "cfg", "seed", "extra"], fields.Select(f => f.Id));
        Assert.Equal("7", fields[2].Value);
        Assert.Equal("k", fields[3].SourceKey);
        Assert.Equal("Steps", AiParameterNames.LabelOf("steps"));
        Assert.Equal("Something new", AiParameterNames.LabelOf("somethingNew"));
    }


    [Fact]
    public void PicksThePrimarySourceAndTheStrongestKind()
    {
        var c2pa = new AiSourceInfo { Source = AiSourceKind.C2pa, Generator = "ChatGPT", Kind = AiKind.Generated, Parameters = [new AiField("x", "X", "1", "x")] };
        var comfy = new AiSourceInfo
        {
            Source = AiSourceKind.ComfyUi, Generator = "ComfyUI", GeneratorVersion = "0.3", Kind = AiKind.Generated,
            Prompts = [new AiPrompt("p", AiPromptRole.Positive), new AiPrompt("n", AiPromptRole.Negative)],
            Parameters = [new AiField("seed", "Seed", "9", "seed"), new AiField("size", "Size", "64x64", "size")],
            Models = [new AiModelRef(AiModelKind.Checkpoint, "m"), new AiModelRef(AiModelKind.Checkpoint, "m")],
            Workflows = [new AiWorkflowSummary(AiWorkflowFormat.ComfyUiWorkflow, "w", 12, 0, 1, 1, 0, 10, "{}", null)],
            SourceKeys = ["PNG:Prompt"],
        };
        var iptc = new AiSourceInfo { Source = AiSourceKind.Iptc, Generator = "IPTC", Kind = AiKind.Composite, SourceKeys = ["XMP:XMP-iptcExt:DigitalSourceType"] };

        var info = new AiGenerationInfo([c2pa, comfy, iptc]);

        Assert.Same(comfy, info.Primary);
        Assert.Equal(AiKind.Generated, info.Kind);
        Assert.True(info.IsAi);
        Assert.Equal(("p", "n", "9", "64x64"), (info.Prompt!.Text, info.NegativePrompt!.Text, info.Seed, info.Size));
        Assert.Single(info.Models);
        Assert.Single(info.Workflows);
        Assert.Equal(["PNG:Prompt", "XMP:XMP-iptcExt:DigitalSourceType"], info.SourceKeys.Order(StringComparer.Ordinal));
        Assert.Equal("ComfyUI 0.3 · Generated · 2 prompts · seed 9 · 1 model · workflow 12 nodes · + ChatGPT, IPTC", info.Summary());
    }


    [Fact]
    public void SummarizesProvenanceOnlyAndEmptyInfo()
    {
        Assert.Equal("no AI data", new AiGenerationInfo([]).Summary());
        Assert.Equal(AiKind.None, new AiGenerationInfo([]).Kind);

        var captured = new AiGenerationInfo([new AiSourceInfo { Source = AiSourceKind.C2pa, Generator = "Camera", Kind = AiKind.Captured }]);
        Assert.False(captured.IsAi);
        Assert.Equal("Camera · Not AI", captured.Summary());
        Assert.Null(captured.Prompt);
        Assert.Empty(captured.Parameters);
    }


    [Theory]
    [InlineData(AiKind.Generated, "Generated")]
    [InlineData(AiKind.Composite, "Composite")]
    [InlineData(AiKind.EditedWithAi, "Edited with AI")]
    [InlineData(AiKind.UnknownAi, "Unknown AI")]
    [InlineData(AiKind.CharacterCard, "Character card")]
    [InlineData(AiKind.Captured, "Not AI")]
    [InlineData(AiKind.None, "None")]
    public void LabelsKinds(AiKind kind, string label) => Assert.Equal(label, AiGenerationInfo.KindLabel(kind));


    [Fact]
    public void MatchesToolAndAgentNames()
    {
        Assert.Equal("Draw Things", AiNames.ToolName("Draw Things 1.2"));
        Assert.Null(AiNames.ToolName("GIMP 2.10"));
        Assert.Null(AiNames.ToolName(null));
        Assert.True(AiNames.IsAiAgent("GPT-4o"));
        Assert.False(AiNames.IsAiAgent("Lightroom"));
        Assert.True(AiNames.IsAiDescription("Created by Google Generative AI."));
        Assert.True(AiNames.IsAiPngKeyword("invokeai_metadata"));
    }


    [Fact]
    public void BuildsTheSetFromLoosePairsAndLooksTagsUp()
    {
        using var tags = AiTagSet.FromPairs([("PNG:Main:Parameters", "a"), ("EXIF:IFD0:Main:Make", "Canon"), ("XMP:XMP-dc:Main:Title", "t")]);

        Assert.Equal(3, tags.Tags.Count);
        Assert.Equal("Canon", tags.GetText("EXIF", "make"));
        Assert.Equal("Canon", tags.GetText("IFD0", "Make"));
        Assert.Equal("t", tags.GetFirst("Title", "PNG", "XMP-dc")!.Text);
        Assert.Null(tags.GetFirst("Title", "PNG"));
        Assert.Single(tags.InGroup("xmp"));
        Assert.Single(tags.WithName("parameters"));
        Assert.Empty(AiTagSet.Empty.Tags);
        Assert.Single(AiTagSet.Parse("{\"Group:Name\": \"an object root reads as the file\"}").Tags);
    }

    #endregion // Tests
}
