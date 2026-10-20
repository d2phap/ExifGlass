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
using System.Buffers;
using System.Text;
using Xunit;

namespace ExifGlass.Core.Tests.Metadata;

public class TableReaderTests
{
    #region Tests

    [Fact]
    public void ReadsLongFormatValuesDescriptionsAndIds()
    {
        var read = Read("""
            [{
              "SourceFile": "C:/a.jpg",
              "ExifTool:ExifTool:ExifToolVersion": {"id": "ExifToolVersion", "desc": "ExifTool Version Number", "val": 13.59},
              "EXIF:ExifIFD:FNumber": {"id": "0x829d", "desc": "F Number", "num": 4, "val": 4.0},
              "EXIF:ExifIFD:ExposureTime": {"id": "0x829a", "desc": "Exposure Time", "num": 0.004, "val": "1/250"},
              "File:System:FileName": {"id": "FileName", "desc": "File Name", "val": "a.jpg"}
            }]
            """);

        Assert.Equal("C:/a.jpg", read.SourceFile);
        Assert.Equal("13.59", read.ExifToolVersion);
        Assert.Equal(4, read.Tags.Count);

        var f = read.Tags[1];
        Assert.Equal("EXIF:ExifIFD:FNumber", f.Key);
        Assert.Equal(("EXIF", "ExifIFD", "FNumber"), (f.Group0, f.Group1, f.Name));
        Assert.Equal("F Number", f.Description);
        Assert.Equal("0x829D", f.DisplayId);
        Assert.Equal("4.0", f.Value);

        // S5: num 4 next to val 4.0 is not a conversion
        Assert.Null(f.Raw);
        Assert.Equal(2, f.FileOrder);

        var t = read.Tags[2];
        Assert.Equal("1/250", t.Value);
        Assert.Equal("0.004", t.Raw);
        Assert.True(t.IsConverted);

        Assert.Equal(TagIdRule.NONE, read.Tags[3].DisplayId);
    }


    [Fact]
    public void ReadsListsBoolsStructsBinaryAndNull()
    {
        var read = Read("""
            [{
              "XMP:XMP-dc:Subject": {"val": ["a", "b", 3]},
              "XMP:XMP-crs:HasSettings": {"val": true},
              "XMP:XMP-x:Flag": {"val": false},
              "JUMBF:CBOR:Actions": {"val": {"action": "c2pa.created"}},
              "EXIF:IFD1:ThumbnailImage": {"val": "(Binary data 5120 bytes, use -b option to extract)"},
              "EXIF:IFD0:Empty": {"val": null}
            }]
            """);

        Assert.Equal(("a, b, 3", TagValueKind.List), (read.Tags[0].Value, read.Tags[0].Kind));
        Assert.Equal(("True", TagValueKind.Bool), (read.Tags[1].Value, read.Tags[1].Kind));
        Assert.Equal("False", read.Tags[2].Value);
        Assert.Equal(TagValueKind.Struct, read.Tags[3].Kind);
        Assert.Contains("c2pa.created", read.Tags[3].Value);
        Assert.True(read.Tags[4].IsBinary);
        Assert.Equal(TagValueKind.Null, read.Tags[5].Kind);
    }


    [Fact]
    public void ReadsShortFormatAndStructValuesThatLookLikeObjects()
    {
        var read = Read("""
            [{
              "PNG:Parameters": "prompt",
              "JUMBF:CBOR:Exclusions": {"start": 20, "length": 4000},
              "EXIF:IFD0:Make": "Canon"
            }]
            """);

        Assert.Equal("prompt", read.Tags[0].Value);
        Assert.Equal(TagValueKind.Struct, read.Tags[1].Kind);
        Assert.Equal("Canon", read.Tags[2].Value);
        Assert.Equal("Make", read.Tags[2].Description);
    }


    [Fact]
    public void KeepsDuplicatesAsFamily4CopiesAndLiftsWarningsAndErrors()
    {
        var read = Read("""
            [{
              "PNG:Parameters": {"val": "first"},
              "PNG:Copy1:Parameters": {"val": "second"},
              "ExifTool:Warning": {"val": "JPEG format error"},
              "ExifTool:Copy1:Warning": {"val": "Another"},
              "ExifTool:Error": {"val": "File format error"}
            }]
            """);

        Assert.Equal(0, read.Tags[0].Copy);
        Assert.Equal(1, read.Tags[1].Copy);
        Assert.Equal(["JPEG format error", "Another"], read.Warnings);
        Assert.Equal("File format error", read.Error);
    }


    [Fact]
    public void ReadsAcrossPipeSegments()
    {
        var json = "[{\"PNG:Parameters\": {\"val\": \"" + new string('x', 10_000) + "\"}, \"EXIF:IFD0:Make\": {\"val\": \"Sony\"}}]";
        var bytes = Encoding.UTF8.GetBytes(json);
        var seq = Segmented(bytes, 997);

        var read = TableReader.Read(seq, new StringPool(), TestContext.Current.CancellationToken);

        Assert.Equal(10_000, read.Tags[0].Value.Length);
        Assert.Equal("Sony", read.Tags[1].Value);
    }


