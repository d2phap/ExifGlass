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
using System.Buffers;
using System.Text;
using System.Text.Json;

namespace ExifGlass.Core.Metadata;

/// <summary>
/// Everything one table read returned.
/// </summary>
public sealed class TableReadResult
{
    #region Properties

    public string? SourceFile { get; set; }

    public List<TagRecord> Tags { get; } = [];

    public List<string> Warnings { get; } = [];

    /// <summary>
    /// ExifTool's own error for the file ("File format error"), or null.
    /// </summary>
    public string? Error { get; set; }

    public string? ExifToolVersion { get; set; }

    #endregion // Properties
}


/// <summary>
/// Reads ExifTool -j -l -G0:1:4 output with Utf8JsonReader straight from the pipe: no DOM, no reflection.
/// </summary>
public static class TableReader
{
    #region Fields and constants

    private const int CANCEL_CHECK_EVERY = 256;
    private const int MAX_DEPTH = 256;

    private static ReadOnlySpan<byte> BinaryPrefix => "(Binary data "u8;

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Parses the first file object of -j output; works with and without -l (long format).
    /// </summary>
    public static TableReadResult Read(in ReadOnlySequence<byte> json, StringPool pool, CancellationToken cancellationToken = default)
    {
        var result = new TableReadResult();
        var reader = new Utf8JsonReader(json, new JsonReaderOptions { AllowMultipleValues = true, MaxDepth = MAX_DEPTH });

        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray) return result;
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return result;

        var order = 0;
        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            if (++order % CANCEL_CHECK_EVERY == 0) cancellationToken.ThrowIfCancellationRequested();

            if (reader.ValueTextEquals("SourceFile"u8))
            {
                reader.Read();
                result.SourceFile = reader.GetString();
                order--;
                continue;
            }

            var keyText = GetString(ref reader);
            var key = TagKey.Parse(keyText, pool);
            reader.Read();

            string description = string.Empty, id = string.Empty, value = string.Empty;
            string? raw = null;
            var kind = TagValueKind.Null;

            if (reader.TokenType == JsonTokenType.StartObject && IsLongFormat(reader))
            {
                TagValueKind rawKind = TagValueKind.Null;
                while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
                {
                    if (reader.ValueTextEquals("val"u8))
                    {
                        reader.Read();
                        (value, kind) = ReadValue(ref reader, json);
                    }
                    else if (reader.ValueTextEquals("num"u8))
                    {
                        reader.Read();
                        (raw, rawKind) = ReadValue(ref reader, json);
                    }
                    else if (reader.ValueTextEquals("desc"u8))
                    {
                        reader.Read();
                        description = Pooled(ref reader, pool);
                    }
                    else if (reader.ValueTextEquals("id"u8))
                    {
                        reader.Read();
                        id = reader.TokenType == JsonTokenType.String ? Pooled(ref reader, pool) : RawText(ref reader);
                    }
                    else
                    {
                        reader.Read();
                        reader.Skip();
                    }
                }

                if (raw is not null && !RawDiffers(raw, rawKind, value, kind)) raw = null;
            }
            else
            {
                (value, kind) = ReadValue(ref reader, json);
            }

            if (key.Group0 == "ExifTool")
            {
                if (key.Name.StartsWith("Warning", StringComparison.Ordinal)) result.Warnings.Add(value);
                else if (key.Name == "Error") result.Error = value;
                else if (key.Name == "ExifToolVersion") result.ExifToolVersion = value;
            }

            result.Tags.Add(new TagRecord
            {
                Key = keyText,
                Group0 = key.Group0,
                Group1 = key.Group1,
                Copy = key.Copy,
                Name = key.Name,
                Description = description.Length > 0 ? description : key.Name,
                IdText = id,
                DisplayId = TagIdRule.ToDisplay(key.Group0, id),
                Value = value,
                Raw = raw,
                Kind = kind,
                FileOrder = order,
            });
        }

        return result;
    }

    #endregion // Public methods


    #region Private methods

    // a long-format value is an object whose first property is one of val/num/desc/id; a -struct value isn't
    private static bool IsLongFormat(Utf8JsonReader reader)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName) return false;

        return reader.ValueTextEquals("val"u8) || reader.ValueTextEquals("num"u8)
            || reader.ValueTextEquals("desc"u8) || reader.ValueTextEquals("id"u8);
    }


    // S5: a float that lost a trailing zero arrives as num 4 next to val 4.0 and is not a conversion
    private static bool RawDiffers(string raw, TagValueKind rawKind, string value, TagValueKind valueKind)
    {
        if (raw == value) return false;

        if (rawKind == TagValueKind.Number && valueKind == TagValueKind.Number
            && double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var a)
            && double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var b))
        {
            return a != b;
        }

        return true;
    }


    private static string GetString(ref Utf8JsonReader reader) => reader.GetString() ?? string.Empty;


    private static string Pooled(ref Utf8JsonReader reader, StringPool pool)
    {
        if (reader.TokenType != JsonTokenType.String) return RawText(ref reader);

        var length = reader.HasValueSequence ? (int)reader.ValueSequence.Length : reader.ValueSpan.Length;
        if (length > 256) return reader.GetString() ?? string.Empty;

        Span<char> buffer = stackalloc char[256];
        var n = reader.CopyString(buffer);
        return pool.Get(buffer[..n]);
    }


    private static string RawText(ref Utf8JsonReader reader) => reader.HasValueSequence
        ? Encoding.UTF8.GetString(reader.ValueSequence.ToArray())
        : Encoding.UTF8.GetString(reader.ValueSpan);


    // numbers keep their JSON text ("1.50" stays "1.50"); lists join with ", "; structs keep their JSON
    private static (string Text, TagValueKind Kind) ReadValue(ref Utf8JsonReader reader, in ReadOnlySequence<byte> json)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                var isBinary = reader.HasValueSequence
                    ? reader.ValueSequence.Length > BinaryPrefix.Length && reader.ValueSequence.Slice(0, BinaryPrefix.Length).ToArray().AsSpan().SequenceEqual(BinaryPrefix)
                    : reader.ValueSpan.StartsWith(BinaryPrefix);
                return (reader.GetString() ?? string.Empty, isBinary ? TagValueKind.Binary : TagValueKind.String);

            case JsonTokenType.Number:
                return (RawText(ref reader), TagValueKind.Number);

            case JsonTokenType.True:
                return ("True", TagValueKind.Bool);

            case JsonTokenType.False:
                return ("False", TagValueKind.Bool);

            case JsonTokenType.StartArray:
            {
                var sb = new StringBuilder();
                var first = true;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (!first) sb.Append(", ");
                    first = false;
                    sb.Append(ReadValue(ref reader, json).Text);
                }

                return (sb.ToString(), TagValueKind.List);
            }

            case JsonTokenType.StartObject:
            {
                var start = reader.TokenStartIndex;
                reader.Skip();
                var end = reader.BytesConsumed;
                return (Encoding.UTF8.GetString(json.Slice(start, end - start).ToArray()), TagValueKind.Struct);
            }

            case JsonTokenType.Null:
                return (string.Empty, TagValueKind.Null);

            default:
                reader.Skip();
                return (string.Empty, TagValueKind.Null);
        }
    }

    #endregion // Private methods
}
