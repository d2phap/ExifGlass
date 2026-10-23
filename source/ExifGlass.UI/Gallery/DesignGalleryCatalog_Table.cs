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
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using ExifGlass.Core.Editing;
using ExifGlass.Core.Export;
using ExifGlass.Core.Metadata;
using ExifGlass.Core.Query;
using ExifGlass.UI.Controls;
using ExifGlass.UI.Tokens;
using System.Buffers;
using System.Text;

namespace ExifGlass.UI.Gallery;

public static partial class DesignGalleryCatalog
{
    #region Fields and constants

    // Proto's sample, as ExifTool's -j -l -G0:1:4 read gives it
    private const string SAMPLE_JSON = """
        [{"SourceFile": "C:/Users/d2pha/Pictures/DSCF4127.JPG",
          "ExifTool:ExifTool:ExifToolVersion": {"id": "ExifToolVersion", "desc": "ExifTool Version Number", "val": 13.59},
          "File:System:FileName": {"id": "FileName", "desc": "File Name", "val": "DSCF4127.JPG"},
          "File:System:Directory": {"id": "Directory", "desc": "Directory", "val": "C:/Users/d2pha/Pictures/2026-10 Hong Kong"},
          "File:System:FileSize": {"id": "FileSize", "desc": "File Size", "num": 18874368, "val": "18 MB"},
          "EXIF:IFD0:Make": {"id": "0x010f", "desc": "Make", "val": "FUJIFILM"},
          "EXIF:IFD0:Model": {"id": "0x0110", "desc": "Camera Model Name", "val": "X-T5"},
          "EXIF:IFD0:Artist": {"id": "0x013b", "desc": "Artist", "val": "Duong Dieu Phap"},
          "EXIF:ExifIFD:ExposureTime": {"id": "0x829a", "desc": "Exposure Time", "num": 0.004, "val": "1/250"},
          "EXIF:ExifIFD:FNumber": {"id": "0x829d", "desc": "F Number", "val": 2.0},
          "EXIF:ExifIFD:ISO": {"id": "0x8827", "desc": "ISO", "val": 160},
          "EXIF:ExifIFD:DateTimeOriginal": {"id": "0x9003", "desc": "Date/Time Original", "val": "2026:10:11 07:42:18"},
          "EXIF:GPS:GPSLatitude": {"id": "0x0002", "desc": "GPS Latitude", "num": 22.2759, "val": "22 deg 16' 33.24\" N"},
          "XMP:XMP-x:XMPToolkit": {"id": "xmptk", "desc": "XMP Toolkit", "val": "XMP Core 6.0.0"},
          "XMP:XMP-xmp:Rating": {"id": "Rating", "desc": "Rating", "val": 4}
        }]
        """;

    private static MetadataDocument? _sample;

    #endregion // Fields and constants


    #region Sections

    private static GallerySection Table()
    {
        var doc = _sample ??= MetadataDocument.Create("C:/Users/d2pha/Pictures/DSCF4127.JPG",
            TableReader.Read(new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(SAMPLE_JSON)), new StringPool()));
        string Key(string name) => doc.Tags.First(t => t.Name == name).Key;

        var edits = EditSet.Empty
            .Set(Key("Artist"), "IFD0:Artist", "Dương Diệu Pháp", "Duong Dieu Phap")
            .Delete(Key("GPSLatitude"), "GPS:GPSLatitude", "22 deg 16' 33.24\" N")
            .Add(new AddedTag("XMP", "Title", "Victoria Harbour"));