    [Fact]
    public void ReturnsNothingForEmptyOrForeignOutput()
    {
        Assert.Empty(Read("").Tags);
        Assert.Empty(Read("{}").Tags);
        Assert.Empty(Read("[]").Tags);
    }


    [Fact]
    public void StopsWhenCancelled()
    {
        var sb = new StringBuilder("[{");
        for (var i = 0; i < 2000; i++) sb.Append(i == 0 ? "" : ",").Append($"\"EXIF:IFD0:T{i}\": {{\"val\": {i}}}");
        sb.Append("}]");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() => TableReader.Read(new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(sb.ToString())), new StringPool(), cts.Token));
    }


    [Theory]
    [InlineData("EXIF:ExifIFD:FNumber", "EXIF", "ExifIFD", null, 0, "FNumber")]
    [InlineData("PNG:Parameters", "PNG", "PNG", null, 0, "Parameters")]
    [InlineData("PNG:Main:Parameters", "PNG", "PNG", "Main", 0, "Parameters")]
    [InlineData("EXIF:IFD0:Main:Make", "EXIF", "IFD0", "Main", 0, "Make")]
    [InlineData("JUMBF:Doc1-2:JUMDLabel", "JUMBF", "JUMBF", "Doc1-2", 0, "JUMDLabel")]
    [InlineData("JUMBF:CBOR:Doc1-2-1-1:Copy1:Actions", "JUMBF", "CBOR", "Doc1-2-1-1", 1, "Actions")]
    [InlineData("MakerNotes:Sony:Copy12:Rating", "MakerNotes", "Sony", null, 12, "Rating")]
    [InlineData("Name", "", "", null, 0, "Name")]
    public void SplitsKeysOfTwoToFiveParts(string key, string g0, string g1, string? g3, int copy, string name)
    {
        var k = TagKey.Parse(key);

        Assert.Equal((g0, g1, g3, copy, name), (k.Group0, k.Group1, k.Group3, k.Copy, k.Name));
    }


    [Theory]
    [InlineData("EXIF:ExifIFD:Main:FNumber", "EXIF:ExifIFD:FNumber")]
    [InlineData("PNG:Main:Copy1:Parameters", "PNG:Copy1:Parameters")]
    [InlineData("QuickTime:Keys:Main:Prompt", "QuickTime:Keys:Prompt")]
    [InlineData("XMP:XMP-dc:Main:Copy2:Title", "XMP:XMP-dc:Copy2:Title")]
    public void GivesTheTableKeyOfAnAiReadKey(string aiKey, string tableKey) => Assert.Equal(tableKey, TagKey.Parse(aiKey).TableKey);


    [Theory]
    [InlineData("Main", true)]
    [InlineData("Doc1", true)]
    [InlineData("Doc1-2-10", true)]
    [InlineData("Doc", false)]
    [InlineData("Docs", false)]
    [InlineData("DocA-1", false)]
    [InlineData("ExifIFD", false)]
    public void RecognisesFamily3Parts(string part, bool expected) => Assert.Equal(expected, TagKey.IsFamily3(part));


    [Theory]
    [InlineData("EXIF", "0x829d", "0x829D")]
    [InlineData("ICC_Profile", "desc", "desc")]
    [InlineData("XMP", "xmptk", "xmptk")]
    [InlineData("File", "FileName", "—")]
    [InlineData("Composite", "Aperture", "—")]
    [InlineData("ExifTool", "ExifToolVersion", "—")]
    [InlineData("EXIF", "", "—")]
    [InlineData("EXIF", null, "—")]
    [InlineData("MPF", "0x", "0x")]
    [InlineData("EXIF", "0xZZ", "0xZZ")]
    public void AppliesTheTagIdRule(string group, string? id, string expected) => Assert.Equal(expected, TagIdRule.ToDisplay(group, id));


    [Fact]
    public void PoolsShortStringsOnly()
    {
        var pool = new StringPool();
        var a = pool.Get("Camera Model Name");
        var b = pool.Get("Camera Model Name".AsSpan());

        Assert.Same(a, b);
        Assert.Equal(1, pool.Count);
        Assert.Equal(string.Empty, pool.Get(ReadOnlySpan<char>.Empty));
        Assert.NotSame(pool.Get(new string('x', 300)), pool.Get(new string('x', 300)));
    }

    #endregion // Tests


    #region Private methods

    private static TableReadResult Read(string json) =>
        TableReader.Read(new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(json)), new StringPool(), TestContext.Current.CancellationToken);


    private static ReadOnlySequence<byte> Segmented(byte[] bytes, int size)
    {
        Segment? first = null, last = null;
        for (var i = 0; i < bytes.Length; i += size)
        {
            var chunk = bytes.AsMemory(i, Math.Min(size, bytes.Length - i));
            if (first is null) first = last = new Segment(chunk, 0);
            else last = last!.Append(chunk);
        }

        return new ReadOnlySequence<byte>(first!, 0, last!, last!.Memory.Length);
    }

    #endregion // Private methods


    #region Nested types

    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memory, long runningIndex)
        {
            Memory = memory;
            RunningIndex = runningIndex;
        }

        public Segment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new Segment(memory, RunningIndex + Memory.Length);
            Next = next;
            return next;
        }
    }

    #endregion // Nested types
}
