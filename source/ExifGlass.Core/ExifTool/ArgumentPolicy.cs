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
using System.Collections.Frozen;
using System.Text;
using System.Text.RegularExpressions;

namespace ExifGlass.Core.ExifTool;

/// <summary>
/// What the app does with one argument of the Settings argument field (D1).
/// </summary>
public enum ArgumentClass
{
    /// <summary>
    /// Output shape the app owns (-G, -t, -H): always on, shown as a locked chip, never sent.
    /// </summary>
    Locked,

    /// <summary>
    /// A display option the table applies itself (-s, -n, -a): never sent.
    /// </summary>
    Display,

    /// <summary>
    /// Sent to ExifTool with the table read (never with the AI read).
    /// </summary>
    PassThrough,

    /// <summary>
    /// Refused with a reason: it writes files, changes the output format or the daemon protocol.
    /// </summary>
    Refused,
}


/// <summary>
/// One chip of the argument field: a flag with its value, if it takes one.
/// </summary>
public sealed record ArgumentToken(string Flag, string? Value, ArgumentClass Class, string Description, string? Reason = null)
{
    #region Properties

    public string Text => Value is null ? Flag : Flag + " " + Value;

    #endregion // Properties
}


/// <summary>
/// The argument field after the policy: chips, what to send, the display toggles, warnings.
/// </summary>
public sealed record ArgumentAnalysis(
    IReadOnlyList<ArgumentToken> Tokens,
    IReadOnlyList<string> PassThrough,
    bool ShowRawValues,
    bool ShowTagNames,
    bool ShowDuplicates,
    IReadOnlyList<string> Warnings)
{
    #region Properties

    public IEnumerable<ArgumentToken> Refused => Tokens.Where(t => t.Class == ArgumentClass.Refused);

    public bool IsValid => !Refused.Any();

    #endregion // Properties
}


/// <summary>
/// The D1 argument model as an allowlist (S5): only known-safe extraction flags reach ExifTool.
/// </summary>
public static partial class ArgumentPolicy
{
    #region Fields and constants

    /// <summary>
    /// The defaults v2 used, shown as the terminal-equivalent command.
    /// </summary>
    public const string DEFAULT_ARGUMENTS = "-fast -G -t -m -q -H -charset UTF8";

    private const string REASON_OUTPUT = "Changes the output format the app reads";
    private const string REASON_WRITE = "Writes or changes files";
    private const string REASON_PROTOCOL = "Belongs to the app's own ExifTool session";
    private const string REASON_CODE = "Runs Perl code";
    private const string REASON_UNKNOWN = "Not supported in the argument field";

    private static readonly FrozenDictionary<string, string> LOCKED = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["-G"] = "Print the group name for each tag",
        ["-t"] = "Tab-delimited output",
        ["-H"] = "Show tag IDs in hexadecimal",
        ["-j"] = "JSON output",
        ["-l"] = "Long output: descriptions and raw values",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> DISPLAY = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["-n"] = "Raw numerical values, no print conversion",
        ["-s"] = "Short tag names instead of descriptions",
        ["-a"] = "Allow duplicate tags",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> FLAGS = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["-fast"] = "Skip scanning for trailers — faster reads",
        ["-fast2"] = "Also skip maker notes and late chunks",
        ["-fast3"] = "Read only the file system and format tags",
        ["-m"] = "Ignore minor errors and warnings",
        ["-q"] = "Quiet: suppress informational messages",
        ["-u"] = "Extract unknown tags",
        ["-U"] = "Extract unknown binary tags too",
        ["-ee"] = "Extract embedded data (e.g. video streams)",
        ["-ee2"] = "Extract embedded data, level 2",
        ["-ee3"] = "Extract embedded data, level 3",
        ["-e"] = "Don't generate composite tags",
        ["--e"] = "Only generate composite tags",
        ["-L"] = "Use Windows Latin1 encoding",
        ["-struct"] = "Keep structures as JSON objects",
        ["--struct"] = "Flatten structures",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> WITH_VALUE = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["-api"] = "Set an ExifTool API option",
        ["-charset"] = "Character set for output",
        ["-lang"] = "Language of tag descriptions and values",
        ["-d"] = "Date and time format",
        ["-c"] = "GPS coordinate format",
        ["-sep"] = "Separator for list items",
        ["-listItem"] = "Show one item of each list",
        ["-x"] = "Exclude a tag",
        ["-ext"] = "Process files with this extension",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // -lang and -charset take a value only when one follows
    private static readonly FrozenSet<string> OPTIONAL_VALUE = new[] { "-lang", "-charset" }.ToFrozenSet(StringComparer.Ordinal);

