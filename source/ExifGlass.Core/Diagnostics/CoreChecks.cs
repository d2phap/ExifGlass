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
using ExifGlass.Core.Common;
using ExifGlass.Core.Editing;
using ExifGlass.Core.ExifTool;
using ExifGlass.Core.Metadata;
using System.Diagnostics;
using System.Text.Json;

namespace ExifGlass.Core.Diagnostics;

/// <summary>
/// Self-test checks that need nothing but Core: build info, JSON under AOT, ExifTool, the tag table, the AI corpus, a write.
/// </summary>
public static class CoreChecks
{
    #region Fields and constants

    private static readonly TimeSpan EXIFTOOL_TIMEOUT = TimeSpan.FromSeconds(20);

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Adds every Core check; the corpus and write checks read the fixtures folder (--fixtures, or found above the app).
    /// </summary>
    public static SelfTest AddCoreChecks(this SelfTest test, string? fixturesDirectory = null)
    {
        var fixtures = fixturesDirectory ?? FindFixtures();

        return test
            .Add("build-info", _ => Task.FromResult(CheckBuildInfo()))
            .Add("json", _ => Task.FromResult(CheckJson()))
            .Add("exiftool", CheckExifToolAsync)
            .Add("tagdb", _ => Task.FromResult(CheckTagDb()))
            .Add("ai-corpus", (log, ct) => CheckAiCorpusAsync(fixtures, log, ct))
            .Add("write", ct => CheckWriteAsync(fixtures, ct));
    }

    #endregion // Public methods


    #region Private methods

    private static string CheckBuildInfo()
    {
        var info = AppInfo.BuildInfo ?? throw new InvalidOperationException("AppInfo.BuildInfo isn't set by the app head");
        if (string.IsNullOrWhiteSpace(info.FullVersion)) throw new InvalidOperationException("The build has no version");

        return $"{info.FullVersion}, {info.Channel}, built {info.BuildDate}";
    }


    private static string CheckJson()
    {
        var probe = new SelfTestProbe("ExifGlass", 3, ["EXIF", "XMP", "AI"]);
        var json = JsonSerializer.Serialize(probe, CoreJsonContext.Default.SelfTestProbe);
        var back = JsonSerializer.Deserialize(json, CoreJsonContext.Default.SelfTestProbe);

        if (back is null || back.Name != probe.Name || back.Count != probe.Count || !back.Tags.SequenceEqual(probe.Tags))
        {
            throw new InvalidOperationException($"Round trip changed the payload: {json}");
        }

        return $"source-generated round trip, {json.Length} bytes";
    }


    private static async Task<string> CheckExifToolAsync(CancellationToken cancellationToken)
    {
        var location = ExifToolLocator.Locate(null) ?? throw new FileNotFoundException("No ExifTool next to the app or on PATH");
        if (location.ConfigPath is null) throw new FileNotFoundException($"{ExifToolLocator.CONFIG_FILE_NAME} isn't next to the app");

        await using var service = new ExifToolService(location, EXIFTOOL_TIMEOUT);
        var watch = Stopwatch.StartNew();
        var version = await service.StartAsync(cancellationToken).ConfigureAwait(false);

        var missing = service.MissingAiFeatures;
        var gate = missing.Count == 0 ? "reads every AI format" : "missing " + string.Join(", ", missing.Select(f => f.Name));
        return $"{version} ({location.Source}) started and primed in {watch.ElapsedMilliseconds} ms, {gate}, at {location.Path}";
    }


    private static string CheckTagDb()
    {
        var watch = Stopwatch.StartNew();
        var db = TagDb.LoadEmbedded() ?? throw new InvalidOperationException("The embedded tag table is missing");
        if (!db.TryFind("EXIF", "FNumber", "ExifIFD", out var f) || !f.IsWritable || f.Format != "rational64u")
        {
            throw new InvalidOperationException("EXIF:FNumber isn't a writable rational64u");
        }

        return $"{db.Count} tags from ExifTool {db.ExifToolVersion}, loaded in {watch.ElapsedMilliseconds} ms";
    }


