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
using Avalonia.Threading;
using ExifGlass.Core.Common;
using ExifGlass.Services;
using ExifGlass.Windows;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace ExifGlass.Diagnostics;

/// <summary>
/// --perf [file]: a scripted run of the real window for the gates (plan 8): startup, idle memory, the open pipeline, panel motion and repeated loads.
/// </summary>
public static class PerfRun
{
    #region Fields and constants

    public const string PERF_ARG = "--perf";

    private const int REPEATED_LOADS = 50;
    private const int MOTION_ROUNDS = 3;
    private const double PUSH_WIDTH = 760;
    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan Motion = TimeSpan.FromMilliseconds(600);

    #endregion // Fields and constants


    #region Public methods

    public static bool IsPerf(IReadOnlyList<string> args) => args.Contains(PERF_ARG, StringComparer.OrdinalIgnoreCase);


    /// <summary>
    /// Runs the scenario on the shown main window and writes the report; returns the process exit code.
    /// </summary>
    public static async Task<int> RunAsync(AppServices services, MainWindow window, FrameProbe probe, UiWatchdog watchdog, TextWriter output)
    {
        var vm = services.ViewModel;
        var file = services.Options.File;
        var budgetsMet = true;

        // a JIT build lists its dependencies for the host; a NativeAOT one has none
        var compiled = AppContext.GetData("APP_CONTEXT_DEPS_FILES") is null ? "AOT" : "JIT";
        output.WriteLine(Invariant($"ExifGlass perf · {AppInfo.BuildInfo?.FullVersion} · {RuntimeInformation.RuntimeIdentifier} · {compiled} · render {(services.Options.SoftwareRendering ? "software" : "GPU")}"));

        await WaitForAsync(() => StartupClock.Of(StartupClock.FIRST_FRAME) is not null, TimeSpan.FromSeconds(5));
        output.WriteLine(Invariant($"window {window.ClientSize.Width:0} x {window.ClientSize.Height:0} at {window.RenderScaling * 100:0} %"));
        output.WriteLine("STARTUP");
        output.Write(StartupClock.Format());
        if (StartupClock.Of(StartupClock.FIRST_FRAME) is { } first) output.WriteLine(Invariant($"  process created -> first frame {StartupClock.BeforeMain + first:0} ms"));

        await Task.Delay(Idle);
        output.WriteLine("IDLE " + Memory());
        output.WriteLine("  Skia GPU cache " + (await probe.ReadGpuCacheAsync() ?? "n/a"));

        await WaitForAsync(() => services.Metadata.Status != ExifToolStatus.Starting, TimeSpan.FromSeconds(30));
        output.WriteLine(Invariant($"EXIFTOOL {services.Metadata.Status} {services.Metadata.Version} at {StartupClock.Now:0} ms after Main"));

        if (file is not null)
        {
            UiWatchdog.Operation = "open";
            var watch = Stopwatch.StartNew();
            var open = vm.OpenFileAsync(file);
            await WaitForAsync(() => vm.Document?.SourcePath == file && vm.Rows.Items.Count > 0, TimeSpan.FromSeconds(30));
            await NextFrameAsync(window);
            var shown = watch.Elapsed.TotalMilliseconds;
            await open;
            var ai = watch.Elapsed.TotalMilliseconds;

            output.WriteLine(Invariant($"OPEN {Path.GetFileName(file)}: {vm.Rows.Total} tags in view, {vm.Document?.Tags.Count} read"));
            output.WriteLine(Invariant($"  table read + parse {vm.Loader.LastLoadTime.TotalMilliseconds:0} ms · rows on screen {shown:0} ms · AI read done {ai:0} ms ({(vm.Document?.Ai is null ? "no AI data" : "AI data")})"));

            UiWatchdog.Operation = "idle";
            await Task.Delay(Idle);
            output.WriteLine("AFTER FIRST LOAD " + Memory());
            output.WriteLine("  Skia GPU cache " + (await probe.ReadGpuCacheAsync() ?? "n/a"));
        }

        if (file is not null) budgetsMet &= await MeasureTableAsync(window, vm, probe, output);

        budgetsMet &= await MeasureAsync(probe, output, "rail column 0 <-> 244", onRenderThread: false, () => vm.ToggleRailCommand.Execute(null));
        budgetsMet &= await MeasureAsync(probe, output, "inspector column 0 <-> 312", onRenderThread: false, () => vm.ToggleInspectorCommand.Execute(null));

        var width = window.Width;
        window.Width = PUSH_WIDTH;
        await Task.Delay(Motion);
        budgetsMet &= await MeasureAsync(probe, output, "rail push", onRenderThread: true, () => vm.ToggleRailCommand.Execute(null));
        budgetsMet &= await MeasureAsync(probe, output, "inspector push", onRenderThread: true, () => vm.ToggleInspectorCommand.Execute(null));
        window.Width = width;
        await Task.Delay(Motion);

        if (file is not null) await RepeatLoadsAsync(services, file, output);

        var max = watchdog.Max;
        var hitches = watchdog.Take();
        output.WriteLine(Invariant($"WATCHDOG longest UI-thread wait {max:0} ms; {hitches.Length} over {UiWatchdog.HITCH_MS:0} ms"));
        foreach (var hitch in hitches) output.WriteLine("  " + hitch);

        output.WriteLine(budgetsMet ? "MOTION BUDGETS MET" : "MOTION BUDGETS MISSED");
        return 0;
    }

