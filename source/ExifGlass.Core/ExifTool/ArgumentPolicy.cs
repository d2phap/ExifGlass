/*
ExifGlass - AI & EXIF metadata viewer
Copyright (C) 2023 - 2027 DUONG DIEU PHAP
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
using ExifGlass.Core.Localization;
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

    /// <summary>
    /// Where the flag sits in the arguments it was read from; its value, if any, follows it.
    /// </summary>
    public int Index { get; init; } = -1;

    #endregion // Properties
}


/// <summary>
/// One chip of the Settings argument field, in the field's order: what it does, why it's refused, whether the app owns it (D1).
/// </summary>
public readonly record struct ArgumentChip(string Description, string? Reason, bool IsLocked);


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
    /// The default arguments, shown as the terminal-equivalent command.
    /// </summary>
    public const string DEFAULT_ARGUMENTS = "-fast -G -t -m -q -H -charset UTF8";

    /// <summary>
    /// The dashed suggestions under the Settings argument field (DS 6.15) with their short labels (Proto).
    /// </summary>
    public static readonly IReadOnlyList<(string Flag, LangId Label)> SUGGESTIONS =
    [
        ("-n", LangId.Argument_HintRaw), ("-a", LangId.Argument_HintDuplicates), ("-u", LangId.Argument_HintUnknown), ("-U", LangId.Argument_HintUnknownBinary), ("-ee", LangId.Argument_HintEmbedded), ("-ee3", LangId.Argument_HintAllEmbedded),
        ("-s", LangId.Argument_HintShort), ("-e", LangId.Argument_HintNoComposite), ("-fast", LangId.Argument_HintFaster), ("-fast2", LangId.Argument_HintFastest), ("-m", LangId.Argument_HintMinorErrors), ("-q", LangId.Argument_HintQuiet),
    ];

    private const LangId REASON_OUTPUT = LangId.Argument_ReasonOutput;
    private const LangId REASON_WRITE = LangId.Argument_ReasonWrite;
    private const LangId REASON_PROTOCOL = LangId.Argument_ReasonProtocol;
    private const LangId REASON_CODE = LangId.Argument_ReasonCode;
    private const LangId REASON_UNKNOWN = LangId.Argument_ReasonUnknown;

    private static readonly FrozenDictionary<string, LangId> LOCKED = new Dictionary<string, LangId>(StringComparer.Ordinal)
    {
        ["-G"] = LangId.Argument_Group,
        ["-t"] = LangId.Argument_Tab,
        ["-H"] = LangId.Argument_Hex,
        ["-j"] = LangId.Argument_Json,
        ["-l"] = LangId.Argument_Long,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, LangId> DISPLAY = new Dictionary<string, LangId>(StringComparer.Ordinal)
    {
        ["-n"] = LangId.Argument_Raw,
        ["-s"] = LangId.Argument_Short,
        ["-a"] = LangId.Argument_Duplicates,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, LangId> FLAGS = new Dictionary<string, LangId>(StringComparer.Ordinal)
    {
        ["-fast"] = LangId.Argument_Fast,
        ["-fast2"] = LangId.Argument_Fast2,
        ["-fast3"] = LangId.Argument_Fast3,
        ["-m"] = LangId.Argument_MinorErrors,
        ["-q"] = LangId.Argument_Quiet,
        ["-u"] = LangId.Argument_Unknown,
        ["-U"] = LangId.Argument_UnknownBinary,
        ["-ee"] = LangId.Argument_Embedded,
        ["-ee2"] = LangId.Argument_Embedded2,
        ["-ee3"] = LangId.Argument_Embedded3,
        ["-e"] = LangId.Argument_NoComposite,
        ["--e"] = LangId.Argument_OnlyComposite,
        ["-L"] = LangId.Argument_Latin1,
        ["-struct"] = LangId.Argument_Struct,
        ["--struct"] = LangId.Argument_Flatten,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, LangId> WITH_VALUE = new Dictionary<string, LangId>(StringComparer.Ordinal)
    {
        ["-api"] = LangId.Argument_Api,
        ["-charset"] = LangId.Argument_Charset,
        ["-lang"] = LangId.Argument_Lang,
        ["-d"] = LangId.Argument_DateFormat,
        ["-c"] = LangId.Argument_GpsFormat,
        ["-sep"] = LangId.Argument_Separator,
        ["-listItem"] = LangId.Argument_ListItem,
        ["-x"] = LangId.Argument_Exclude,
        ["-ext"] = LangId.Argument_Extension,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // -lang and -charset take a value only when one follows
    private static readonly FrozenSet<string> OPTIONAL_VALUE = new[] { "-lang", "-charset" }.ToFrozenSet(StringComparer.Ordinal);

    // API options that only change how a file is read; others (Filter, UserParam, Geolocation...) eval code or keep state
    private static readonly FrozenSet<string> API_OPTIONS = new[]
    {
        "LargeFileSupport", "QuickTimeUTC", "KeepUTCTime", "RequestAll", "IgnoreTags", "LimitLongValues", "StructFormat", "TimeZone", "NoMultiExif", "MissingTagValue",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, LangId> REFUSED = new Dictionary<string, LangId>(StringComparer.Ordinal)
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
            var start = i;
            if (arg.Length == 0) continue;

            if (TryGet(LOCKED, arg, out var lockedDesc) || IsGroupFlag(arg))
            {
                tokens.Add(new ArgumentToken(arg, null, ArgumentClass.Locked, lockedDesc ?? Lang.Current[LOCKED["-G"]]) { Index = start });
                continue;
            }

            if (TryGet(DISPLAY, arg, out var displayDesc) || arg is "-S" or "-s2" or "-s3")
            {
                if (arg == "-n") raw = true;
                else if (arg == "-a") dups = true;
                else names = true;

                tokens.Add(new ArgumentToken(arg, null, ArgumentClass.Display, displayDesc ?? Lang.Current[DISPLAY["-s"]]) { Index = start });
                continue;
            }

            if (TryGet(FLAGS, arg, out var flagDesc))
            {
                if (arg is "-fast2" or "-fast3") warnings.Add(Lang.Current[LangId.Argument_FastHidesAi, arg]);
                tokens.Add(new ArgumentToken(arg, null, ArgumentClass.PassThrough, flagDesc) { Index = start });
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
                tokens.Add(new ArgumentToken(arg, value, reason is null ? ArgumentClass.PassThrough : ArgumentClass.Refused, valueDesc, reason) { Index = start });
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
                tokens.Add(new ArgumentToken(arg, null, ArgumentClass.Refused, string.Empty, refused) { Index = start });
                continue;
            }

            var exclude = arg.StartsWith("--", StringComparison.Ordinal);
            tokens.Add(new ArgumentToken(arg, null, ArgumentClass.PassThrough, Lang.Current[exclude ? LangId.Argument_ExcludeTags : LangId.Argument_ReadTags]) { Index = start });
            pass.Add(arg);
        }

        return new ArgumentAnalysis(tokens, pass, raw, names, dups, warnings);
    }


    /// <summary>
    /// A description for a chip, from the token text alone (the Settings suggestions use it).
    /// </summary>
    public static string Describe(string flag) =>
        LOCKED.TryGetValue(flag, out var key) || DISPLAY.TryGetValue(flag, out key) || FLAGS.TryGetValue(flag, out key) || WITH_VALUE.TryGetValue(flag, out key) ? Lang.Current[key] : string.Empty;


    /// <summary>
    /// A chip for every argument, in order: a value says which flag it belongs to and shares its reason; only the first of a repeated app-owned flag is locked, so a typed repeat can go.
    /// </summary>
    public static IReadOnlyList<ArgumentChip> Chips(IReadOnlyList<string> args)
    {
        var chips = new ArgumentChip[args.Count];
        var locked = new HashSet<string>(StringComparer.Ordinal);

        foreach (var token in Analyze(args).Tokens)
        {
            var isLocked = token.Class == ArgumentClass.Locked && locked.Add(token.Flag);
            chips[token.Index] = new ArgumentChip(token.Description, token.Reason, isLocked);
            if (token.Value is not null) chips[token.Index + 1] = new ArgumentChip(Lang.Current[LangId.Argument_ValueFor, token.Flag], token.Reason, false);
        }

        return chips;
    }


    /// <summary>
    /// The field's arguments as the settings keep them; one with a space is quoted, as Split reads it back.
    /// </summary>
    public static string Join(IEnumerable<string> args) =>
        string.Join(' ', args.Where(a => a.Length > 0).Select(a => a.AsSpan().IndexOfAny(' ', '\t') >= 0 ? $"\"{a}\"" : a));

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
    private static bool TryGet(FrozenDictionary<string, LangId> map, string arg, out string value)
    {
        value = null!;
        if (map.TryGetValue(arg, out var text))
        {
            value = Lang.Current[text];
            return true;
        }

        if (arg.Length <= 2) return false;

        foreach (var (key, v) in map)
        {
            if (key.Length > 2 && key.Equals(arg, StringComparison.OrdinalIgnoreCase))
            {
                value = Lang.Current[v];
                return true;
            }
        }

        return false;
    }


    private static string Canonical(FrozenDictionary<string, LangId> map, string arg) =>
        map.ContainsKey(arg) ? arg : map.Keys.First(k => k.Length > 2 && k.Equals(arg, StringComparison.OrdinalIgnoreCase));


    private static string? ValueReason(string flag, string? value)
    {
        if (value is null) return flag is "-lang" or "-charset" ? null : Lang.Current[LangId.Argument_NeedsValue];
        if (value.AsSpan().IndexOfAny('\r', '\n') >= 0) return Lang.Current[LangId.Argument_ValueLines];

        switch (flag)
        {
            case "-api":
            {
                var name = value.Split('=', 2)[0].TrimEnd('^');
                return API_OPTIONS.Contains(name) ? null : Lang.Current[LangId.Argument_ApiNotAllowed, name];
            }

            case "-charset" when value.StartsWith("filename", StringComparison.OrdinalIgnoreCase):
                return Lang.Current[LangId.Argument_FileNameCharset];

            default:
                return null;
        }
    }


    private static string? RefusedReason(string arg)
    {
        if (TryGet(REFUSED, arg, out var reason)) return reason;
        if (NumberedOption().IsMatch(arg)) return Lang.Current[REASON_OUTPUT];

        if (!arg.StartsWith('-')) return Lang.Current[LangId.Argument_ReasonFileName];
        if (arg.AsSpan().IndexOfAny('=', '<', '>') >= 0) return Lang.Current[REASON_WRITE];
        if (arg.StartsWith("-list", StringComparison.OrdinalIgnoreCase)) return Lang.Current[REASON_OUTPUT];

        return TagSelection().IsMatch(arg) ? null : Lang.Current[REASON_UNKNOWN];
    }

    #endregion // Private methods
}
