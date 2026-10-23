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
using ExifGlass.Core.Ai;
using ExifGlass.Core.Metadata;
using Xunit;

namespace ExifGlass.Core.Tests.Metadata;

public class MetadataDocumentTests
{
    #region Fields and constants

    // a Fujifilm-like JPEG, as the prototype's sample photo
    internal const string CAMERA_JSON = """
        [{
          "SourceFile": "C:/DSCF4127.JPG",
          "ExifTool:ExifTool:ExifToolVersion": {"id": "ExifToolVersion", "desc": "ExifTool Version Number", "val": 13.59},
          "File:System:FileName": {"id": "FileName", "desc": "File Name", "val": "DSCF4127.JPG"},
          "File:System:FileSize": {"id": "FileSize", "desc": "File Size", "num": 18874368, "val": "18 MB"},
          "File:FileType": {"id": "FileType", "desc": "File Type", "val": "JPEG"},
          "File:ImageWidth": {"id": "ImageWidth", "desc": "Image Width", "val": 7728},
          "File:ImageHeight": {"id": "ImageHeight", "desc": "Image Height", "val": 5152},
          "EXIF:IFD0:Make": {"id": "0x010f", "desc": "Make", "val": "FUJIFILM"},
          "EXIF:IFD0:Model": {"id": "0x0110", "desc": "Camera Model Name", "val": "X-T5"},
          "EXIF:IFD0:Software": {"id": "0x0131", "desc": "Software", "val": "Digital Camera X-T5 Ver2.10"},
          "EXIF:ExifIFD:ExposureTime": {"id": "0x829a", "desc": "Exposure Time", "num": 0.004, "val": "1/250"},
          "EXIF:ExifIFD:FNumber": {"id": "0x829d", "desc": "F Number", "val": 5.6},
          "EXIF:ExifIFD:ExposureProgram": {"id": "0x8822", "desc": "Exposure Program", "num": 3, "val": "Aperture-priority AE"},
          "EXIF:ExifIFD:ISO": {"id": "0x8827", "desc": "ISO", "val": 125},
          "EXIF:ExifIFD:DateTimeOriginal": {"id": "0x9003", "desc": "Date/Time Original", "val": "2026:10:12 14:03:22"},
          "EXIF:ExifIFD:OffsetTimeOriginal": {"id": "0x9011", "desc": "Offset Time Original", "val": "+08:00"},
          "EXIF:ExifIFD:ExposureCompensation": {"id": "0x9204", "desc": "Exposure Compensation", "num": -0.333333333333333, "val": "-1/3"},
          "EXIF:ExifIFD:MaxApertureValue": {"id": "0x9205", "desc": "Max Aperture Value", "val": 2.8},
          "EXIF:ExifIFD:FocalLength": {"id": "0x920a", "desc": "Focal Length", "num": 33, "val": "33.0 mm"},
          "EXIF:ExifIFD:FocalLengthIn35mmFormat": {"id": "0xa405", "desc": "Focal Length In 35mm Format", "num": 50, "val": "50 mm"},
          "EXIF:ExifIFD:LensModel": {"id": "0xa434", "desc": "Lens Model", "val": "XF33mmF1.4 R LM WR"},
          "EXIF:GPS:GPSAltitude": {"id": "0x0006", "desc": "GPS Altitude", "num": 12, "val": "12 m Above Sea Level"},
          "MakerNotes:FujiFilm:FilmMode": {"id": "0x1401", "desc": "Film Mode", "num": 1536, "val": "Classic Chrome"},
          "MakerNotes:FujiFilm:FocusPixel": {"id": "0x1031", "desc": "Focus Pixel", "val": "3864 2576"},
          "Composite:GPSPosition": {"id": "GPSPosition", "desc": "GPS Position", "num": "22.2759 114.1455", "val": "22 deg 16' 33.24\" N, 114 deg 8' 43.80\" E"},
          "Composite:ImageSize": {"id": "ImageSize", "desc": "Image Size", "num": "7728 5152", "val": "7728x5152"}
        }]
        """;

    #endregion // Fields and constants


    #region Tests

    [Fact]
    public void GroupsTagsInExifToolOrderWithDescriptions()
    {
        var doc = Doc(CAMERA_JSON);

        Assert.Equal(["ExifTool", "File", "EXIF", "MakerNotes", "Composite"], doc.Groups.Select(g => g.Name));
        Assert.Equal("Fujifilm maker notes", doc.Groups[3].Description);
        Assert.Equal("Camera & capture", doc.Groups[2].Description);
        Assert.Equal(15, doc.Groups[2].Count);
        Assert.Equal("13.59", doc.ExifToolVersion);
    }


