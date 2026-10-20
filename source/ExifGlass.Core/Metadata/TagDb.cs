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
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace ExifGlass.Core.Metadata;

/// <summary>
/// Flags from exiftool -listx -f, plus Writable.
/// </summary>
[Flags]
public enum TagDefFlags : ushort
{
    None = 0,
    Writable = 1 << 0,
    List = 1 << 1,
    Bag = 1 << 2,
    Seq = 1 << 3,
    Alt = 1 << 4,
    Avoid = 1 << 5,
    Unsafe = 1 << 6,
    Protected = 1 << 7,
    Permanent = 1 << 8,
    Mandatory = 1 << 9,
    Binary = 1 << 10,
    Flattened = 1 << 11,
    Unknown = 1 << 12,
    Struct = 1 << 13,
}


/// <summary>
/// One tag definition from exiftool -listx.
/// </summary>
public readonly record struct TagDef(string Group0, string Group1, string Name, string Type, int Count, TagDefFlags Flags)
{
    #region Properties

    public bool IsWritable => (Flags & TagDefFlags.Writable) != 0;

    /// <summary>
    /// The Format row (D18): "rational64u", "int16u[2]", or "—" for an unknown type.
    /// </summary>
    public string Format => Type is "" or "?" ? TagIdRule.NONE : Count > 1 ? $"{Type}[{Count}]" : Type;

    #endregion // Properties
}


/// <summary>
/// ExifTool's tag table as a compact sorted binary: writable flag, type and groups for each tag (S5).
/// </summary>
public sealed class TagDb
{
    #region Fields and constants

    public const string RESOURCE_NAME = "ExifGlass.Core.TagDb.bin.br";

    private const uint MAGIC = 0x31424454; // "TDB1"
    private const int FIELDS = 6;

    private static readonly Lock _embeddedLock = new();
    private static TagDb? _embedded;

    private readonly byte[] _data;
    private readonly int[] _stringOffsets;
    private readonly int[] _records;

    #endregion // Fields and constants


    #region Constructors

    private TagDb(byte[] data)
    {
        var span = (ReadOnlySpan<byte>)data;
        if (data.Length < 4 || BinaryPrimitives.ReadUInt32LittleEndian(span) != MAGIC) throw new InvalidDataException("Not a TagDb");

        _data = data;
        var pos = 4;
        ExifToolVersion = ReadString(span, ref pos);

        var n = (int)ReadVar(span, ref pos);
        _stringOffsets = new int[n];
        for (var i = 0; i < n; i++)
        {
            _stringOffsets[i] = pos;

            // not "pos += ReadVar(ref pos)": the left pos would be read before the call moves it
            var length = (int)ReadVar(span, ref pos);
            pos += length;
        }

        var count = (int)ReadVar(span, ref pos);
        _records = new int[count * FIELDS];
        for (var i = 0; i < _records.Length; i++) _records[i] = (int)ReadVar(span, ref pos);
    }

    #endregion // Constructors


    #region Properties

    /// <summary>
    /// The ExifTool version the table came from.
    /// </summary>
    public string ExifToolVersion { get; }

    public int Count => _records.Length / FIELDS;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// The table generated from the bundled ExifTool, decompressed once; null when the resource is missing.
    /// </summary>
    public static TagDb? LoadEmbedded()
    {
        lock (_embeddedLock)
        {
            if (_embedded is not null) return _embedded;

            using var stream = typeof(TagDb).Assembly.GetManifestResourceStream(RESOURCE_NAME);
            if (stream is null) return null;

            using var brotli = new BrotliStream(stream, CompressionMode.Decompress);
            using var buffer = new MemoryStream();
            brotli.CopyTo(buffer);

            return _embedded = new TagDb(buffer.ToArray());
        }
    }


    /// <summary>
    /// Loads the binary form (not compressed).
    /// </summary>
    public static TagDb FromBinary(byte[] data) => new(data);


    /// <summary>
    /// Builds the table straight from exiftool -listx -s -f output, for a custom ExifTool of another version.
    /// </summary>
    public static TagDb FromListx(ReadOnlySpan<byte> xml, string exifToolVersion) => new(ToBinary(ListxParser.Parse(xml), exifToolVersion));


