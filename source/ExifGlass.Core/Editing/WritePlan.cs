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
using System.Text;

namespace ExifGlass.Core.Editing;

/// <summary>
/// What a command-bar token is, for its colour.
/// </summary>
public enum CommandTokenKind
{
    Executable,
    Flag,
    Value,
    Write,
    Path,
}


/// <summary>
/// One token of the command bar.
/// </summary>
public sealed record CommandToken(string Text, CommandTokenKind Kind);


/// <summary>
/// The ExifTool arguments of one save, the temp files they read, and the command bar's tokens.
/// </summary>
public sealed class WritePlan : IDisposable
{
    #region Fields and constants

    private readonly List<string> _tempFiles = [];
    private readonly List<string> _arguments = [];
    private readonly List<string> _warnings = [];

    #endregion // Fields and constants


    #region Constructors

    private WritePlan(string path)
    {
        SourcePath = path;
    }

    #endregion // Constructors


    #region Properties

    public string SourcePath { get; }

    /// <summary>
    /// The write arguments, without the file (ExifToolCommands.Write adds it).
    /// </summary>
    public IReadOnlyList<string> Arguments => _arguments;

    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>
    /// The backup ExifTool keeps next to the file: "DSCF4127.JPG_original".
    /// </summary>
    public string BackupName => Path.GetFileName(SourcePath) + "_original";

    public bool IsEmpty => _arguments.Count == 0;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Builds the arguments for the staged edits: bulk deletes first, then edits, deletes and added tags.
    /// </summary>
    public static WritePlan Build(string path, EditSet edits, AiRedactionPlan? redaction = null, string? tempDirectory = null)
    {
        var plan = new WritePlan(path);
        var args = plan._arguments;

        if (edits.StripAll) args.Add("-all=");
        if (edits.RemoveGps) args.Add("-gps:all=");
        if (edits.RemoveAi && redaction is not null)
        {
            args.AddRange(redaction.Arguments);
            plan._warnings.AddRange(redaction.Warnings);
        }

        foreach (var edit in edits.Edits.Values.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            var target = "-" + edit.WriteName + (edit.IsRaw ? "#" : string.Empty);
            if (edit.IsDelete) args.Add(target + "=");
            else plan.AddAssignment(target, edit.Value!, tempDirectory);
        }

        var writesIptc = false;
        foreach (var added in edits.Added)
        {
            plan.AddAssignment($"-{added.Group}:{added.Name}", added.Value, tempDirectory);
            writesIptc |= added.Group.Equals("IPTC", StringComparison.OrdinalIgnoreCase);
        }

        writesIptc |= edits.Edits.Values.Any(e => e.WriteName.StartsWith("IPTC:", StringComparison.OrdinalIgnoreCase));

        // without it IPTC stores non-Latin text as "?" (S5)
        if (writesIptc) args.Add("-codedcharacterset=utf8");

        return plan;
    }


    /// <summary>
    /// The command bar for a write: exiftool, the arguments, the file.
    /// </summary>
    public IReadOnlyList<CommandToken> Tokens(string executable) =>
    [
        new(Quote(executable), CommandTokenKind.Executable),
        .. _arguments.Select(a => new CommandToken(Quote(a), a.Contains('=', StringComparison.Ordinal) ? CommandTokenKind.Write : CommandTokenKind.Flag)),
        new(Quote(SourcePath), CommandTokenKind.Path),
    ];


    /// <summary>
    /// The terminal-equivalent command for a read (the command bar while no edit is staged).
    /// </summary>
    public static IReadOnlyList<CommandToken> ReadTokens(string executable, string? configPath, IReadOnlyList<string> arguments, string? path)
    {
        var tokens = new List<CommandToken> { new(Quote(executable), CommandTokenKind.Executable) };

        // the terminal must load the same config, or animated PNG graphs read differently (S9)
        if (configPath is not null)
        {
            tokens.Add(new("-config", CommandTokenKind.Flag));
            tokens.Add(new(Quote(configPath), CommandTokenKind.Value));
        }

        foreach (var a in arguments)
        {
            tokens.Add(new(Quote(a), a.StartsWith('-') ? CommandTokenKind.Flag : CommandTokenKind.Value));
        }

        tokens.Add(new(path is null ? "<file>" : Quote(path), CommandTokenKind.Path));
        return tokens;
    }


    /// <summary>
    /// Tokens joined as one line to paste into a terminal.
    /// </summary>
    public static string ToCommandLine(IEnumerable<CommandToken> tokens) => string.Join(' ', tokens.Select(t => t.Text));


    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // the OS cleans its temp folder
            }
        }

        _tempFiles.Clear();
    }

    #endregion // Public methods


    #region Private methods

    // a value that can't travel on one argument line (newlines with $ or @, edge spaces) is written from a file (S5)
    private void AddAssignment(string target, string value, string? tempDirectory)
    {
        var arg = target + "=" + value;
        if (ArgFile.IsPlainSafe(arg) || (ArgFile.CanEncode(arg) && !HasEdgeSpace(value)))
        {
            _arguments.Add(arg);
            return;
        }

        var dir = tempDirectory ?? Path.GetTempPath();
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"exifglass-value-{Guid.NewGuid():N}.txt");
        File.WriteAllText(file, value, new UTF8Encoding(false));
        _tempFiles.Add(file);
        _arguments.Add(target + "<=" + file);
    }


    private static bool HasEdgeSpace(string value) => value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]));


    private static string Quote(string s) => s.Length == 0 || s.AsSpan().IndexOfAny(" \t\"'") >= 0 ? "\"" + s.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"" : s;

    #endregion // Private methods
}
