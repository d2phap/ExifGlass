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
using ExifGlass.Core.Editing;
using ExifGlass.Core.ExifTool;
using ExifGlass.Core.Metadata;
using Xunit;

namespace ExifGlass.Core.Tests.ExifTool;

/// <summary>
/// The service against the real ExifTool: table reads, previews, binary data, writes with _original.
/// </summary>
public class ExifToolServiceTests(ExifToolFixture exifTool)
{
    #region Tests

    [Fact]
    public async Task ReadsADocumentWithGroupsIdsAndFlags()
    {
        var service = exifTool.Require();
        var tagDb = TagDb.LoadEmbedded();

        var doc = await service.ReadAsync(TestEnvironment.Fixture("negative_camera.jpg"), ArgumentPolicy.Analyze(ArgumentPolicy.DEFAULT_ARGUMENTS).PassThrough, tagDb, TestContext.Current.CancellationToken);

        Assert.Null(doc.Error);
        Assert.Equal("Canon", doc.Find("EXIF", "Make")!.Value);
        Assert.Equal("0x010F", doc.Find("EXIF", "Make")!.DisplayId);
        Assert.True(doc.Find("EXIF", "Make")!.IsWritable);
        Assert.True(doc.Find("File", "FileName")!.IsReadOnly);
        Assert.Contains(doc.Groups, g => g.Name == "XMP");
        Assert.Equal(service.Version, doc.ExifToolVersion);
        Assert.True(doc.FileSize > 0);
        Assert.Empty(service.MissingAiFeatures);
    }


    [Fact]
    public async Task AiTagsStayIntactAfterTheDaemonReadsTheNextFile()
    {
        var service = exifTool.Require();
        var ct = TestContext.Current.CancellationToken;

        using var first = await service.ReadAiTagsAsync(TestEnvironment.Fixture("c2pa_dalle3.webp"), ct);
        var before = string.Join("|", first.Tags.Select(t => t.Key.TableKey + "=" + t.Value.GetRawText()));

        // the next replies reuse the pipe buffer the first one arrived in
        for (var i = 0; i < 5; i++) (await service.ReadAiTagsAsync(TestEnvironment.Fixture("comfyui_big_workflow.png"), ct)).Dispose();

        Assert.Equal(before, string.Join("|", first.Tags.Select(t => t.Key.TableKey + "=" + t.Value.GetRawText())));
    }


    [Fact]
    public async Task ReportsAMissingFileAsTheDocumentError()
    {
        var service = exifTool.Require();

        var doc = await service.ReadAsync(Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid() + ".jpg"), [], null, TestContext.Current.CancellationToken);

