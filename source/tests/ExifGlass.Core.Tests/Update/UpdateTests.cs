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
using ExifGlass.Core.Update;
using Xunit;

namespace ExifGlass.Core.Tests.Update;

public class UpdateTests
{
    #region Fields and constants

    private static readonly DateTimeOffset NOW = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private const string FEED = """{ "ApiVersion": 1, "Version": "3.0.1", "Title": "ExifGlass 3.0.1", "DownloadUrl": "https://github.com/d2phap/ExifGlass/releases" }""";

    #endregion // Fields and constants


    #region Tests

    [Fact]
    public async Task FindsANewerVersionAndOnlyChecksWhenDue()
    {
        var calls = 0;
        var checker = new UpdateChecker(_ => { calls++; return Task.FromResult(FEED); });
        var ct = TestContext.Current.CancellationToken;

        var available = await checker.CheckAsync("3.0.0.1020", force: false, enabled: true, lastCheck: null, NOW, ct);
        Assert.Equal(UpdateStatus.Available, available.Status);
        Assert.Equal("ExifGlass 3.0.1", available.Info!.Title);

        Assert.Equal(UpdateStatus.Skipped, (await checker.CheckAsync("3.0.0", false, true, UpdateChecker.Stamp(NOW.AddDays(-1)), NOW, ct)).Status);
        Assert.Equal(UpdateStatus.Skipped, (await checker.CheckAsync("3.0.0", false, false, null, NOW, ct)).Status);
        Assert.Equal(1, calls);

        Assert.Equal(UpdateStatus.UpToDate, (await checker.CheckAsync("3.0.1", true, false, UpdateChecker.Stamp(NOW), NOW, ct)).Status);
        Assert.Equal(2, calls);
    }


    [Theory]
    [InlineData("{ broken", "The update information couldn't be read")]
    [InlineData("{ \"Version\": \"\" }", "The update information couldn't be read")]
    public async Task ReportsABadFeed(string feed, string error)
    {
        var result = await new UpdateChecker(_ => Task.FromResult(feed)).CheckAsync("3.0.0", true, true, null, NOW, TestContext.Current.CancellationToken);

        Assert.Equal((UpdateStatus.Failed, error), (result.Status, result.Error));
    }


