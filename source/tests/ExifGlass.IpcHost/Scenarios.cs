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
using System.Diagnostics;

namespace ExifGlass.IpcHost;

/// <summary>
/// One scenario run: its folder, the host's log, the app's log and what it measured.
/// </summary>
public sealed class Run : IDisposable
{
    #region Properties

    public required string Dir { get; init; }

    public required HostLog HostLog { get; init; }

    public required AppLog App { get; init; }

    public Dictionary<string, object?> M { get; } = [];

    public string StderrPath => Path.Combine(Dir, "app.stderr.txt");

    #endregion // Properties


    #region Public methods

    public static Run Create(string outDir, string scenario, int n)
    {
        var dir = Path.Combine(outDir, scenario, $"run-{n:00}");
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        Directory.CreateDirectory(dir);
        return new Run { Dir = dir, HostLog = new HostLog(Path.Combine(dir, "host.jsonl")), App = new AppLog(Path.Combine(dir, "app.jsonl")) };
    }


    public void Dispose() => HostLog.Dispose();

    #endregion // Public methods
}


/// <summary>
/// The ImageGlass 10 scenarios (P10, from S6): rapid navigation, single steps, SHUTDOWN mid-read, the app killed, the host gone, a launcher in front, Unicode, repeated and long sessions, navigation while the app starts.
/// </summary>
public static class Scenarios
{
    #region Fields and constants

    public static readonly string[] Ig10All =
        ["rapid", "single-steps", "shutdown-mid-load", "shutdown-mid-long-load", "killed", "host-killed", "host-close", "via-launcher", "unicode", "repeat", "long", "early-burst", "events-before-init"];

    #endregion // Fields and constants


    #region Public methods

    public static async Task<Verdict> RunIg10Async(Settings s, string scenario)
    {
        var results = new List<Dictionary<string, object?>>();
        var runs = scenario switch { "repeat" => 10, "long" => 1, _ => s.Runs };
        for (var n = 1; n <= runs; n++)
        {
            using var run = Run.Create(s.OutDir, scenario, n);
            try
            {
                await (scenario switch
                {
                    "rapid" => Rapid(s, run),
                    "repeat" => Rapid(s, run, collect: true),
                    "single-steps" => SingleSteps(s, run),
                    "shutdown-mid-load" => ShutdownMidLoad(s, run, s.Fx.Slow),
                    "shutdown-mid-long-load" => ShutdownMidLoad(s, run, s.Fx.SlowXl),
                    "killed" => Killed(s, run),
                    "host-killed" => HostKilled(s, run),
                    "host-close" => HostClose(s, run),
                    "via-launcher" => ViaLauncher(s, run),
                    "unicode" => Unicode(s, run),
                    "long" => Long(s, run),
                    "early-burst" => EarlyBurst(s, run, eventsBeforeInit: false),
                    "events-before-init" => EarlyBurst(s, run, eventsBeforeInit: true),
                    _ => throw new ArgumentException($"unknown scenario {scenario}"),
                });
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                run.M["error"] = ex.GetType().Name + ": " + ex.Message;
            }

            Console.WriteLine($"  {scenario} run {n}: {string.Join(", ", run.M.Select(kv => $"{kv.Key}={Report.Fmt(kv.Value)}"))}");
            results.Add(run.M);
        }

        return Report.Write(s.OutDir, "ig10-" + scenario, results);
    }


