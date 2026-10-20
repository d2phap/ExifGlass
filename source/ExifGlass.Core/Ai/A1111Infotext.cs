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
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ExifGlass.Core.Ai;

/// <summary>
/// One "Key: value" of the parameters line; Segment is the exact text between commas, so formatting gives the line back.
/// </summary>
public sealed record A1111Param(string Key, string RawValue, string Segment)
{
    #region Properties

    /// <summary>
    /// The value with JSON quoting removed: "\"smiling, blue eyes\"" becomes "smiling, blue eyes".
    /// </summary>
    public string Value => A1111Infotext.Unquote(RawValue);

    #endregion // Properties
}


/// <summary>
/// A1111 "infotext": prompt lines, an optional "Negative prompt:" block, the parameters line and trailing lines (SD.Next Template).
/// </summary>
public sealed partial class A1111Infotext
{
    #region Fields and constants

    public const string NEGATIVE_PREFIX = "Negative prompt:";

    #endregion // Fields and constants


    #region Properties

    public string Positive { get; init; } = string.Empty;

    public string? Negative { get; init; }

    /// <summary>
    /// The exact text that introduced the negative prompt, usually "Negative prompt: ".
    /// </summary>
    public string NegativeLead { get; init; } = NEGATIVE_PREFIX + " ";

    public IReadOnlyList<A1111Param> Params { get; init; } = [];

    /// <summary>
    /// Lines after the parameters line, such as SD.Next's "Template: ...".
    /// </summary>
    public IReadOnlyList<string> TrailingLines { get; init; } = [];

    /// <summary>
    /// True when the text came with CRLF line ends; Format gives them back.
    /// </summary>
    public bool CrLf { get; init; }

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Parses infotext; null when the text has no parameters line (A1111's rule: the last line with 3 or more "key: value").
    /// </summary>
    public static A1111Infotext? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var crlf = text.Contains("\r\n", StringComparison.Ordinal);
        var lines = (crlf ? text.Replace("\r\n", "\n", StringComparison.Ordinal) : text).Split('\n');

        var paramIndex = FindParamsLine(lines);
        if (paramIndex < 0) return null;

        var parameters = Tokenize(lines[paramIndex], strict: false);
        if (parameters is null || parameters.Count == 0) return null;

        // A1111: lines before "Negative prompt:" are the prompt, that line and the rest up to the parameters the negative
        var positive = new StringBuilder();
        StringBuilder? negative = null;
        var lead = NEGATIVE_PREFIX + " ";
        for (var i = 0; i < paramIndex; i++)
        {
            var line = lines[i];
            if (negative is null && line.StartsWith(NEGATIVE_PREFIX, StringComparison.Ordinal))
            {
                var rest = line[NEGATIVE_PREFIX.Length..];
                var trimmed = rest.TrimStart();
                lead = NEGATIVE_PREFIX + rest[..(rest.Length - trimmed.Length)];
                negative = new StringBuilder(trimmed);
                continue;
            }

            if (negative is not null) negative.Append('\n').Append(line);
            else
            {
                if (i > 0) positive.Append('\n');
                positive.Append(line);
            }
        }

        return new A1111Infotext
        {
            Positive = positive.ToString(),
            Negative = negative?.ToString(),
            NegativeLead = lead,
            Params = parameters,
            TrailingLines = lines[(paramIndex + 1)..],
            CrLf = crlf,
        };
    }


    /// <summary>
    /// Formats the infotext back; Format(Parse(text)) gives the same text.
    /// </summary>
    public string Format()
    {
        var sb = new StringBuilder(Positive.Length + 512);
        sb.Append(Positive);

        if (Negative is not null)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(NegativeLead).Append(Negative);
        }

        if (Params.Count > 0)
        {
            if (sb.Length > 0 || Negative is not null) sb.Append('\n');
            sb.Append(string.Join(",", Params.Select(p => p.Segment)));
        }

        foreach (var line in TrailingLines) sb.Append('\n').Append(line);

