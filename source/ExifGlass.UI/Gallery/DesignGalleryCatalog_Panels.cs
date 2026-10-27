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
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ExifGlass.Core.Ai;
using ExifGlass.Core.Editing;
using ExifGlass.Core.Metadata;
using ExifGlass.UI.Controls;
using ExifGlass.UI.Tokens;

namespace ExifGlass.UI.Gallery;

public static partial class DesignGalleryCatalog
{
    #region Fields and constants

    private const double RAIL_CONTENT = 220;
    private const double CARD_WIDTH = 304;

    // Proto's AI sample: A1111 infotext from SD WebUI 1.10.1
    private const string SAMPLE_PROMPT = "a lighthouse on a basalt cliff at blue hour, volumetric fog drifting over the sea, long exposure, 35mm film grain, highly detailed, cinematic lighting, (dramatic sky:1.2), <lora:film_grain_v2:0.6>";

    private static readonly GroupSegment[] SEGMENTS =
    [
        new("ExifTool", 1, true), new("File", 17, true), new("EXIF", 57, true), new("MakerNotes", 23, true),
        new("XMP", 4, true), new("ICC_Profile", 9, true), new("Composite", 17, true),
    ];

    private static Bitmap? _previewSample;

    #endregion // Fields and constants


    #region Sections

