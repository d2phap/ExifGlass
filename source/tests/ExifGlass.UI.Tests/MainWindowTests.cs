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
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using ExifGlass.Core.Common;
using ExifGlass.Windows;
using Xunit;

namespace ExifGlass.UI.Tests;

public class MainWindowTests
{
    #region Tests

    [AvaloniaFact]
    public void OpensAtTheSpecMinimumSizeAndRenders()
    {
        AppInfo.BuildInfo = new TestBuildInfo();
        var window = new MainWindow();
        window.Show();

        Assert.Equal("ExifGlass", window.Title);
        Assert.Equal(420, window.MinWidth);
        Assert.Equal(480, window.MinHeight);
        Assert.NotNull(window.CaptureRenderedFrame());

        window.Close();
    }


    [AvaloniaTheory]
    [InlineData(new[] { "--theme", "dark" }, "Dark")]
    [InlineData(new[] { "--THEME", "Light" }, "Light")]
    [InlineData(new[] { "--theme" }, null)]
    [InlineData(new[] { "photo.jpg" }, null)]
    public void ReadsTheThemeOverride(string[] args, string? expected)
    {
        Assert.Equal(expected, AppBootstrap.ReadTheme(args)?.Key?.ToString());
    }

    #endregion // Tests


    #region Nested types

    private sealed class TestBuildInfo : IAppBuildInfo
    {
        public string Version => "3.0.0.0";
        public string FullVersion => "3.0.0.0-test";
        public string ReleaseType => "test";
        public string Channel => "Direct";
        public string PlatformId => "test";
        public string Architecture => "x64";
        public string BuildDate => "2026.10.20";
        public string Copyright => "© 2023 - 2026 Duong Dieu Phap";
    }

    #endregion // Nested types
}
