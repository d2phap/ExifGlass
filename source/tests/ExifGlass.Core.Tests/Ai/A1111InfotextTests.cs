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
using Xunit;

namespace ExifGlass.Core.Tests.Ai;

public class A1111InfotextTests
{
    #region Fields and constants

    private const string A1111 = """
        masterpiece, 1girl, (detailed eyes:1.2), <lora:add_detail:0.8>, embedding:easynegative
        second prompt line, BREAK sunset
        Negative prompt: lowres, bad anatomy, easynegative
        Steps: 28, Sampler: DPM++ 2M, Schedule type: Karras, CFG scale: 7, Seed: 1234567890, Size: 832x1216, Model hash: 6ce0161689, Model: realisticVision_v51, ADetailer prompt: "smiling, blue eyes", ControlNet 0: "Module: canny, Model: control_v11p_sd15_canny [d14c016b], Weight: 1.0", Hires prompt: "a \"quoted\" hires prompt,\nwith newline", Lora hashes: "add_detail: 7c6bad76eb54", Version: v1.10.1
        """;

    private const string CIVITAI = "a knight\nNegative prompt: bad hands\nSteps: 25, Sampler: DPM++ 2M Karras, Civitai resources: [{\"type\":\"lora\",\"weight\":0.8,\"modelName\":\"Detail \\u0027XL\\u0027\"}], Civitai metadata: {\"workflow\":\"txt2img\"}";

    #endregion // Fields and constants


    #region Properties

    // every infotext the fixtures carry, plus awkward shapes
    public static TheoryData<string> RoundTrips => new()
    {
        A1111.ReplaceLineEndings("\n"),
        A1111.ReplaceLineEndings("\r\n"),
        "猫の少女, 🌸 cherry blossom\nNegative prompt: worst quality\nSteps: 20, Sampler: Euler a, CFG scale: 7, Seed: 1, Size: 512x512",
        "a cat wearing a wizard hat\nNegative prompt: lowres, blurry\nSteps: 20, Seed: 1620085323, Sampler: DPM++ 2M, CFG scale: 6, App: SD.Next, Version: 0a0cbd5e\nTemplate: masterpiece, {prompt}",
        CIVITAI,
        "a knight, <lora:add_detail:0.6>\nSteps: 30, Sampler: Euler a, CFG scale: 7, Hashes: {\"vae\": \"735e4c3a44\", \"lora:add_detail\": \"7c6bad76eb\"}, Version: v1.10.1",
        "Steps: 20, Sampler: Euler, CFG scale: 7",
        "line one\nline two\nNegative prompt: neg one\nneg two\nSteps: 1, Sampler: x, CFG scale: 2",
        "Negative prompt:no space\nSteps: 1, Sampler: x, CFG scale: 2",
        "prompt\nSteps: 1, Sampler: x, CFG scale: 2\n",
    };

    #endregion // Properties


    #region Tests

    [Theory]
    [MemberData(nameof(RoundTrips))]
    public void FormatGivesBackTheSameText(string text)
    {
        var info = A1111Infotext.Parse(text);

        Assert.NotNull(info);
        Assert.Equal(text, info.Format());
    }


    [Fact]
    public void SplitsPromptNegativeAndQuotedParameters()
    {
        var info = A1111Infotext.Parse(A1111.ReplaceLineEndings("\n"))!;

        Assert.Equal("masterpiece, 1girl, (detailed eyes:1.2), <lora:add_detail:0.8>, embedding:easynegative\nsecond prompt line, BREAK sunset", info.Positive);
        Assert.Equal("lowres, bad anatomy, easynegative", info.Negative);
        Assert.Equal("smiling, blue eyes", info.Get("ADetailer prompt")!.Value);
        Assert.Equal("a \"quoted\" hires prompt,\nwith newline", info.Get("hires prompt")!.Value);
        Assert.Equal("Module: canny, Model: control_v11p_sd15_canny [d14c016b], Weight: 1.0", info.Get("ControlNet 0")!.Value);
        Assert.Equal(13, info.Params.Count);
        Assert.Null(info.Get("Nope"));
    }


    [Fact]
    public void CutsUnquotedJsonByBrackets()
    {
        var info = A1111Infotext.Parse(CIVITAI)!;

        Assert.StartsWith("[{\"type\":\"lora\"", info.Get("Civitai resources")!.RawValue);
        Assert.EndsWith("}]", info.Get("Civitai resources")!.RawValue);
        Assert.Equal("{\"workflow\":\"txt2img\"}", info.Get("Civitai metadata")!.RawValue);
    }


    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("just a prompt, no parameters")]
    [InlineData("Holiday in Hoi An")]
    [InlineData("a: b, c: d")]
    [InlineData("masterpiece, (eyes:1.2), <lora:x:0.5>\nmore text here")]
    public void ReadsNothingWithoutAParametersLine(string? text) => Assert.Null(A1111Infotext.Parse(text));


    [Fact]
    public void CountsPairsLikeA1111()
    {
        Assert.Equal(3, A1111Infotext.CountPairs("Steps: 1, Sampler: x, CFG scale: 2"));
        Assert.Equal(1, A1111Infotext.CountPairs("Template: masterpiece, {prompt}"));
    }


    [Fact]
    public void CreatesQuotedInfotextFromParts()
    {
        var info = A1111Infotext.Create("a cat", "dog", [("Steps", "20"), ("Hires prompt", "a, b"), ("Note", "x: y"), ("Quote", "\"q\"")]);

        Assert.Equal("a cat\nNegative prompt: dog\nSteps: 20, Hires prompt: \"a, b\", Note: \"x: y\", Quote: \"\\u0022q\\u0022\"", info.Format());
        Assert.Equal("a, b", A1111Infotext.Parse(info.Format())!.Get("Hires prompt")!.Value);
        Assert.Equal("Steps: 1", A1111Infotext.Create(string.Empty, null, [("Steps", "1")]).Format());
    }


    [Theory]
    [InlineData("\"abc\"", "abc")]
    [InlineData("\"a\\nb\"", "a\nb")]
    [InlineData("plain", "plain")]
    [InlineData("\"broken \\", "\"broken \\")]
    [InlineData("\"bad \\x\"", "bad \\x")]
    public void UnquotesValues(string raw, string expected) => Assert.Equal(expected, A1111Infotext.Unquote(raw));

    #endregion // Tests
}
