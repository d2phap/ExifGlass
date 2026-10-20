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

// Writes ExifGlass.Core/Metadata/TagDb.bin.br from the bundled ExifTool. Run from source/tools after updating ExifTool: dotnet run gen-tagdb.cs
using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;

var toolsDir = Path.GetDirectoryName(ThisFile())!;
var sourceDir = Path.GetFullPath(Path.Combine(toolsDir, ".."));
var outPath = Path.Combine(sourceDir, "ExifGlass.Core", "Metadata", "TagDb.bin.br");
var configPath = Path.Combine(sourceDir, "__assets", "exiftool", "exifglass.config");
var exifToolPath = Path.Combine(sourceDir, "__assets", "exiftool",
    OperatingSystem.IsWindows() ? Path.Combine("win", "exiftool.exe") : Path.Combine("unix", "exiftool"));

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--exiftool": exifToolPath = Path.GetFullPath(args[++i]); break;
        case "--out": outPath = Path.GetFullPath(args[++i]); break;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            return 2;
    }
}

if (!File.Exists(exifToolPath))
{
    Console.Error.WriteLine($"ExifTool not found: {exifToolPath} (pass --exiftool <path>)");
    return 2;
}

var watch = Stopwatch.StartNew();
var version = Run(exifToolPath, ["-ver"]).Trim();

// -s drops descriptions, -f adds flags; -config changes nothing here: -listx leaves out user-defined tags
var xml = Encoding.UTF8.GetBytes(Run(exifToolPath, ["-config", configPath, "-listx", "-s", "-f"]));
var defs = Listx.Parse(xml);
var binary = TagDbFormat.Write(defs, version, out var records);

using (var file = File.Create(outPath))
using (var brotli = new BrotliStream(file, CompressionLevel.SmallestSize))
{
    brotli.Write(binary);
}

Console.WriteLine($"ExifTool {version}: {defs.Count} definitions, {records} records, {binary.Length:N0} bytes, {new FileInfo(outPath).Length:N0} compressed, {watch.ElapsedMilliseconds} ms -> {outPath}");
return 0;


static string ThisFile([CallerFilePath] string path = "") => path;


// a checkout of the Perl script may have lost its executable bit, so scripts run through perl
static string Run(string exe, string[] args)
{
    var script = IsScript(exe);
    var psi = new ProcessStartInfo(script ? "perl" : exe) { RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8 };
    if (script) psi.ArgumentList.Add(exe);
    foreach (var a in args) psi.ArgumentList.Add(a);

    using var p = Process.Start(psi)!;
    var stdout = p.StandardOutput.ReadToEndAsync();
    var stderr = p.StandardError.ReadToEndAsync();
    p.WaitForExit();
    if (p.ExitCode != 0) throw new InvalidOperationException($"ExifTool failed: {stderr.Result}");
    return stdout.Result;
}


static bool IsScript(string path)
{
    using var fs = File.OpenRead(path);
    return fs.ReadByte() == '#' && fs.ReadByte() == '!';
}


/// <summary>
/// One tag definition from exiftool -listx; the same fields and flag bits as ExifGlass.Core's TagDef.
/// </summary>
readonly record struct TagDef(string Group0, string Group1, string Name, string Type, int Count, ushort Flags);


/// <summary>
/// Reads exiftool -listx -s -f line by line, as ExifGlass.Core's ListxParser does.
/// </summary>
static class Listx
{
    #region Fields and constants

    // bit order of ExifGlass.Core.Metadata.TagDefFlags (Writable is bit 0, Struct bit 13)
    private static readonly string[] FLAGS = ["List", "Bag", "Seq", "Alt", "Avoid", "Unsafe", "Protected", "Permanent", "Mandatory", "Binary", "Flattened", "Unknown"];

    #endregion // Fields and constants


    #region Public methods

    public static List<TagDef> Parse(byte[] xml)
    {
        var list = new List<TagDef>(40_000);
        string g0 = "", g1 = "";

        foreach (var raw in Encoding.UTF8.GetString(xml).Split('\n'))
        {
            var t = raw.TrimStart(' ');
            if (t.StartsWith("<table ", StringComparison.Ordinal))
            {
                g0 = Attr(t, "g0");
                g1 = Attr(t, "g1");
            }
            else if (t.StartsWith("<tag ", StringComparison.Ordinal))
            {
                ushort flags = Attr(t, "writable") == "true" ? (ushort)1 : (ushort)0;
                foreach (var f in Attr(t, "flags").Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var i = Array.IndexOf(FLAGS, f);
                    if (i >= 0) flags |= (ushort)(1 << (i + 1));
                }

                if (Attr(t, "struct").Length > 0) flags |= 1 << 13;

                var tg0 = Attr(t, "g0");
                var tg1 = Attr(t, "g1");
                list.Add(new TagDef(tg0.Length > 0 ? tg0 : g0, tg1.Length > 0 ? tg1 : g1, Attr(t, "name"), Attr(t, "type"),
                    int.TryParse(Attr(t, "count"), out var c) ? c : 0, flags));
            }
        }

        return list;
    }

    #endregion // Public methods


    #region Private methods

    private static string Attr(string line, string name)
    {
        var key = " " + name + "='";
        var i = line.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return string.Empty;

        var start = i + key.Length;
        var end = line.IndexOf('\'', start);
        return end < 0 ? string.Empty : line[start..end];
    }

    #endregion // Private methods
}


/// <summary>
/// The binary form ExifGlass.Core's TagDb reads: "TDB1", the version, a string table, then sorted varint records.
/// </summary>
static class TagDbFormat
{
    #region Public methods

    public static byte[] Write(List<TagDef> defs, string version, out int records)
    {
        var unique = defs
            .GroupBy(d => (d.Group0, d.Group1, d.Name))
            .Select(g => g.OrderByDescending(d => (d.Flags & 1) != 0).ThenBy(d => d.Type == "?" ? 1 : 0).First())
            .OrderBy(d => d.Group0, StringComparer.Ordinal).ThenBy(d => d.Name, StringComparer.Ordinal).ThenBy(d => d.Group1, StringComparer.Ordinal)
            .ToList();
        records = unique.Count;

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
        BinaryPrimitives.WriteUInt32LittleEndian(w.GetSpan(4), 0x31424454);
        w.Advance(4);
        Str(w, version);
        Var(w, (uint)strings.Count);
        foreach (var s in strings) Str(w, s);

        Var(w, (uint)unique.Count);
        foreach (var d in unique)
        {
            Var(w, (uint)index[d.Group0]);
            Var(w, (uint)index[d.Group1]);
            Var(w, (uint)index[d.Name]);
            Var(w, (uint)index[d.Type]);
            Var(w, (uint)d.Count);
            Var(w, d.Flags);
        }

        return w.WrittenSpan.ToArray();
    }

    #endregion // Public methods


    #region Private methods

    private static void Var(ArrayBufferWriter<byte> w, uint v)
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


    private static void Str(ArrayBufferWriter<byte> w, string s)
    {
        var n = Encoding.UTF8.GetByteCount(s);
        Var(w, (uint)n);
        Encoding.UTF8.GetBytes(s, w.GetSpan(n));
        w.Advance(n);
    }

    #endregion // Private methods
}
