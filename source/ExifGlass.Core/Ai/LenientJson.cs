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
using System.Text.Json.Serialization;

namespace ExifGlass.Core.Ai;

/// <summary>
/// Parses JSON that AI generators embed: Python's bare NaN/Infinity, a trailing NUL, size and depth caps.
/// </summary>
public static class LenientJson
{
    #region Fields and constants

    /// <summary>
    /// Embedded JSON larger than this isn't parsed (R14); the card says so and the table still shows the text.
    /// </summary>
    public const int MAX_LENGTH = 64 * 1024 * 1024;

    public const int MAX_DEPTH = 256;

    private static readonly JsonDocumentOptions OPTIONS = new()
    {
        MaxDepth = MAX_DEPTH,
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// True when the text looks like a JSON object (after spaces and an optional prefix such as "prompt:").
    /// </summary>
    public static bool LooksLikeObject(string? text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        var span = text.AsSpan().TrimStart();
        return span.Length > 1 && span[0] == '{';
    }


    /// <summary>
    /// Parses a JSON value; null with a warning when it is too large, too deep or not JSON.
    /// </summary>
    public static JsonDocument? TryParse(string? text, out string? warning)
    {
        warning = null;
        if (string.IsNullOrWhiteSpace(text)) return null;

        if (text.Length > MAX_LENGTH)
        {
            warning = $"Embedded JSON over {MAX_LENGTH / (1024 * 1024)} MB isn't parsed";
            return null;
        }

        var clean = Sanitize(text);
        try
        {
            // over an array we own: Parse(string) transcodes into a pooled buffer the pool then keeps
            return JsonDocument.Parse(Encoding.UTF8.GetBytes(clean), OPTIONS);
        }
        catch (JsonException ex)
        {
            warning = "Embedded JSON couldn't be read: " + ex.Message;
            return null;
        }
    }


    /// <summary>
    /// Replaces bare NaN, Infinity and -Infinity outside strings with null, and drops trailing NULs.
    /// </summary>
    public static string Sanitize(string text)
    {
        var trimmed = text.AsSpan().TrimEnd('\0').Trim();

        // a multi-megabyte workflow without them isn't copied (R14)
        if (trimmed.IndexOf("NaN", StringComparison.Ordinal) < 0 && trimmed.IndexOf("Infinity", StringComparison.Ordinal) < 0)
        {
            return trimmed.Length == text.Length ? text : trimmed.ToString();
        }

        var sb = new StringBuilder(trimmed.Length);
        var inString = false;
        for (var i = 0; i < trimmed.Length; i++)
        {
            var c = trimmed[i];
            if (inString)
            {
                sb.Append(c);
                if (c == '\\' && i + 1 < trimmed.Length) sb.Append(trimmed[++i]);
                else if (c == '"') inString = false;
                continue;
            }

            if (c == '"')
            {
                inString = true;
                sb.Append(c);
            }
            else if (trimmed[i..].StartsWith("NaN"))
            {
                sb.Append("null");
                i += 2;
            }
            else if (trimmed[i..].StartsWith("-Infinity"))
            {
                sb.Append("null");
                i += 8;
            }
            else if (trimmed[i..].StartsWith("Infinity"))
            {
                sb.Append("null");
                i += 7;
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }


    /// <summary>
    /// A property's value as text, or null when missing or empty.
    /// </summary>
    public static string? Str(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var v)) return null;

        var text = AiTagSet.ToText(v);
        return text.Length == 0 ? null : text;
    }


    /// <summary>
    /// A property's value as an element, or null when missing.
    /// </summary>
    public static JsonElement? Prop(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v : null;

    #endregion // Public methods
}


[JsonSerializable(typeof(string))]
internal sealed partial class AiJsonContext : JsonSerializerContext;
