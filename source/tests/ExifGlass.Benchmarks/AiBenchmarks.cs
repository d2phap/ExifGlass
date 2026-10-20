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
using BenchmarkDotNet.Attributes;
using ExifGlass.Core.Ai;
using ExifGlass.Core.ExifTool;
using System.Buffers;

namespace ExifGlass.Benchmarks;

/// <summary>
/// §8 AI rows: the parse alone (20 ms typical, 150 ms for a 2 MB workflow) and the warm reads around it.
/// </summary>
[MemoryDiagnoser]
public class AiBenchmarks
{
    #region Fields and constants

    private ExifToolService _service = null!;
    private byte[] _a1111 = [];
    private byte[] _comfy = [];
    private byte[] _c2pa = [];
    private byte[] _bigWorkflow = [];

    #endregion // Fields and constants


    #region Setup

    [GlobalSetup]
    public void Setup()
    {
        _service = new ExifToolService(BenchData.ExifTool);
        _service.StartAsync().GetAwaiter().GetResult();

        _a1111 = Capture("a1111.png");
        _comfy = Capture("comfyui.png");
        _c2pa = Capture("c2pa_chatgpt.png");
        _bigWorkflow = Capture("comfyui_big_workflow.png");
    }


    [GlobalCleanup]
    public void Cleanup() => _service.DisposeAsync().AsTask().GetAwaiter().GetResult();

    #endregion // Setup


    #region Benchmarks

    [Benchmark]
    public AiKind ParseA1111() => Parse(_a1111);


    [Benchmark]
    public AiKind ParseComfyUi() => Parse(_comfy);


    [Benchmark]
    public AiKind ParseC2pa() => Parse(_c2pa);


    [Benchmark]
    public AiKind ParseComfyUi2MB() => Parse(_bigWorkflow);


    [Benchmark]
    public int ReadTableJpegWarm() => _service.ReadAsync(BenchData.Fixture("negative_camera.jpg"), ["-fast", "-m", "-q"]).GetAwaiter().GetResult().Tags.Count;


    [Benchmark]
    public AiKind ReadAndParseAiComfyUiWarm() => _service.ReadAiAsync(BenchData.Fixture("comfyui.png")).GetAwaiter().GetResult()!.Kind;


    [Benchmark]
    public AiKind ReadAndParseAi2MBWarm() => _service.ReadAiAsync(BenchData.Fixture("comfyui_big_workflow.png")).GetAwaiter().GetResult()!.Kind;

    #endregion // Benchmarks


    #region Private methods

    private static AiKind Parse(byte[] json)
    {
        using var tags = AiTagSet.Parse(new ReadOnlySequence<byte>(json));
        return AiEngine.Parse(tags)!.Kind;
    }


    private byte[] Capture(string fixture) =>
        _service.Daemon.ExecuteAsync(ExifToolCommands.AiRead(BenchData.Fixture(fixture)), static (in ExifToolOutput o, CancellationToken _) => o.Stdout.ToArray()).GetAwaiter().GetResult();

    #endregion // Private methods
}
