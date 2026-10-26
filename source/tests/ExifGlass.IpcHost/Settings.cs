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
using System.Runtime.InteropServices;

namespace ExifGlass.IpcHost;

/// <summary>
/// The run's options: which ExifGlass, where the results go, the fixtures, the timings of a burst.
/// </summary>
public sealed class Settings
{
    #region Fields and constants

    // read by the app: its JSON-lines log and its own settings folder, so a run never touches the user's
    public const string IPC_LOG = "EG_IPC_LOG";
    public const string SETTINGS_DIR = "EG_SETTINGS_DIR";
    public const string GC_ON_SETTLE = "EG_IPC_GC";

    #endregion // Fields and constants


    #region Properties

    public required string SourceDir { get; init; }

    public required string AppExe { get; init; }

    public required string OutDir { get; init; }

    public required Fixtures Fx { get; init; }

    public int Runs { get; set; } = 3;

    public int IntervalMs { get; init; } = 20;

    public int Count { get; init; } = 100;

    public int MetaDelayMs { get; init; } = 5;

    public string? PipeDir { get; init; }

    public bool BarePipe { get; init; }

    /// <summary>
    /// The folder of the run a "victim" host process serves (host-killed).
    /// </summary>
    public string? RunDir { get; init; }

    public string? ToolExe { get; init; }

    public string ToolArgs { get; init; } = "<file>";

    public ToolSpec Tool => new(ToolExe ?? AppExe, ToolArgs);

    #endregion // Properties


    #region Public methods

    public static Settings Parse(string[] args, out string[] rest)
    {
        var source = FindSourceDir();
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var positional = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a == "--bare-pipe")
            {
                flags.Add(a);
                continue;
            }

            if (a.StartsWith("--", StringComparison.Ordinal) && i + 1 < args.Length)
            {
                map[a[2..]] = args[++i];
                continue;
            }

            positional.Add(a);
        }

        rest = [.. positional];
        string? Get(string k) => map.TryGetValue(k, out var v) ? v : null;
        int? Int(string k) => int.TryParse(Get(k), out var v) ? v : null;

        var os = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "mac" : "linux";
        var exe = OperatingSystem.IsWindows() ? "ExifGlass.exe" : "ExifGlass";
        var artifacts = Path.Combine(source, "__artifacts");

        return new Settings
        {
            SourceDir = source,
            AppExe = Path.GetFullPath(Get("app") ?? Path.Combine(artifacts, "publish", RuntimeInformation.RuntimeIdentifier, exe)),
            OutDir = Path.GetFullPath(Get("out") ?? Path.Combine(artifacts, "ipc", os)),
            Fx = Fixtures.Ensure(Get("fixtures") ?? Path.Combine(artifacts, "ipc", "fixtures"), Path.Combine(source, "tests", "fixtures", "ai")),
            Runs = Int("runs") ?? 3,
            IntervalMs = Int("interval") ?? 20,
            Count = Int("count") ?? 100,
            MetaDelayMs = Int("meta-delay") ?? 5,
            PipeDir = Get("pipe-dir"),
            BarePipe = flags.Contains("--bare-pipe"),
            RunDir = Get("run-dir"),
            ToolExe = Get("tool-exe"),
            ToolArgs = Get("tool-args") ?? "<file>",
        };
    }


    /// <summary>
    /// <paramref name="collect"/>: the app runs a full GC before each SETTLED sample, for the memory scenarios.
    /// </summary>
    public Dictionary<string, string> AppEnv(string runDir, bool collect = false) => new()
    {
        [IPC_LOG] = Path.Combine(runDir, "app.jsonl"),
        [SETTINGS_DIR] = Path.Combine(runDir, "settings"),
        [GC_ON_SETTLE] = collect ? "1" : "0",
    };

    #endregion // Public methods


    #region Private methods

    private static string FindSourceDir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "global.json")) && Directory.Exists(Path.Combine(dir.FullName, "tests"))) return dir.FullName;
        }

        return Directory.GetCurrentDirectory();
    }

    #endregion // Private methods
}
