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
using Xunit;

namespace ExifGlass.Core.Tests.ExifTool;

public sealed class ExifToolLocatorTests : IDisposable
{
    #region Fields and constants

    private readonly string _app = Directory.CreateTempSubdirectory("exifglass-app-").FullName;
    private readonly string _path = Directory.CreateTempSubdirectory("exifglass-path-").FullName;
    private readonly string _name = OperatingSystem.IsWindows() ? "exiftool.exe" : "exiftool";

    #endregion // Fields and constants


    #region Tests

    [Fact]
    public void PrefersTheCustomPathThenTheBundledOneThenPath()
    {
        var custom = Path.Combine(_path, "custom-" + _name);
        File.WriteAllText(custom, "#!/usr/bin/env perl");
        File.WriteAllText(Path.Combine(_app, _name), "#!/usr/bin/env perl");
        File.WriteAllText(Path.Combine(_app, ExifToolLocator.CONFIG_FILE_NAME), "1;");
        File.WriteAllText(Path.Combine(_path, _name), "#!/usr/bin/env perl");

        var a = ExifToolLocator.Locate($"\"{custom}\"", _app, _path)!;
        Assert.Equal((ExifToolSource.Custom, Path.GetFullPath(custom)), (a.Source, a.Path));
        Assert.Equal(Path.Combine(_app, ExifToolLocator.CONFIG_FILE_NAME), a.ConfigPath);

        var b = ExifToolLocator.Locate(Path.Combine(_path, "missing"), _app, _path)!;
        Assert.Equal(ExifToolSource.Bundled, b.Source);
        Assert.True(b.CustomPathMissing);

        File.Delete(Path.Combine(_app, _name));
        var c = ExifToolLocator.Locate(null, _app, _path)!;
        Assert.Equal((ExifToolSource.SystemPath, Path.Combine(_path, _name)), (c.Source, c.Path));

        File.Delete(Path.Combine(_path, _name));
        Assert.Null(ExifToolLocator.Locate("  ", _app, _path));
    }


    [Fact]
    public void ToleratesEmptyAndBrokenPathEntries()
    {
        Assert.Null(ExifToolLocator.FindOnPath(null));
        Assert.Null(ExifToolLocator.FindOnPath(string.Empty));
        Assert.Null(ExifToolLocator.FindOnPath($"{Path.PathSeparator}\"{_path}\"{Path.PathSeparator}bad\0dir"));
    }


    [Theory]
    [InlineData("13.59", "13.59")]
    [InlineData("13.59\n", "13.59")]
    [InlineData("12.40 [Unsupported]", "12.40")]
    [InlineData("", null)]
    [InlineData("abc", null)]
    public void ParsesVersions(string text, string? expected) => Assert.Equal(expected is null ? null : decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), ExifToolVersion.Parse(text));


    [Fact]
    public void GatesAiFeaturesByVersion()
    {
        Assert.Empty(ExifToolVersion.MissingAiFeatures("13.59"));
        Assert.Equal([ExifToolVersion.LearningOptOut], ExifToolVersion.MissingAiFeatures("13.40"));
        Assert.Equal([ExifToolVersion.LearningOptOut, ExifToolVersion.IptcAiFields, ExifToolVersion.PngAiTags, ExifToolVersion.C2paEverywhere], ExifToolVersion.MissingAiFeatures("12.70"));
        Assert.Equal(4, ExifToolVersion.MissingAiFeatures(null).Count);
    }


    [Fact]
    public void BuildsCommandsWithFullPathsAndNoEndOfOptions()
    {
        var read = ExifToolCommands.TableRead("a.jpg", ["-fast"]);
        Assert.Equal(["-j", "-l", "-G0:1:4", "-H", "-fast", Path.GetFullPath("a.jpg")], read);
        Assert.DoesNotContain("--", ExifToolCommands.AiRead("a.jpg"));
        Assert.Equal(["-listx", "-s", "-f"], ExifToolCommands.ListTags());
        Assert.Equal("-EXIF:ThumbnailImage", ExifToolCommands.BinaryRead("a.jpg", "EXIF:ThumbnailImage")[2]);
        Assert.Equal(["-charset", "UTF8", "-Make=x", Path.GetFullPath("a.jpg")], ExifToolCommands.Write("a.jpg", ["-Make=x"]));
    }

    #endregion // Tests


    #region IDisposable

    public void Dispose()
    {
        Directory.Delete(_app, true);
        Directory.Delete(_path, true);
    }

    #endregion // IDisposable
}
