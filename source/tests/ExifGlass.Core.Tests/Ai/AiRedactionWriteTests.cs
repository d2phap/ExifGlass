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
using ExifGlass.Core.Editing;
using ExifGlass.Core.Metadata;
using Xunit;

namespace ExifGlass.Core.Tests.Ai;

/// <summary>
/// D24 on real files: after "Remove AI generation data" no prompt, workflow or parameter is left, and provenance stays.
/// </summary>
public class AiRedactionWriteTests(ExifToolFixture exifTool)
{
    #region Properties

    // every fixture ExifTool can write; audio, WebM, EXR and the comf chunk are covered by the warning test
    public static TheoryData<string> Writable =>
    [
        "a1111.png", "a1111.jpg", "a1111.webp", "a1111.gif", "a1111_utf8.png", "a1111_ztxt.png", "a1111_text_after_idat.png", "a1111_mojibake.png",
        "forge.png", "sdnext.png", "civitai.jpg", "civitai.png", "civitai_extension.png", "krita.png", "krita_cloud.png",
        "comfyui.png", "comfyui_flux_subgraph.png", "comfyui_anim.webp", "comfyui.avif", "comfyui.mp4",
        "invokeai.png", "invokeai_graph_only.png", "invokeai_legacy.png", "novelai.png",
        "fooocus.png", "fooocus_a1111.png", "fooocus.jpg", "fooocus.webp", "fooocus_mre.png", "fooocus_mre.jpg", "ruinedfooocus.png",
        "swarmui.png", "swarmui.jpg", "easydiffusion.png", "easydiffusion.jpg", "drawthings.png", "midjourney.png", "tensorart.png",
        "iptc_ai_fields.jpg", "c2pa_gemini.png", "c2pa_firefly.jpg",
    ];

    #endregion // Properties


    #region Tests

    [Theory]
    [MemberData(nameof(Writable))]
    public async Task RemovesGenerationDataAndKeepsProvenance(string fixture)
    {
        var service = exifTool.Require();
        var ct = TestContext.Current.CancellationToken;
        var path = TestEnvironment.CopyToTemp(TestEnvironment.Fixture(fixture));
        try
        {
            var before = (await service.ReadAiAsync(path, ct))!;
            var redaction = AiRedaction.Plan(before, TagDb.LoadEmbedded());

            // provenance-only files have nothing to remove
            if (redaction.IsEmpty)
            {
                Assert.All(before.Sources, s => Assert.True(s.IsProvenance, s.ToString()));
                return;
            }

            using var plan = WritePlan.Build(path, EditSet.Empty.WithRemoveAi(true), redaction);
            var result = await service.WriteAsync(plan, ct);
            Assert.True(result.Outcome == SaveOutcome.Updated, string.Join("; ", result.Errors));

            var after = await service.ReadAiAsync(path, ct);
            var generation = after?.Sources.Where(s => !s.IsProvenance && s.Source != AiSourceKind.ToolName).ToList() ?? [];
            Assert.All(generation, s =>
            {
                Assert.Empty(s.Prompts);
                Assert.Empty(s.Workflows);
                Assert.Empty(s.Parameters);
            });

            // IPTC prompt fields go, its source type stays; C2PA, AIGC and training signals stay as they were
            var kept = before.Sources.Where(s => s.IsProvenance).Select(s => (s.Source, s.Kind)).ToList();
            Assert.Equal(kept, after?.Sources.Where(s => s.IsProvenance).Select(s => (s.Source, s.Kind)).ToList() ?? []);
            Assert.All(after?.Sources ?? [], s => Assert.DoesNotContain(s.Prompts, p => p.Label == "Prompt information"));
            Assert.True(File.Exists(path + "_original"));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    #endregion // Tests
}
