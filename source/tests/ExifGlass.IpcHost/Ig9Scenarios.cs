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
#if WINDOWS
using System.Diagnostics;

namespace ExifGlass.IpcHost;

/// <summary>
/// The ImageGlass 9 scenarios (Windows): rapid navigation, TOOL_TERMINATE mid-read, the pipe dropped as ImageGlass 9 exits, Unicode.
/// </summary>
public static class Ig9Scenarios
{
    #region Fields and constants

    public static readonly string[] All = ["rapid", "terminate-mid-load", "host-close", "unicode"];

    #endregion // Fields and constants


    #region Public methods

    public static async Task<Verdict> RunAsync(Settings s, string scenario)
    {
        var results = new List<Dictionary<string, object?>>();
        for (var n = 1; n <= s.Runs; n++)
        {
            using var run = Run.Create(s.OutDir, "ig9-" + scenario, n);
            try
            {
                await (scenario switch
                {
                    "rapid" => Rapid(s, run),
                    "terminate-mid-load" => TerminateMidLoad(s, run),
                    "host-close" => HostClose(s, run),
                    "unicode" => Unicode(s, run),
                    _ => throw new ArgumentException($"unknown scenario {scenario}"),
                });
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                run.M["error"] = ex.GetType().Name + ": " + ex.Message;
            }

            Console.WriteLine($"  ig9-{scenario} run {n}: {string.Join(", ", run.M.Select(kv => $"{kv.Key}={Report.Fmt(kv.Value)}"))}");
            results.Add(run.M);
        }

        return Report.Write(s.OutDir, "ig9-" + scenario, results);
    }

    #endregion // Public methods


    #region Private methods

    private static async Task<bool> Launch(Settings s, Run run, Ig9Host host, string file)
    {
        var t = Stopwatch.GetTimestamp();
        if (!await host.LaunchAsync(s.AppExe, file, s.AppEnv(run.Dir)))
        {
            run.M["error"] = "not connected";
            return false;
        }

        var loaded = await run.App.WaitForAsync("LOADED", e => e.Str("path") == file, 15000);
        run.M["firstLoadedMs"] = loaded is null ? null : HostLog.Ms(t, loaded.T);
        return true;
    }


    private static async Task Rapid(Settings s, Run run)
    {
        using var host = new Ig9Host(run.HostLog);
        if (!await Launch(s, run, host, s.Fx.Unicode[1])) return;
        await run.App.WaitForAsync("SETTLED", null, 5000);

        var files = s.Fx.Burst.Take(s.Count).ToArray();
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

                host.ImageLoading(i, files[i]);
            }
        });

        var last = files[^1];
        var loaded = await run.App.WaitForAsync("LOADED", e => e.Str("path") == last, 15000, start);
        var settled = await run.App.WaitForAsync("SETTLED", e => e.Str("path") == last, 3000, loaded?.T ?? start);
        run.M["settledOnLast"] = settled is not null && run.App.Last("LOADED")?.Str("path") == last;
        run.M["lastChangeToLoadedMs"] = loaded is null ? null : HostLog.Ms(host.LastSentTicks, loaded.T);
        run.M["received"] = run.App.Count("IMAGE_LOADING");
        run.M["reads"] = run.App.Count("LOAD_START", start);
        run.M["aiOnLast"] = settled?.Bool("ai");
        await Terminate(run, host);
    }


    private static async Task TerminateMidLoad(Settings s, Run run)
    {
        using var host = new Ig9Host(run.HostLog);
        if (!await Launch(s, run, host, s.Fx.Burst[0])) return;
        await run.App.WaitForAsync("SETTLED", null, 5000);

        var t = Stopwatch.GetTimestamp();
        host.ImageLoading(1, s.Fx.Slow);
        run.M["loadStarted"] = await run.App.WaitForAsync("LOAD_START", e => e.Str("path") == s.Fx.Slow, 5000, t) is not null;
        await Terminate(run, host);
    }


    // ImageGlass 9 sends no TOOL_TERMINATE when it exits: its pipe just closes, and the app closes too
    private static async Task HostClose(Settings s, Run run)
    {
        using var host = new Ig9Host(run.HostLog);
        if (!await Launch(s, run, host, s.Fx.Burst[0])) return;
        await run.App.WaitForAsync("SETTLED", null, 5000);

        var start = Stopwatch.GetTimestamp();
        host.Server.Stop();
        host.Server.Dispose();
        run.M["closing"] = (await run.App.WaitForAsync("IG9_CLOSING", null, 5000, start))?.Str("reason");

        var (ms, killed) = await host.WaitExitAsync(start, 8000);
        run.M["closeToExitMs"] = ms;
        run.M["killed"] = killed;
        run.M["exitLogged"] = run.App.Last("EXIT") is not null;
        run.M["orphan"] = await Scenarios.OrphanCheck(run, host.ClientStarted);
    }


    private static async Task Unicode(Settings s, Run run)
    {
        using var host = new Ig9Host(run.HostLog);
        var u = s.Fx.Unicode;
        if (!await Launch(s, run, host, u[0])) return;

        var ok = 1;
        for (var i = 1; i < u.Length; i++)
        {
            var t = Stopwatch.GetTimestamp();
            host.ImageLoading(i, u[i]);
            if (await run.App.WaitForAsync("LOADED", e => e.Str("path") == u[i], 5000, t) is not null) ok++;
        }

        run.M["loaded"] = $"{ok}/{u.Length}";
        run.M["argvIntact"] = run.App.First("START")?.Str("args")?.Contains(u[0], StringComparison.Ordinal) == true;
        await Terminate(run, host);
    }


    private static async Task Terminate(Run run, Ig9Host host)
    {
        var (ms, killed) = await host.TerminateAsync();
        run.M["terminateToExitMs"] = ms;
        run.M["killed"] = killed;
        run.M["closing"] = run.App.Last("IG9_CLOSING")?.Str("reason");
        run.M["exitLogged"] = run.App.Last("EXIT") is not null;
        run.M["orphan"] = await Scenarios.OrphanCheck(run, host.ClientStarted);
    }

    #endregion // Private methods
}
#endif