    private static GallerySection Rail()
    {
        static FileCard File(string title, string meta, string? extension, IReadOnlyList<GroupSegment>? segments = null, bool preview = false) => new()
        {
            Width = RAIL_CONTENT,
            Title = title,
            Meta = meta,
            Extension = extension,
            Segments = segments,
            Preview = preview ? PreviewSample() : null,
        };

        static RailGroupRow Row(string group, string count, bool shown = true, bool ai = false) => new()
        {
            Width = RAIL_CONTENT,
            Group = group,
            Text = ai ? UiText.AI_GROUP : group,
            Count = count,
            IsShown = shown,
            IsAi = ai,
        };

        // a row's state lives on its two buttons: a hover on both, a press on one (owner after G7)
        static RailGroupRow Part(RailGroupRow row, string part, Func<Button, Button> state)
        {
            row.TemplateApplied += (_, e) =>
            {
                if (e.NameScope.Find<Button>(part) is { } button) state(button);
            };
            return row;
        }

        var hidden = SEGMENTS.Select(s => s.Group is "MakerNotes" or "ICC_Profile" ? s with { IsShown = false } : s).ToArray();
        var header = new DockPanel { Width = RAIL_CONTENT, Height = 22 };
        var all = new GlassButton { Content = "Show all", Classes = { "link" }, Height = 22, FontSize = 11.5 };
        DockPanel.SetDock(all, Dock.Right);
        header.Children.Add(all);
        header.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { DesignGallery.Text("GROUPS", TextRole.Overline, TokenKeys.TEXT_SECONDARY_BRUSH), new Badge { Kind = BadgeKind.Count, Text = "5/7" } },
        });


        return new GallerySection("c-rail", "Rail", "DS 6.10 / S2: file card (Emphasis 3.5%, radius 12; thumbnail 38 radius 10 with the preview, the extension, or a dashed outline; composition bar 6 px, 2 px gaps, square segments clipped to radius 2, growing 1100 ms); group rows 32 in two parts 2 apart (owner after G7): the checkbox, dot and name show or hide, the count jumps; hovering either lights both, a press only its own (hover Emphasis 5%, radius 14); hidden in TextTertiary with the dot at 35%.",
        [
            new("file card", File("DSCF4127.JPG", "JPEG · 18 MB · 7728×5152", "JPG", SEGMENTS), "the extension until the preview comes"),
            new("file card :preview", File("DSCF4127.JPG", "JPEG · 18 MB · 7728×5152", "JPG", SEGMENTS, preview: true), "preview as cover, radius 10"),
            new("two groups hidden", File("DSCF4127.JPG", "JPEG · 18 MB · 7728×5152", "JPG", hidden), "hidden segments shrink to nothing"),
            new("reading", File("Reading…", "ExifTool is parsing the file", null), "dashed thumbnail (Proto)"),
            new("groups header", header, "count 5/7 while some are hidden"),
            new("row", Row("EXIF", "57"), "count mono 11, TextSecondary"),
            new(":pointerover", Part(Part(Row("File", "17"), "PART_Check", b => b.Hover()), "PART_Jump", b => b.Hover()), "both parts, Emphasis 5%"),
            new("name :pressed", Part(Part(Row("File", "17"), "PART_Check", b => b.Pressed()), "PART_Jump", b => b.Hover()), "Emphasis 12% on its part, the box at 0.96"),
            new("count :pressed", Part(Part(Row("File", "17"), "PART_Check", b => b.Hover()), "PART_Jump", b => b.Pressed()), "wide press on the count's part"),
            new("row hidden", Row("ICC_Profile", "9", shown: false), "TextTertiary, dot 35%"),
            new("row filtering", Row("EXIF", "12/57"), "while a search or quick filter runs"),
            new("row AI group", Row("AI", "23", ai: true), "the sparkle in Accent2 (D22)"),
            new("name :focus-visible", Part(Row("XMP", "4"), "PART_Check", b => b.FocusVisible()), "Space shows or hides"),
            new("count :focus-visible", Part(Row("XMP", "4"), "PART_Jump", b => b.FocusVisible()), "Enter or Space jumps"),
        ]);
    }


    private static GallerySection Inspector()
    {
        static SelectedTagView Tag(EditState state = EditState.None, bool readOnly = false) => readOnly
            ? new SelectedTagView("File:System:FileSize", "File", "—", "File Size", "18 MB", "18874368", "int64u", "-System:FileSize", true)
            : new SelectedTagView("EXIF:ExifIFD:FNumber", "EXIF", "0x829D", "F Number", state == EditState.Edited ? "2.8" : "2.0", "2.0", "rational64u", "-ExifIFD:FNumber", false, state, state == EditState.Edited ? "2.0" : null);

        static SelectedTagCard Card(SelectedTagView view, bool editing = false) => new()
        {
            Width = CARD_WIDTH,
            View = view,
            IsEditing = editing,
            EditText = editing ? "FUJIFILM Corporation" : null,
            FocusesEditor = false,
        };

        var source = new SelectedTagView("PNG:Parameters", "PNG", "parameters", "Parameters", SAMPLE_PROMPT, UiText.SAME_AS_VALUE, "string", "-PNG:Parameters", true, AiFields: 23);

        var frame = new FrameInfo(7728, 5152, "3:2", 39.8, 52, 46);
        DetailItem[] details =
        [
            new("Camera", "FUJIFILM X-T5"), new("Lens", "XF33mmF1.4 R LM WR"), new("Captured", "Oct 11, 2026 · 07:42:18", "UTC+08:00"),
            new("Location", "22.2759° N, 114.1455° E", "12 m altitude", "https://www.openstreetmap.org/?mlat=22.2759&mlon=114.1455#map=15/22.2759/114.1455"),
            new("Software", "Digital Camera X-T5 Ver2.10"),
        ];
        ExposureItem[] exposure =
        [
            new("Aperture", 2.0, 1, "f/", "", "max f/1.4"), new("Shutter", 250, 0, "1/", "", "Aperture-priority AE"),
            new("ISO", 160, 0, "", "", "EV -1/3"), new("Focal", 33, 0, "", " mm", "50 mm full-frame eq."),
        ];

        return new GallerySection("c-inspector", "Inspector cards", "DS 6.11 / S4: cards 304 wide, 8 apart, each rising 12 px as it appears (400 ms). Selected tag: Accent 35% border + 3 px ring; chip, Tag ID, Delete or Undo, Close; name Title with the value's Copy at its end; mono value well radius 12, its scroll bar at the edge; Raw (-n), Format, Tag key; Edit primary; edit mode Cancel / Apply; no edit UI for read-only groups; a tag the AI group was read from says how many fields it fed (the AI card is in c-ai). Preview (DS 6.11's Frame, renamed by the owner at G7), Exposure (22 Medium, counting up 1100 ms), Details; each hides when the file has nothing for it.",
        [
            new("selected tag", Card(Tag()), "an editable EXIF tag"),
            new("edited", Card(Tag(EditState.Edited)), "Was: and Revert"),
            new("deleted", Card(Tag(EditState.Deleted)), "struck through; Delete turns into Undo"),
            new("read-only group", Card(Tag(readOnly: true)), "File, Composite, ExifTool, ICC_Profile, PNG: no Delete, no Edit, no badge"),
            new("edit mode", Card(Tag(), editing: true), "mono editor; Cancel (Esc), Apply (Ctrl+Enter)"),
            new("AI source tag", Card(source), "Parsed into 23 AI fields, Go to AI generation; Raw says (same as value) and copies the value (AI addendum, owner after G7)"),
            new("preview, decoding", new FrameCard { Width = CARD_WIDTH, Frame = frame }, "rule of thirds, AF point, ratio and MP chips while a preview is on its way"),
            new("preview", new FrameCard { Width = CARD_WIDTH, Frame = frame, Preview = PreviewSample() }, "the preview as cover; its own shape"),
            new("preview, unknown size", new FrameCard { Width = CARD_WIDTH, Frame = new FrameInfo(0, 0, "—", 0, null, null) }),
            new("exposure", new ExposureCard { Width = CARD_WIDTH, Items = exposure }),
            new("details", new DetailsCard { Width = CARD_WIDTH, Items = details }),
            new("details, no file", new DetailsCard { Width = CARD_WIDTH }, "pulsing skeleton rows"),
        ]);
    }

    #endregion // Sections


    #region Helpers

    private static Bitmap PreviewSample() => _previewSample ??= new Bitmap(AssetLoader.Open(new Uri(string.Format(System.Globalization.CultureInfo.InvariantCulture, LOGO, 312))));

    #endregion // Helpers
}