    [Fact]
    public async Task ReportsAnUnreachableServerAndPassesCancellationOn()
    {
        var failing = new UpdateChecker(_ => throw new HttpRequestException("offline"));
        var result = await failing.CheckAsync("3.0.0", true, true, null, NOW, TestContext.Current.CancellationToken);
        Assert.Equal((UpdateStatus.Failed, "Couldn't reach the update server"), (result.Status, result.Error));

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var cancelled = new UpdateChecker(ct => Task.FromCanceled<string>(ct));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.CheckAsync("3.0.0", true, true, null, NOW, cts.Token));
    }


    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("garbage", true)]
    [InlineData("2026-10-16T12:00:00Z", false)]
    [InlineData("2026-10-15T12:00:00.0000000+00:00", true)]
    [InlineData("2026-10-15T19:00:00+07:00", true)]
    public void ThrottlesTheStartupCheckToFiveDays(string? last, bool due) => Assert.Equal(due, UpdateChecker.ShouldCheck(last, NOW, UpdateChecker.THROTTLE));


    [Theory]
    [InlineData("3.0.1", "3.0.0.1020", true)]
    [InlineData("3.0.0", "3.0.0.0", false)]
    [InlineData("v3.1", "3.0.9", true)]
    [InlineData("3.0.0-beta", "3.0.0", false)]
    [InlineData("3.0.0+build.5", "2.9", true)]
    [InlineData("10.0", "9.99", true)]
    [InlineData("3.x.1", "3.0.0", true)]
    [InlineData("", "1.0", false)]
    [InlineData(null, null, false)]
    public void ComparesVersions(string? candidate, string? current, bool newer) => Assert.Equal(newer, UpdateChecker.IsNewer(candidate, current));


    [Fact]
    public void StampsInUtcRoundTripFormat() => Assert.Equal("2026-10-20T12:00:00.0000000+00:00", UpdateChecker.Stamp(NOW.ToOffset(TimeSpan.FromHours(7))));


    [Theory]
    [InlineData("win", "Direct", null, AppChannel.WindowsZip, true, StoreBadgeKind.MicrosoftStore, true)]
    [InlineData("win", "Direct", "DuongDieuPhap.ExifGlass_xyz", AppChannel.WindowsSideload, true, StoreBadgeKind.MicrosoftStore, true)]
    [InlineData("win", "Direct", "9662DuongDieuPhap.ExifGlass_xjrmsrdc1fgj6", AppChannel.WindowsStore, false, StoreBadgeKind.Hidden, true)]
    [InlineData("mac", "Direct", null, AppChannel.MacDirect, true, StoreBadgeKind.Hidden, true)]
    [InlineData("mac", "MacAppStore", null, AppChannel.MacAppStore, false, StoreBadgeKind.Hidden, false)]
    [InlineData("linux", "Direct", null, AppChannel.Development, false, StoreBadgeKind.Hidden, true)]
    public void DecidesWhatEachChannelAllows(string platform, string build, string? package, AppChannel channel, bool update, StoreBadgeKind badge, bool ig10)
    {
        var p = ChannelPolicy.Detect(platform, build, package, _ => null);

        Assert.Equal((channel, update, badge, ig10), (p.Channel, p.UpdateCheck, p.StoreBadge, p.ImageGlass10));
        Assert.Equal(channel == AppChannel.MacAppStore, p.BackupInContainer);
        Assert.Equal(channel != AppChannel.MacAppStore, p.CustomExifTool);
    }


    [Fact]
    public void TellsFlatpakFromAppImage()
    {
        static string? ownAppImage(string name) => name switch
        {
            "APPIMAGE" => "/home/me/ExifGlass.AppImage",
            "APPDIR" => "/tmp/.mount_ExifGlA1b2C3",
            _ => null,
        };

        var flatpak = ChannelPolicy.Detect("linux", "Direct", null, n => n == "FLATPAK_ID" ? "io.github.d2phap.exifglass" : null);
        var appImage = ChannelPolicy.Detect("linux", "Direct", null, ownAppImage, "/tmp/.mount_ExifGlA1b2C3/usr/lib/exifglass/ExifGlass");

        Assert.Equal((AppChannel.LinuxFlatpak, false, StoreBadgeKind.Hidden), (flatpak.Channel, flatpak.UpdateCheck, flatpak.StoreBadge));
        Assert.Equal((AppChannel.LinuxAppImage, true, StoreBadgeKind.Hidden), (appImage.Channel, appImage.UpdateCheck, appImage.StoreBadge));
        Assert.Equal(OperatingSystem.IsWindows(), appImage.ImageGlass9);
    }


    [Fact]
    public void ATarballStartedByAnotherAppImageStaysDevelopment()
    {
        // G10 finding 7: ImageGlass's AppImage starts its tools with its own APPIMAGE and APPDIR
        static string? imageGlass(string name) => name switch
        {
            "APPIMAGE" => "/home/me/Applications/ImageGlass",
            "APPDIR" => "/tmp/.mount_ImageGXyZ123",
            _ => null,
        };

        var tarball = ChannelPolicy.Detect("linux", "Direct", null, imageGlass, "/home/me/ExifGlass/ExifGlass");
        var nextDoor = ChannelPolicy.Detect("linux", "Direct", null, imageGlass, "/tmp/.mount_ImageGXyZ123-other/ExifGlass");
        var noAppDir = ChannelPolicy.Detect("linux", "Direct", null, n => n == "APPIMAGE" ? "/home/me/ExifGlass.AppImage" : null, "/home/me/ExifGlass/ExifGlass");
        var extracted = ChannelPolicy.Detect("linux", "Direct", null, n => n == "APPDIR" ? "/home/me/squashfs-root" : null, "/home/me/squashfs-root/usr/lib/exifglass/ExifGlass");

        Assert.Equal((AppChannel.Development, false, StoreBadgeKind.Hidden), (tarball.Channel, tarball.UpdateCheck, tarball.StoreBadge));
        Assert.Equal(AppChannel.Development, nextDoor.Channel);
        Assert.Equal(AppChannel.Development, noAppDir.Channel);
        Assert.Equal(AppChannel.Development, extracted.Channel);
    }


    [Fact]
    public void ParsesTheCommandLine()
    {
        var o = CommandLine.Parse(["exifglass:C%3A%2Fphotos%2Fa%20b.jpg", "-p:Theme=dark", "-p:=x", "-p:bad", "--theme", "Light", "--render", "software", "--fixtures", "f", "--gallery", "--selftest", "--other", "second.jpg"]);

        Assert.Equal(AppMode.Standalone, o.Mode);
        Assert.Equal("C:/photos/a b.jpg", o.File);
        Assert.Equal("dark", Assert.Single(o.Overrides).Value);
        Assert.Equal(("light", "f", true, true, true), (o.Theme, o.FixturesDirectory, o.SoftwareRendering, o.Gallery, o.SelfTest));
    }


    [Fact]
    public void TakesThePipeNameAsExactlyTwoTokensAndIgnoresTheFileForImageGlass10()
    {
        var ig10 = CommandLine.Parse(["--pipe", "ig_abc", "C:/a.jpg"]);
        Assert.Equal((AppMode.ImageGlass10, "ig_abc", null), (ig10.Mode, ig10.PipeName, ig10.File));

        var noName = CommandLine.Parse(["--pipe", "--selftest"]);
        Assert.Equal((AppMode.Standalone, null), (noName.Mode, noName.PipeName));
        Assert.True(noName.SelfTest);

        var ig9 = CommandLine.Parse(["--ig-tool-pipe-code=123", "C:/a.jpg"]);
        Assert.Equal((AppMode.ImageGlass9, "123", "C:/a.jpg"), (ig9.Mode, ig9.ImageGlass9PipeCode, ig9.File));
        Assert.Equal(AppMode.Standalone, CommandLine.Parse(["--ig-tool-pipe-code="]).Mode);
        Assert.Equal(AppMode.Standalone, CommandLine.Parse(["", "--render"]).Mode);
    }


    [Theory]
    [InlineData("exifglass:///home/me/a.jpg", "/home/me/a.jpg")]
    [InlineData("EXIFGLASS:C:%5Ca.jpg", "C:\\a.jpg")]
    [InlineData("\"C:/a b.jpg\"", "C:/a b.jpg")]
    [InlineData("exifglass:%E0%A4", "%E0%A4")]
    public void NormalizesFileArguments(string arg, string expected) => Assert.Equal(expected, CommandLine.NormalizeFile(arg));

    #endregion // Tests
}