        Assert.Empty(doc.Tags);
        Assert.Equal("File not found", doc.Error);
    }


    [Fact]
    public async Task ShowsDuplicatesAsCopiesWithFamily4()
    {
        var service = exifTool.Require();

        // the Midjourney fixture has a PNG Description and an XMP one: different groups, both kept
        var doc = await service.ReadAsync(TestEnvironment.Fixture("midjourney.png"), [], null, TestContext.Current.CancellationToken);

        Assert.NotNull(doc.Find("PNG", "Description"));
        Assert.All(doc.Tags, t => Assert.Equal(t.Key, t.Key.Trim()));
        Assert.Equal(doc.Tags.Count, doc.Tags.Select(t => t.Key).Distinct().Count());
    }


    [Fact]
    public async Task ReadsEmbeddedPreviewsAndBinaryData()
    {
        var service = exifTool.Require();

        // the synthetic C2PA JPEG embeds its own thumbnail; plain fixtures have none
        Assert.Null(await service.ReadPreviewAsync(TestEnvironment.Fixture("a1111.png"), TestContext.Current.CancellationToken));

        var icc = await service.ReadBinaryAsync(TestEnvironment.Fixture("c2pa_synthetic.jpg"), "JUMBF:C2PAThumbnailClaimJpegData", TestContext.Current.CancellationToken);
        Assert.NotNull(icc);
        Assert.Equal(0xFF, icc[0]);
        Assert.Equal(0xD8, icc[1]);
    }


    [Fact]
    public async Task WritesStagedEditsKeepsTheOriginalAndReloads()
    {
        var service = exifTool.Require();
        var path = TestEnvironment.CopyToTemp(TestEnvironment.Fixture("negative_camera.jpg"), "ảnh edit.jpg");
        var ct = TestContext.Current.CancellationToken;
        try
        {
            var doc = await service.ReadAsync(path, [], TagDb.LoadEmbedded(), ct);
            var make = doc.Find("EXIF", "Make")!;
            var comment = doc.Find("EXIF", "UserComment")!;

            var edits = EditSet.Empty
                .Set(make.Key, make.WriteName, "Fujifilm", make.Value)
                .Delete(comment.Key, comment.WriteName, comment.Value)
                .Add(new AddedTag("XMP", "Title", "line one\nline $two @ home"))
                .Add(new AddedTag("IPTC", "Keywords", "từ khóa 写真"));

            using var plan = WritePlan.Build(path, edits);
            var result = await service.WriteAsync(plan, ct);

            Assert.Equal(SaveOutcome.Updated, result.Outcome);
            Assert.Empty(result.Errors);
            Assert.Equal(path + "_original", result.BackupPath);

            var after = await service.ReadAsync(path, [], null, ct);
            Assert.Equal("Fujifilm", after.Find("EXIF", "Make")!.Value);
            Assert.Null(after.Find("EXIF", "UserComment"));
            Assert.Equal("line one\nline $two @ home", after.Find("XMP", "Title")!.Value);
            Assert.Equal("từ khóa 写真", after.Find("IPTC", "Keywords")!.Value);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }


    [Fact]
    public async Task RemovesEveryGpsTagThenStripsAll()
    {
        var service = exifTool.Require();
        var path = TestEnvironment.CopyToTemp(TestEnvironment.Fixture("negative_camera.jpg"));
        var ct = TestContext.Current.CancellationToken;
        try
        {
            // a location in EXIF and in XMP, as phones and photo apps write it
            var located = EditSet.Empty
                .Add(new AddedTag("EXIF", "GPSLatitude", "22.2759"))
                .Add(new AddedTag("EXIF", "GPSLatitudeRef", "N"))
                .Add(new AddedTag("XMP", "GPSLatitude", "22.2759"))
                .Add(new AddedTag("XMP", "GPSLongitude", "114.1455"));
            using (var plan = WritePlan.Build(path, located)) Assert.Equal(SaveOutcome.Updated, (await service.WriteAsync(plan, ct)).Outcome);

            var before = await service.ReadAsync(path, [], TagDb.LoadEmbedded(), ct);
            Assert.Contains(before.Tags, t => t.Group0 == "XMP" && t.Name == "GPSLatitude");
            Assert.True(EditTargets.GpsCount(before) >= 3);

            using (var plan = WritePlan.Build(path, EditSet.Empty.WithRemoveGps(true))) Assert.Equal(SaveOutcome.Updated, (await service.WriteAsync(plan, ct)).Outcome);

            var after = await service.ReadAsync(path, [], null, ct);
            Assert.DoesNotContain(after.Tags, t => t.Name.StartsWith("GPS", StringComparison.Ordinal));
            Assert.Equal(before.Find("EXIF", "Make")!.Value, after.Find("EXIF", "Make")!.Value);

            using (var plan = WritePlan.Build(path, EditSet.Empty.WithStripAll(true))) Assert.Equal(SaveOutcome.Updated, (await service.WriteAsync(plan, ct)).Outcome);

            var stripped = await service.ReadAsync(path, [], null, ct);
            Assert.DoesNotContain(stripped.Tags, t => t.Group0 is "EXIF" or "XMP" or "IPTC" or "MakerNotes");
            Assert.True(File.Exists(path + "_original"));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }


    [Fact]
    public async Task ReportsANoOpWriteAsUnchanged()
    {
        var service = exifTool.Require();
        var path = TestEnvironment.CopyToTemp(TestEnvironment.Fixture("negative_text.png"));
        try
        {
            using var plan = WritePlan.Build(path, EditSet.Empty.WithRemoveGps(true));
            var result = await service.WriteAsync(plan, TestContext.Current.CancellationToken);

            Assert.Equal(SaveOutcome.Unchanged, result.Outcome);
            Assert.Null(result.BackupPath);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    #endregion // Tests
}
