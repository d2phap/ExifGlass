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
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using ExifGlass.UI.Tokens;

namespace ExifGlass.UI.Gallery;

/// <summary>
/// One state of a gallery component: its caption and a live sample pinned in that state.
/// </summary>
public sealed record GalleryState(string Label, Control Sample, string Note = "");


/// <summary>
/// One component in the gallery: an id, a name, the spec values it follows and its states.
/// </summary>
public sealed record GallerySection(string Id, string Name, string Spec, IReadOnlyList<GalleryState> States);


/// <summary>
/// Lays out gallery sections the way the spec pages do: id and name, the spec line, then each state over its caption.
/// </summary>
public static class DesignGallery
{
    #region Fields and constants

    public const double SECTION_GAP = 36;

    #endregion // Fields and constants


    #region Public methods

    public static Control BuildSection(GallerySection section)
    {
        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(Text(section.Id.ToUpperInvariant(), TextRole.Overline, TokenKeys.TEXT_TERTIARY_BRUSH));
        header.Children.Add(Text(section.Name, TextRole.Title, TokenKeys.TEXT_PRIMARY_BRUSH));
        if (section.Spec.Length > 0)
        {
            var spec = Text(section.Spec, TextRole.Caption, TokenKeys.TEXT_SECONDARY_BRUSH);
            spec.TextWrapping = TextWrapping.Wrap;
            spec.MaxWidth = 620;
            spec.HorizontalAlignment = HorizontalAlignment.Left;
            header.Children.Add(spec);
        }

        var states = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 24, LineSpacing = 20 };
        foreach (var state in section.States)
        {
            var figure = new StackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Left };
            figure.Children.Add(new Panel { MinHeight = 24, HorizontalAlignment = HorizontalAlignment.Left, Children = { state.Sample } });
            figure.Children.Add(Text(state.Label, TextRole.Mono, TokenKeys.ACCENT_TEXT_BRUSH));
            if (state.Note.Length > 0)
            {
                var note = Text(state.Note, TextRole.Meta, TokenKeys.TEXT_TERTIARY_BRUSH);
                note.MaxWidth = 240;
                note.HorizontalAlignment = HorizontalAlignment.Left;
                note.TextWrapping = TextWrapping.Wrap;
                figure.Children.Add(note);
            }

            states.Children.Add(figure);
        }

        var root = new StackPanel { Spacing = 14, Tag = section.Id };
        root.Children.Add(header);
        root.Children.Add(states);
        return root;
    }


    /// <summary>
    /// Builds every section under one theme, on that theme's window colour.
    /// </summary>
    public static ThemeVariantScope BuildThemed(IReadOnlyList<GallerySection> sections, ThemeVariant theme)
    {
        var stack = new StackPanel { Margin = new Thickness(32, 28), Spacing = SECTION_GAP };
        foreach (var section in sections) stack.Children.Add(BuildSection(section));

        var surface = new Border { Child = stack };
        surface[!Border.BackgroundProperty] = surface.GetResourceObservable(TokenKeys.BG_WINDOW_BRUSH).ToBinding();

        var scope = new ThemeVariantScope { RequestedThemeVariant = theme, Child = surface };
        scope[!TextElement.ForegroundProperty] = scope.GetResourceObservable(TokenKeys.TEXT_PRIMARY_BRUSH).ToBinding();
        return scope;
    }


    public static TextBlock Text(string text, TextRole role, string brushKey)
    {
        var block = new TextBlock { Text = text };
        Typography.SetRole(block, role);
        block[!TextBlock.ForegroundProperty] = block.GetResourceObservable(brushKey).ToBinding();
        return block;
    }

    #endregion // Public methods
}
