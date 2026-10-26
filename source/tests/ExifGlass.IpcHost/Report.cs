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
using System.Globalization;
using System.Text;

namespace ExifGlass.IpcHost;

/// <summary>
/// A scenario's outcome: PASS when every run met the checks its measurements call for.
/// </summary>
public sealed record Verdict(string Name, bool Pass, IReadOnlyList<string> Problems);


/// <summary>
/// One markdown table per scenario (runs, then medians) and its verdict.
/// </summary>
public static class Report
{
    #region Public methods

    public static Verdict Write(string outDir, string name, List<Dictionary<string, object?>> runs)
    {
        var keys = runs.SelectMany(r => r.Keys).Distinct().ToList();
        var verdict = Judge(name, runs);

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"# {name} ({runs.Count} runs, {DateTime.Now:yyyy-MM-dd HH:mm}): {(verdict.Pass ? "PASS" : "FAIL")}");
        foreach (var problem in verdict.Problems) sb.AppendLine(CultureInfo.InvariantCulture, $"- {problem}");
        sb.AppendLine();
        sb.AppendLine("| run | " + string.Join(" | ", keys) + " |");
        sb.AppendLine("|---|" + string.Concat(keys.Select(_ => "---|")));
        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            sb.AppendLine(CultureInfo.InvariantCulture, $"| {i + 1} | {string.Join(" | ", keys.Select(k => Fmt(run.GetValueOrDefault(k))))} |");
        }

        sb.AppendLine("| **median** | " + string.Join(" | ", keys.Select(k => Summary([.. runs.Select(r => r.GetValueOrDefault(k))]))) + " |");

        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, name + ".md"), sb.ToString());
        return verdict;
    }


    public static string Fmt(object? value) => value switch
    {
        null => "-",
        double d => d.ToString("0.#", CultureInfo.InvariantCulture),
        bool b => b ? "yes" : "no",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    #endregion // Public methods


    #region Private methods

    private static Verdict Judge(string name, List<Dictionary<string, object?>> runs)
    {
        var problems = new List<string>();
        for (var i = 0; i < runs.Count; i++)
        {
            var m = runs[i];
            var run = $"run {i + 1}";
            if (m.GetValueOrDefault("error") is { } error) problems.Add($"{run}: {error}");
            if (m.GetValueOrDefault("settledOnLast") is false) problems.Add($"{run}: didn't settle on the last photo");
            if (m.GetValueOrDefault("aiOnLast") is false) problems.Add($"{run}: the last photo's AI data didn't show");
            if (m.GetValueOrDefault("orphan") is string orphan && !orphan.StartsWith("none", StringComparison.Ordinal)) problems.Add($"{run}: ExifTool {orphan}");
            if (m.GetValueOrDefault("killed") is true) problems.Add($"{run}: killed by the host");
            if (m.GetValueOrDefault("shutdownToExitMs") is double exit && exit >= 3000) problems.Add($"{run}: exit took {exit:0} ms");
            if (m.GetValueOrDefault("exitLogged") is false) problems.Add($"{run}: no clean exit");
            if (m.GetValueOrDefault("aliveAfter2s") is false) problems.Add($"{run}: the window didn't stay as a standalone viewer");
            if (m.GetValueOrDefault("loaded") is string loaded && loaded.Split('/') is [var a, var b] && a != b) problems.Add($"{run}: loaded {loaded}");
            if (m.GetValueOrDefault("steps") is int steps && steps != 10) problems.Add($"{run}: {steps} of 10 steps loaded");

            // plan 8: repeated loads may add 5 MB of live heap per 100
            if (m.GetValueOrDefault("heap1") is double heap1 && m.GetValueOrDefault("heap20") is double heap20 && heap20 - heap1 > 5) problems.Add($"{run}: live heap grew {heap20 - heap1:0.0} MB over 20 bursts");
            if (name.EndsWith("host-close", StringComparison.Ordinal) && name.StartsWith("ig9", StringComparison.Ordinal) && m.GetValueOrDefault("closing") is not "pipe-closed") problems.Add($"{run}: didn't close with the pipe");
        }

        return new Verdict(name, problems.Count == 0, problems);
    }


    private static string Summary(List<object?> values)
    {
        var numbers = values.OfType<double>().Concat(values.OfType<long>().Select(l => (double)l)).Concat(values.OfType<int>().Select(i => (double)i)).ToList();
        if (numbers.Count == values.Count && numbers.Count > 0) return Stats.Range(numbers);

        var bools = values.OfType<bool>().ToList();
        if (bools.Count == values.Count && bools.Count > 0) return $"{bools.Count(b => b)}/{bools.Count} yes";

        var distinct = values.Select(Fmt).Distinct().ToList();
        return distinct.Count == 1 ? distinct[0] : string.Join(" / ", values.Select(Fmt).GroupBy(v => v).Select(g => $"{g.Key} x{g.Count()}"));
    }

    #endregion // Private methods
}
