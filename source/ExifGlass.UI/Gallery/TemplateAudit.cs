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
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace ExifGlass.UI.Gallery;

/// <summary>
/// What the template audit found under a visual: controls drawn by a theme that isn't ExifGlass's.
/// </summary>
public sealed record TemplateAuditResult(IReadOnlyList<string> Failures, IReadOnlyList<string> Deferred, int Checked)
{
    #region Properties

    public bool Passed => Failures.Count == 0;

    #endregion // Properties
}


/// <summary>
/// Finds controls still on a SimpleTheme template (O14, gate G3): every templated control under a visual must take an ExifGlass ControlTheme; DataGrid parts are P5's.
/// </summary>
public static class TemplateAudit
{
    #region Public methods

    public static TemplateAuditResult Run(Visual root)
    {
        var ours = OurThemes();
        var failures = new SortedSet<string>(StringComparer.Ordinal);
        var deferred = new SortedSet<string>(StringComparer.Ordinal);
        var checkedCount = 0;

        foreach (var visual in root.GetSelfAndVisualDescendants())
        {
            if (visual is not TemplatedControl control) continue;

            checkedCount++;
            var key = control.StyleKey;
            var theme = control.Theme ?? (control.TryFindResource(key, control.ActualThemeVariant, out var found) ? found as ControlTheme : null);
            if (theme is not null && ours.Contains(theme)) continue;

            var name = key.Name;
            if (name.StartsWith("DataGrid", StringComparison.Ordinal) || control.TemplatedParent?.GetType().Name.StartsWith("DataGrid", StringComparison.Ordinal) == true) deferred.Add(name);
            else failures.Add((theme is null ? $"{name} (no theme)" : name) + (control.TemplatedParent is { } parent ? $" in {parent.GetType().Name}" : string.Empty));
        }

        return new TemplateAuditResult([.. failures], [.. deferred], checkedCount);
    }

    #endregion // Public methods


    #region Private methods

    // every ControlTheme in the app's resources: ours (Themes/Controls.axaml); SimpleTheme keeps its own under Application.Styles
    private static HashSet<ControlTheme> OurThemes()
    {
        var set = new HashSet<ControlTheme>(ReferenceEqualityComparer.Instance);
        if (Application.Current is { } app) Collect(app.Resources, set);
        return set;
    }


    private static void Collect(IResourceDictionary dictionary, HashSet<ControlTheme> set)
    {
        foreach (var value in dictionary.Values)
        {
            if (value is ControlTheme theme) set.Add(theme);
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            if (merged is ResourceInclude { Loaded: { } loaded }) Collect(loaded, set);
            else if (merged is IResourceDictionary inner) Collect(inner, set);
        }
    }

    #endregion // Private methods
}