    /// <summary>
    /// Writes definitions as the binary form: one record per (group0, group1, name), sorted by (group0, name, group1).
    /// </summary>
    public static byte[] ToBinary(IReadOnlyList<TagDef> defs, string exifToolVersion)
    {
        var unique = defs
            .GroupBy(d => (d.Group0, d.Group1, d.Name))
            .Select(g => g.OrderByDescending(d => d.IsWritable).ThenBy(d => d.Type == "?" ? 1 : 0).First())
            .OrderBy(d => d.Group0, StringComparer.Ordinal)
            .ThenBy(d => d.Name, StringComparer.Ordinal)
            .ThenBy(d => d.Group1, StringComparer.Ordinal)
            .ToList();

        var strings = new List<string>();
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        int Id(string s)
        {
            if (!index.TryGetValue(s, out var i))
            {
                i = strings.Count;
                strings.Add(s);
                index[s] = i;
            }

            return i;
        }

        foreach (var d in unique)
        {
            Id(d.Group0);
            Id(d.Group1);
            Id(d.Name);
            Id(d.Type);
        }

        var w = new ArrayBufferWriter<byte>(1 << 20);
        BinaryPrimitives.WriteUInt32LittleEndian(w.GetSpan(4), MAGIC);
        w.Advance(4);
        WriteString(w, exifToolVersion);
        WriteVar(w, (uint)strings.Count);
        foreach (var s in strings) WriteString(w, s);

        WriteVar(w, (uint)unique.Count);
        foreach (var d in unique)
        {
            WriteVar(w, (uint)index[d.Group0]);
            WriteVar(w, (uint)index[d.Group1]);
            WriteVar(w, (uint)index[d.Name]);
            WriteVar(w, (uint)index[d.Type]);
            WriteVar(w, (uint)d.Count);
            WriteVar(w, (uint)d.Flags);
        }

        return w.WrittenSpan.ToArray();
    }


    /// <summary>
    /// Finds a tag by family-0 group and name, preferring the entry of the given family-1 group.
    /// </summary>
    public bool TryFind(string group0, string name, string? group1, out TagDef def)
    {
        def = default;
        Span<byte> g0 = stackalloc byte[128];
        Span<byte> nm = stackalloc byte[512];
        if (!Encoding.UTF8.TryGetBytes(group0, g0, out var g0Length) || !Encoding.UTF8.TryGetBytes(name, nm, out var nameLength)) return false;

        var first = LowerBound(g0[..g0Length], nm[..nameLength]);
        if (first < 0) return false;

        var chosen = first;
        Span<byte> g1 = stackalloc byte[128];
        if (group1 is not null && Encoding.UTF8.TryGetBytes(group1, g1, out var g1Length))
        {
            for (var i = first; i < Count && Matches(i, g0[..g0Length], nm[..nameLength]); i++)
            {
                if (Utf8(_records[(i * FIELDS) + 1]).SequenceEqual(g1[..g1Length]))
                {
                    chosen = i;
                    break;
                }
            }
        }

        var r = chosen * FIELDS;
        def = new TagDef(group0, Text(_records[r + 1]), name, Text(_records[r + 3]), _records[r + 4], (TagDefFlags)_records[r + 5]);
        return true;
    }

    #endregion // Public methods


    #region Private methods

    // the first record with this (group0, name), or -1
    private int LowerBound(ReadOnlySpan<byte> group0, ReadOnlySpan<byte> name)
    {
        int lo = 0, hi = Count - 1, found = -1;
        while (lo <= hi)
        {
            var mid = (lo + hi) >>> 1;
            var c = Compare(mid, group0, name);
            if (c == 0) found = mid;
            if (c >= 0) hi = mid - 1;
            else lo = mid + 1;
        }

        return found;
    }


    private int Compare(int record, ReadOnlySpan<byte> group0, ReadOnlySpan<byte> name)
    {
        var c = Utf8(_records[record * FIELDS]).SequenceCompareTo(group0);
        return c != 0 ? c : Utf8(_records[(record * FIELDS) + 2]).SequenceCompareTo(name);
    }


    private bool Matches(int record, ReadOnlySpan<byte> group0, ReadOnlySpan<byte> name) => Compare(record, group0, name) == 0;


    private ReadOnlySpan<byte> Utf8(int stringIndex)
    {
        var pos = _stringOffsets[stringIndex];
        var span = (ReadOnlySpan<byte>)_data;
        var length = (int)ReadVar(span, ref pos);
        return span.Slice(pos, length);
    }


    private string Text(int stringIndex) => Encoding.UTF8.GetString(Utf8(stringIndex));


    private static void WriteVar(ArrayBufferWriter<byte> w, uint v)
    {
        var s = w.GetSpan(5);
        var i = 0;
        while (v >= 0x80)
        {
            s[i++] = (byte)(v | 0x80);
            v >>= 7;
        }

        s[i++] = (byte)v;
        w.Advance(i);
    }


    private static uint ReadVar(ReadOnlySpan<byte> d, ref int pos)
    {
        uint v = 0;
        var shift = 0;
        while (true)
        {
            var b = d[pos++];
            v |= (uint)(b & 0x7F) << shift;
            if (b < 0x80) return v;
            shift += 7;
        }
    }


