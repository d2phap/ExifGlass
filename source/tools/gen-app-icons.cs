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

// Renders every app icon from __assets/__app/logo.svg with resvg 0.47 (plan 5.8): the in-app logo PNGs, the .ico, the MSIX tiles, the .icns and the Linux icon. Run from source/tools: dotnet run gen-app-icons.cs
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

var toolsDir = Path.GetDirectoryName(ThisFile())!;
var sourceDir = Path.GetFullPath(Path.Combine(toolsDir, ".."));
var logoSvg = File.ReadAllText(Path.Combine(sourceDir, "__assets", "__app", "logo.svg"));
var viewBox = Regex.Match(logoSvg, "viewBox=\"([^\"]+)\"").Groups[1].Value;
var resvg = FindResvg(sourceDir);
var workDir = Directory.CreateTempSubdirectory("eg-icons-").FullName;

try
{
    // the app: the boxes the UI draws the logo in at 1x-3x (DS 6.9 empty card 60, drag scrim 80, About 104, small cards 48), and 256 for the Linux window icon
    var logoDir = Path.Combine(sourceDir, "ExifGlass.UI", "Assets", "Logo");
    Directory.CreateDirectory(logoDir);
    foreach (var old in Directory.GetFiles(logoDir, "logo-*.png")) File.Delete(old);

    var sizes = new SortedSet<int> { 256 };
    foreach (var box in new[] { 48, 60, 80, 104 })
    {
        for (var scale = 1; scale <= 3; scale++) sizes.Add(box * scale);
    }

    foreach (var px in sizes) File.Copy(Render(px, px), Path.Combine(logoDir, $"logo-{px}.png"));

    // Windows: the exe's and every window's icon, PNG frames
    int[] icoSizes = [16, 20, 24, 32, 40, 48, 64, 256];
    WriteIco(Path.Combine(sourceDir, "ExifGlass.UI", "Assets", "ExifGlass.ico"), icoSizes.Select(n => (n, Render(n, n))));

    // MSIX tiles under the names AppxManifest.xml uses; makepri picks the scale or target size per DPI
    var appx = Fresh(Path.Combine(sourceDir, "__assets", "win", "appxmanifest", "Assets"));
    int[] scales = [100, 125, 150, 200, 400];
    (string Name, int Width, int Height)[] tiles =
    [
        ("EXIFGLASS-Square44x44Logo", 44, 44), ("EXIFGLASS-Square71x71Logo", 71, 71), ("EXIFGLASS-Square150x150Logo", 150, 150),
        ("EXIFGLASS-Square310x310Logo", 310, 310), ("EXIFGLASS-Wide310x150Logo", 310, 150), ("StoreLogo", 50, 50),
    ];

    foreach (var (name, width, height) in tiles)
    {
        foreach (var scale in scales)
        {
            File.Copy(Render((int)Math.Round(width * scale / 100.0), (int)Math.Round(height * scale / 100.0)), Path.Combine(appx, $"{name}.scale-{scale}.png"));
        }
    }

    // the taskbar and Start list icon, plated and unplated alike: the logo carries its own plate
    foreach (var n in new[] { 16, 24, 32, 48, 256 })
    {
        var png = Render(n, n);
        File.Copy(png, Path.Combine(appx, $"EXIFGLASS-Square44x44Logo.targetsize-{n}.png"));
        File.Copy(png, Path.Combine(appx, $"EXIFGLASS-Square44x44Logo.targetsize-{n}_altform-unplated.png"));
        File.Copy(png, Path.Combine(appx, $"EXIFGLASS-Square44x44Logo.altform-lightunplated_targetsize-{n}.png"));
    }

    // macOS: the bundle icon (CFBundleIconFile)
    WriteIcns(Path.Combine(sourceDir, "__assets", "mac", "logo.icns"), n => Render(n, n));

    // Linux: the Flatpak's and the AppImage's icon, named after the app id
    var linux = Path.Combine(sourceDir, "__assets", "linux", "flatpak");
    File.Copy(Render(512, 512), Path.Combine(linux, "io.github.d2phap.exifglass.png"), overwrite: true);
    File.WriteAllText(Path.Combine(linux, "io.github.d2phap.exifglass.svg"), logoSvg);

    Console.WriteLine($"Done: {sizes.Count} logo PNGs and ExifGlass.ico in ExifGlass.UI/Assets, {Directory.GetFiles(appx).Length} MSIX tiles, __assets/mac/logo.icns, the Linux icons in __assets/linux/flatpak");
    return 0;
}
finally
{
    Directory.Delete(workDir, true);
}


