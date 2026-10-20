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
using System.Text.RegularExpressions;

namespace ExifGlass.Core.Editing;

/// <summary>
/// How a save ended.
/// </summary>
public enum SaveOutcome
{
    Updated,
    Unchanged,
    Failed,
}


/// <summary>
/// ExifTool's summary of one write: the outcome, its errors and warnings, the backup it kept.
/// </summary>
public sealed partial record SaveResult(SaveOutcome Outcome, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings, string? BackupPath)
{
    #region Public methods

    /// <summary>
    /// Reads "1 image files updated" / "unchanged" from stdout and "Error:" / "Warning:" lines from stderr.
    /// </summary>
    public static SaveResult Parse(string stdout, string stderr, int? status, string sourcePath)
    {
        var updated = Count(UpdatedLine(), stdout);
        var unchanged = Count(UnchangedLine(), stdout);
        var errors = new List<string>();
        var warnings = new List<string>();

        foreach (var raw in stderr.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("Error:", StringComparison.OrdinalIgnoreCase)) errors.Add(Clean(line[6..], sourcePath));
            else if (line.StartsWith("Warning:", StringComparison.OrdinalIgnoreCase)) warnings.Add(Clean(line[8..], sourcePath));
        }

        // ExifTool can also print warnings on stdout, after the summary
        foreach (var raw in stdout.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith("Warning:", StringComparison.OrdinalIgnoreCase)) warnings.Add(Clean(line[8..], sourcePath));
        }

        var outcome = updated > 0 ? SaveOutcome.Updated : unchanged > 0 && errors.Count == 0 ? SaveOutcome.Unchanged : SaveOutcome.Failed;
        if (outcome == SaveOutcome.Failed && errors.Count == 0) errors.Add(status is { } s and not 0 ? $"ExifTool stopped with status {s}" : "ExifTool didn't update the file");

        var backup = sourcePath + "_original";
        return new SaveResult(outcome, errors, [.. warnings.Distinct()], outcome == SaveOutcome.Updated && File.Exists(backup) ? backup : null);
    }

    #endregion // Public methods


    #region Private methods

    [GeneratedRegex(@"(\d+) image files? updated", RegexOptions.CultureInvariant)]
    private static partial Regex UpdatedLine();


    [GeneratedRegex(@"(\d+) image files? unchanged", RegexOptions.CultureInvariant)]
    private static partial Regex UnchangedLine();


    private static int Count(Regex regex, string text) => regex.Match(text) is { Success: true } m ? int.Parse(m.Groups[1].ValueSpan, System.Globalization.CultureInfo.InvariantCulture) : 0;


    // "File not found - C:/Users/.../a.jpg" -> "File not found": the toast already names the file
    private static string Clean(string message, string sourcePath)
    {
        var m = message.Trim();
        var full = Path.GetFullPath(sourcePath);
        return m.Replace(" - " + full.Replace('\\', '/'), string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" - " + full, string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
    }

    #endregion // Private methods
}