        var text = sb.ToString();
        return CrLf ? text.Replace("\n", "\r\n", StringComparison.Ordinal) : text;
    }


    /// <summary>
    /// The first parameter with this key (case-insensitive).
    /// </summary>
    public A1111Param? Get(string key) => Params.FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase));


    /// <summary>
    /// Builds infotext from parts, quoting values that hold a comma, a colon or a newline as A1111 does.
    /// </summary>
    public static A1111Infotext Create(string positive, string? negative, IEnumerable<(string Key, string Value)> parameters)
    {
        var list = new List<A1111Param>();
        foreach (var (key, value) in parameters)
        {
            var raw = NeedsQuotes(value) ? JsonSerializer.Serialize(value, AiJsonContext.Default.String) : value;
            list.Add(new A1111Param(key, raw, (list.Count == 0 ? string.Empty : " ") + key + ": " + raw));
        }

        return new A1111Infotext { Positive = positive, Negative = string.IsNullOrEmpty(negative) ? null : negative, Params = list };
    }


    /// <summary>
    /// Removes A1111's JSON quoting from a value.
    /// </summary>
    public static string Unquote(string raw)
    {
        var t = raw.Trim();
        if (t.Length >= 2 && t[0] == '"' && t[^1] == '"')
        {
            try
            {
                return JsonSerializer.Deserialize(t, AiJsonContext.Default.String) ?? t;
            }
            catch (JsonException)
            {
                return t[1..^1];
            }
        }

        return t;
    }


    /// <summary>
    /// How many "key: value" pairs A1111's own regex finds in a line.
    /// </summary>
    public static int CountPairs(string line) => ParamPair().Count(line);

    #endregion // Public methods


    #region Private methods

    // A1111 infotext_utils re_param_code
    [GeneratedRegex("""\s*(\w[\w \-/]+):\s*("(?:\\.|[^\\"])+"|[^,]*)(?:,|$)""", RegexOptions.CultureInvariant)]
    private static partial Regex ParamPair();


    [GeneratedRegex(@"^\w[\w \-/]*$", RegexOptions.CultureInvariant)]
    private static partial Regex ParamKey();


    private static bool NeedsQuotes(string value) => value.AsSpan().IndexOfAny(',', ':', '\n') >= 0 || value.StartsWith('"');


    // the last line with 3+ pairs; a strict parameters line may be followed by a few "Key: text" lines (SD.Next)
    private static int FindParamsLine(string[] lines)
    {
        var last = lines.Length - 1;
        while (last > 0 && lines[last].Length == 0) last--;

        if (CountPairs(lines[last]) >= 3) return last;

        for (var i = last - 1; i >= Math.Max(0, last - 3); i--)
        {
            if (Tokenize(lines[i], strict: true) is { Count: >= 3 })
            {
                var trailing = lines[(i + 1)..(last + 1)];
                if (trailing.All(l => l.Length == 0 || TrailingKey(l))) return i;
            }
        }

        return -1;
    }


    private static bool TrailingKey(string line)
    {
        var colon = line.IndexOf(':');
        return colon > 0 && ParamKey().IsMatch(line[..colon]);
    }


    // splits "Key: value, Key: \"quoted, value\", Key: {json}, ..." keeping each segment's exact text
    private static List<A1111Param>? Tokenize(string line, bool strict)
    {
        var result = new List<A1111Param>();
        var pos = 0;

        while (pos < line.Length)
        {
            var start = pos;
            while (pos < line.Length && line[pos] == ' ') pos++;

            var colon = line.IndexOf(':', pos);
            var comma = line.IndexOf(',', pos);
            if (colon < 0 || (comma >= 0 && comma < colon) || !ParamKey().IsMatch(line[pos..colon]))
            {
                if (strict) return null;

                // junk without a key: kept in the previous segment so the line formats back unchanged
                var end = comma < 0 ? line.Length : comma;
                if (result.Count > 0)
                {
                    var prev = result[^1];
                    result[^1] = prev with { Segment = prev.Segment + "," + line[start..end] };
                }

                pos = comma < 0 ? line.Length : comma + 1;
                continue;
            }

            var key = line[pos..colon];
            pos = colon + 1;
            while (pos < line.Length && line[pos] == ' ') pos++;

            var valueStart = pos;
            pos = ValueEnd(line, pos);
            var raw = line[valueStart..pos].TrimEnd();

            result.Add(new A1111Param(key, raw, line[start..pos]));

            if (pos < line.Length)
            {
                if (line[pos] == ',') pos++;
                else if (strict) return null;
                else
                {
                    // a quoted value followed by more text before the comma
                    var next = line.IndexOf(',', pos);
                    var end = next < 0 ? line.Length : next;
                    result[^1] = result[^1] with { Segment = result[^1].Segment + line[pos..end], RawValue = (raw + line[pos..end]).TrimEnd() };
                    pos = next < 0 ? line.Length : next + 1;
                }
            }
        }

        return result;
    }


    // a JSON-quoted string, an unquoted {object} or [array] (Civitai resources, Hashes), else up to the next comma
    private static int ValueEnd(string line, int pos)
    {
        if (pos >= line.Length) return pos;

        var c = line[pos];
        if (c == '"')
        {
            for (var i = pos + 1; i < line.Length; i++)
            {
                if (line[i] == '\\') i++;
                else if (line[i] == '"') return i + 1;
            }

            return line.Length;
        }

        if (c is '{' or '[')
        {
            var depth = 0;
            var inString = false;
            for (var i = pos; i < line.Length; i++)
            {
                var ch = line[i];
                if (inString)
                {
                    if (ch == '\\') i++;
                    else if (ch == '"') inString = false;
                }
                else if (ch == '"') inString = true;
                else if (ch is '{' or '[') depth++;
                else if (ch is '}' or ']' && --depth == 0) return i + 1;
            }

            return line.Length;
        }

        var commaAt = line.IndexOf(',', pos);
        return commaAt < 0 ? line.Length : commaAt;
    }

    #endregion // Private methods
}