    [Fact]
    public void FindsTagsByFamily0Family1AndKey()
    {
        var doc = Doc(CAMERA_JSON);

        Assert.Equal("5.6", doc.Find("EXIF", "FNumber")!.Value);
        Assert.Equal("5.6", doc.Find("ExifIFD", "fnumber")!.Value);
        Assert.Equal("X-T5", doc.GetByKey("EXIF:IFD0:Model")!.Value);
        Assert.Equal("X-T5", doc.FindFirst("Model", "XMP", "EXIF")!.Value);
        Assert.Null(doc.FindFirst("Model", "XMP"));
        Assert.Null(doc.GetByKey("nope"));
    }


    [Fact]
    public void DerivesExposureDetailsFrameAndMeta()
    {
        var d = Doc(CAMERA_JSON).Derived;

        Assert.Equal(["f/5.6", "1/250", "125", "33 mm"], d.Exposure.Select(e => e.Text));
        Assert.Equal(["max f/2.8", "Aperture-priority AE", "EV -1/3", "50 mm full-frame eq."], d.Exposure.Select(e => e.Sub));

        Assert.Equal("FUJIFILM X-T5", Detail(d, "Camera").Value);
        Assert.Equal("XF33mmF1.4 R LM WR", Detail(d, "Lens").Value);
        Assert.Equal(("Oct 12, 2026 · 14:03:22", "UTC+08:00"), (Detail(d, "Captured").Value, Detail(d, "Captured").Sub));
        Assert.Equal(("Classic Chrome", "Film simulation"), (Detail(d, "Look").Value, Detail(d, "Look").Sub));
        Assert.Equal("22.2759° N, 114.1455° E", Detail(d, "Location").Value);
        Assert.Equal("12 m Above Sea Level altitude", Detail(d, "Location").Sub);
        Assert.StartsWith("https://www.openstreetmap.org/?mlat=22.2759&mlon=114.1455", Detail(d, "Location").Link);
        Assert.Equal("Digital Camera X-T5 Ver2.10", Detail(d, "Software").Value);

        Assert.Equal((7728, 5152, "3:2", 39.8), (d.Frame.Width, d.Frame.Height, d.Frame.RatioLabel, d.Frame.Megapixels));
        Assert.True(d.Frame.HasFocusPoint);
        Assert.Equal(50, d.Frame.FocusX!.Value, 1);
        Assert.Equal("7728×5152", d.Frame.SizeLabel);
        Assert.Equal("JPEG · 18 MB · 7728×5152", d.MetaLine);
    }


    [Fact]
    public void DerivesNothingForAFileWithoutCameraData()
    {
        var d = Doc("""[{"PNG:Parameters": {"val": "x"}}]""").Derived;

        Assert.Empty(d.Exposure);
        Assert.Empty(d.Details);
        Assert.Equal("—", d.Frame.RatioLabel);
        Assert.False(d.Frame.HasFocusPoint);
        Assert.Equal(string.Empty, d.MetaLine);
    }


    [Fact]
    public void FormatsLongShuttersSonyFocusAndModelsThatNameTheMake()
    {
        var doc = Doc("""
            [{
              "EXIF:IFD0:Make": {"val": "SONY"},
              "EXIF:IFD0:Model": {"val": "SONY ILCE-7M4"},
              "EXIF:ExifIFD:ExposureTime": {"num": 2.5, "val": "2.5"},
              "EXIF:ExifIFD:CreateDate": {"val": "not a date"},
              "MakerNotes:Sony:FocusLocation": {"val": "6000 4000 1500 1000"},
              "File:ImageWidth": {"val": 6000},
              "File:ImageHeight": {"val": 4000}
            }]
            """);

        Assert.Equal("2.5s", doc.Derived.Exposure[1].Text);
        Assert.Equal("SONY ILCE-7M4", Detail(doc.Derived, "Camera").Value);
        Assert.Equal("not a date", Detail(doc.Derived, "Captured").Value);
        Assert.Equal((25.0, 25.0), (doc.Derived.Frame.FocusX!.Value, doc.Derived.Frame.FocusY!.Value));
    }


