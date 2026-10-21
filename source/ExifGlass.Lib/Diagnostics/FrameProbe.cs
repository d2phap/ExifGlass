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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using SkiaSharp;
using System.Diagnostics;
using System.Globalization;

namespace ExifGlass.Diagnostics;

/// <summary>
/// Frame gaps on the UI thread and on the render thread, with GC and allocations (plan 8 motion row, S8's recorder); its first render marks the first frame.
/// </summary>
public sealed class FrameProbe
{
    #region Fields and constants

    private const string DRAW = "draw";
    private const string READ_GPU = "gpu";

    private readonly TopLevel _top;
    private readonly List<double> _ui = new(4096);
    private readonly RenderClock _clock = new();
    private readonly CompositionCustomVisual? _visual;
    private int _generation;
    private TimeSpan? _last;
    private int _gen0, _gen1, _gen2;
    private long _allocated;
    private TimeSpan _paused;
    private long _started;

    #endregion // Fields and constants


    #region Constructors

    public FrameProbe(TopLevel top)
    {
        _top = top;
        if (ElementComposition.GetElementVisual(top)?.Compositor is not { } compositor) return;

        _visual = compositor.CreateCustomVisual(_clock);
        _visual.Size = new Vector(1, 1);
        ElementComposition.SetElementChildVisual(top, _visual);
        _visual.SendHandlerMessage(DRAW);
    }

    #endregion // Constructors


    #region Public methods

    /// <summary>
    /// Reads Skia's GPU resource cache on the render thread's next frame: what it holds and its limit; null on the software path.
    /// </summary>
    public async Task<string?> ReadGpuCacheAsync()
    {
        if (_visual is null) return null;

        _visual.SendHandlerMessage(READ_GPU);
        for (var i = 0; i < 50 && _clock.Gpu is null; i++) await Task.Delay(20);
        return _clock.Gpu;
    }


    public void Start()
    {
        _ui.Clear();
        _last = null;
        _gen0 = GC.CollectionCount(0);
        _gen1 = GC.CollectionCount(1);
        _gen2 = GC.CollectionCount(2);
        _allocated = GC.GetTotalAllocatedBytes(false);
        _paused = GC.GetTotalPauseDuration();
        _started = Stopwatch.GetTimestamp();
        _clock.Take();
        _visual?.SendHandlerMessage(true);

        // a chain from an earlier run ends at its next frame instead of doubling this one's
        var generation = ++_generation;
        _top.RequestAnimationFrame(t => OnFrame(t, generation));
    }


    public FrameStats Stop(string label)
    {
        _generation++;
        _visual?.SendHandlerMessage(false);

        return new FrameStats
        {
            Label = label,
            Seconds = Stopwatch.GetElapsedTime(_started).TotalSeconds,
            Ui = GapStats.Of([.. _ui]),
            Render = GapStats.Of(_clock.Take()),
            Gc0 = GC.CollectionCount(0) - _gen0,
            Gc1 = GC.CollectionCount(1) - _gen1,
            Gc2 = GC.CollectionCount(2) - _gen2,
            GcPauseMs = (GC.GetTotalPauseDuration() - _paused).TotalMilliseconds,
            AllocatedKb = (GC.GetTotalAllocatedBytes(false) - _allocated) / 1024.0,
        };
    }

    #endregion // Public methods


    #region Private methods

    private void OnFrame(TimeSpan time, int generation)
    {
        if (generation != _generation) return;

        if (_last is { } last) _ui.Add((time - last).TotalMilliseconds);
        _last = time;
        _top.RequestAnimationFrame(t => OnFrame(t, generation));
    }

    #endregion // Private methods


    #region Nested types

    // runs on the render thread: one gap per composition frame while the probe records
    private sealed class RenderClock : CompositionCustomVisualHandler
    {
        private readonly Lock _lock = new();
        private readonly List<double> _gaps = new(4096);
        private TimeSpan? _last;
        private bool _runs;
        private bool _drawn;
        private bool _readGpu;

        public string? Gpu { get; private set; }

        public override void OnMessage(object message)
        {
            // the first draw is the window's first frame
            if (message is DRAW)
            {
                Invalidate();
                return;
            }

            if (message is READ_GPU)
            {
                Gpu = null;
                _readGpu = true;
                Invalidate();
                return;
            }

            _runs = message is true;
            _last = null;
            if (!_runs) return;

            RegisterForNextAnimationFrameUpdate();
            Invalidate();
        }

        public override void OnAnimationFrameUpdate()
        {
            if (!_runs) return;

            var now = CompositionNow;
            if (_last is { } last)
            {
                lock (_lock) _gaps.Add((now - last).TotalMilliseconds);
            }

            _last = now;
            RegisterForNextAnimationFrameUpdate();
        }