        return new GallerySection("c-table", "Metadata table", "DS 6.9 / S3: header 32 on Bg.Header with Overline labels and the sort glyph (35% to an AccentText arrow); group headers 36 with chevron, dot, name, count, description; rows 36 with one 3.5% hover layer; one squircle cell ring (1.5 px Accent, Accent 8%, radius 10); selected rows Accent 10% and the 3 px bar; the copy button 28; highlights #FFBA68 60%, radius 3; staged badges.",
        [
            new("rest", Sample(doc, ViewQuery.Default)),
            new("sorted", Sample(doc, ViewQuery.Default with { SortColumn = SortColumn.TagName, SortDirection = SortDirection.Descending }), "TAG NAME descending; within each group"),
            new("row :pointerover", Sample(doc, ViewQuery.Default, after: t => RowOf(t, Key("Directory"))?.Hover()), "Emphasis 3.5%, copy button shown"),
            new("row :pressed", Sample(doc, ViewQuery.Default, after: t => RowOf(t, Key("FileName"))?.Pressed()), "keeps the hover fill, no scale"),
            new("cell selected", Sample(doc, ViewQuery.Default, after: t => t.Selection?.SelectCell(Key("FileName"), TableColumns.Value)), "the ring follows the cell"),
            new("rows selected", Sample(doc, ViewQuery.Default, after: t =>
            {
                t.Selection?.ToggleRow(Key("FileName"));
                t.Selection?.ToggleRow(Key("FileSize"));
            }), "Accent 10% + 3 px bar, scaleY spring 260 ms"),
            new("copied", Sample(doc, ViewQuery.Default, after: t => t.MarkCopied(Key("Directory"))), "green check for 1.3 s"),
            new("search hit", Sample(doc, ViewQuery.Default with { Search = "f" }), "every match, name, value and ID"),
            new("staged edits", Sample(doc, ViewQuery.Default with { Edits = edits, CollapsedGroups = new HashSet<string>(["ExifTool", "File"]) }, rowsShown: 15), "Edited, Delete (struck through), New (index +)"),
            new("group collapsed :pointerover", Sample(doc, ViewQuery.Default with { CollapsedGroups = new HashSet<string>(["File", "EXIF"]) },
                after: t => RowOf(t, TableItem.HeaderKey("XMP"))?.Hover()), "chevron -90 deg, header hover"),
            new("keyboard on a header", Sample(doc, ViewQuery.Default, after: t => t.Selection?.SelectHeader(TableItem.HeaderKey("File"))), "2 px focus ring; arrows collapse and expand"),
            new("sticky header", Sample(doc, ViewQuery.Default, after: t => Scroll(t, 110)), "the group at the top of the rows"),
            new("loading", Sample(doc, null, loading: true), "header + pulsing skeleton rows"),
            new("narrow", Sample(doc, ViewQuery.Default, width: 420, narrow: true), "below 560 # and Tag ID hide; below 460 inset 28, Tag name 44%, no descriptions"),
        ]);
    }

    #endregion // Sections


    #region Helpers

    private static Control Sample(MetadataDocument doc, ViewQuery? query, double width = 540, int rowsShown = 8, bool loading = false, bool narrow = false, Action<MetadataTable>? after = null)
    {
        var view = query is null ? null : QueryEngine.Run(doc, query);
        var rows = view?.Items.Count ?? 6;
        var table = new MetadataTable
        {
            Selection = new TableSelection(),
            View = view,
            IsLoading = loading,
            IsNarrow = narrow,
            HidesIdColumns = narrow,
            SortColumn = query?.SortColumn ?? SortColumn.None,
            SortDirection = query?.SortDirection ?? SortDirection.None,
        };

        var frame = new Border
        {
            Width = width,
            Height = MetadataTable.COLUMN_HEADER_HEIGHT + Math.Min(rows, rowsShown) * MetadataTable.ROW_HEIGHT + 2,
            ClipToBounds = true,
            Child = table,
        };
        Bind(frame, Border.BackgroundProperty, TokenKeys.BG_PANEL_BRUSH);
        Bind(frame, Border.BorderBrushProperty, TokenKeys.STROKE_HAIRLINE_BRUSH);
        frame.BorderThickness = new Thickness(1);

        // the rows exist after the first layout: the forced states go on then, once
        if (after is not null)
        {
            void Apply(object? sender, EventArgs e)
            {
                if (!table.RealizedRows().Any()) return;

                table.LayoutUpdated -= Apply;
                after(table);
            }

            table.LayoutUpdated += Apply;
        }

        return frame;
    }


    private static DataGridRow? RowOf(MetadataTable table, string key) => table.RealizedRows().FirstOrDefault(r => r.DataContext is TableItem item && item.Key == key);


    private static void Scroll(MetadataTable table, double offset)
    {
        if (table.Grid.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault() is { } scroller) scroller.Offset = new Vector(0, offset);
    }

    #endregion // Helpers
}
