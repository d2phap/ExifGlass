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
using ExifGlass.Core.Settings;
using Xunit;

namespace ExifGlass.Core.Tests.Settings;

public sealed class SettingsTests : IDisposable
{
    #region Fields and constants

    private readonly string _dir = Directory.CreateTempSubdirectory("exifglass-settings-").FullName;

    #endregion // Fields and constants


    #region Tests

    [Fact]
    public async Task StartsFromDefaultsAndSavesAtomically()
    {
        await using var store = new SettingsStore(_dir);
        var s = store.Load();

        Assert.Equal(AppTheme.System, s.Theme);
        Assert.Equal(ArgumentPolicy.DEFAULT_ARGUMENTS, s.ExifToolArguments);
        Assert.Equal(["TagId", "TagName", "Value"], s.VisibleColumns);
        Assert.True(s.CheckForUpdates);
        Assert.False(File.Exists(store.FilePath));

        store.Update(x =>
        {
            x.Theme = AppTheme.Dark;
            x.Windows[AppSettings.WINDOW_SETTINGS] = new WindowPlacementState { X = 10, Y = 20, Width = 560, Height = 600, Maximized = true, Scaling = 1.5, FrameWidth = 16, FrameHeight = 39 };
        });
        await store.FlushAsync();

        Assert.True(File.Exists(store.FilePath));
        Assert.False(File.Exists(store.FilePath + ".tmp"));

        await using var again = new SettingsStore(_dir);
        var loaded = again.Load();
        Assert.Equal(AppTheme.Dark, loaded.Theme);
        var w = loaded.Windows[AppSettings.WINDOW_SETTINGS];
        Assert.Equal((10, 20, 560.0, 600.0, true, 1.5, 16.0, 39.0), (w.X, w.Y, w.Width, w.Height, w.Maximized, w.Scaling, w.FrameWidth, w.FrameHeight));
        Assert.Contains("\"Theme\": \"Dark\"", await File.ReadAllTextAsync(again.FilePath, TestContext.Current.CancellationToken));
    }


    [Fact]
    public async Task DebouncesSavesAndFlushesThem()
    {
        await using var store = new SettingsStore(_dir);
        store.Load();

        for (var i = 0; i < 20; i++) store.Update(x => x.AlwaysOnTop = i % 2 == 0);
        store.Update(x => x.AlwaysOnTop = true);
        await store.FlushAsync();
        await store.FlushAsync();

        Assert.True(new SettingsStore(_dir).Load().AlwaysOnTop);
    }


    [Fact]
    public async Task KeepsOverridesOutOfTheFile()
    {
        File.WriteAllText(Path.Combine(_dir, SettingsStore.FILE_NAME), """{ "Theme": "Light", "AlwaysOnTop": false, "ExifToolPath": "C:/et.exe" }""");
        await using var store = new SettingsStore(_dir);

        var s = store.Load(new Dictionary<string, string>
        {
            ["theme"] = "dark", ["AlwaysOnTop"] = "yes", ["ExifToolPath"] = "D:/other.exe", ["ExifToolArguments"] = "-fast",
            ["VisibleColumns"] = "Index, Value", ["CommandBarExpanded"] = "1", ["CheckForUpdates"] = "off", ["Unknown"] = "x", ["Theme2"] = "y",
        });

        Assert.Equal((AppTheme.Dark, true, "D:/other.exe", "-fast", false, true), (s.Theme, s.AlwaysOnTop, s.ExifToolPath, s.ExifToolArguments, s.CheckForUpdates, s.CommandBarExpanded));
        Assert.Equal(["Index", "Value"], s.VisibleColumns);
        Assert.Equal(7, store.OverriddenKeys.Count);

        store.Update(x => x.RailOpen = false);
        await store.FlushAsync();

        var file = new SettingsStore(_dir).Load();
        Assert.Equal((AppTheme.Light, false, "C:/et.exe", ArgumentPolicy.DEFAULT_ARGUMENTS), (file.Theme, file.AlwaysOnTop, file.ExifToolPath, file.ExifToolArguments));
        Assert.False(file.RailOpen);
    }


    [Theory]
    [InlineData("Theme", "purple")]
    [InlineData("AlwaysOnTop", "maybe")]
    [InlineData("CheckForUpdates", "")]
    [InlineData("VisibleColumns", " , ")]
    [InlineData("CommandBarExpanded", "2")]
    public void IgnoresBadOverrideValues(string key, string value)
    {
        var s = new AppSettings();

        Assert.Empty(SettingsOverrides.Apply(s, new Dictionary<string, string> { [key] = value }));
        Assert.Equal(AppTheme.System, s.Theme);
    }


