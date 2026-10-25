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

// Renders the in-app logo PNGs (1x-3x for ScaledImage, 256 px for the Linux window icon) from __assets/__app/logo.svg with resvg 0.47. Run from source/tools: dotnet run gen-app-icons.cs
using System.Diagnostics;
using System.Runtime.CompilerServices;

var toolsDir = Path.GetDirectoryName(ThisFile())!;
var sourceDir = Path.GetFullPath(Path.Combine(toolsDir, ".."));
var logo = Path.Combine(sourceDir, "__assets", "__app", "logo.svg");
var outDir = Path.Combine(sourceDir, "ExifGlass.UI", "Assets", "Logo");
var resvg = FindResvg(sourceDir);

// the boxes the UI draws the logo in (DS 6.9 empty card 60, drag scrim 80, About 104: FewerPhotos's 128 icon draws its art in about 103, small cards 48)
int[] boxes = [48, 60, 80, 104];
var sizes = new SortedSet<int> { 256 };
foreach (var box in boxes)
{
    for (var scale = 1; scale <= 3; scale++) sizes.Add(box * scale);
}

Directory.CreateDirectory(outDir);
foreach (var old in Directory.GetFiles(outDir, "logo-*.png")) File.Delete(old);

foreach (var px in sizes)
{
    var output = Path.Combine(outDir, $"logo-{px}.png");
    using var process = Process.Start(new ProcessStartInfo(resvg, ["--width", $"{px}", "--height", $"{px}", logo, output]) { RedirectStandardError = true })!;
    process.StandardError.ReadToEnd();
    process.WaitForExit();
    if (process.ExitCode != 0) throw new InvalidOperationException($"resvg failed for {px} px");
}

Console.WriteLine($"{sizes.Count} logo PNGs in {outDir}: {string.Join(", ", sizes)}");
return 0;


static string ThisFile([CallerFilePath] string path = "") => path;


// $RESVG, else the copy cached in source/__artifacts/tools/resvg, else PATH
static string FindResvg(string sourceDir)
{
    var exe = OperatingSystem.IsWindows() ? "resvg.exe" : "resvg";
    if (Environment.GetEnvironmentVariable("RESVG") is { Length: > 0 } env && File.Exists(env)) return env;

    var cached = Path.Combine(sourceDir, "__artifacts", "tools", "resvg", exe);
    return File.Exists(cached) ? cached : exe;
}