    /// <summary>
    /// The "victim" host of host-killed: a separate process the scenario kills, so the pipe breaks without SHUTDOWN.
    /// </summary>
    public static async Task VictimAsync(Settings s)
    {
        var runDir = s.RunDir!;
        using var log = new HostLog(Path.Combine(runDir, "host.jsonl"));
        await using var host = new Ig10Host(log, s.PipeDir, s.BarePipe) { MetaDelayMs = s.MetaDelayMs };
        var app = new AppLog(Path.Combine(runDir, "app.jsonl"));
        var initial = s.Fx.Burst[0];
        if (!await host.LaunchAsync(s.Tool, initial, s.AppEnv(runDir))) return;

        host.SendInitAndExecute();
        await app.WaitForAsync("LOADED", e => e.Str("path") == initial, 15000);
        var t = Stopwatch.GetTimestamp();
        var last = initial;
        foreach (var p in s.Fx.Burst.Skip(1).Take(20))
        {
            host.PhotoChanged(p);
            last = p;
            await Task.Delay(s.IntervalMs);
        }

        await app.WaitForAsync("SETTLED", e => e.Str("path") == last, 10000, t);
        await File.WriteAllTextAsync(Path.Combine(runDir, "victim.ready"), string.Empty);
        await Task.Delay(Timeout.Infinite);
    }


    /// <summary>
    /// The app's ExifTool must be gone once the app is; waits up to 3 s (the macOS/Linux watchdog polls every second), then kills a leftover.
    /// </summary>
    public static async Task<string> OrphanCheck(Run run, DateTime appStart)
    {
        if (run.App.Last("EXIFTOOL_READY") is not { } ready) return "no-exiftool";

        var pid = (int)ready.Long("pid");
        var start = Stopwatch.GetTimestamp();
        while (HostLog.Ms(start, Stopwatch.GetTimestamp()) < 3000)
        {
            if (!Procs.IsAlive(pid, appStart)) return HostLog.Ms(start, Stopwatch.GetTimestamp()) < 60 ? "none" : $"none after {HostLog.Ms(start, Stopwatch.GetTimestamp()):0} ms";
            await Task.Delay(20);
        }

        Procs.TryKill(pid);
        return $"ORPHAN pid {pid}, killed";
    }


    public static double MB(long bytes) => Math.Round(bytes / 1048576.0, 1);

    #endregion // Public methods


    #region Private methods

    // 100 PHOTO_CHANGED in 2 s, settle on the last (an A1111 PNG, so the AI card follows), then SHUTDOWN
    private static async Task Rapid(Settings s, Run run, bool collect = false)
    {
        await using var host = NewHost(s, run);
        var initial = s.Fx.Unicode[1];
        if (!await host.LaunchAsync(s.Tool, initial, s.AppEnv(run.Dir, collect)))
        {
            await NotConnected(run, host);
            return;
        }

        host.SendInitAndExecute();
        await FirstLoad(run, host, initial);

        var t = Stopwatch.GetTimestamp();
        var last = await Burst(s, run, host, [.. s.Fx.Burst.Take(s.Count)]);
        await Settle(run, host, last, t);
        await Stop(run, host);
    }


    // one change at a time, a key press rather than a held key: change to loaded
    private static async Task SingleSteps(Settings s, Run run)
    {
        await using var host = NewHost(s, run);
        var initial = s.Fx.Unicode[1];
        if (!await host.LaunchAsync(s.Tool, initial, s.AppEnv(run.Dir)))
        {
            await NotConnected(run, host);
            return;
        }

        host.SendInitAndExecute();
        await FirstLoad(run, host, initial);
        await run.App.WaitForAsync("SETTLED", null, 5000);

        var latencies = new List<double>();
        foreach (var p in s.Fx.Burst.Skip(40).Take(10))
        {
            await Task.Delay(400);
            var t = Stopwatch.GetTimestamp();
            host.PhotoChanged(p);
            if (await run.App.WaitForAsync("LOADED", e => e.Str("path") == p, 5000, t) is { } loaded) latencies.Add(HostLog.Ms(host.LastSentTicks, loaded.T));
        }

        run.M["steps"] = latencies.Count;
        run.M["stepToLoadedMs"] = Stats.Median(latencies);
        run.M["stepToLoadedMaxMs"] = latencies.Count > 0 ? latencies.Max() : null;
        await Stop(run, host);
    }