    [Fact]
    public void StartsFromDefaultsWhenTheFileIsBroken()
    {
        File.WriteAllText(Path.Combine(_dir, SettingsStore.FILE_NAME), "{ not json");

        Assert.Equal(AppTheme.System, new SettingsStore(_dir).Load().Theme);
    }


    [Fact]
    public async Task MigratesV2Settings()
    {
        File.WriteAllText(Path.Combine(_dir, SettingsStore.V2_FILE_NAME), """
            {
              "Theme": "Dark", "AlwaysOnTop": true, "ExifToolPath": " C:/exiftool.exe ", "ExifToolArguments": "-a -u",
              "ShowCommandPreview": false, "ShowIndex": true, "ShowTagId": false, "ShowTagName": true, "ShowValue": true,
              "Window": { "X": 1398, "Y": 73, "Width": 793, "Height": 862, "Maximized": true },
              "CheckForUpdates": false, "LastUpdateCheck": "2026-10-19T12:50:10.2104219\u002B00:00"
            }
            """);

        await using var store = new SettingsStore(_dir);
        var s = store.Load();

        Assert.True(store.Migrated);
        Assert.Equal((AppTheme.Dark, true, "C:/exiftool.exe", false), (s.Theme, s.AlwaysOnTop, s.ExifToolPath, s.CheckForUpdates));
        Assert.Equal("-fast -G -t -m -q -H -a -u -charset UTF8", s.ExifToolArguments);
        Assert.Equal(["Index", "TagName", "Value"], s.VisibleColumns);
        Assert.Equal((1398, 73, 793.0, 862.0, true), (s.Windows["Main"].X, s.Windows["Main"].Y, s.Windows["Main"].Width, s.Windows["Main"].Height, s.Windows["Main"].Maximized));
        Assert.Equal("2026-10-19T12:50:10.2104219+00:00", s.LastUpdateCheck);

        await store.FlushAsync();
        Assert.True(File.Exists(store.FilePath));
        Assert.False(new SettingsStore(_dir).Load() is { } again && again.Theme != AppTheme.Dark);
    }


    [Theory]
    [InlineData("Light", false, false, false, false, AppTheme.Light, "Value")]
    [InlineData("1", true, true, true, true, AppTheme.Dark, "Index,TagId,TagName,Value")]
    [InlineData(null, false, false, true, false, AppTheme.System, "TagName")]
    public void MapsV2ThemesAndColumns(string? theme, bool index, bool id, bool name, bool value, AppTheme expected, string columns)
    {
        var s = V2Migration.ToV3(new V2Settings { Theme = theme, ShowIndex = index, ShowTagId = id, ShowTagName = name, ShowValue = value, Window = new V2WindowBounds() });

        Assert.Equal(expected, s.Theme);
        Assert.Equal(columns, string.Join(',', s.VisibleColumns));
        Assert.Empty(s.Windows);
        Assert.Equal("-fast -G -t -m -q -H -charset UTF8", s.ExifToolArguments);
    }


    [Fact]
    public void ClonesDeeply()
    {
        var a = new AppSettings();
        a.Windows["Main"] = new WindowPlacementState { X = 1 };

        var b = a.Clone();
        b.Windows["Main"].X = 2;
        b.VisibleColumns.Add("Index");

        Assert.Equal(1, a.Windows["Main"].X);
        Assert.Equal(3, a.VisibleColumns.Count);
        Assert.EndsWith("ExifGlass", SettingsStore.DefaultDirectory());
    }


    [Fact]
    public void ThePortableMarkerKeepsTheSettingsNextToTheApp()
    {
        Assert.False(SettingsStore.IsPortable(_dir));
        Assert.NotEqual(_dir, SettingsStore.DefaultDirectory(_dir));

        File.WriteAllBytes(Path.Combine(_dir, SettingsStore.PORTABLE_MARKER), []);
        Assert.True(SettingsStore.IsPortable(_dir));
        Assert.Equal(Path.GetFullPath(_dir), SettingsStore.DefaultDirectory(_dir + Path.DirectorySeparatorChar));
    }

    #endregion // Tests


    #region IDisposable

    public void Dispose() => Directory.Delete(_dir, true);

    #endregion // IDisposable
}
