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
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Xunit;

namespace ExifGlass.UI.Tests;

/// <summary>
/// The main window in the DS 2.1 bands and its states, both themes, against per-OS baselines (G4: layout against the reference screenshots).
/// </summary>
public class ShellSnapshotTests
{
    #region Fields and constants

    private const string FILE = "C:/Users/d2pha/Pictures/DSCF4127.JPG";

    #endregion // Fields and constants


    #region Tests

    [AvaloniaTheory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task TheShellMatchesItsSnapshots(string theme)
    {
        var variant = theme == "dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var failures = new List<string>();

        foreach (var width in new double[] { 1400, 1100, 760, 480 })
        {
            using var shell = TestShell.Open(width, theme: variant);
            await shell.ViewModel.OpenFileAsync(FILE);
            await shell.WaitForAsync(() => shell.ViewModel.HasDocument);
            Check(shell, $"shell-{width}", theme, failures);

            if (width == 1100)
            {
                shell.ViewModel.ToggleInspectorCommand.Execute(null);
                Check(shell, "shell-inspector-overlay-1100", theme, failures);
            }
            else if (width == 480)
            {
                shell.ViewModel.ToggleRailCommand.Execute(null);
                Check(shell, "shell-rail-overlay-480", theme, failures);
            }
        }

        using (var empty = TestShell.Open(1400, theme: variant))
        {
            Check(empty, "shell-empty-1400", theme, failures);

            empty.Window.Table.EmptyCard.IsDragOver = true;
            Check(empty, "shell-drag-empty-1400", theme, failures);
        }

        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    #endregion // Tests


    #region Private methods

    private static void Check(TestShell shell, string id, string theme, List<string> failures)
    {
        shell.Settle(250);
        var frame = shell.Window.CaptureRenderedFrame() ?? throw new InvalidOperationException("no frame rendered");
        Render.Save(frame, $"shell/{id}-{theme}");
        if (Render.CompareToBaseline(frame, id, theme) is { } failure) failures.Add(failure);
    }

    #endregion // Private methods
}
