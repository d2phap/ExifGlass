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
using ExifGlass.Core.ExifTool;
using System.Diagnostics;
using Xunit;

namespace ExifGlass.Core.Tests.ExifTool;

/// <summary>
/// The daemon against the real bundled ExifTool: framing, ordering, errors, timeouts, cancellation, exit.
/// </summary>
public class ExifToolDaemonTests
{
    #region Tests

    [Fact]
    public async Task StartsPrimesAndAnswersInOrder()
    {
        await using var daemon = New();
        var ct = TestContext.Current.CancellationToken;

        var version = await daemon.StartAsync(ct);
        Assert.Matches(@"^\d+\.\d+$", version);
        Assert.Equal(version, daemon.Version);
        Assert.True(daemon.ProcessId > 0);

        // queued from many threads, answered one by one with the right output each
        var tasks = Enumerable.Range(0, 20).Select(i => daemon.RunTextAsync(["-echo", $"line {i}", "-ver"], ct)).ToList();
        var results = await Task.WhenAll(tasks);
        for (var i = 0; i < results.Length; i++)
        {
            Assert.StartsWith($"line {i}", results[i].Stdout);
            Assert.Equal(0, results[i].Status);
        }
    }


    [Fact]
    public async Task ReportsStatusAndStderrOfAFailedCommand()
    {
        await using var daemon = New();

        var (stdout, stderr, status) = await daemon.RunTextAsync(["-j", Path.Combine(Path.GetTempPath(), "no-such-file-" + Guid.NewGuid() + ".jpg")], TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, stdout.Trim());
        Assert.Contains("File not found", stderr);
        Assert.Equal(1, status);

        // still in sync afterwards
        var (ver, _, ok) = await daemon.RunTextAsync(["-ver"], TestContext.Current.CancellationToken);
        Assert.Matches(@"^\d+\.\d+", ver.Trim());
        Assert.Equal(0, ok);
    }


    [Fact]
    public async Task ReadsUnicodeLongAndAwkwardPaths()
    {
        await using var daemon = New();
        var dir = Directory.CreateTempSubdirectory("exifglass-ảnh-写真-").FullName;
        try
        {
            var deep = Path.Combine(dir, new string('d', 60), new string('e', 60), new string('f', 60));
            Directory.CreateDirectory(deep);
            var names = new[] { Path.Combine(dir, "ảnh đẹp 😀.jpg"), Path.Combine(deep, "long path ünïcødé.jpg"), Path.Combine(dir, "-starts with dash.jpg") };

            foreach (var name in names)
            {
                File.Copy(TestEnvironment.Fixture("negative_camera.jpg"), name);
                var (stdout, stderr, status) = await daemon.RunTextAsync(ExifToolCommands.TableRead(name, []), TestContext.Current.CancellationToken);

                Assert.True(status == 0, $"{name}: {stderr}");
                Assert.Contains("\"EXIF:IFD0:Make\"", stdout);
            }
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }


    [Fact]
    public async Task ReturnsAtOnceWhenTheCallerCancelsAndStaysInSync()
    {
        await using var daemon = New();
        await daemon.StartAsync(TestContext.Current.CancellationToken);

        using var cts = new CancellationTokenSource();
        var slow = daemon.RunTextAsync(ExifToolCommands.AiRead(TestEnvironment.Fixture("comfyui_big_workflow.png")), cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => slow);

        var (stdout, _, _) = await daemon.RunTextAsync(["-ver"], TestContext.Current.CancellationToken);
        Assert.Matches(@"^\d+\.\d+", stdout.Trim());
    }


    [Fact]
    public async Task SkipsACommandCancelledBeforeItsTurn()
    {
        await using var daemon = New();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => daemon.RunTextAsync(["-ver"], cts.Token));
    }


