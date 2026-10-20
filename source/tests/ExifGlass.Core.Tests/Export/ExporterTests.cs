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
using ExifGlass.Core.Editing;
using ExifGlass.Core.Export;
using ExifGlass.Core.Query;
using ExifGlass.Core.Tests.Metadata;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ExifGlass.Core.Tests.Export;

public class ExporterTests
{
    #region Fields and constants

    private const string JSON = """
        [{
          "File:FileType": {"id": "FileType", "desc": "File Type", "val": "JPEG"},
          "EXIF:ExifIFD:FNumber": {"id": "0x829d", "desc": "F Number", "val": 4.0},
          "EXIF:IFD0:Artist": {"id": "0x013b", "desc": "Artist", "val": "Jane \"JD\" Doe, Esq."},
          "XMP:XMP-dc:Description": {"id": "description", "desc": "Description", "val": "line one\nline two"},
          "XMP:XMP-crs:HasSettings": {"id": "HasSettings", "desc": "Has Settings", "val": true},
          "MakerNotes:Canon:Copy1:Artist": {"id": "0x0001", "desc": "Artist", "val": "dup"}
        }]
        """;

    #endregion // Fields and constants


    #region Tests

    [Fact]
    public void ExportsCsvWithAGroupColumnAndTheVisibleColumns()
    {
        var csv = Exporter.Export(ExportFormat.Csv, Rows(), TableColumns.Default, "a.jpg");

        Assert.Equal(
            "\"Group\",\"Tag ID\",\"Tag name\",\"Value\"\r\n" +
            "\"File\",\"—\",\"File Type\",\"JPEG\"\r\n" +
            "\"EXIF\",\"0x829D\",\"F Number\",\"4.0\"\r\n" +
            "\"EXIF\",\"0x013B\",\"Artist\",\"Jane \"\"JD\"\" Doe, Esq.\"\r\n" +
            "\"XMP\",\"description\",\"Description\",\"line one\nline two\"\r\n" +
            "\"XMP\",\"HasSettings\",\"Has Settings\",\"True\"\r\n",
            csv);
        Assert.StartsWith("\"Group\",\"#\",\"Value\"\r\n\"File\",\"1\",\"JPEG\"", Exporter.ToCsv(Rows(), TableColumns.Index | TableColumns.Value));
    }


    [Fact]
    public void ExportsPlainTextLikeExifToolTab()
    {
        var text = Exporter.Export(ExportFormat.PlainText, Rows(), TableColumns.All, "a.jpg");

        Assert.Equal(
            "[File]\t1\t—\tFile Type\tJPEG\n" +
            "[EXIF]\t2\t0x829D\tF Number\t4.0\n" +
            "[EXIF]\t3\t0x013B\tArtist\tJane \"JD\" Doe, Esq.\n" +
            "[XMP]\t4\tdescription\tDescription\tline one\\nline two\n" +
            "[XMP]\t5\tHasSettings\tHas Settings\tTrue\n",
            text);
    }


    [Fact]
    public void ExportsJsonCompatibleWithExifToolJG()
    {
        var json = Exporter.Export(ExportFormat.Json, Rows(duplicates: true), TableColumns.None, "C:\\photos\\a.jpg");

        using var doc = JsonDocument.Parse(json);
        var o = doc.RootElement[0];
        Assert.Equal("C:/photos/a.jpg", o.GetProperty("SourceFile").GetString());
        Assert.Equal(JsonValueKind.Number, o.GetProperty("EXIF:FNumber").ValueKind);
        Assert.Equal("4.0", o.GetProperty("EXIF:FNumber").GetRawText());
        Assert.True(o.GetProperty("XMP:HasSettings").GetBoolean());
        Assert.Equal("line one\nline two", o.GetProperty("XMP:Description").GetString());
        Assert.Equal("Jane \"JD\" Doe, Esq.", o.GetProperty("EXIF:Artist").GetString());
        Assert.Equal("dup", o.GetProperty("MakerNotes:Artist").GetString());
        Assert.Contains("ö", Exporter.ToExifToolJson([Row("ö")], "a"), StringComparison.Ordinal);
    }


    [Fact]
    public void ExportsEditedValuesAsText()
    {
        var doc = MetadataDocumentTests.Doc(JSON);
        var f = doc.Find("EXIF", "FNumber")!;
        var rows = QueryEngine.Run(doc, ViewQuery.Default with { Edits = EditSet.Empty.Set(f.Key, f.WriteName, "2.8", f.Value) }, TestContext.Current.CancellationToken).RowsInView;

        using var json = JsonDocument.Parse(Exporter.ToExifToolJson(rows, "a.jpg"));
        Assert.Equal("2.8", json.RootElement[0].GetProperty("EXIF:FNumber").GetString());
    }


    [Fact]
    public void CopiesSelectedRowsAsTsvAndJson()
    {
        var rows = Rows();

        Assert.Equal("File Type\tJPEG\nF Number\t4.0\nArtist\tJane \"JD\" Doe, Esq.\nDescription\tline one\\nline two\nHas Settings\tTrue", Exporter.ToTsv(rows));

        using var doc = JsonDocument.Parse(Exporter.ToJsonObject(rows));
        Assert.Equal(5, doc.RootElement.EnumerateObject().Count());
        Assert.Equal("JPEG", doc.RootElement.GetProperty("File:FileType").GetString());
    }


    [Fact]
    public async Task WritesUtf8WithABomForCsvOnly()
    {
        using var csv = new MemoryStream();
        await Exporter.WriteAsync(csv, ExportFormat.Csv, "ö", TestContext.Current.CancellationToken);
        Assert.Equal([0xEF, 0xBB, 0xBF, 0xC3, 0xB6], csv.ToArray());

        using var txt = new MemoryStream();
        await Exporter.WriteAsync(txt, ExportFormat.PlainText, "ö", TestContext.Current.CancellationToken);
        Assert.Equal(Encoding.UTF8.GetBytes("ö"), txt.ToArray());

        Assert.Equal(["csv", "txt", "json"], new[] { ExportFormat.Csv, ExportFormat.PlainText, ExportFormat.Json }.Select(Exporter.Extension));
    }

    #endregion // Tests


    #region Private methods

    private static IReadOnlyList<ViewRow> Rows(bool duplicates = false) =>
        QueryEngine.Run(MetadataDocumentTests.Doc(JSON), ViewQuery.Default with { ShowDuplicates = duplicates }, TestContext.Current.CancellationToken).RowsInView;


    private static ViewRow Row(string value) => new() { Key = "X:Y", Group = "X", Name = "Y", TagName = "Y", Value = value };

    #endregion // Private methods
}