    // SHUTDOWN while ExifTool reads a slow file: the exit fits ImageGlass's 3 s
    private static async Task ShutdownMidLoad(Settings s, Run run, string slow)
    {
        await using var host = NewHost(s, run);
        var initial = s.Fx.Burst[0];
        if (!await host.LaunchAsync(s.Tool, initial, s.AppEnv(run.Dir)))
        {
            await NotConnected(run, host);
            return;
        }

        host.SendInitAndExecute();
        await FirstLoad(run, host, initial);
        await run.App.WaitForAsync("SETTLED", null, 5000);

        var t = Stopwatch.GetTimestamp();
        host.PhotoChanged(slow);
        run.M["loadStarted"] = await run.App.WaitForAsync("LOAD_START", e => e.Str("path") == slow, 5000, t) is not null;
        await Task.Delay(30);

        await Stop(run, host);
        run.M["exiftoolStoppedMs"] = run.App.Last("EXIFTOOL_STOPPED")?.Dbl("ms");
    }


    // ImageGlass kills the app (its 3 s grace ran out, or anything else did): ExifTool must not outlive it (R11)
    private static async Task Killed(Settings s, Run run)
    {
        await using var host = NewHost(s, run);
        var initial = s.Fx.Burst[0];
        if (!await host.LaunchAsync(s.Tool, initial, s.AppEnv(run.Dir)))
        {
            await NotConnected(run, host);
            return;
        }

        host.SendInitAndExecute();
        await FirstLoad(run, host, initial);
        await run.App.WaitForAsync("SETTLED", null, 5000);

        // mid-read, as in S6's killed-by-host
        host.PhotoChanged(s.Fx.Slow);
        await run.App.WaitForAsync("LOAD_START", e => e.Str("path") == s.Fx.Slow, 5000);
        Procs.TryKill(host.Client!.Id);
        await host.Client.WaitForExitAsync();
        run.M["orphan"] = await OrphanCheck(run, host.ClientStarted);
    }


