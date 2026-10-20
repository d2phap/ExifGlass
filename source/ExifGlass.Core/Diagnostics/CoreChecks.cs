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
using ExifGlass.Core.Common;
using ExifGlass.Core.ExifTool;
using System.Diagnostics;
using System.Text.Json;

namespace ExifGlass.Core.Diagnostics;

/// <summary>
/// Self-test checks that need nothing but Core: build info, JSON under AOT, the bundled ExifTool.
/// </summary>
public static class CoreChecks
{
    #region Constants

    private static readonly TimeSpan EXIFTOOL_TIMEOUT = TimeSpan.FromSeconds(20);

    #endregion // Constants


    #region Public methods

    public static SelfTest AddCoreChecks(this SelfTest test) => test
        .Add("build-info", _ => Task.FromResult(CheckBuildInfo()))
        .Add("json", _ => Task.FromResult(CheckJson()))
        .Add("exiftool", CheckExifToolAsync);

    #endregion // Public methods


    #region Private methods

    private static string CheckBuildInfo()
    {
        var info = AppInfo.BuildInfo ?? throw new InvalidOperationException("AppInfo.BuildInfo isn't set by the app head");
        if (string.IsNullOrWhiteSpace(info.FullVersion)) throw new InvalidOperationException("The build has no version");

        return $"{info.FullVersion}, {info.Channel}, built {info.BuildDate}";
    }


    private static string CheckJson()
    {
        var probe = new SelfTestProbe("ExifGlass", 3, ["EXIF", "XMP", "AI"]);
        var json = JsonSerializer.Serialize(probe, CoreJsonContext.Default.SelfTestProbe);
        var back = JsonSerializer.Deserialize(json, CoreJsonContext.Default.SelfTestProbe);

        if (back is null || back.Name != probe.Name || back.Count != probe.Count || !back.Tags.SequenceEqual(probe.Tags))
        {
            throw new InvalidOperationException($"Round trip changed the payload: {json}");
        }

        return $"source-generated round trip, {json.Length} bytes";
    }


    private static async Task<string> CheckExifToolAsync(CancellationToken cancellationToken)
    {
        var path = BundledExifTool.FindPath() ?? throw new FileNotFoundException("No ExifTool next to the app");

        using var process = Process.Start(new ProcessStartInfo(path, "-ver")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException($"Couldn't start {path}");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(EXIFTOOL_TIMEOUT);

        try
        {
            var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);

            var version = (await stdout.ConfigureAwait(false)).Trim();
            if (process.ExitCode != 0 || version.Length == 0)
            {
                throw new InvalidOperationException($"exit {process.ExitCode}: {(await stderr.ConfigureAwait(false)).Trim()}");
            }

            return $"{version} at {path}";
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"No answer in {EXIFTOOL_TIMEOUT.TotalSeconds:0} s from {path}");
        }
    }

    #endregion // Private methods
}
