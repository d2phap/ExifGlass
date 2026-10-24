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
using Avalonia.Controls.Primitives;
using ExifGlass.Core.Editing;
using ExifGlass.UI.Controls;

namespace ExifGlass.UI.Gallery;

public static partial class DesignGalleryCatalog
{
    #region Fields and constants

    private const double BAR_SAMPLE_WIDTH = 520;

    #endregion // Fields and constants


    #region Sections

    private static GallerySection Editing()
    {
        static PendingBar Bar(string? note = null, bool saving = false) => new()
        {
            Width = BAR_SAMPLE_WIDTH,
            Title = "3 pending changes",
            Subtitle = "Save writes DSCF4127.JPG · backup kept as DSCF4127.JPG_original",
            Note = note,
            IsSaving = saving,
            IsOpen = true,
        };

        static T Pin<T>(T control, string part, Func<Control, Control> state) where T : TemplatedControl
        {
            control.TemplateApplied += (_, e) =>
            {
                if (e.NameScope.Find<Control>(part) is { } target) state(target);
            };
            return control;
        }

        static CommandPanel Command(IReadOnlyList<CommandToken> tokens, bool write = false, bool expanded = false) => new()
        {
            Width = BAR_SAMPLE_WIDTH,
            Tokens = tokens,
            IsWrite = write,
            IsExpanded = expanded,
        };

        IReadOnlyList<CommandToken> read =
        [
            new("exiftool.exe", CommandTokenKind.Executable), new("-config", CommandTokenKind.Flag), new("exifglass.config", CommandTokenKind.Value),
            new("-fast", CommandTokenKind.Flag), new("-G", CommandTokenKind.Flag), new("-t", CommandTokenKind.Flag), new("-m", CommandTokenKind.Flag),
            new("-q", CommandTokenKind.Flag), new("-H", CommandTokenKind.Flag), new("-charset", CommandTokenKind.Flag), new("UTF8", CommandTokenKind.Value),
            new("C:\\Users\\d2pha\\Pictures\\DSCF4127.JPG", CommandTokenKind.Path),
        ];
        IReadOnlyList<CommandToken> write =
        [
            new("exiftool.exe", CommandTokenKind.Executable), new("-config", CommandTokenKind.Flag), new("exifglass.config", CommandTokenKind.Value),
            new("-gps*=", CommandTokenKind.Write), new("\"-IFD0:Artist=Dương Diệu Pháp\"", CommandTokenKind.Write), new("-XMP:Title=Lanterns", CommandTokenKind.Write),
            new("C:\\Users\\d2pha\\Pictures\\DSCF4127.JPG", CommandTokenKind.Path),
        ];

        static SelectedTagCard Tag(SelectedTagView view) => new() { Width = CARD_WIDTH, View = view };

        return new GallerySection("c-editing", "Editing, pending bar, command bar", "DS 6.12, 6.13, 7.3 / S5: the pending bar (Inverse.Bg, radius 20, min 50; Discard ghost at its own colour 14%, Save primary with the turning icon; slides up from +90 in 380 ms with a 220 ms fade; a note line for what Save can't do); the Add-tag card (EXIF, XMP or IPTC; names from ExifTool's tag table; Cancel, Add tag); the command bar (tile, Write chip, token colours; one line collapsed, up to 140 expanded, Copy beside Expand or stacked above it); the selected tag's binary, removed and new states.",
        [
            new("pending bar", Bar(), "DS 6.13"),
            new("Discard :pointerover", Pin(Bar(), "PART_Discard", c => c.Hover()), "its own colour at 14%"),
            new("Save :focus-visible", Pin(Bar(), "PART_Save", c => c.FocusVisible()), "white ring 2 inside"),
            new("saving", Bar(saving: true), "the save icon turns, 800 ms a turn"),
            new("with a note", Bar("The location in the Content Credentials stays: Strip all metadata removes it with the manifest"), "what Save can't do (plan P8)"),
            new("add tag", new AddTagCard { Width = CARD_WIDTH, Groups = ["EXIF", "XMP", "IPTC"], Group = "EXIF" }, "first in the inspector while adding"),
            new("add tag, typed", new AddTagCard { Width = CARD_WIDTH, Groups = ["EXIF", "XMP", "IPTC"], Group = "XMP", TagName = "Title", Value = "Lanterns at the harbour" }),
            new("add tag, unknown name", new AddTagCard { Width = CARD_WIDTH, Groups = ["EXIF", "XMP"], Group = "EXIF", TagName = "Artst", Hint = "ExifTool has no EXIF tag it can write named Artst" }, "Add tag stays off"),
            new("command bar", Command(read), "the read command, one line"),
            new("command bar, write", Command(write, write: true), "the write Save runs, with the Write chip"),
            new("command bar, expanded", Command(read, expanded: true), "wrapped; Copy above Collapse"),
            new("Expand :pointerover", Pin(Command(read), "PART_Toggle", c => c.Hover()), "State.Hover"),
            new("binary tag", Tag(new SelectedTagView("EXIF:IFD1:ThumbnailImage", "EXIF", "0x0201", "Thumbnail Image", "(Binary data 8164 bytes, use -b option to extract)", UiText.SAME_AS_VALUE, "undef", "-IFD1:ThumbnailImage", true, IsBinary: true)), "Save binary data… (D12); no Edit"),
            new("removed by a bulk action", Tag(new SelectedTagView("EXIF:GPS:GPSLatitude", "EXIF", "0x0002", "GPS Latitude", "22 deg 16' 33.24\"", "22.2759", "rational64u[3]", "-GPS:GPSLatitude", false, EditState.Removed)), "struck through; no Delete or Undo of its own"),
            new("new tag", Tag(new SelectedTagView("New:XMP:Title", "XMP", "—", "Title", "Lanterns at the harbour", UiText.SAME_AS_VALUE, "lang-alt", "-XMP:Title", false, EditState.Added)), "staged by Add tag; Edit and Delete"),
        ]);
    }

    #endregion // Sections
}