// a wide canvas centres the square logo, as the Store's wide tile does
string Render(int width, int height)
{
    var input = Path.Combine(workDir, $"{Guid.NewGuid():N}.svg");
    var output = Path.ChangeExtension(input, ".png");
    if (width == height)
    {
        File.WriteAllText(input, logoSvg);
    }
    else
    {
        var side = Math.Min(width, height);
        var inner = Regex.Replace(logoSvg, @"^\s*<svg[^>]*>|</svg>\s*$", "");
        File.WriteAllText(input, $"""<svg xmlns="http://www.w3.org/2000/svg" width="{width}" height="{height}" viewBox="0 0 {width} {height}"><svg x="{(width - side) / 2}" y="{(height - side) / 2}" width="{side}" height="{side}" viewBox="{viewBox}" fill="none">{inner}</svg></svg>""");
    }

    using var process = Process.Start(new ProcessStartInfo(resvg, ["--width", $"{width}", "--height", $"{height}", input, output]) { RedirectStandardError = true })!;
    var error = process.StandardError.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0) throw new InvalidOperationException($"resvg failed for {width}x{height}: {error}");

    return output;
}


static string Fresh(string dir)
{
    if (Directory.Exists(dir)) Directory.Delete(dir, true);
    return Directory.CreateDirectory(dir).FullName;
}


// every frame PNG-compressed: Windows reads them at any size since Vista
static void WriteIco(string path, IEnumerable<(int Size, string Png)> frames)
{
    var images = frames.Select(f => (f.Size, Bytes: File.ReadAllBytes(f.Png))).ToArray();
    using var file = File.Create(path);
    using var writer = new BinaryWriter(file);

    writer.Write((ushort)0);
    writer.Write((ushort)1);
    writer.Write((ushort)images.Length);

    var offset = 6 + 16 * images.Length;
    foreach (var (size, bytes) in images)
    {
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write(bytes.Length);
        writer.Write(offset);
        offset += bytes.Length;
    }

    foreach (var (_, bytes) in images) writer.Write(bytes);
}


// the PNG element types iconutil writes, a 1x and a 2x per point size
static void WriteIcns(string path, Func<int, string> render)
{
    (string Type, int Pixels)[] elements =
    [
        ("icp4", 16), ("ic11", 32), ("icp5", 32), ("ic12", 64), ("ic07", 128),
        ("ic13", 256), ("ic08", 256), ("ic14", 512), ("ic09", 512), ("ic10", 1024),
    ];

    var rendered = new Dictionary<int, byte[]>();
    var body = new List<byte>();
    Span<byte> header = stackalloc byte[8];

    foreach (var (type, pixels) in elements)
    {
        if (!rendered.TryGetValue(pixels, out var png)) rendered[pixels] = png = File.ReadAllBytes(render(pixels));

        System.Text.Encoding.ASCII.GetBytes(type, header);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], 8 + png.Length);
        body.AddRange(header.ToArray());
        body.AddRange(png);
    }

    System.Text.Encoding.ASCII.GetBytes("icns", header);
    BinaryPrimitives.WriteInt32BigEndian(header[4..], 8 + body.Count);
    using var file = File.Create(path);
    file.Write(header);
    file.Write(body.ToArray());
}


static string ThisFile([CallerFilePath] string path = "") => path;


// $RESVG, else the copy cached in source/__artifacts/tools/resvg, else PATH
static string FindResvg(string sourceDir)
{
    var exe = OperatingSystem.IsWindows() ? "resvg.exe" : "resvg";
    if (Environment.GetEnvironmentVariable("RESVG") is { Length: > 0 } env && File.Exists(env)) return env;

    var cached = Path.Combine(sourceDir, "__artifacts", "tools", "resvg", exe);
    return File.Exists(cached) ? cached : exe;
}