    [Fact]
    public async Task RestartsAfterATimeoutAndRetriesOnce()
    {
        var location = TestEnvironment.RequireExifTool();
        await using var daemon = new ExifToolDaemon(new ExifToolDaemonOptions { ExePath = location.Path, ConfigPath = location.ConfigPath, Timeout = TimeSpan.FromMilliseconds(1) });

        // nothing answers within 1 ms, so both attempts time out and the process restarts once
        await Assert.ThrowsAsync<TimeoutException>(() => daemon.RunTextAsync(["-ver"], TestContext.Current.CancellationToken));
        Assert.Equal(1, daemon.Restarts);
    }


    [Fact]
    public async Task EndsTheProcessOnDispose()
    {
        var daemon = New();
        await daemon.StartAsync(TestContext.Current.CancellationToken);
        var pid = daemon.ProcessId;

        await daemon.DisposeAsync();
        await daemon.DisposeAsync();

        Assert.Throws<ArgumentException>(() => Process.GetProcessById(pid).WaitForExit(0));
        Assert.Throws<ObjectDisposedException>(() => { _ = daemon.RunTextAsync(["-ver"], TestContext.Current.CancellationToken); });
    }


    [Fact]
    public async Task StopsInsideImageGlassBudgetWhileAReadRuns()
    {
        var daemon = New();
        var ct = TestContext.Current.CancellationToken;
        await daemon.StartAsync(ct);
        var pid = daemon.ProcessId;

        // -if runs Perl per file: a read that takes 5 s, and one queued behind it
        var slow = daemon.RunTextAsync(["-if", "sleep(5) || 1", "-FileName", TestEnvironment.Fixture("a1111.png")], ct);
        var queued = daemon.RunTextAsync(["-ver"], ct);
        await Task.Delay(300, ct);

        var watch = Stopwatch.StartNew();
        await daemon.DisposeAsync();
        watch.Stop();

        Assert.InRange(watch.ElapsedMilliseconds, 0, 1500);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        await Assert.ThrowsAnyAsync<Exception>(() => slow);
        Assert.Throws<ArgumentException>(() => Process.GetProcessById(pid).WaitForExit(0));
    }


    [Fact]
    public async Task LetsAWriteFinishBeforeItStops()
    {
        var daemon = New();
        var ct = TestContext.Current.CancellationToken;
        await daemon.StartAsync(ct);

        // a 1 s command marked as a write outlasts a read's 500 ms but not a write's 1.5 s
        var write = daemon.ExecuteAsync(["-if", "sleep(1) || 1", "-FileName", TestEnvironment.Fixture("a1111.png")],
            static (in ExifToolOutput o, CancellationToken _) => o.StdoutText, ct, writes: true);
        await Task.Delay(200, ct);
        await daemon.DisposeAsync();

        Assert.Contains("a1111.png", await write);
    }


    [Fact]
    public async Task FailsClearlyWhenExifToolIsMissing()
    {
        await using var daemon = new ExifToolDaemon(new ExifToolDaemonOptions { ExePath = Path.Combine(Path.GetTempPath(), "no-exiftool-" + Guid.NewGuid() + ".exe") });

        var ex = await Assert.ThrowsAsync<ExifToolException>(() => daemon.RunTextAsync(["-ver"], TestContext.Current.CancellationToken));
        Assert.Contains("couldn't start", ex.Message);
    }


    [Fact]
    public async Task RefusesArgumentsTheStreamCantCarry()
    {
        await using var daemon = New();

        Assert.Throws<ArgumentException>(() => { _ = daemon.RunTextAsync(["-Title=$x\ny"], TestContext.Current.CancellationToken); });
        await Task.CompletedTask;
    }

    #endregion // Tests


    #region Private methods

    private static ExifToolDaemon New()
    {
        var location = TestEnvironment.RequireExifTool();
        return new ExifToolDaemon(new ExifToolDaemonOptions { ExePath = location.Path, ConfigPath = location.ConfigPath, PrimingFiles = [TestEnvironment.Fixture("negative_camera.jpg")] });
    }

    #endregion // Private methods
}
