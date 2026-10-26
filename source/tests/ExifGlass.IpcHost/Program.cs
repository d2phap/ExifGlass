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
using System.Text;

namespace ExifGlass.IpcHost;

/// <summary>
/// Plays ImageGlass 10 (ImageGlass.SDK) and ImageGlass 9 (ImageGlass.Tools, Windows) against a built ExifGlass, as S6's mock hosts did against its stand-in (P10).
/// </summary>
internal static class Program
{
    #region Entry point

    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            Console.WriteLine("""
                ExifGlass.IpcHost <ig10|ig9|all> [scenario|all] [options]

                  ig10 scenarios: rapid single-steps shutdown-mid-load shutdown-mid-long-load killed host-killed host-close via-launcher unicode repeat long early-burst events-before-init
                  ig9 scenarios (Windows): rapid terminate-mid-load host-close unicode

                  --app PATH       ExifGlass to test (default: __artifacts/publish/<rid>/)
                  --out DIR        results (default: __artifacts/ipc/<os>)
                  --runs N         runs per scenario (default 3)
                  --fixtures DIR   --interval MS   --count N   --pipe-dir DIR   --bare-pipe
                """);
            return 0;
        }

        Console.OutputEncoding = new UTF8Encoding(false);
        var command = args[0];
        var settings = Settings.Parse(args[1..], out var rest);
        var scenario = rest.FirstOrDefault() ?? "all";

        if (command == "victim")
        {
            await Scenarios.VictimAsync(settings);
            return 0;
        }

        if (!File.Exists(settings.AppExe))
        {
            Console.Error.WriteLine($"ExifGlass not found: {settings.AppExe}");
            return 1;
        }

        Console.WriteLine($"app: {settings.AppExe}");
        Console.WriteLine($"out: {settings.OutDir}");

        var verdicts = new List<Verdict>();
        if (command is "ig10" or "all")
        {
            foreach (var name in scenario == "all" || command == "all" ? Scenarios.Ig10All : [scenario])
            {
                Console.WriteLine($"ig10 {name}");
                verdicts.Add(await Scenarios.RunIg10Async(settings, name));
            }
        }

#if WINDOWS
        if (command is "ig9" or "all")
        {
            foreach (var name in scenario == "all" || command == "all" ? Ig9Scenarios.All : [scenario])
            {
                Console.WriteLine($"ig9 {name}");
                verdicts.Add(await Ig9Scenarios.RunAsync(settings, name));
            }
        }
#endif

        var summary = new StringBuilder("# IPC scenarios\n\n");
        foreach (var v in verdicts)
        {
            summary.Append(v.Pass ? "PASS " : "FAIL ").AppendLine(v.Name);
            foreach (var p in v.Problems) summary.Append("     ").AppendLine(p);
        }

        File.WriteAllText(Path.Combine(settings.OutDir, "summary.md"), summary.ToString());
        Console.WriteLine();
        Console.Write(summary);
        return verdicts.All(v => v.Pass) ? 0 : 1;
    }

    #endregion // Entry point
}