        public override void OnRender(ImmediateDrawingContext context)
        {
            if (!_drawn)
            {
                _drawn = true;
                StartupClock.Mark(StartupClock.FIRST_FRAME);
            }

            if (!_readGpu) return;

            // S8: a lease costs a window-sized surface, so it is taken only when a report asks
            _readGpu = false;
            if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } feature) return;

            using var lease = feature.Lease();
            if (lease.GrContext is not { } gpu)
            {
                Gpu = "none (software)";
                return;
            }

            gpu.GetResourceCacheUsage(out var resources, out var bytes);
            using var dump = new GpuDump();
            gpu.DumpMemoryStatistics(dump);
            Gpu = string.Create(CultureInfo.InvariantCulture, $"{bytes / 1048576.0:0.0} MB in {resources} resources, limit {gpu.GetResourceCacheLimit() / 1048576.0:0} MB") + dump.Format();
        }

        public double[] Take()
        {
            lock (_lock)
            {
                var gaps = _gaps.ToArray();
                _gaps.Clear();
                return gaps;
            }
        }
    }

    // Skia's per-resource memory report: size, type and category of each GPU resource, largest first
    private sealed class GpuDump() : SKTraceMemoryDump(detailedDump: true, dumpWrappedObjects: true)
    {
        private const int TOP = 8;

        private readonly Dictionary<string, (ulong Size, string Type, string Category)> _items = [];

        protected override void OnDumpNumericValue(string dumpName, string valueName, string units, ulong value)
        {
            if (valueName != "size") return;

            var item = _items.GetValueOrDefault(dumpName, (0UL, "", ""));
            _items[dumpName] = (value, item.Item2, item.Item3);
        }

        protected override void OnDumpStringValue(string dumpName, string valueName, string value)
        {
            var item = _items.GetValueOrDefault(dumpName, (0UL, "", ""));
            if (valueName == "type") _items[dumpName] = (item.Item1, value, item.Item3);
            else if (valueName == "category") _items[dumpName] = (item.Item1, item.Item2, value);
        }

        public string Format()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var group in _items.Values.GroupBy(i => $"{i.Type} {i.Category}".Trim()).OrderByDescending(g => g.Sum(i => (double)i.Size)))
            {
                sb.AppendLine().Append(CultureInfo.InvariantCulture, $"    {group.Key}: {group.Count()} · {group.Sum(i => (double)i.Size) / 1048576.0:0.0} MB");
            }

            foreach (var item in _items.Values.OrderByDescending(i => i.Size).Take(TOP))
            {
                sb.AppendLine().Append(CultureInfo.InvariantCulture, $"      {item.Size / 1048576.0:0.00} MB {item.Type} {item.Category}");
            }

            return sb.ToString();
        }
    }

    #endregion // Nested types
}


/// <summary>
/// Frame gaps as percentiles, with the counts over a frame, 25 ms and 50 ms.
/// </summary>
public readonly record struct GapStats(int Count, double P50, double P95, double P99, double Max, int Over17, int Over25, int Over50)
{
    #region Public methods

    public static GapStats Of(double[] gaps)
    {
        if (gaps.Length == 0) return default;

        Array.Sort(gaps);
        double At(double q) => gaps[(int)Math.Min(gaps.Length - 1, Math.Ceiling(q * gaps.Length) - 1)];
        int Over(double ms) => gaps.Count(g => g > ms);
        return new GapStats(gaps.Length, At(0.5), At(0.95), At(0.99), gaps[^1], Over(17.5), Over(25), Over(50));
    }


    /// <summary>
    /// Whether the plan's motion budget holds: p95 16.7 ms, p99 25 ms, none over 50 ms.
    /// </summary>
    public bool MeetsBudget => Count == 0 || (P95 <= 16.7 + 0.5 && P99 <= 25 && Over50 == 0);


    public override string ToString() => Count == 0
        ? "no frames"
        : string.Create(CultureInfo.InvariantCulture, $"n {Count} · p50 {P50:0.0} · p95 {P95:0.0} · p99 {P99:0.0} · max {Max:0.0} ms · >17.5: {Over17} · >25: {Over25} · >50: {Over50}");

    #endregion // Public methods
}


/// <summary>
/// What a FrameProbe recorded over one scenario.
/// </summary>
public sealed class FrameStats
{
    #region Properties

    public required string Label { get; init; }

    public double Seconds { get; init; }

    public GapStats Ui { get; init; }

    public GapStats Render { get; init; }

    public int Gc0 { get; init; }

    public int Gc1 { get; init; }

    public int Gc2 { get; init; }

    public double GcPauseMs { get; init; }

    public double AllocatedKb { get; init; }

    #endregion // Properties


    #region Public methods

    public override string ToString() => string.Create(CultureInfo.InvariantCulture,
        $"{Label} ({Seconds:0.0} s)\n    UI thread frames:     {Ui}\n    render thread frames: {Render}\n    GC {Gc0}/{Gc1}/{Gc2} (pause {GcPauseMs:0.0} ms) · allocated {AllocatedKb:0} KB");

    #endregion // Public methods
}
