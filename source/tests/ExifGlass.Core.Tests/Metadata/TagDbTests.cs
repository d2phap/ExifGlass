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
using ExifGlass.Core.Metadata;
using System.Diagnostics;
using System.Text;
using Xunit;

namespace ExifGlass.Core.Tests.Metadata;

public class TagDbTests(ExifToolFixture exifTool)
{
    #region Tests

    [Fact]
    public void LoadsTheEmbeddedTableQuickly()
    {
        var watch = Stopwatch.StartNew();
        var db = TagDb.LoadEmbedded();

        Assert.NotNull(db);
        Assert.True(db.Count > 20_000, $"only {db.Count} records");
        Assert.Matches(@"^\d+\.\d+$", db.ExifToolVersion);
        Assert.Same(db, TagDb.LoadEmbedded());
        Assert.True(watch.ElapsedMilliseconds < 500, $"{watch.ElapsedMilliseconds} ms");
    }


    [Theory]
    [InlineData("EXIF", "FNumber", "ExifIFD", true, "rational64u")]
    [InlineData("EXIF", "Make", "IFD0", true, "string")]
    [InlineData("XMP", "Title", "XMP-dc", true, "lang-alt")]
    [InlineData("IPTC", "Keywords", null, true, "string")]
    [InlineData("PNG", "Parameters", null, true, "string")]
    [InlineData("Composite", "Aperture", null, false, "—")]
    public void FindsTagsWithWritabilityAndFormat(string g0, string name, string? g1, bool writable, string format)
    {
        var db = TagDb.LoadEmbedded()!;

        Assert.True(db.TryFind(g0, name, g1, out var def), $"{g0}:{name} not found");
        Assert.Equal(writable, def.IsWritable);
        Assert.StartsWith(format, def.Format);
        if (g1 is not null) Assert.Equal(g1, def.Group1);
    }


    [Fact]
    public void FindsNothingForUnknownTags()
    {
        var db = TagDb.LoadEmbedded()!;

        // -listx leaves out tags that exifglass.config defines, even when the config is loaded
        Assert.False(db.TryFind("PNG", "Prompt", null, out _));

        Assert.False(db.TryFind("EXIF", "NoSuchTagAnywhere", null, out _));
        Assert.False(db.TryFind("NoSuchGroup", "Make", null, out _));
        Assert.False(db.TryFind(new string('G', 200), "Make", null, out _));
    }


    [Fact]
    public void ParsesListxAndRoundTripsTheBinaryForm()
    {
        var xml = """
            <?xml version='1.0' encoding='UTF-8'?>
            <taginfo>
            <table name='Exif::Main' g0='EXIF' g1='IFD0' g2='Image'>
             <tag id='271' name='Make' type='string' writable='true' g1='IFD0'/>
             <tag id='33437' name='FNumber' type='rational64u' writable='true' g1='ExifIFD'/>
             <tag id='37500' name='MakerNoteUnknown' type='undef' writable='false' flags='Binary,Unknown'/>
             <tag id='282' name='XResolution' type='rational64u' count='2' writable='true' flags='Mandatory'/>
            </table>
            <table name='XMP::dc' g0='XMP' g1='XMP-dc' g2='Other'>
             <tag id='subject' name='Subject' type='string' writable='true' flags='Bag,List'/>
             <tag id='creator' name='Creator' type='struct' writable='true' struct='Person'/>
            </table>
            </taginfo>
            """;

        var defs = ListxParser.Parse(Encoding.UTF8.GetBytes(xml));
        var db = TagDb.FromListx(Encoding.UTF8.GetBytes(xml), "99.99");

        Assert.Equal(6, defs.Count);
        Assert.Equal("99.99", db.ExifToolVersion);
        Assert.True(db.TryFind("EXIF", "MakerNoteUnknown", null, out var unknown));
        Assert.Equal(TagDefFlags.Binary | TagDefFlags.Unknown, unknown.Flags);
        Assert.False(unknown.IsWritable);
        Assert.True(db.TryFind("EXIF", "XResolution", null, out var res));
        Assert.Equal("rational64u[2]", res.Format);
        Assert.True(db.TryFind("XMP", "Subject", "XMP-dc", out var subject));
        Assert.Equal(TagDefFlags.Writable | TagDefFlags.Bag | TagDefFlags.List, subject.Flags);
        Assert.True(db.TryFind("XMP", "Creator", null, out var creator));
        Assert.True(creator.Flags.HasFlag(TagDefFlags.Struct));
        Assert.Throws<InvalidDataException>(() => TagDb.FromBinary([1, 2, 3, 4, 5]));
    }


    [Fact]
    public async Task TheEmbeddedTableMatchesWhatTheBundledExifToolLists()
    {
        var service = exifTool.Require();

        var live = await service.BuildTagDbAsync(TestContext.Current.CancellationToken);
        var embedded = TagDb.LoadEmbedded()!;

        Assert.NotNull(live);
        Assert.Equal(embedded.ExifToolVersion, live.ExifToolVersion);
        Assert.Equal(embedded.Count, live.Count);
        Assert.Same(embedded, await service.GetTagDbAsync(TestContext.Current.CancellationToken));
    }


    [Fact]
    public void KeepsReadOnlyGroupsAndBinaryValuesOutOfEdits()
    {
        var db = TagDb.LoadEmbedded()!;
        TagRecord Tag(string g0, string g1, string name, TagValueKind kind = TagValueKind.String) => new() { Key = $"{g0}:{g1}:{name}", Group0 = g0, Group1 = g1, Name = name, Kind = kind };

        Assert.True(TagWriteRules.IsReadOnly(Tag("PNG", "PNG", "Parameters"), db));
        Assert.True(TagWriteRules.IsReadOnly(Tag("EXIF", "IFD1", "ThumbnailImage", TagValueKind.Binary), db));
        Assert.True(TagWriteRules.IsReadOnly(Tag("EXIF", "IFD0", "NoSuchTag"), db));
        Assert.False(TagWriteRules.IsReadOnly(Tag("EXIF", "IFD0", "Make"), db));
        Assert.False(TagWriteRules.IsReadOnly(Tag("EXIF", "IFD0", "Make"), null));
        Assert.Contains("JPEG", TagWriteRules.IptcFileTypes);
        Assert.Equal(["EXIF", "XMP", "IPTC"], TagWriteRules.AddableGroups);
    }

    #endregion // Tests
}