    // API options that only change how a file is read; others (Filter, UserParam, Geolocation...) eval code or keep state
    private static readonly FrozenSet<string> API_OPTIONS = new[]
    {
        "LargeFileSupport", "QuickTimeUTC", "KeepUTCTime", "RequestAll", "IgnoreTags", "LimitLongValues", "StructFormat", "TimeZone", "NoMultiExif", "MissingTagValue",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, string> REFUSED = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["-o"] = REASON_WRITE, ["-out"] = REASON_WRITE, ["-w"] = REASON_WRITE, ["-W"] = REASON_WRITE, ["-textOut"] = REASON_WRITE,
        ["-tagsFromFile"] = REASON_WRITE, ["-geotag"] = REASON_WRITE, ["-overwrite_original"] = REASON_WRITE,
        ["-overwrite_original_in_place"] = REASON_WRITE, ["-delete_original"] = REASON_WRITE, ["-restore_original"] = REASON_WRITE,
        ["-P"] = REASON_WRITE, ["-preserve"] = REASON_WRITE, ["-globalTimeShift"] = REASON_WRITE, ["-wm"] = REASON_WRITE, ["-writeMode"] = REASON_WRITE,
        ["-X"] = REASON_OUTPUT, ["-xmlFormat"] = REASON_OUTPUT, ["-csv"] = REASON_OUTPUT, ["-php"] = REASON_OUTPUT, ["-J"] = REASON_OUTPUT,
        ["-b"] = REASON_OUTPUT, ["-binary"] = REASON_OUTPUT, ["-p"] = REASON_OUTPUT, ["-printFormat"] = REASON_OUTPUT,
        ["-g"] = REASON_OUTPUT, ["-v"] = REASON_OUTPUT, ["-verbose"] = REASON_OUTPUT, ["-h"] = REASON_OUTPUT, ["-htmlFormat"] = REASON_OUTPUT,
        ["-htmlDump"] = REASON_OUTPUT, ["-T"] = REASON_OUTPUT, ["-table"] = REASON_OUTPUT, ["-D"] = REASON_OUTPUT, ["-decimal"] = REASON_OUTPUT,
        ["-f"] = REASON_OUTPUT, ["-forcePrint"] = REASON_OUTPUT, ["-E"] = REASON_OUTPUT, ["-ex"] = REASON_OUTPUT, ["-ec"] = REASON_OUTPUT,
        ["-z"] = REASON_OUTPUT, ["-zip"] = REASON_OUTPUT, ["-list"] = REASON_OUTPUT, ["-listx"] = REASON_OUTPUT, ["-ver"] = REASON_OUTPUT,
        ["-sort"] = REASON_OUTPUT, ["-progress"] = REASON_OUTPUT, ["-r"] = REASON_OUTPUT, ["-recurse"] = REASON_OUTPUT, ["-i"] = REASON_OUTPUT,
        ["-config"] = REASON_PROTOCOL, ["-@"] = REASON_PROTOCOL, ["-stay_open"] = REASON_PROTOCOL, ["-execute"] = REASON_PROTOCOL,
        ["-common_args"] = REASON_PROTOCOL, ["-echo"] = REASON_PROTOCOL, ["-echo2"] = REASON_PROTOCOL, ["-echo3"] = REASON_PROTOCOL,
        ["-echo4"] = REASON_PROTOCOL, ["-srcfile"] = REASON_PROTOCOL, ["-k"] = REASON_PROTOCOL, ["-pause"] = REASON_PROTOCOL,
        ["-efile"] = REASON_PROTOCOL, ["-userParam"] = REASON_PROTOCOL, ["-use"] = REASON_PROTOCOL, ["-csvDelim"] = REASON_PROTOCOL,
        ["-password"] = REASON_PROTOCOL, ["-fileOrder"] = REASON_PROTOCOL,
        ["-if"] = REASON_CODE,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Splits the argument field into tokens; double quotes group a value with spaces.
    /// </summary>
    public static IReadOnlyList<string> Split(string? text)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var sb = new StringBuilder();
        var inQuotes = false;
        var hasToken = false;
        foreach (var c in text)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                hasToken = true;
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (hasToken) result.Add(sb.ToString());
                sb.Clear();
                hasToken = false;
            }
            else
            {
                sb.Append(c);
                hasToken = true;
            }
        }

        if (hasToken) result.Add(sb.ToString());
        return result;
    }


    /// <summary>
    /// Classifies every argument of the field.
    /// </summary>
    public static ArgumentAnalysis Analyze(string? text) => Analyze(Split(text));


    /// <summary>
    /// Classifies every argument of the field.
    /// </summary>
    public static ArgumentAnalysis Analyze(IReadOnlyList<string> args)
    {
        var tokens = new List<ArgumentToken>();
        var pass = new List<string>();
        var warnings = new List<string>();
        bool raw = false, names = false, dups = false;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg.Length == 0) continue;

            if (TryGet(LOCKED, arg, out var lockedDesc) || IsGroupFlag(arg))
            {
                tokens.Add(new ArgumentToken(arg, null, ArgumentClass.Locked, lockedDesc ?? LOCKED["-G"]));
                continue;
            }

            if (TryGet(DISPLAY, arg, out var displayDesc) || arg is "-S" or "-s2" or "-s3")
            {
                if (arg == "-n") raw = true;
                else if (arg == "-a") dups = true;
                else names = true;

                tokens.Add(new ArgumentToken(arg, null, ArgumentClass.Display, displayDesc ?? DISPLAY["-s"]));
                continue;
            }

            if (TryGet(FLAGS, arg, out var flagDesc))
            {
                if (arg is "-fast2" or "-fast3") warnings.Add($"{arg} can hide AI data in the table: late PNG text and MP4 keys aren't read (the AI card still shows it)");
                tokens.Add(new ArgumentToken(arg, null, ArgumentClass.PassThrough, flagDesc));
                pass.Add(arg);
                continue;
            }

            if (TryGet(WITH_VALUE, arg, out var valueDesc))
            {
                arg = Canonical(WITH_VALUE, arg);
                string? value = null;
                if (i + 1 < args.Count && !(OPTIONAL_VALUE.Contains(arg) && args[i + 1].StartsWith('-')))
                {
                    value = args[++i];
                }

                var reason = ValueReason(arg, value);
                tokens.Add(new ArgumentToken(arg, value, reason is null ? ArgumentClass.PassThrough : ArgumentClass.Refused, valueDesc, reason));
                if (reason is null)
                {
                    pass.Add(arg);
                    if (value is not null) pass.Add(value);
                }

                continue;
            }

            var refused = RefusedReason(arg);
            if (refused is not null)
            {
                tokens.Add(new ArgumentToken(arg, null, ArgumentClass.Refused, string.Empty, refused));
                continue;
            }

            var exclude = arg.StartsWith("--", StringComparison.Ordinal);
            tokens.Add(new ArgumentToken(arg, null, ArgumentClass.PassThrough, exclude ? "Exclude tags" : "Read only these tags"));
            pass.Add(arg);
        }

        return new ArgumentAnalysis(tokens, pass, raw, names, dups, warnings);
    }


    /// <summary>
    /// A description for a chip, from the token text alone (the Settings suggestions use it).
    /// </summary>
    public static string Describe(string flag) =>
        LOCKED.GetValueOrDefault(flag) ?? DISPLAY.GetValueOrDefault(flag) ?? FLAGS.GetValueOrDefault(flag) ?? WITH_VALUE.GetValueOrDefault(flag) ?? string.Empty;

    #endregion // Public methods


    #region Private methods

    [GeneratedRegex(@"^-G[0-9:]*$", RegexOptions.CultureInvariant)]
    private static partial Regex GroupFlag();


    [GeneratedRegex(@"^--?[A-Za-z][\w\-]*(?::[\w\-]+)*[*?]?$|^--?(?:[\w\-]+:)*all$", RegexOptions.CultureInvariant)]
    private static partial Regex TagSelection();


    [GeneratedRegex(@"^-(?:v[0-5]?|g[0-9:]*|ee[4-9]|fast[4-9]|s[0-9]|echo[0-9])$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberedOption();


    private static bool IsGroupFlag(string arg) => GroupFlag().IsMatch(arg);


    // ExifTool matches long option names in any case and single letters exactly (-n is not -N)
    private static bool TryGet(FrozenDictionary<string, string> map, string arg, out string value)
    {
        if (map.TryGetValue(arg, out value!)) return true;
        if (arg.Length <= 2) return false;

        foreach (var (key, v) in map)
        {
            if (key.Length > 2 && key.Equals(arg, StringComparison.OrdinalIgnoreCase))
            {
                value = v;
                return true;
            }
        }

        return false;
    }


    private static string Canonical(FrozenDictionary<string, string> map, string arg) =>
        map.ContainsKey(arg) ? arg : map.Keys.First(k => k.Length > 2 && k.Equals(arg, StringComparison.OrdinalIgnoreCase));


    private static string? ValueReason(string flag, string? value)
    {
        if (value is null) return flag is "-lang" or "-charset" ? null : "Needs a value";
        if (value.AsSpan().IndexOfAny('\r', '\n') >= 0) return "A value can't span lines";

        switch (flag)
        {
            case "-api":
            {
                var name = value.Split('=', 2)[0].TrimEnd('^');
                return API_OPTIONS.Contains(name) ? null : $"The API option {name} isn't allowed here";
            }

            case "-charset" when value.StartsWith("filename", StringComparison.OrdinalIgnoreCase):
                return "The app sets the file-name character set itself";

            default:
                return null;
        }
    }


    private static string? RefusedReason(string arg)
    {
        if (TryGet(REFUSED, arg, out var reason)) return reason;
        if (NumberedOption().IsMatch(arg)) return REASON_OUTPUT;

        if (!arg.StartsWith('-')) return "A file name: the app passes the file itself";
        if (arg.AsSpan().IndexOfAny('=', '<', '>') >= 0) return REASON_WRITE;
        if (arg.StartsWith("-list", StringComparison.OrdinalIgnoreCase)) return REASON_OUTPUT;

        return TagSelection().IsMatch(arg) ? null : REASON_UNKNOWN;
    }

    #endregion // Private methods
}
