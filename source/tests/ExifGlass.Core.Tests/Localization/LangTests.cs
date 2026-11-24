/*
ExifGlass - AI & EXIF metadata viewer
Copyright (C) 2023 - 2027 DUONG DIEU PHAP
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
using ExifGlass.Core.Localization;
using ExifGlass.Core.Settings;
using System.Text.Json;
using Xunit;

namespace ExifGlass.Core.Tests.Localization;

/// <summary>
/// Language packs (*.eglang.json): the built-in English, reading, gating, contributors, export and install. These never set Lang.Current, which every test shares.
/// </summary>
public sealed class LangTests : IDisposable
{
    #region Fields and constants

    private readonly string _dir = Directory.CreateTempSubdirectory("exifglass-lang-").FullName;
    private readonly string? _userDirectory = Lang.UserDirectory;

    #endregion // Fields and constants


    #region Tests

    [Fact]
    public void EveryKeyHasItsEnglish()
    {
        var missing = Enum.GetValues<LangId>().Where(k => string.IsNullOrWhiteSpace(Lang.DefaultLangMap.GetValueOrDefault(k))).ToList();

        Assert.Empty(missing);
        Assert.Equal(Enum.GetValues<LangId>().Length, Lang.DefaultLangMap.Count);
    }


    [Fact]
    public void TheEnglishHasNoEmDash()
    {
        Assert.DoesNotContain(Lang.DefaultLangMap, kv => kv.Value.Contains('—'));
    }


    [Fact]
    public void APackReadsItsMetadataItemsAndContributors()
    {
        var path = Write("Vietnamese", """
            {
              // a translator's note
              "_Metadata": { "Code": "vi-VN", "EnglishName": "Vietnamese", "LocalName": "Tiếng Việt", "Contributors": ["An", " Bình "], "MinVersion": 3 },
              "Items": { "_Ok": "Đồng ý", "Settings_Language": "Ngôn ngữ", "No_Such_Key": "x", "_Cancel": 5, },
            }
            """);

        var lang = Lang.Load(path)!;

        Assert.Equal(("vi-VN", "Vietnamese", "Tiếng Việt"), (lang.Metadata.Code, lang.Metadata.EnglishName, lang.Metadata.LocalName));
        Assert.Equal(["An", "Bình"], lang.Metadata.Contributors);
        Assert.Equal("Đồng ý", lang[LangId._Ok]);
        Assert.Equal("Ngôn ngữ", lang[LangId.Settings_Language]);
        Assert.Equal(2, lang.Items.Count);

        // a key the pack doesn't have, or doesn't have as text, shows the English
        Assert.Equal("Cancel", lang[LangId._Cancel]);
        Assert.Equal("Vietnamese.eglang.json", lang.SettingValue);
        Assert.False(lang.IsBuiltIn);
    }


    [Fact]
    public void OneContributorCanBeAString()
    {
        var lang = Lang.Load(Write("One", """{ "_Metadata": { "LocalName": "One", "Contributors": "Ann", "MinVersion": "3.0" }, "Items": {} }"""))!;

        Assert.Equal(["Ann"], lang.Metadata.Contributors);
    }


    [Fact]
    public void TheBuiltInEnglishCreditsItsAuthor()
    {
        var english = new Lang();

        Assert.True(english.IsBuiltIn);
        Assert.Equal(Lang.BUILT_IN, english.SettingValue);
        Assert.Equal(["Duong Dieu Phap"], english.Metadata.Contributors);
    }


    [Theory]
    [InlineData("""{ "_Metadata": { "LocalName": "Old", "MinVersion": 2 }, "Items": { "_Ok": "x" } }""")]
    [InlineData("""{ "Metadata": { "LocalName": "No underscore", "MinVersion": 3 }, "Items": { "_Ok": "x" } }""")]
    [InlineData("""{ "Items": { "_Ok": "x" } }""")]
    [InlineData("""not json""")]
    public void AnOldOrBrokenPackIsNotLoaded(string json)
    {
        var path = Write("Bad", json);

        Assert.Null(Lang.Load(path));
        Assert.True(Lang.FromSetting(path).IsBuiltIn);
    }


    [Fact]
    public void AMissingSettingFallsBackToEnglish()
    {
        Lang.UserDirectory = _dir;

        Assert.True(Lang.FromSetting("Nowhere.eglang.json").IsBuiltIn);
        Assert.True(Lang.FromSetting(Lang.BUILT_IN).IsBuiltIn);
        Assert.True(Lang.FromSetting(null).IsBuiltIn);
    }


    [Fact]
    public void ABrokenPlaceholderShowsTheEnglishInstead()
    {
        var lang = Lang.Load(Write("Broken", """{ "_Metadata": { "LocalName": "B", "MinVersion": 3 }, "Items": { "Toast_Exported": "Xuất {0", "Rail_JumpTo": "Đến {1}" } }"""))!;

        Assert.Equal("Exported a.csv", lang[LangId.Toast_Exported, "a.csv"]);
        Assert.Equal("Jump to EXIF", lang[LangId.Rail_JumpTo, "EXIF"]);
    }


    [Fact]
    public void CountsFormatTheSameInEveryLanguage()
    {
        Assert.Equal("1,234 tags", new Lang()[LangId._Tags, 1234]);
    }


    [Fact]
    public void ExportWritesEveryKeyAndReadsBack()
    {
        var source = Lang.Load(Write("Source", """{ "_Metadata": { "Code": "vi-VN", "EnglishName": "Vietnamese", "LocalName": "Tiếng Việt", "Contributors": ["An"], "MinVersion": 3 }, "Items": { "_Ok": "Đồng ý" } }"""))!;
        var path = Path.Combine(_dir, "Out" + Lang.FILE_EXTENSION);
        using (var stream = File.Create(path)) source.Export(stream);

        var text = File.ReadAllText(path);
        var back = Lang.Load(path)!;

        Assert.Contains("Tiếng Việt", text, StringComparison.Ordinal);
        Assert.Equal(Enum.GetValues<LangId>().Length, back.Items.Count);
        Assert.Equal("Đồng ý", back[LangId._Ok]);
        Assert.Equal("Cancel", back[LangId._Cancel]);
        Assert.Equal(["An"], back.Metadata.Contributors);
        Assert.Equal(Lang.SPEC_VERSION, back.Metadata.MinVersion);
    }


    [Fact]
    public void TheEnglishExportsAsATemplate()
    {
        using var stream = new MemoryStream();
        new Lang().Export(stream);
        using var doc = JsonDocument.Parse(stream.ToArray());
        var meta = doc.RootElement.GetProperty("_Metadata");

        Assert.StartsWith("<", meta.GetProperty("LocalName").GetString(), StringComparison.Ordinal);
        Assert.Equal("<Your name>", meta.GetProperty("Contributors")[0].GetString());
        Assert.Equal("OK", doc.RootElement.GetProperty("Items").GetProperty(nameof(LangId._Ok)).GetString());
    }


    [Fact]
    public void InstallCopiesCurrentPacksAndNamesTheOldOnes()
    {
        var user = Directory.CreateDirectory(Path.Combine(_dir, "user")).FullName;
        Lang.UserDirectory = user;
        var good = Write("Zulu", """{ "_Metadata": { "LocalName": "isiZulu", "MinVersion": 3 }, "Items": {} }""");
        var old = Write("Ancient", """{ "_Metadata": { "LocalName": "Old", "MinVersion": 1 }, "Items": {} }""");

        var result = Lang.Install([good, old]);
        var all = Lang.LoadAll();

        Assert.Equal(["Zulu.eglang.json"], result.Installed);
        Assert.Equal(["Ancient"], result.Incompatible);
        Assert.True(File.Exists(Path.Combine(user, "Zulu.eglang.json")));
        Assert.True(all[0].IsBuiltIn);
        Assert.Contains(all, l => l.Metadata.LocalName == "isiZulu");
        Assert.Equal("isiZulu", Lang.FromSetting("Zulu.eglang.json").Metadata.LocalName);
    }


    [Fact]
    public void LoadAllSortsByLocalName()
    {
        var user = Directory.CreateDirectory(Path.Combine(_dir, "sorted")).FullName;
        Lang.UserDirectory = user;
        File.Copy(Write("B", """{ "_Metadata": { "LocalName": "Zeta", "MinVersion": 3 }, "Items": {} }"""), Path.Combine(user, "B.eglang.json"));
        File.Copy(Write("A", """{ "_Metadata": { "LocalName": "Alpha", "MinVersion": 3 }, "Items": {} }"""), Path.Combine(user, "A.eglang.json"));

        var names = Lang.LoadAll().Skip(1).Select(l => l.Metadata.LocalName).ToList();

        Assert.Equal(["Alpha", "Zeta"], names);
    }


    [Fact]
    public void TheLanguageSettingDefaultsToEnglishAndTakesAnOverride()
    {
        var settings = new AppSettings();
        SettingsOverrides.Apply(settings, new Dictionary<string, string> { ["language"] = "Vietnamese.eglang.json" });

        Assert.Equal(Lang.BUILT_IN, new AppSettings().Language);
        Assert.Equal("Vietnamese.eglang.json", settings.Language);
    }

    #endregion // Tests


    #region Public methods

    public void Dispose()
    {
        Lang.UserDirectory = _userDirectory;
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    #endregion // Public methods


    #region Private methods

    private string Write(string name, string json)
    {
        var path = Path.Combine(_dir, name + Lang.FILE_EXTENSION);
        File.WriteAllText(path, json);
        return path;
    }

    #endregion // Private methods
}