    // reads every fixture with both reads; with ai-golden next to the fixtures, each AI result must match its golden file
    private static async Task<string> CheckAiCorpusAsync(string? fixtures, TextWriter log, CancellationToken cancellationToken)
    {
        if (fixtures is null || !Directory.Exists(fixtures)) return "skipped: no fixtures folder (pass --fixtures <source>/tests/fixtures/ai)";

        var location = ExifToolLocator.Locate(null) ?? throw new FileNotFoundException("No ExifTool");
        await using var service = new ExifToolService(location, EXIFTOOL_TIMEOUT);
        await service.StartAsync(cancellationToken).ConfigureAwait(false);

        var golden = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(fixtures))!, "ai-golden");
        int files = 0, ai = 0, compared = 0;
        var mismatches = new List<string>();
        var watch = Stopwatch.StartNew();

        foreach (var path in Directory.GetFiles(fixtures).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(path);
            if (name is "README.md" or ".gitattributes" || name.EndsWith(".txt", StringComparison.Ordinal)) continue;

            files++;
            var doc = await service.ReadAsync(path, [], null, cancellationToken).ConfigureAwait(false);
            var info = await service.ReadAiAsync(path, cancellationToken).ConfigureAwait(false);
            if (info?.IsAi == true) ai++;

            var goldenFile = Path.Combine(golden, name + ".json");
            var verdict = string.Empty;
            if (File.Exists(goldenFile))
            {
                compared++;
                var expected = (await File.ReadAllTextAsync(goldenFile, cancellationToken).ConfigureAwait(false)).ReplaceLineEndings("\n").TrimEnd('\n');
                var actual = info is null ? "null" : AiExport.ToJson(info, stable: true).ReplaceLineEndings("\n");
                if (expected != actual)
                {
                    mismatches.Add(name);
                    verdict = " MISMATCH";
                }
            }

            await log.WriteLineAsync($"  {name}: {doc.Tags.Count} tags · {info?.Summary() ?? "no AI data"}{verdict}").ConfigureAwait(false);
        }

        if (mismatches.Count > 0) throw new InvalidOperationException($"{mismatches.Count} of {compared} differ from ai-golden: {string.Join(", ", mismatches)}");

        return $"{files} fixtures, {ai} read as AI, {compared} match ai-golden, {watch.ElapsedMilliseconds} ms";
    }


    // edits a temp copy of a fixture, saves, reloads and checks the value and the _original backup
    private static async Task<string> CheckWriteAsync(string? fixtures, CancellationToken cancellationToken)
    {
        var source = fixtures is null ? null : Path.Combine(fixtures, "negative_camera.jpg");
        if (source is null || !File.Exists(source)) return "skipped: no fixtures folder";

        var dir = Directory.CreateTempSubdirectory("exifglass-selftest-").FullName;
        try
        {
            var path = Path.Combine(dir, "ảnh selftest.jpg");
            File.Copy(source, path);

            var location = ExifToolLocator.Locate(null) ?? throw new FileNotFoundException("No ExifTool");
            await using var service = new ExifToolService(location, EXIFTOOL_TIMEOUT);

            const string TITLE = "Self-test ✓ 写真\nsecond line";
            using var plan = WritePlan.Build(path, EditSet.Empty.Add(new AddedTag("XMP", "Title", TITLE)), null, dir);
            var result = await service.WriteAsync(plan, cancellationToken).ConfigureAwait(false);
            if (result.Outcome != SaveOutcome.Updated) throw new InvalidOperationException($"Save {result.Outcome}: {string.Join("; ", result.Errors)}");

            var doc = await service.ReadAsync(path, [], null, cancellationToken).ConfigureAwait(false);
            if (doc.Find("XMP", "Title")?.Value != TITLE) throw new InvalidOperationException($"Read back \"{doc.Find("XMP", "Title")?.Value}\"");
            if (!File.Exists(path + "_original")) throw new InvalidOperationException("No _original backup");

            return "edited, saved and read back a temp copy; _original kept";
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }


    private static string? FindFixtures()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "fixtures", "ai");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        return null;
    }

    #endregion // Private methods
}