    // the host process dies: no SHUTDOWN, the pipe breaks; the window stays as a standalone viewer (D20)
    private static async Task HostKilled(Settings s, Run run)
    {
        var psi = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        if (Path.GetFileNameWithoutExtension(Environment.ProcessPath!) == "dotnet") psi.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "ExifGlass.IpcHost.dll"));
        foreach (var a in new[] { "victim", "--run-dir", run.Dir, "--app", s.AppExe, "--fixtures", s.Fx.Dir, "--out", s.OutDir }) psi.ArgumentList.Add(a);
        if (s.PipeDir is not null)
        {
            psi.ArgumentList.Add("--pipe-dir");
            psi.ArgumentList.Add(s.PipeDir);
        }

        using var victim = Process.Start(psi)!;
        var ready = Path.Combine(run.Dir, "victim.ready");
        for (var i = 0; i < 600 && !File.Exists(ready); i++) await Task.Delay(50);
        if (!File.Exists(ready))
        {
            run.M["error"] = "victim not ready";
            Procs.TryKill(victim.Id);
            return;
        }

        var appPid = (int)run.App.First("START")!.Long("pid");
        var appStart = Process.GetProcessById(appPid).StartTime;
        var t0 = Stopwatch.GetTimestamp();
        victim.Kill();
        await victim.WaitForExitAsync();

        await Standalone(run, t0, appPid, appStart);
    }


    // the host closes its end cleanly without SHUTDOWN
    private static async Task HostClose(Settings s, Run run)
    {
        await using var host = NewHost(s, run);
        var initial = s.Fx.Burst[0];
        if (!await host.LaunchAsync(s.Tool, initial, s.AppEnv(run.Dir)))
        {
            await NotConnected(run, host);
            return;
        }

        host.SendInitAndExecute();
        await FirstLoad(run, host, initial);
        var t = Stopwatch.GetTimestamp();
        var last = await Burst(s, run, host, [.. s.Fx.Burst.Skip(1).Take(20)]);
        await Settle(run, host, last, t);

        var t0 = Stopwatch.GetTimestamp();
        await host.ClosePipeAsync();
        await Standalone(run, t0, host.Client!.Id, host.ClientStarted);
    }


    // Windows: a launcher in front of the app fails ImageGlass's PID check (R21); the app, connected but dropped, stays standalone
    private static async Task ViaLauncher(Settings s, Run run)
    {
        if (!OperatingSystem.IsWindows())
        {
            run.M["skipped"] = "Windows only";
            return;
        }

        var launcher = new Settings
        {
            SourceDir = s.SourceDir,
            AppExe = s.AppExe,
            OutDir = s.OutDir,
            Fx = s.Fx,
            ToolExe = "cmd.exe",
            ToolArgs = $"/c \"{s.AppExe}\" <file>",
        };

        await using var host = NewHost(s, run);
        var connected = await host.LaunchAsync(launcher.Tool, s.Fx.Burst[0], s.AppEnv(run.Dir));
        run.M["pidVerified"] = host.ClientPidVerified;
        run.M["rejected"] = !connected;

        if (await run.App.WaitForAsync("START", null, 5000) is not { } start) return;

        var appPid = (int)start.Long("pid");
        var appStart = Process.GetProcessById(appPid).StartTime;
        await Standalone(run, host.LaunchTicks, appPid, appStart);
    }


    // Unicode paths through the INIT reply and PHOTO_CHANGED
    private static async Task Unicode(Settings s, Run run)
    {
        await using var host = NewHost(s, run);
        var u = s.Fx.Unicode;
        if (!await host.LaunchAsync(s.Tool, u[0], s.AppEnv(run.Dir)))
        {
            await NotConnected(run, host);
            return;
        }

        host.SendInitAndExecute();
        var ok = await run.App.WaitForAsync("LOADED", e => e.Str("path") == u[0], 15000) is null ? 0 : 1;
        foreach (var p in u.Skip(1))
        {
            var t = Stopwatch.GetTimestamp();
            host.PhotoChanged(p);
            if (await run.App.WaitForAsync("LOADED", e => e.Str("path") == p, 5000, t) is not null) ok++;
        }

        run.M["loaded"] = $"{ok}/{u.Length}";
        run.M["failed"] = run.App.Count("LOAD_FAILED");
        await Stop(run, host);
    }


    // one session, 20 bursts: live memory after each settle
    private static async Task Long(Settings s, Run run)
    {
        await using var host = NewHost(s, run);
        var initial = s.Fx.Unicode[1];
        if (!await host.LaunchAsync(s.Tool, initial, s.AppEnv(run.Dir, collect: true)))
        {
            await NotConnected(run, host);
            return;
        }

        host.SendInitAndExecute();
        await FirstLoad(run, host, initial);

        var memory = new List<string>();
        for (var b = 0; b < 20; b++)
        {
            // every burst ends on a file that isn't on screen
            var files = b % 2 == 0 ? s.Fx.Burst.Take(s.Count).ToArray() : s.Fx.Burst.Take(s.Count).Reverse().ToArray();
            var t = Stopwatch.GetTimestamp();
            var last = await Burst(s, run, host, files, record: false);
            if (await run.App.WaitForAsync("SETTLED", e => e.Str("path") == last, 15000, t) is not { } settled)
            {
                run.M["error"] = $"burst {b} did not settle";
                break;
            }

            memory.Add($"{b}: ws {MB(settled.Long("ws"))} MB, heap {MB(settled.Long("heap"))} MB");
            if (b == 0)
            {
                run.M["ws1"] = MB(settled.Long("ws"));
                run.M["heap1"] = MB(settled.Long("heap"));
            }

            if (b == 19)
            {
                run.M["ws20"] = MB(settled.Long("ws"));
                run.M["heap20"] = MB(settled.Long("heap"));
            }
        }

        // the shared ArrayPool keeps big read buffers until they go unused for about 60 s; a gen-2 GC after that trims them
        await Task.Delay(65_000);
        var small = s.Fx.Unicode[1];
        var idle = Stopwatch.GetTimestamp();
        host.PhotoChanged(small);
        if (await run.App.WaitForAsync("SETTLED", e => e.Str("path") == small, 15000, idle) is { } trimmed)
        {
            memory.Add($"after 65 s idle: ws {MB(trimmed.Long("ws"))} MB, heap {MB(trimmed.Long("heap"))} MB");
            run.M["wsIdle"] = MB(trimmed.Long("ws"));
            run.M["heapIdle"] = MB(trimmed.Long("heap"));
        }

        run.M["reads"] = run.App.Count("LOAD_START");
        run.M["changes"] = run.App.Count("PHOTO_CHANGED");
        await File.WriteAllLinesAsync(Path.Combine(run.Dir, "memory.txt"), memory);
        await Stop(run, host);
    }


    // navigation goes on while the app starts: the photo moves before it connects, then events stream in
    private static async Task EarlyBurst(Settings s, Run run, bool eventsBeforeInit)
    {
        await using var host = new Ig10Host(run.HostLog, s.PipeDir, s.BarePipe) { MetaDelayMs = 30, StderrPath = run.StderrPath };
        var files = s.Fx.Burst.Take(s.Count).ToArray();
        var initSent = 0;

        var launch = host.LaunchAsync(s.Tool, files[0], s.AppEnv(run.Dir));
        var advance = Task.Run(async () =>
        {
            var start = Stopwatch.GetTimestamp();
            for (var i = 0; i < files.Length; i++)
            {
                var target = start + ((long)i * s.IntervalMs * Stopwatch.Frequency / 1000);
                while (Stopwatch.GetTimestamp() < target) await Task.Delay(1);
                if (Volatile.Read(ref initSent) == 1) host.PhotoChanged(files[i]);
                else host.CurrentPath = files[i];
            }
        });

        if (!await launch)
        {
            await NotConnected(run, host);
            return;
        }

        // ImageGlass registers the process before it sends INIT, so a broadcast can slip in first
        if (eventsBeforeInit)
        {
            foreach (var p in files.Take(3)) host.Send(ImageGlass.SDK.Tools.MessageTypes.PHOTO_CHANGED, new ImageGlass.SDK.Tools.PhotoChangedEventArgs { FilePath = p });
        }

        host.SendInitAndExecute();
        Volatile.Write(ref initSent, 1);
        await advance;
        await Task.Delay(100);

        run.M["connectMs"] = HostLog.Ms(host.LaunchTicks, host.ConnectedTicks);
        await Settle(run, host, files[^1], host.LaunchTicks);
        run.M["staleReplies"] = run.App.Count("STALE_REPLY");
        await Stop(run, host);
    }


    public static string HostLogText(Run run)
    {
        using var fs = new FileStream(Path.Combine(run.Dir, "host.jsonl"), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return new StreamReader(fs).ReadToEnd();
    }


    private static Ig10Host NewHost(Settings s, Run run) => new(run.HostLog, s.PipeDir, s.BarePipe) { MetaDelayMs = s.MetaDelayMs, StderrPath = run.StderrPath };


    private static async Task NotConnected(Run run, Ig10Host host)
    {
        run.M["error"] = HostLogText(run).Contains("PID_REJECTED", StringComparison.Ordinal) ? "pid rejected" : "connect timeout";
        await Task.Delay(1500);
        run.M["appRunFailed"] = run.App.Last("RUN_FAILED")?.Str("error");
        if (run.App.First("START") is { } start && Procs.IsAlive((int)start.Long("pid"), host.ClientStarted)) await Procs.QuitAsync((int)start.Long("pid"));
    }


    private static async Task FirstLoad(Run run, Ig10Host host, string initial)
    {
        var loaded = await run.App.WaitForAsync("LOADED", e => e.Str("path") == initial, 15000);
        var start = run.App.First("START");
        run.M["startMs"] = start is null ? null : HostLog.Ms(host.LaunchTicks, start.T);
        run.M["connectMs"] = HostLog.Ms(host.LaunchTicks, host.ConnectedTicks);
        run.M["firstLoadedMs"] = loaded is null ? null : HostLog.Ms(host.LaunchTicks, loaded.T);
        run.M["warmMs"] = run.App.First("EXIFTOOL_READY")?.Dbl("warmMs");
        run.M["pidVerified"] = host.ClientPidVerified;
    }


    private static async Task<string> Burst(Settings s, Run run, Ig10Host host, string[] files, bool record = true)
    {
        var start = Stopwatch.GetTimestamp();
        await Task.Run(() =>
        {
            for (var i = 0; i < files.Length; i++)
            {
                var target = start + ((long)i * s.IntervalMs * Stopwatch.Frequency / 1000);
                while (Stopwatch.GetTimestamp() is var now && now < target)
                {
                    if (target - now > Stopwatch.Frequency / 500) Thread.Sleep(1);
                    else Thread.SpinWait(100);
                }

                host.PhotoChanged(files[i]);
            }
        });

        if (record) run.M["burstMs"] = HostLog.Ms(start, Stopwatch.GetTimestamp());
        return files[^1];
    }


    private static async Task Settle(Run run, Ig10Host host, string last, long t)
    {
        var loaded = await run.App.WaitForAsync("LOADED", e => e.Str("path") == last, 15000, t);
        var settled = await run.App.WaitForAsync("SETTLED", e => e.Str("path") == last, 3000, loaded?.T ?? t);
        await Task.Delay(50);

        var lastSent = host.LastSentTicks;
        run.M["settledOnLast"] = settled is not null && run.App.Last("LOADED")?.Str("path") == last;
        run.M["lastChangeToLoadedMs"] = loaded is null ? null : HostLog.Ms(lastSent, loaded.T);
        run.M["reads"] = run.App.Count("LOAD_START", t);
        run.M["shownDuringBurst"] = run.App.Events.Count(e => e.E == "LOADED" && e.T > t && e.T < lastSent);
        if (settled is null) return;

        run.M["aiOnLast"] = settled.Bool("ai");
        run.M["wsMB"] = MB(settled.Long("ws"));
        run.M["heapMB"] = MB(settled.Long("heap"));
    }


    private static async Task Stop(Run run, Ig10Host host)
    {
        var appStart = host.ClientStarted;
        var (ms, killed) = await host.StopToolAsync();
        run.M["shutdownToExitMs"] = ms;
        run.M["killed"] = killed;
        run.M["exitCode"] = host.Client!.ExitCode;
        run.M["exitLogged"] = run.App.Last("EXIT") is not null;
        run.M["orphan"] = await OrphanCheck(run, appStart);
    }


    // D20: still running 2 s after the host went, then closed as a user would
    private static async Task Standalone(Run run, long t0, int appPid, DateTime appStart)
    {
        var standalone = await run.App.WaitForAsync("STANDALONE", null, 5000, t0);
        run.M["standaloneMs"] = standalone is null ? null : HostLog.Ms(t0, standalone.T);
        run.M["endReason"] = run.App.Last("SESSION_END")?.Str("reason");
        await Task.Delay(2000);
        run.M["aliveAfter2s"] = Procs.IsAlive(appPid, appStart);

        var t1 = Stopwatch.GetTimestamp();
        run.M["quit"] = await Procs.QuitAsync(appPid) ? $"{HostLog.Ms(t1, Stopwatch.GetTimestamp()):0} ms" : "timeout, killed";
        run.M["orphan"] = await OrphanCheck(run, appStart);
    }

    #endregion // Private methods
}
