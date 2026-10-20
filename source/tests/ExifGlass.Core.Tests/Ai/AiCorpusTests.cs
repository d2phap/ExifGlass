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

/// <summary>
/// Every AI fixture read with the real AI read and compared with its golden output (G2).
/// </summary>
public class AiCorpusTests(ExifToolFixture exifTool)
{
    #region Fields and constants

    // EG_UPDATE_GOLDEN=1 rewrites the golden files instead of comparing; review the diff before committing it
    private static readonly bool UPDATE = Environment.GetEnvironmentVariable("EG_UPDATE_GOLDEN") == "1";

    // not AI: a camera, plain text, capture provenance, training signals, a character card, stealth pixels (D26)
    private static readonly HashSet<string> NOT_AI = new(StringComparer.Ordinal)
    {
        "negative_camera.jpg", "negative_text.png", "c2pa_capture.jpg", "ai_training_signals.jpg", "charactercard.png", "novelai_stealth_only.png",
    };

    #endregion // Fields and constants


    #region Properties

    public static TheoryData<string> Fixtures
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var f in Directory.GetFiles(TestEnvironment.AiFixtures).Select(Path.GetFileName).Order(StringComparer.Ordinal))
            {
                if (f is "README.md" or ".gitattributes" || f!.EndsWith(".txt", StringComparison.Ordinal)) continue;
                data.Add(f);
            }

            return data;
        }
    }

    #endregion // Properties


    #region Tests

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task MatchesItsGoldenOutput(string fixture)
    {
        var service = exifTool.Require();
        var info = await service.ReadAiAsync(TestEnvironment.Fixture(fixture), TestContext.Current.CancellationToken);
        var actual = info is null ? "null" : AiExport.ToJson(info, stable: true);

        var golden = Path.Combine(TestEnvironment.AiGolden, fixture + ".json");
        if (UPDATE)
        {
            Directory.CreateDirectory(TestEnvironment.AiGolden);
            await File.WriteAllTextAsync(golden, actual.ReplaceLineEndings("\n") + "\n", TestContext.Current.CancellationToken);
            return;
        }

        Assert.True(File.Exists(golden), $"No golden output for {fixture}: run with EG_UPDATE_GOLDEN=1");
        var expected = (await File.ReadAllTextAsync(golden, TestContext.Current.CancellationToken)).ReplaceLineEndings("\n").TrimEnd('\n');
        Assert.Equal(expected, actual.ReplaceLineEndings("\n"));
    }


    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task FlagsAiExactlyOnTheAiFixtures(string fixture)
    {
        var service = exifTool.Require();
        var info = await service.ReadAiAsync(TestEnvironment.Fixture(fixture), TestContext.Current.CancellationToken);

        if (NOT_AI.Contains(fixture)) Assert.False(info?.IsAi ?? false, $"{fixture} must not read as AI: {info?.Summary()}");
        else Assert.True(info?.IsAi, $"{fixture} must read as AI");
    }

    #endregion // Tests
}
