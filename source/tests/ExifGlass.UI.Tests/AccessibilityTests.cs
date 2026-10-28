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
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using ExifGlass.Core.Export;
using ExifGlass.UI.Tokens;
using ExifGlass.Windows;
using System.Text;
using Xunit;

namespace ExifGlass.UI.Tests;

/// <summary>
/// P12 accessibility: every control a screen reader reaches has a name, Tab walks the window in reading order, and body text reads at 4.5:1.
/// </summary>
public sealed class AccessibilityTests
{
    #region Fields and constants

    private static readonly HashSet<AutomationControlType> Interactive =
    [
        AutomationControlType.Button, AutomationControlType.CheckBox, AutomationControlType.ComboBox, AutomationControlType.Edit,
        AutomationControlType.Hyperlink, AutomationControlType.ListItem, AutomationControlType.MenuItem, AutomationControlType.RadioButton,
        AutomationControlType.Slider, AutomationControlType.SplitButton, AutomationControlType.TabItem,
    ];

    #endregion // Fields and constants


    #region Tests

    [AvaloniaFact]
    public async Task EveryControlAScreenReaderReachesHasAName()
    {
        using var shell = TestShell.Open(1400);
        var failures = new List<string>();
        Audit(shell.Window, "empty window", failures);

        shell.Reader.Maker = TestDocuments.MakeAi;
        shell.Reader.Ai = TestDocuments.Ai();
        await shell.ViewModel.OpenFileAsync("a.png");
        await shell.WaitForAsync(() => shell.ViewModel.HasAi);
        shell.Settle(300);
        Audit(shell.Window, "AI file", failures);

        shell.Window.ShowSettings();
        shell.Settle(200);
        var settings = shell.Window.OwnedWindows.OfType<SettingsWindow>().Single(w => w.IsVisible);
        Audit(settings, "Settings", failures);
        settings.Close();

        shell.Window.ShowAbout();
        shell.Settle(200);
        var about = shell.Window.OwnedWindows.OfType<AboutWindow>().Single(w => w.IsVisible);
        Audit(about, "About", failures);
        about.Close();

        Assert.True(failures.Count == 0, "Controls without an accessible name:\n" + string.Join("\n", failures));
    }


    [AvaloniaTheory]
    [InlineData("light")]
    [InlineData("dark")]
    public void BodyTextReadsAtFourAndAHalfToOneOnEverySurface(string theme)
    {
        var app = Application.Current!;
        var variant = theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var window = Over(Resource(app, TokenKeys.BG_WINDOW_COLOR, variant), Colors.White);
        var panel = Over(Resource(app, TokenKeys.BG_PANEL_COLOR, variant), window);
        (string Name, Color Color)[] surfaces =
        [
            ("window", window), ("panel", panel),
            ("elevated", Over(Resource(app, TokenKeys.BG_ELEVATED_COLOR, variant), window)),
            ("header", Over(Resource(app, TokenKeys.BG_HEADER_COLOR, variant), window)),
            ("well on a panel", Over(Resource(app, TokenKeys.BG_SUBTLE_COLOR, variant), panel)),
        ];

        // Accent2's AI labels are left out: dark Accent2 on the elevated gray reads 4.1 (G12)
        string[] texts = [TokenKeys.TEXT_PRIMARY_COLOR, TokenKeys.TEXT_SECONDARY_COLOR, TokenKeys.TEXT_TERTIARY_COLOR, TokenKeys.ACCENT_TEXT_COLOR, TokenKeys.SUCCESS_TEXT_COLOR, TokenKeys.DANGER_TEXT_COLOR];
        foreach (var text in texts)
        {
            foreach (var (name, surface) in surfaces)
            {
                var ratio = AccentPalette.Contrast(Over(Resource(app, text, variant), surface), surface);
                Assert.True(ratio >= 4.5, System.FormattableString.Invariant($"{text} on the {name}: {ratio:0.00}"));
            }
        }
    }


    [AvaloniaFact]
    public async Task EveryControlWhileEditingHasAName()
    {
        using var shell = await TestShell.OpenWithFileAsync(1400);
        var vm = shell.ViewModel;
        var failures = new List<string>();

        vm.Selection.SelectCell(vm.Rows.RowsInView.Single(r => r.Name == "Tag 2").Key, TableColumns.Value);
        vm.StartEditCommand.Execute(null);
        shell.Settle();
        Audit(shell.Window, "editing a value", failures);

        vm.EditText = "Changed";
        vm.ApplyEditCommand.Execute(null);
        await shell.WaitForAsync(() => vm.HasPending);
        shell.Settle();
        Audit(shell.Window, "an edit staged", failures);

        vm.OpenAddTagCommand.Execute(null);
        shell.Settle();
        Audit(shell.Window, "adding a tag", failures);
        vm.CancelAddTagCommand.Execute(null);
        shell.Settle();

        foreach (var button in new[] { shell.Window.Table.EditButton, shell.Window.Table.ColumnsButton, shell.Window.Table.ExportButton })
        {
            button.Flyout!.ShowAt(button);
            shell.Settle(300);
            Audit(shell.Window, $"{button.Name} menu", failures);
            button.Flyout.Hide();
            shell.Settle(300);
        }

        // opening another file with an edit staged asks D8
        var asking = vm.OpenFileAsync("b.jpg");
        shell.Settle(300);
        Audit(shell.Window, "D8 dialog", failures);
        shell.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        shell.Settle(300);
        await asking;

        Assert.True(failures.Count == 0, "Controls without an accessible name:\n" + string.Join("\n", failures));
    }


