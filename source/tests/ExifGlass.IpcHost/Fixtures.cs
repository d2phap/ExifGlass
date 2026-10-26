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
using System.Buffers.Binary;
using System.Text;

namespace ExifGlass.IpcHost;

/// <summary>
/// The files a session browses, made the same on every OS: 100 copies of the AI fixtures ending on an A1111 PNG, two slow PNGs and four Unicode names.
/// </summary>
public sealed class Fixtures
{
    #region Fields and constants

    public static readonly string[] UnicodeNames =
    [
        Path.Combine("unicode", "Ảnh đẹp – Hà Nội 日本語 😀.jpg"),
        Path.Combine("unicode", "Résumé ñ ŞĞÜ ÿ.png"),
        Path.Combine("unicode", "Тест фото.jpg"),
        Path.Combine("unicode", "中文 目录", "照片 #1.png"),
    ];

    #endregion // Fields and constants


    #region Properties

    public required string Dir { get; init; }

    public required string[] Burst { get; init; }

    /// <summary>
    /// A 3.7 MB workflow chunk: ExifTool busy for about 150 ms.
    /// </summary>
    public required string Slow { get; init; }

    /// <summary>
    /// A 37 MB workflow chunk: ExifTool busy for over 500 ms.
    /// </summary>
    public required string SlowXl { get; init; }

    public required string[] Unicode { get; init; }

    #endregion // Properties


    #region Public methods

    public static Fixtures Ensure(string dir, string aiFixtures)
    {
        var burstDir = Path.Combine(dir, "burst");
        var slow = Path.Combine(dir, "slow", "huge-text.png");
        var slowXl = Path.Combine(dir, "slow", "huge-text-xl.png");
        var unicode = UnicodeNames.Select(n => Path.Combine(dir, n)).ToArray();

        if (!Directory.Exists(burstDir) || Directory.GetFiles(burstDir).Length < 100 || !unicode.All(File.Exists))
        {
            var sources = Directory.GetFiles(aiFixtures).Where(f => f.EndsWith(".jpg", StringComparison.Ordinal) || f.EndsWith(".png", StringComparison.Ordinal))
                .Order(StringComparer.Ordinal).ToArray();
            if (sources.Length == 0) throw new DirectoryNotFoundException($"No AI fixtures in {aiFixtures}");

            Directory.CreateDirectory(burstDir);
            for (var i = 0; i < 100; i++)
            {
                // the last photo carries A1111 data, so "AI data follows navigation" has something to show
                var source = i == 99 ? Path.Combine(aiFixtures, "a1111.png") : sources[i % sources.Length];
                File.Copy(source, Path.Combine(burstDir, $"p{i:000}{Path.GetExtension(source)}"), overwrite: true);
            }

            var jpg = sources.First(s => s.EndsWith(".jpg", StringComparison.Ordinal));
            var png = sources.First(s => s.EndsWith(".png", StringComparison.Ordinal));
            foreach (var u in unicode)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(u)!);
                File.Copy(u.EndsWith(".png", StringComparison.Ordinal) ? png : jpg, u, overwrite: true);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(slow)!);
        if (!File.Exists(slow)) WritePng(slow, "workflow", WorkflowJson(120_000));
        if (!File.Exists(slowXl)) WritePng(slowXl, "workflow", WorkflowJson(1_200_000));

        return new Fixtures
        {
            Dir = dir,
            Burst = [.. Directory.GetFiles(burstDir).Order(StringComparer.Ordinal)],
            Slow = slow,
            SlowXl = slowXl,
            Unicode = unicode,
        };
    }

    #endregion // Public methods


    #region Private methods

    private static string WorkflowJson(int nodes) =>
        "{\"nodes\":[" + string.Join(",", Enumerable.Range(0, nodes).Select(i => $"{{\"id\":{i},\"type\":\"KSampler\"}}")) + "]}";


    // a 1x1 gray PNG with one tEXt chunk
    private static void WritePng(string path, string keyword, string text)
    {
        using var fs = File.Create(path);
        fs.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        Chunk(fs, "IHDR", [0, 0, 0, 1, 0, 0, 0, 1, 8, 0, 0, 0, 0]);
        Chunk(fs, "tEXt", [.. Encoding.Latin1.GetBytes(keyword), 0, .. Encoding.Latin1.GetBytes(text)]);
        Chunk(fs, "IDAT", [0x78, 0x9C, 0x63, 0x60, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01]);
        Chunk(fs, "IEND", []);
    }


    private static void Chunk(Stream stream, string type, byte[] data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(number, (uint)data.Length);
        stream.Write(number);

        var typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(number, Crc32([.. typeBytes, .. data]));
        stream.Write(number);
    }


    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return ~crc;
    }

    #endregion // Private methods
}
