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
using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ExifGlass.Core.Ai;

/// <summary>
/// One tag of the AI read: its key parts and its JSON value.
/// </summary>
public sealed class AiTag(TagKey key, JsonElement value)
{
    #region Fields and constants

    private string? _text;

    #endregion // Fields and constants


    #region Properties

    public TagKey Key { get; } = key;

    public JsonElement Value { get; } = value;

    /// <summary>
    /// The key the same tag has in the table read, for "Show source tag".
    /// </summary>
    public string TableKey => Key.TableKey;

    public string Name => Key.Name;

    /// <summary>
    /// The value as text: strings as they are, numbers as JSON text, booleans as True/False, lists joined with ", ".
    /// </summary>
    public string Text => _text ??= Load();

    /// <summary>
    /// True when the text was UTF-8 stored in a Latin-1 PNG chunk and is shown repaired (S9).
    /// </summary>
    public bool WasRepaired { get; private set; }

    #endregion // Properties


    #region Private methods

    private string Load()
    {
        var text = AiTagSet.ToText(Value);
        if (Key.Group0 == "PNG" && Value.ValueKind == JsonValueKind.String && TextRepair.TryRepairMojibake(text) is { } repaired)
        {
            WasRepaired = true;
            return repaired;
        }

        return text;
    }

    #endregion // Private methods
}


/// <summary>
/// The tags of the AI read (-j -a -u -G0:1:3:4 -struct), kept in one JsonDocument for the parsers.
/// </summary>
public sealed class AiTagSet : IDisposable
{
    #region Fields and constants

    private readonly JsonDocument? _document;
    private readonly List<AiTag> _tags = [];

    #endregion // Fields and constants


    #region Constructors

    private AiTagSet(JsonDocument? document, string? sourceFile)
    {
        _document = document;
        SourceFile = sourceFile;
    }

    #endregion // Constructors


    #region Properties

    public string? SourceFile { get; }

    public IReadOnlyList<AiTag> Tags => _tags;

    public static AiTagSet Empty => new(null, null);

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Parses the AI read; the bytes are copied, so the set outlives the pipe buffer.
    /// </summary>
    public static AiTagSet Parse(in ReadOnlySequence<byte> json, StringPool? pool = null)
    {
        if (json.IsEmpty) return Empty;

        // our own copy: Parse(sequence) keeps pointing into the pipe buffer the next command reuses
        try
        {
            return FromDocument(JsonDocument.Parse(json.ToArray(), new JsonDocumentOptions { MaxDepth = 256 }), pool);
        }
        catch (JsonException)
        {
            // ExifTool printed no JSON (a missing or unreadable file): the table read reports why
            return Empty;
        }
    }


    /// <summary>
    /// Parses the AI read from text (tests and the self-test).
    /// </summary>
    public static AiTagSet Parse(string json, StringPool? pool = null) => Parse(new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(json)), pool);


    /// <summary>
    /// Builds a set from loose tags (tests, and the A1111 .txt sidecar).
    /// </summary>
    public static AiTagSet FromPairs(IEnumerable<(string Key, string Value)> pairs)
    {
        var sb = new StringBuilder("[{");
        var first = true;
        foreach (var (key, value) in pairs)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append(JsonSerializer.Serialize(key, AiJsonContext.Default.String)).Append(':').Append(JsonSerializer.Serialize(value, AiJsonContext.Default.String));
        }

        sb.Append("}]");
        return Parse(sb.ToString());
    }


    /// <summary>
    /// The first tag with this name in a family-0 or family-1 group ("PNG", "ExifIFD", "XMP-dc").
    /// </summary>
    public AiTag? Get(string group, string name)
    {
        foreach (var t in _tags)
        {
            if (Matches(t, group, name)) return t;
        }

        return null;
    }


    /// <summary>
    /// The first tag with this name in any of the groups, in the order given.
    /// </summary>
    public AiTag? GetFirst(string name, params ReadOnlySpan<string> groups)
    {
        foreach (var g in groups)
        {
            if (Get(g, name) is { } t) return t;
        }

        return null;
    }


    /// <summary>
    /// Every tag with this name in any group.
    /// </summary>
    public IEnumerable<AiTag> WithName(string name) => _tags.Where(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));


    /// <summary>
    /// Every tag of a family-0 or family-1 group.
    /// </summary>
    public IEnumerable<AiTag> InGroup(string group) =>
        _tags.Where(t => t.Key.Group0.Equals(group, StringComparison.OrdinalIgnoreCase) || t.Key.Group1.Equals(group, StringComparison.OrdinalIgnoreCase));


    public string? GetText(string group, string name) => Get(group, name)?.Text;


    public void Dispose() => _document?.Dispose();


    /// <summary>
    /// A JSON value as display text.
    /// </summary>
    public static string ToText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "True",
        JsonValueKind.False => "False",
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
        JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Select(ToText)),
        _ => value.GetRawText(),
    };


    /// <summary>
    /// A number from a JSON number or a numeric string.
    /// </summary>
    public static double? ToNumber(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Number when value.TryGetDouble(out var d) => d,
        JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
        _ => null,
    };

    #endregion // Public methods


    #region Private methods

    private static AiTagSet FromDocument(JsonDocument doc, StringPool? pool)
    {
        var root = doc.RootElement;
        var file = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0 ? root[0] : root;
        if (file.ValueKind != JsonValueKind.Object) return new AiTagSet(doc, null);

        string? source = file.TryGetProperty("SourceFile", out var s) ? s.GetString() : null;
        var set = new AiTagSet(doc, source);

        foreach (var property in file.EnumerateObject())
        {
            if (property.NameEquals("SourceFile")) continue;
            set._tags.Add(new AiTag(TagKey.Parse(property.Name, pool), property.Value));
        }

        return set;
    }


    private static bool Matches(AiTag t, string group, string name) =>
        t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
        && (t.Key.Group0.Equals(group, StringComparison.OrdinalIgnoreCase) || t.Key.Group1.Equals(group, StringComparison.OrdinalIgnoreCase));

    #endregion // Private methods
}
