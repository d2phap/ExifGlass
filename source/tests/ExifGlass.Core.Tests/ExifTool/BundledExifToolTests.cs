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
using System.Runtime.Versioning;
using Xunit;

namespace ExifGlass.Core.Tests.ExifTool;

public sealed class BundledExifToolTests : IDisposable
{
    #region Fields

    private readonly string _dir = Directory.CreateTempSubdirectory("exifglass-tests-").FullName;

    #endregion // Fields


    #region Tests

    [Fact]
    public void FindsNothingInAnEmptyFolder()
    {
        Assert.Null(BundledExifTool.FindPath(_dir));
    }


    [Fact]
    public void FindsThePlatformsExecutableName()
    {
        var expected = Path.Combine(_dir, OperatingSystem.IsWindows() ? "exiftool.exe" : "exiftool");
        File.WriteAllText(expected, string.Empty);

        Assert.Equal(expected, BundledExifTool.FindPath(_dir));
    }


    [Fact]
    public void GivesTheScriptItsExecuteBits()
    {
        if (OperatingSystem.IsWindows()) Assert.Skip("Unix file modes only");
        else AssertExecuteBitsRestored();
    }

    #endregion // Tests


    #region Private methods

    [UnsupportedOSPlatform("windows")]
    private void AssertExecuteBitsRestored()
    {
        var script = Path.Combine(_dir, "exiftool");
        File.WriteAllText(script, "#!/usr/bin/env perl");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        BundledExifTool.EnsureExecutable(_dir);

        Assert.True(File.GetUnixFileMode(script).HasFlag(UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute));
    }

    #endregion // Private methods


    #region IDisposable

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    #endregion // IDisposable
}
