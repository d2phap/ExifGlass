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
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using ExifGlass.UI.Controls;
using ExifGlass.UI.Tokens;
using ExifGlass.UI.Windows;

namespace ExifGlass.UI.Gallery;

/// <summary>
/// The Design Gallery window (--gallery, Debug): every control in every state, Light and Dark side by side, the 8-pt grid, and a loud failure for any control left on a SimpleTheme template.
/// </summary>
public sealed class GalleryWindow : Window
{
    #region Fields and constants

    private const int MODE_LIGHT = 0;
    private const int MODE_DARK = 1;
    private const int MODE_BOTH = 2;

    private readonly ScrollViewer _scroller = new() { HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    private readonly GridOverlay _grid = new() { IsVisible = false };
    private readonly Panel _canvas = new();
    private readonly ListBox _index = new() { Width = 220 };
    private readonly TextBlock _audit = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _banner = new() { IsVisible = false, Padding = new Thickness(16, 10) };
    private readonly SegmentedControl _mode = new();
    private IReadOnlyList<GallerySection> _sections = [];

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// Gets the last template audit, for the tests.
    /// </summary>
    public TemplateAuditResult? LastAudit { get; private set; }

    #endregion // Properties


    #region Constructors

    public GalleryWindow()
    {
        Title = "ExifGlass Design Gallery";
        Width = 1480;
        Height = 920;
        MinWidth = 640;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        AppWindow.Prepare(this);

        _mode.ItemsSource = new[] { "Light", "Dark", "Side by side" };
        _mode.SelectedIndex = MODE_BOTH;
        _mode.SelectionChanged += (_, _) => Rebuild();

        var grid = new CheckBox { Content = "8-pt grid", VerticalAlignment = VerticalAlignment.Center };
        grid.IsCheckedChanged += (_, _) => _grid.IsVisible = grid.IsChecked == true;

        var freeze = new CheckBox { Content = "Freeze motion", VerticalAlignment = VerticalAlignment.Center };
        freeze.IsCheckedChanged += (_, _) =>
        {
            MotionTokens.IsFrozen = freeze.IsChecked == true;
            Rebuild();
        };

        var rebuild = new GlassButton { Content = "Replay", Icon = IconId.ArrowSync, VerticalAlignment = VerticalAlignment.Center };
        rebuild.Click += (_, _) => Rebuild();

        var bar = new DockPanel { Height = 56, Margin = new Thickness(16, 0) };
        var title = DesignGallery.Text("Design Gallery", TypeClasses.SUBTITLE, TokenKeys.TEXT_PRIMARY_BRUSH);
        title.VerticalAlignment = VerticalAlignment.Center;
        title.Margin = new Thickness(0, 0, 20, 0);
        DockPanel.SetDock(title, Dock.Left);
        bar.Children.Add(title);

        var tools = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        tools.Children.Add(_mode);
        tools.Children.Add(grid);
        tools.Children.Add(freeze);
        tools.Children.Add(rebuild);
        tools.Children.Add(_audit);
        bar.Children.Add(tools);

        _index.SelectionChanged += Index_SelectionChanged;
        _canvas.Children.Add(_scroller);
        _canvas.Children.Add(_grid);

        var body = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        DockPanel.SetDock(_banner, Dock.Top);
        DockPanel.SetDock(_index, Dock.Left);
        body.Children.Add(bar);
        body.Children.Add(_banner);
        body.Children.Add(_index);
        body.Children.Add(_canvas);
        Content = body;

        _banner[!Border.BackgroundProperty] = _banner.GetResourceObservable(TokenKeys.DANGER_BRUSH).ToBinding();
        Opened += (_, _) => Rebuild();
    }

    #endregion // Constructors


    #region Public methods

    /// <summary>
    /// Builds the sections again in the chosen mode, then audits the templates once laid out.
    /// </summary>
    public void Rebuild()
    {
        _sections = DesignGalleryCatalog.Build();
        _index.ItemsSource = _sections.Select(s => s.Name).ToArray();

        _scroller.Content = _mode.SelectedIndex switch
        {
            MODE_LIGHT => DesignGallery.BuildThemed(_sections, ThemeVariant.Light),
            MODE_DARK => DesignGallery.BuildThemed(_sections, ThemeVariant.Dark),
            _ => SideBySide(),
        };

        Dispatcher.UIThread.Post(AuditNow, DispatcherPriority.Background);
    }


    /// <summary>
    /// Audits the templates now; fails loudly with a red banner naming every control still drawn by SimpleTheme.
    /// </summary>
    public void AuditNow()
    {
        if (_scroller.Content is not Visual content) return;

        var result = TemplateAudit.Run(content);
        LastAudit = result;

        _audit.Text = result.Passed
            ? $"Template check passed: {result.Checked} controls, DataGrid stubs for P5: {result.Deferred.Count}"
            : $"Template check FAILED: {result.Failures.Count}";
        _audit[!TextBlock.ForegroundProperty] = _audit.GetResourceObservable(result.Passed ? TokenKeys.SUCCESS_TEXT_BRUSH : TokenKeys.DANGER_TEXT_BRUSH).ToBinding();

        _banner.IsVisible = !result.Passed;
        _banner.Child = new TextBlock
        {
            Text = "Still on a SimpleTheme template: " + string.Join(", ", result.Failures),
            Foreground = Brushes.White,
            FontWeight = FontWeight.Medium,
            TextWrapping = TextWrapping.Wrap,
        };

        if (!result.Passed) System.Diagnostics.Trace.WriteLine("[gallery] " + _audit.Text + ": " + string.Join(", ", result.Failures));
    }

    #endregion // Public methods


    #region Private methods

    private Control SideBySide()
    {
        var both = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        var light = DesignGallery.BuildThemed(_sections, ThemeVariant.Light);
        var dark = DesignGallery.BuildThemed(DesignGalleryCatalog.Build(), ThemeVariant.Dark);
        Grid.SetColumn(dark, 1);
        both.Children.Add(light);
        both.Children.Add(dark);
        return both;
    }

    #endregion // Private methods


    #region Event handlers

    private void Index_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_index.SelectedIndex < 0 || _index.SelectedIndex >= _sections.Count) return;

        var id = _sections[_index.SelectedIndex].Id;
        if (_scroller.Content is not Visual content) return;

        foreach (var visual in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(content))
        {
            if (visual is Control { Tag: string tag } section && tag == id)
            {
                section.BringIntoView();
                return;
            }
        }
    }

    #endregion // Event handlers
}
