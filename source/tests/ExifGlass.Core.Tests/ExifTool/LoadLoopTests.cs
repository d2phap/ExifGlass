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
using ExifGlass.Core.ExifTool;
using ExifGlass.Core.Metadata;
using System.Runtime.CompilerServices;
using Xunit;

namespace ExifGlass.Core.Tests.ExifTool;

/// <summary>
/// Runs alone: the heap is process-wide, so tests running in parallel would blur the numbers.
/// </summary>
[CollectionDefinition(nameof(MemoryCollection), DisableParallelization = true)]
public sealed class MemoryCollection;


/// <summary>
/// §8 leaks: 100 loads in a row grow the heap by no more than 5 MB.
/// </summary>
[Collection(nameof(MemoryCollection))]
public class LoadLoopTests(ExifToolFixture exifTool)
{
    #region Tests

    [Fact]
    public async Task AHundredLoadsDontGrowTheHeap()
    {
        var service = exifTool.Require();
        var ct = TestContext.Current.CancellationToken;
        string[] files = ["c2pa_chatgpt.png", "a1111.png", "negative_camera.jpg", "comfyui.png"];
        var tagDb = TagDb.LoadEmbedded();

        for (var i = 0; i < 20; i++) await Load(service, files[i % files.Length], tagDb, ct);
        var before = Heap();

        for (var i = 0; i < 100; i++) await Load(service, files[i % files.Length], tagDb, ct);
        var growth = Heap() - before;

        Assert.True(growth < 5 * 1024 * 1024, $"heap grew {growth / 1024} KB over 100 loads");
    }


    // multi-MB strings leave buffers in ArrayPool until ~30 s of idle (G2), so a heap delta can't tell a leak here: the objects must go
    [Fact]
    public async Task ABigWorkflowDocumentIsCollectedWhenDropped()
    {
        var service = exifTool.Require();
        var ct = TestContext.Current.CancellationToken;

        var (doc, ai, workflow) = await LoadWeak(service, TestEnvironment.Fixture("comfyui_big_workflow.png"), ct);

        // the awaits above may finish inline, on the stack of the parse task that still roots its result
        await Task.Delay(50, ct);
        Heap();

        Assert.False(doc.TryGetTarget(out _), "the document is still alive");
        Assert.False(ai.TryGetTarget(out _), "the AI information is still alive");
        Assert.False(workflow.TryGetTarget(out _), "the 2 MB workflow string is still alive");
    }

    #endregion // Tests


    #region Private methods

    private static async Task Load(ExifToolService service, string file, TagDb? tagDb, CancellationToken ct)
    {
        var path = TestEnvironment.Fixture(file);
        var doc = await service.ReadAsync(path, ["-fast", "-m", "-q"], tagDb, ct);
        GC.KeepAlive(doc.WithAi(await service.ReadAiAsync(path, ct)));
    }


    // in its own method so no local keeps the objects alive
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<(WeakReference<MetadataDocument>, WeakReference<AiGenerationInfo>, WeakReference<string>)> LoadWeak(ExifToolService service, string path, CancellationToken ct)
    {
        var doc = (await service.ReadAsync(path, [], null, ct)).WithAi(await service.ReadAiAsync(path, ct));
        return (new(doc), new(doc.Ai!), new(doc.Ai!.Workflows[0].Json));
    }


    private static long Heap()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return GC.GetTotalMemory(forceFullCollection: true);
    }

    #endregion // Private methods
}
