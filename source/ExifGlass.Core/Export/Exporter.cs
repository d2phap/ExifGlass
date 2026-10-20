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
using ExifGlass.Core.Metadata;
using ExifGlass.Core.Query;
using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ExifGlass.Core.Export;

/// <summary>
/// The Export menu's formats.
/// </summary>
public enum ExportFormat
{
    Csv,
    PlainText,
    Json,
}


/// <summary>
/// A table column, in table order.
/// </summary>
[Flags]
public enum TableColumns
{
    None = 0,
    Index = 1 << 0,
    TagId = 1 << 1,
    TagName = 1 << 2,
    Value = 1 << 3,

    /// <summary>
    /// The prototype's default: # off, the rest on (Appendix A item 24).
    /// </summary>
    Default = TagId | TagName | Value,

    All = Index | TagId | TagName | Value,
}


/// <summary>
/// Exports rows in view with the visible columns (DS 7.4), and the clipboard formats of a selection.
/// </summary>
public static class Exporter
{
    #region Fields and constants

    private static readonly (TableColumns Column, string Label)[] COLUMNS =
    [
        (TableColumns.Index, "#"), (TableColumns.TagId, "Tag ID"), (TableColumns.TagName, "Tag name"), (TableColumns.Value, "Value"),
    ];

    private static readonly JsonWriterOptions WRITER_OPTIONS = new() { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// The file extension of a format, without the dot.
    /// </summary>
    public static string Extension(ExportFormat format) => format switch
    {
        ExportFormat.Csv => "csv",
        ExportFormat.PlainText => "txt",
        _ => "json",
    };


    public static string Export(ExportFormat format, IReadOnlyList<ViewRow> rows, TableColumns columns, string sourcePath) => format switch
    {
        ExportFormat.Csv => ToCsv(rows, columns),
        ExportFormat.PlainText => ToPlainText(rows, columns),
        _ => ToExifToolJson(rows, sourcePath),
    };


    /// <summary>
    /// CSV: a Group column, then the visible columns; RFC 4180 quoting and CRLF; written with a BOM so spreadsheets read UTF-8.
    /// </summary>
    public static string ToCsv(IReadOnlyList<ViewRow> rows, TableColumns columns)
    {
        var visible = Visible(columns);
        var sb = new StringBuilder();
        sb.Append(Quote("Group"));
        foreach (var (_, label) in visible) sb.Append(',').Append(Quote(label));
        sb.Append("\r\n");

        foreach (var r in rows)
        {
            sb.Append(Quote(r.Group));
            foreach (var (column, _) in visible) sb.Append(',').Append(Quote(Cell(r, column)));
            sb.Append("\r\n");
        }

        return sb.ToString();
    }


    /// <summary>
    /// Plain text like exiftool -t: "[Group]", then the visible columns, tab-separated.
    /// </summary>
    public static string ToPlainText(IReadOnlyList<ViewRow> rows, TableColumns columns)
    {
        var visible = Visible(columns);
        var sb = new StringBuilder();
        foreach (var r in rows)
        {
            sb.Append('[').Append(r.Group).Append(']');
            foreach (var (column, _) in visible) sb.Append('\t').Append(OneLine(Cell(r, column)));
            sb.Append('\n');
        }

        return sb.ToString();
    }


    /// <summary>
    /// JSON compatible with exiftool -j -G: [{"SourceFile": ..., "Group:TagName": value}]; numbers stay numbers, AI rows go under "AI".
    /// </summary>
    public static string ToExifToolJson(IReadOnlyList<ViewRow> rows, string sourcePath)
    {
        var buffer = new ArrayBufferWriter<byte>(8192);
        using (var w = new Utf8JsonWriter(buffer, WRITER_OPTIONS))
        {
            w.WriteStartArray();
            w.WriteStartObject();
            w.WriteString("SourceFile", sourcePath.Replace('\\', '/'));

            // like -j: one value per key, the first wins
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in rows)
            {
                var key = r.Group + ":" + KeyName(r);
                if (!seen.Add(key)) continue;

                w.WritePropertyName(key);
                WriteValue(w, r);
            }

            w.WriteEndObject();
            w.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }


    /// <summary>
    /// Copy of selected rows: "Tag name\tValue" per line.
    /// </summary>
    public static string ToTsv(IReadOnlyList<ViewRow> rows) => string.Join("\n", rows.Select(r => $"{r.Name}\t{OneLine(r.Value)}"));


    /// <summary>
    /// Copy of selected rows as JSON: one object with group-prefixed keys.
    /// </summary>
    public static string ToJsonObject(IReadOnlyList<ViewRow> rows)
    {
        var buffer = new ArrayBufferWriter<byte>(4096);
        using (var w = new Utf8JsonWriter(buffer, WRITER_OPTIONS))
        {
            w.WriteStartObject();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in rows)
            {
                var key = r.Group + ":" + KeyName(r);
                if (!seen.Add(key)) continue;

                w.WritePropertyName(key);
                WriteValue(w, r);
            }

            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }


    /// <summary>
    /// Writes an export to a stream as UTF-8; CSV gets a BOM.
    /// </summary>
    public static async Task WriteAsync(Stream destination, ExportFormat format, string content, CancellationToken cancellationToken = default)
    {
        if (format == ExportFormat.Csv) await destination.WriteAsync(Encoding.UTF8.Preamble.ToArray(), cancellationToken).ConfigureAwait(false);
        await destination.WriteAsync(Encoding.UTF8.GetBytes(content), cancellationToken).ConfigureAwait(false);
        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    #endregion // Public methods


    #region Private methods

    private static List<(TableColumns Column, string Label)> Visible(TableColumns columns) => [.. COLUMNS.Where(c => (columns & c.Column) != 0)];


    private static string Cell(ViewRow r, TableColumns column) => column switch
    {
        TableColumns.Index => r.Index > 0 ? r.Index.ToString(CultureInfo.InvariantCulture) : string.Empty,
        TableColumns.TagId => r.DisplayId,
        TableColumns.TagName => r.Name,
        _ => r.Value,
    };


    private static string Quote(string s) => "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";


    // a value with a line break would split the row in a tab-separated file
    private static string OneLine(string s) => s.Contains('\n', StringComparison.Ordinal) || s.Contains('\r', StringComparison.Ordinal)
        ? s.Replace("\r\n", "\\n", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal).Replace("\r", "\\n", StringComparison.Ordinal)
        : s;


    private static string KeyName(ViewRow r) => r.Tag?.Name ?? r.TagName;


    private static void WriteValue(Utf8JsonWriter w, ViewRow r)
    {
        if (r.Tag is { Kind: TagValueKind.Number } && r.EditState == Editing.EditState.None && IsJsonNumber(r.Value))
        {
            w.WriteRawValue(r.Value, skipInputValidation: true);
        }
        else if (r.Tag is { Kind: TagValueKind.Bool } && r.EditState == Editing.EditState.None)
        {
            w.WriteBooleanValue(r.Value.Equals("True", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            w.WriteStringValue(r.Value);
        }
    }


    private static bool IsJsonNumber(string s)
    {
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(s));
        try
        {
            return reader.Read() && reader.TokenType == JsonTokenType.Number && reader.BytesConsumed == Encoding.UTF8.GetByteCount(s);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    #endregion // Private methods
}
