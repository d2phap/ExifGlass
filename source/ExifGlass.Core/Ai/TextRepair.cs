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

namespace ExifGlass.Core.Ai;

/// <summary>
/// Repairs UTF-8 text that a non-compliant writer put in a PNG tEXt chunk, which ExifTool decodes as cp1252 (S9).
/// </summary>
public static class TextRepair
{
    #region Fields and constants

    // cp1252 0x80-0x9F; the 5 undefined bytes come out of ExifTool as U+0081, U+008D, U+008F, U+0090, U+009D
    private static readonly char[] CP1252_HIGH =
    [
        '€', '\u0081', '‚', 'ƒ', '„', '…', '†', '‡', 'ˆ', '‰', 'Š', '‹', 'Œ', '\u008D', 'Ž', '\u008F',
        '\u0090', '‘', '’', '“', '”', '•', '–', '—', '˜', '™', 'š', '›', 'œ', '\u009D', 'ž', 'Ÿ',
    ];

    private static readonly UTF8Encoding STRICT_UTF8 = new(false, throwOnInvalidBytes: true);

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Gives back the UTF-8 text when the input is cp1252-decoded UTF-8 ("çŒ«ã\u0081®" becomes "猫の"); otherwise null.
    /// </summary>
    public static string? TryRepairMojibake(string text)
    {
        var hasHigh = false;
        var bytes = new byte[text.Length];

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c < 0x80)
            {
                bytes[i] = (byte)c;
                continue;
            }

            hasHigh = true;
            if (c <= 0xFF && c is not (>= '\u0080' and <= '\u009F') || c is '\u0081' or '\u008D' or '\u008F' or '\u0090' or '\u009D')
            {
                bytes[i] = (byte)c;
                continue;
            }

            var index = Array.IndexOf(CP1252_HIGH, c);
            if (index < 0) return null;
            bytes[i] = (byte)(0x80 + index);
        }

        if (!hasHigh) return null;

        try
        {
            var repaired = STRICT_UTF8.GetString(bytes);
            return repaired == text ? null : repaired;
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    #endregion // Public methods
}