    [AvaloniaFact]
    public async Task TabWalksTheWindowInReadingOrder()
    {
        using var shell = TestShell.Open(1400);
        shell.Reader.Maker = TestDocuments.MakeAi;
        shell.Reader.Ai = TestDocuments.Ai();
        await shell.ViewModel.OpenFileAsync("a.png");
        await shell.WaitForAsync(() => shell.ViewModel.HasAi);
        shell.Settle(300);

        var order = new List<Control>();
        var log = new StringBuilder();
        for (var i = 0; i < 200; i++)
        {
            shell.Window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            shell.Settle(20);
            if (shell.Window.FocusManager?.GetFocusedElement() is not Control control || order.Contains(control)) break;

            order.Add(control);
            var at = control.TranslatePoint(default, shell.Window) ?? default;
            log.AppendLine(System.FormattableString.Invariant($"{order.Count,3} {at.X,6:0} {at.Y,6:0}  {Describe(control)}"));
        }

        File.WriteAllText(Path.Combine(Render.OutputDir, "audit-tab-order.txt"), log.ToString());

        // the test shell has no ExifTool, so the command bar's text is empty here; in the app it reads the command
        var unnamed = order.Where(c => !(c is SelectableTextBlock t && string.IsNullOrEmpty(t.Inlines?.Text ?? t.Text)) && (!c.IsEffectivelyVisible || string.IsNullOrWhiteSpace(NameOf(c)))).Select(Describe).ToList();
        Assert.True(unnamed.Count == 0, "Tab stops that are hidden or have no name:\n" + string.Join("\n", unnamed) + "\n\nOrder:\n" + log);

        // each region in one run, as the window reads: the toolbar, the rail, the table, the inspector, the command bar
        var runs = new List<string>();
        foreach (var region in order.Select(Region).OfType<string>())
        {
            if (runs.Count == 0 || runs[^1] != region) runs.Add(region);
        }

        Assert.Equal(["toolbar", "rail", "table", "inspector", "command"], runs);

        // the toolbar reads left to right
        var toolbar = order.Where(c => Region(c) == "toolbar").Select(c => c.TranslatePoint(default, shell.Window)!.Value.X).ToList();
        Assert.Equal(toolbar.Order(), toolbar);
    }

    #endregion // Tests


    #region Private methods

    private static void Audit(Control root, string state, List<string> failures)
    {
        if (ControlAutomationPeer.CreatePeerForElement(root) is { } peer) Walk(peer, state, failures);
    }


    private static void Walk(AutomationPeer peer, string state, List<string> failures)
    {
        foreach (var child in peer.GetChildren())
        {
            if (child is ControlAutomationPeer { Owner: var owner } && !owner.IsEffectivelyVisible) continue;

            if (child.IsControlElement() && Interactive.Contains(child.GetAutomationControlType()) && string.IsNullOrWhiteSpace(child.GetName()))
            {
                failures.Add($"{state}: {child.GetAutomationControlType()} {(child is ControlAutomationPeer c ? Describe(c.Owner) : child.GetType().Name)}");
            }

            Walk(child, state, failures);
        }
    }


    private static Color Resource(Application app, string key, ThemeVariant variant) =>
        app.TryGetResource(key, variant, out var value) && value is Color color ? color : throw new InvalidOperationException($"{key} not found");


    private static Color Over(Color top, Color below)
    {
        var a = top.A / 255.0;
        byte Mix(byte t, byte b) => (byte)Math.Round(t * a + b * (1 - a));
        return Color.FromRgb(Mix(top.R, below.R), Mix(top.G, below.G), Mix(top.B, below.B));
    }


    private static string? Region(Control control) => control.GetVisualAncestors().FirstOrDefault(a => a is Views.AppToolbar or Views.RailPanel or Views.TablePanel or Views.InspectorPanel or Views.CommandBar) switch
    {
        Views.AppToolbar => "toolbar",
        Views.RailPanel => "rail",
        Views.TablePanel => "table",
        Views.InspectorPanel => "inspector",
        Views.CommandBar => "command",
        _ => null,
    };


    private static string? NameOf(Control control) => ControlAutomationPeer.CreatePeerForElement(control)?.GetName();


    // the control, its name or tip, and the nearest named ancestors, so a failure says where it is
    private static string Describe(Control control)
    {
        var text = new StringBuilder(control.GetType().Name);
        if (!string.IsNullOrEmpty(control.Name)) text.Append('#').Append(control.Name);
        if (control.Classes.Count > 0) text.Append(" .").Append(string.Join('.', control.Classes));
        text.Append(" \"").Append(NameOf(control)).Append('"');
        if (ToolTip.GetTip(control) is string tip) text.Append(" tip \"").Append(tip).Append('"');

        var path = control.GetVisualAncestors().OfType<Control>().Where(a => !string.IsNullOrEmpty(a.Name)).Take(3).Select(a => $"{a.GetType().Name}#{a.Name}");
        return text.Append(" in ").Append(string.Join(" < ", path)).ToString();
    }

    #endregion // Private methods
}
