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
using ExifGlass.Core.Query;
using System.Diagnostics;
using Xunit;

namespace ExifGlass.Core.Tests.ExifTool;

/// <summary>
/// Runs alone: every test shares the one ExifTool daemon, so parallel reads would queue ahead of the timed ones.
/// </summary>
[CollectionDefinition(nameof(TimingCollection), DisableParallelization = true)]
public sealed class TimingCollection;


/// <summary>
/// Camera files: HEIC, TIFF, RAW and a 5,000-tag video from the owner's samples (§9: never committed, used when present).
/// </summary>
[Collection(nameof(TimingCollection))]
public class GeneralFixtureTests(ExifToolFixture exifTool)
{
    #region Properties

    // __temp/fixtures-private/general next to source/, or EG_PRIVATE_FIXTURES
    private static string Folder => Environment.GetEnvironmentVariable("EG_PRIVATE_FIXTURES")
        ?? Path.Combine(TestEnvironment.SourceDir, "..", "__temp", "fixtures-private", "general");

    #endregion // Properties


    #region Tests

    [Theory]
    [InlineData("cam.jpg", "EXIF", "Make")]
    [InlineData("iphone.heic", "EXIF", "Make")]
    [InlineData("galaxy.heic", "EXIF", "Model")]
    [InlineData("8Bim.tif", "Photoshop", null)]
    [InlineData("StillLife.cr2", "MakerNotes", "Make")]
    [InlineData("RAW_SONY_ILCE-7M2.ARW", "MakerNotes", "Make")]
    [InlineData("DSC8695.dng", "EXIF", "Make")]
    public async Task ReadsCameraFilesWithinTheBudget(string file, string group, string? name)
    {
        var path = Private(file);
        var service = exifTool.Require();
        var ct = TestContext.Current.CancellationToken;

        await service.ReadAsync(path, ["-fast", "-m", "-q"], TagDb.LoadEmbedded(), ct);
        var watch = Stopwatch.StartNew();
        var doc = await service.ReadAsync(path, ["-fast", "-m", "-q"], TagDb.LoadEmbedded(), ct);
        var ms = watch.ElapsedMilliseconds;

        Assert.Null(doc.Error);
        Assert.Contains(doc.Groups, g => g.Name == group);
        if (name is not null) Assert.NotNull(doc.Find("EXIF", name));

        // §8: a JPEG under 150 ms to rows, a RAW under 400 ms
        Assert.True(ms < (file.EndsWith(".jpg", StringComparison.Ordinal) ? 150 : 400), $"{file}: {ms} ms");
        Assert.Null(await service.ReadAiAsync(path, ct) is { IsAi: true } ai ? ai.Summary() : null);
    }


    [Theory]
    [InlineData("RAW_SONY_ILCE-7M2.ARW", 400_000)]
    [InlineData("StillLife.cr2", 100_000)]
    [InlineData("cam.jpg", 1_000)]
    public async Task ReadsTheLargestEmbeddedPreviewAsAJpeg(string file, int atLeast)
    {
        var preview = await exifTool.Require().ReadPreviewAsync(Private(file), TestContext.Current.CancellationToken);

        Assert.NotNull(preview);
        Assert.True(preview.Length >= atLeast, $"{file}: {preview.Length} bytes");
        Assert.Equal([0xFF, 0xD8], preview[..2]);
    }


    [Fact]
    public async Task ReadsFiveThousandEmbeddedTagsAndQueriesThem()
    {
        var path = Private("big_timed.mp4");
        var service = exifTool.Require();

        var doc = await service.ReadAsync(path, ["-ee"], null, TestContext.Current.CancellationToken);
        var view = QueryEngine.Run(doc, ViewQuery.Default with { Search = "text" }, TestContext.Current.CancellationToken);

        Assert.True(doc.Tags.Count > 5000, $"{doc.Tags.Count} tags");
        Assert.True(view.IsFiltering);
    }


    [Theory]
    [InlineData("iphone.heic")]
    [InlineData("StillLife.cr2")]
    [InlineData("8Bim.tif")]
    public async Task WritesCameraFilesAndKeepsTheOriginal(string file)
    {
        var service = exifTool.Require();
        var path = TestEnvironment.CopyToTemp(Private(file));
        var ct = TestContext.Current.CancellationToken;
        try
        {
            using var plan = WritePlan.Build(path, EditSet.Empty.Add(new AddedTag("XMP", "Title", "ExifGlass test")).WithRemoveGps(true));
            var result = await service.WriteAsync(plan, ct);

            Assert.Equal(SaveOutcome.Updated, result.Outcome);
            Assert.Equal("ExifGlass test", (await service.ReadAsync(path, [], null, ct)).Find("XMP", "Title")!.Value);
            Assert.Equal(new FileInfo(Private(file)).Length, new FileInfo(path + "_original").Length);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }


    [Fact]
    public async Task ReadsAndWritesAReadOnlyFile()
    {
        var service = exifTool.Require();
        var path = TestEnvironment.CopyToTemp(TestEnvironment.Fixture("negative_camera.jpg"));
        var ct = TestContext.Current.CancellationToken;
        try
        {
            File.SetAttributes(path, FileAttributes.ReadOnly);

            Assert.Equal("Canon", (await service.ReadAsync(path, [], null, ct)).Find("EXIF", "Make")!.Value);

            using var plan = WritePlan.Build(path, EditSet.Empty.Set("EXIF:IFD0:Make", "IFD0:Make", "Nikon", "Canon"));
            var result = await service.WriteAsync(plan, ct);

            // S5: ExifTool writes a temp file and renames it over the read-only original
            Assert.Equal(SaveOutcome.Updated, result.Outcome);
            Assert.Equal("Nikon", (await service.ReadAsync(path, [], null, ct)).Find("EXIF", "Make")!.Value);
        }
        finally
        {
            foreach (var f in Directory.GetFiles(Path.GetDirectoryName(path)!)) File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(Path.GetDirectoryName(path)!, true);
        }
    }

    #endregion // Tests


    #region Private methods

    private static string Private(string file)
    {
        var path = Path.GetFullPath(Path.Combine(Folder, file));
        if (!File.Exists(path)) Assert.Skip($"Private sample {file} isn't in {Folder}");
        return path;
    }

    #endregion // Private methods
}