    private static void WriteString(ArrayBufferWriter<byte> w, string s)
    {
        var n = Encoding.UTF8.GetByteCount(s);
        WriteVar(w, (uint)n);
        Encoding.UTF8.GetBytes(s, w.GetSpan(n));
        w.Advance(n);
    }


    private static string ReadString(ReadOnlySpan<byte> d, ref int pos)
    {
        var n = (int)ReadVar(d, ref pos);
        var s = Encoding.UTF8.GetString(d.Slice(pos, n));
        pos += n;
        return s;
    }

    #endregion // Private methods
}


/// <summary>
/// Reads exiftool -listx -s -f without System.Xml: the output has one element per line.
/// </summary>
public static class ListxParser
{
    #region Public methods

    public static List<TagDef> Parse(ReadOnlySpan<byte> xml)
    {
        var list = new List<TagDef>(40_000);
        string g0 = string.Empty, g1 = string.Empty;
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);

        string Intern(ReadOnlySpan<byte> s)
        {
            var str = Encoding.UTF8.GetString(s);
            if (strings.TryGetValue(str, out var v)) return v;
            strings[str] = str;
            return str;
        }

        while (!xml.IsEmpty)
        {
            var nl = xml.IndexOf((byte)'\n');
            var line = nl < 0 ? xml : xml[..nl];
            xml = nl < 0 ? default : xml[(nl + 1)..];
            var t = line.TrimStart((byte)' ');

            if (t.StartsWith("<table "u8))
            {
                g0 = Intern(Attr(t, "g0"u8));
                g1 = Intern(Attr(t, "g1"u8));
            }
            else if (t.StartsWith("<tag "u8))
            {
                var tg0 = Attr(t, "g0"u8);
                var tg1 = Attr(t, "g1"u8);
                var flags = Attr(t, "writable"u8).SequenceEqual("true"u8) ? TagDefFlags.Writable : TagDefFlags.None;
                var fl = Attr(t, "flags"u8);
                if (!fl.IsEmpty)
                {
                    foreach (var range in fl.Split((byte)','))
                    {
                        flags |= Flag(fl[range]);
                    }
                }

                if (!Attr(t, "struct"u8).IsEmpty) flags |= TagDefFlags.Struct;

                var count = Attr(t, "count"u8);
                list.Add(new TagDef(
                    tg0.IsEmpty ? g0 : Intern(tg0),
                    tg1.IsEmpty ? g1 : Intern(tg1),
                    Intern(Attr(t, "name"u8)),
                    Intern(Attr(t, "type"u8)),
                    count.IsEmpty || !int.TryParse(count, out var c) ? 0 : c,
                    flags));
            }
        }

        return list;
    }

    #endregion // Public methods


    #region Private methods

    private static TagDefFlags Flag(ReadOnlySpan<byte> s) => s switch
    {
        _ when s.SequenceEqual("List"u8) => TagDefFlags.List,
        _ when s.SequenceEqual("Bag"u8) => TagDefFlags.Bag,
        _ when s.SequenceEqual("Seq"u8) => TagDefFlags.Seq,
        _ when s.SequenceEqual("Alt"u8) => TagDefFlags.Alt,
        _ when s.SequenceEqual("Avoid"u8) => TagDefFlags.Avoid,
        _ when s.SequenceEqual("Unsafe"u8) => TagDefFlags.Unsafe,
        _ when s.SequenceEqual("Protected"u8) => TagDefFlags.Protected,
        _ when s.SequenceEqual("Permanent"u8) => TagDefFlags.Permanent,
        _ when s.SequenceEqual("Mandatory"u8) => TagDefFlags.Mandatory,
        _ when s.SequenceEqual("Binary"u8) => TagDefFlags.Binary,
        _ when s.SequenceEqual("Flattened"u8) => TagDefFlags.Flattened,
        _ when s.SequenceEqual("Unknown"u8) => TagDefFlags.Unknown,
        _ => TagDefFlags.None,
    };


    private static ReadOnlySpan<byte> Attr(ReadOnlySpan<byte> line, ReadOnlySpan<byte> name)
    {
        Span<byte> key = stackalloc byte[name.Length + 3];
        key[0] = (byte)' ';
        name.CopyTo(key[1..]);
        key[^2] = (byte)'=';
        key[^1] = (byte)'\'';

        var i = line.IndexOf(key);
        if (i < 0) return default;

        var rest = line[(i + key.Length)..];
        var end = rest.IndexOf((byte)'\'');
        return end < 0 ? default : rest[..end];
    }

    #endregion // Private methods
}