    #endregion // Public methods


    #region Private methods

    // each round runs the action twice: the panel goes one way, then back; composition motion is judged on the render thread (plan 8), layout motion on the UI thread
    private static async Task<bool> MeasureAsync(FrameProbe probe, TextWriter output, string label, bool onRenderThread, Action toggle)
    {
        UiWatchdog.Operation = label;
        probe.Start();
        for (var i = 0; i < MOTION_ROUNDS * 2; i++)
        {
            toggle();
            await Task.Delay(Motion);
        }

        var stats = probe.Stop(Invariant($"MOTION {label}, {MOTION_ROUNDS} times each way"));
        UiWatchdog.Operation = "idle";

        var judged = onRenderThread ? "render-thread frames" : "UI-thread frames";
        var met = (onRenderThread ? stats.Render : stats.Ui).MeetsBudget && stats.Gc1 == 0 && stats.Gc2 == 0;
        output.WriteLine(stats + (met ? $"\n    budget met ({judged})" : $"\n    BUDGET MISSED on {judged} (p95 16.7, p99 25, none over 50 ms, no gen-1/2 GC)"));
        return met;
    }


    // P5: the table's own motion and scrolling with the file's rows (plan 8: view apply, FLIP, 60 fps scrolling, allocations per frame)
    private static async Task<bool> MeasureTableAsync(MainWindow window, ViewModels.MainViewModel vm, FrameProbe probe, TextWriter output)
    {
        var table = window.Table.Table;
        var met = true;

        vm.CycleSort(Core.Query.SortColumn.Value);
        await WaitForAsync(() => vm.Rows.Query.SortColumn == Core.Query.SortColumn.Value, TimeSpan.FromSeconds(5));
        await Task.Delay(Motion);
        output.WriteLine(Invariant($"TABLE {vm.Rows.Items.Count} items: a sorted view applied in {table.LastApply.TotalMilliseconds:0.0} ms on the UI thread (reset + layout)"));
        vm.CycleSort(Core.Query.SortColumn.Value);
        vm.CycleSort(Core.Query.SortColumn.Value);
        await Task.Delay(Motion);

        if (table.Scroller is { } scroller)
        {
            UiWatchdog.Operation = "table scroll";
            probe.Start();
            var frames = await ScrollAsync(window, scroller, TimeSpan.FromSeconds(8), 24);
            var stats = probe.Stop(Invariant($"SCROLL 24 px a frame over {vm.Rows.Items.Count} items"));
            var perFrame = stats.AllocatedKb / Math.Max(1, frames);
            var ok = stats.Ui.MeetsBudget && perFrame <= 64;
            var verdict = ok ? "budget met (UI-thread frames, 64 KB a frame)" : "BUDGET MISSED (UI-thread frames p95 16.7, 64 KB a frame)";
            output.WriteLine(stats + Invariant($"\n    {perFrame:0.0} KB allocated a frame over {frames} frames\n    {verdict}"));
            met &= ok;
            scroller.Offset = default;
            await Task.Delay(Motion);
        }

        met &= await MeasureAsync(probe, output, "FLIP sort by value (asc, desc, none)", onRenderThread: true, () => vm.CycleSort(Core.Query.SortColumn.Value));
        met &= await MeasureAsync(probe, output, "FLIP search date and clear", onRenderThread: true, () => vm.SearchText = vm.SearchText.Length == 0 ? "date" : string.Empty);
        if (vm.Rows.Groups.Count > 1)
        {
            var group = vm.Rows.Groups[1].Name;
            met &= await MeasureAsync(probe, output, $"group collapse {group}", onRenderThread: true, () => vm.ToggleGroup(group));
        }

        met &= await MeasureAsync(probe, output, "column # 0 <-> 74", onRenderThread: false, () => vm.ToggleColumnCommand.Execute(Core.Export.TableColumns.Index));
        return met;
    }


