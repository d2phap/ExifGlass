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
using System.Text.RegularExpressions;

namespace ExifGlass.Core.ExifTool;

/// <summary>
/// Encodes arguments as lines of the -@ argument stream (exiftool FilterArgfileLine).
/// </summary>
public static partial class ArgFile
{
    #region Public methods

    /// <summary>
    /// True when the argument fits on one line of the stream, plain or as a #[CSTR] line.
    /// </summary>
    public static bool CanEncode(string arg) => IsPlainSafe(arg) || arg.AsSpan().IndexOfAny('$', '@') < 0;


    /// <summary>
    /// Appends one argument as a plain line when that round-trips, otherwise as a #[CSTR] line.
    /// </summary>
    public static void AppendLine(StringBuilder sb, string arg)
    {
        if (IsPlainSafe(arg))
        {
            sb.Append(arg).Append('\n');
            return;
        }

        // ExifTool turns $ and @ of a #[CSTR] line into \$ and \@: such values go through -TAG<=file instead
        if (!CanEncode(arg)) throw new ArgumentException("A multi-line argument with $ or @ can't be encoded; write it from a file", nameof(arg));

        // a newline inside one argument would otherwise split it into new arguments (S5)
        sb.Append("#[CSTR]");
        foreach (var c in arg)
        {
            switch (c)
            {
                case '\\': sb.Append(@"\\"); break;
                case '\n': sb.Append(@"\n"); break;
                case '\r': sb.Append(@"\r"); break;
                case '\t': sb.Append(@"\t"); break;
                default: sb.Append(c); break;
            }
        }

        sb.Append('\n');
    }


    /// <summary>
    /// True when a plain argfile line gives back exactly this argument.
    /// </summary>
    public static bool IsPlainSafe(string arg)
    {
        if (arg.Length == 0) return false;
        if (arg[0] == '#' || char.IsWhiteSpace(arg[0]) || char.IsWhiteSpace(arg[^1])) return false;
        if (arg.AsSpan().IndexOfAny('\r', '\n') >= 0) return false;

        return !SpaceAroundAssign().IsMatch(arg);
    }

    #endregion // Public methods


    #region Private methods

    // FilterArgfileLine drops whitespace around "=" and one space after it on plain lines
    [GeneratedRegex(@"^-[-_0-9A-Z:]+#?(\s+[-+<]?=|[-+<]?= )", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SpaceAroundAssign();

    #endregion // Private methods
}