    [Theory]
    [InlineData(3, 2, "3:2")]
    [InlineData(1080, 1920, "9:16")]
    [InlineData(2520, 1080, "21:9")]
    [InlineData(1000, 563, "16:9")]
    [InlineData(1000, 300, "3.33:1")]
    [InlineData(0, 100, "—")]
    public void LabelsRatios(int w, int h, string expected) => Assert.Equal(expected, DerivedInfo.RatioLabel(w, h));


    [Theory]
    [InlineData("2026:01:02 03:04:05", "Jan 2, 2026 · 03:04:05")]
    [InlineData("2026:13:02 03:04:05", "2026:13:02 03:04:05")]
    [InlineData("today", "today")]
    public void FormatsDates(string value, string expected) => Assert.Equal(expected, DerivedInfo.FormatDate(value));


    [Fact]
    public void MarksAiSourceTagsIncludingFlattenedC2paRows()
    {
        var doc = Doc("""
            [{
              "PNG:Parameters": {"val": "a\nSteps: 1, Sampler: x, CFG scale: 7"},
              "JUMBF:CBOR:ActionsAction": {"val": "c2pa.created"},
              "JUMBF:CBOR:Claim_Generator_InfoName": {"val": "ChatGPT"},
              "JUMBF:CBOR:Alg": {"val": "sha256"},
              "EXIF:IFD0:Make": {"val": "Canon"}
            }]
            """);
        var ai = new AiGenerationInfo([new AiSourceInfo { Source = AiSourceKind.A1111, Generator = "x", Kind = AiKind.Generated, SourceKeys = ["PNG:Parameters", "JUMBF:CBOR:Actions", "JUMBF:CBOR:Claim_generator_info"] }]);

        var marked = doc.WithAi(ai);

        Assert.Equal(["PNG:Parameters", "JUMBF:CBOR:ActionsAction", "JUMBF:CBOR:Claim_Generator_InfoName"], marked.Tags.Where(t => t.IsAiSource).Select(t => t.Key));
        Assert.Same(ai, marked.Ai);
        Assert.Equal("JUMBF:CBOR:ActionsAction", marked.FindSourceTag("JUMBF:CBOR:Actions")!.Key);
        Assert.Equal("PNG:Parameters", marked.FindSourceTag("PNG:Parameters")!.Key);
        Assert.Null(marked.FindSourceTag("XMP:XMP-dc:Nothing"));
        Assert.Null(doc.WithAi(null).Ai);

        // the AI data arriving is the same read; reading the file again is not (the table reveals a new read, P5)
        Assert.True(marked.IsSameRead(doc));
        Assert.True(doc.IsSameRead(doc.WithAi(null)));
        Assert.False(doc.IsSameRead(Doc(CAMERA_JSON)));
        Assert.False(doc.IsSameRead(null));
    }


    [Fact]
    public void FlagsReadOnlyAndWritableTagsWithTheTagDb()
    {
        var tagDb = TagDb.LoadEmbedded()!;
        var doc = Doc(CAMERA_JSON, tagDb);

        Assert.True(doc.Find("EXIF", "FNumber")!.IsWritable);
        Assert.True(doc.Find("File", "FileName")!.IsReadOnly);
        Assert.True(doc.Find("Composite", "ImageSize")!.IsReadOnly);
        Assert.True(doc.Find("MakerNotes", "FilmMode")!.IsWritable);
    }


    [Fact]
    public void ReadsFileSizeAndTimeFromDiskWhenTheFileExists()
    {
        var path = Path.Combine(Path.GetTempPath(), "exifglass-doc-" + Guid.NewGuid().ToString("N") + ".bin");
        File.WriteAllBytes(path, new byte[1234]);
        try
        {
            var doc = MetadataDocument.Create(path, Read(CAMERA_JSON));
            Assert.Equal(1234, doc.FileSize);
            Assert.NotNull(doc.FileModified);
            Assert.Equal(Path.GetFileName(path), doc.FileName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    #endregion // Tests


    #region Private methods

    internal static TableReadResult Read(string json) =>
        TableReader.Read(new System.Buffers.ReadOnlySequence<byte>(System.Text.Encoding.UTF8.GetBytes(json)), new StringPool());


    internal static MetadataDocument Doc(string json, TagDb? tagDb = null) => MetadataDocument.Create("C:/DSCF4127.JPG", Read(json), tagDb);


    private static DetailItem Detail(DerivedInfo d, string label) => d.Details.Single(x => x.Label == label);

    #endregion // Private methods
}