    // one step a frame, back and forth through the whole table
    private static Task<int> ScrollAsync(MainWindow window, Avalonia.Controls.ScrollViewer scroller, TimeSpan duration, double step)
    {
        var done = new TaskCompletionSource<int>();
        var watch = Stopwatch.StartNew();
        var direction = 1;
        var frames = 0;
        void Tick(TimeSpan _)
        {
            var max = Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height);
            var y = scroller.Offset.Y + direction * step;
            if (y >= max) (y, direction) = (max, -1);
            else if (y <= 0) (y, direction) = (0, 1);

            scroller.Offset = scroller.Offset.WithY(y);
            frames++;
            if (watch.Elapsed < duration) window.RequestAnimationFrame(Tick);
            else done.TrySetResult(frames);
        }

        window.RequestAnimationFrame(Tick);
        return done.Task;
    }


    // DoD 5 and plan 8: memory doesn't grow over repeated loads of the same file
    private static async Task RepeatLoadsAsync(AppServices services, string file, TextWriter output)
    {
        var vm = services.ViewModel;
        UiWatchdog.Operation = "repeated loads";

        var heapBefore = Heap();
        var setBefore = WorkingSet();
        for (var i = 0; i < REPEATED_LOADS; i++)
        {
            await vm.OpenFileAsync(file);
            await WaitForAsync(() => ReferenceEquals(vm.Document, vm.Loader.Document), TimeSpan.FromSeconds(30));
        }

        UiWatchdog.Operation = "idle";
        await Task.Delay(Idle);
        var heapAfter = Heap();
        var setAfter = WorkingSet();

        output.WriteLine(Invariant($"LOADS {REPEATED_LOADS} repeated: managed heap {Mb(heapBefore)} -> {Mb(heapAfter)} ({Mb(heapAfter - heapBefore)}); working set {Mb(setBefore)} -> {Mb(setAfter)} ({Mb(setAfter - setBefore)})"));
        output.WriteLine("AFTER REPEATED LOADS " + Memory());
    }


    private static async Task WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        var watch = Stopwatch.StartNew();
        while (!condition() && watch.Elapsed < timeout) await Task.Delay(10);
    }


    private static Task NextFrameAsync(MainWindow window)
    {
        var frame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(() => window.RequestAnimationFrame(_ => frame.TrySetResult()));
        return frame.Task;
    }


    private static long Heap()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return GC.GetTotalMemory(true);
    }


    private static long WorkingSet()
    {
        using var self = Process.GetCurrentProcess();
        return self.WorkingSet64;
    }


    private static string Memory()
    {
        var heap = Heap();
        using var self = Process.GetCurrentProcess();
        return Invariant($"working set {Mb(self.WorkingSet64)} · private {Mb(self.PrivateMemorySize64)} · managed heap {Mb(heap)}");
    }


    private static string Mb(long bytes) => Invariant($"{bytes / 1048576.0:0.0} MB");


    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    #endregion // Private methods
}
