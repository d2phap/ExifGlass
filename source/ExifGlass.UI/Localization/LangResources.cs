/*
ExifGlass - AI & EXIF metadata viewer
Copyright (C) 2023 - 2027 DUONG DIEU PHAP
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
using Avalonia.Markup.Xaml.MarkupExtensions;
using ExifGlass.Core.Localization;

namespace ExifGlass.UI.Localization;

/// <summary>
/// Puts <see cref="Lang.Current"/> into the app's resources, keyed by <see cref="LangId"/>, so every <c>{l:Lang Key}</c> in every open window follows a language change.
/// </summary>
public static class LangResources
{
    #region Fields and constants

    private static ResourceDictionary _strings = [];
    private static readonly Dictionary<LangId, object?[]> ARGUMENTS = [];
    private static bool _listening;

    #endregion // Fields and constants


    #region Properties

    /// <summary>
    /// Counts language changes, so a control can tell one from a theme or accent change.
    /// </summary>
    public static int Version { get; private set; }

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Adds the strings to the app's resources and follows <see cref="Lang.CurrentChanged"/> from now on.
    /// </summary>
    public static void Attach(IResourceDictionary resources)
    {
        // one table per app: the headless tests build a new app for each test, and a dictionary has one owner
        if (!resources.MergedDictionaries.Contains(_strings))
        {
            _strings = [];
            resources.MergedDictionaries.Add(_strings);
        }

        Fill();
        if (_listening) return;

        _listening = true;
        Lang.CurrentChanged += Lang_CurrentChanged;
    }


    /// <summary>
    /// Fills a key's placeholders in its resource, for text a XAML attribute can't format: "Open file ({0})" with the platform's shortcut.
    /// </summary>
    public static void SetArguments(LangId key, params object?[] args)
    {
        ARGUMENTS[key] = args;
        if (_strings.Count > 0) _strings[key] = Lang.Current[key, args];
    }


    /// <summary>
    /// Binds a property to a key from code, as <c>{l:Lang Key}</c> does in XAML.
    /// </summary>
    public static void Bind(StyledElement target, AvaloniaProperty property, LangId key) => target.Bind(property, new DynamicResourceExtension(key));


    /// <summary>
    /// Runs <paramref name="refresh"/> when the language changes, for text set in code; it rides the element's own ResourcesChanged, so nothing static holds the element.
    /// </summary>
    public static void Track(StyledElement element, Action refresh)
    {
        var seen = Version;
        element.ResourcesChanged += (_, _) =>
        {
            if (seen == Version) return;

            seen = Version;
            refresh();
        };
    }

    #endregion // Public methods


    #region Private methods

    private static void Fill()
    {
        var lang = Lang.Current;
        var keys = Enum.GetValues<LangId>();
        var items = new KeyValuePair<object, object?>[keys.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            var key = keys[i];
            items[i] = new(key, ARGUMENTS.TryGetValue(key, out var args) ? lang[key, args] : lang[key]);
        }

        // one change notification for the whole table, not one per string
        _strings.SetItems(items);
    }

    #endregion // Private methods


    #region Event handlers

    private static void Lang_CurrentChanged(object? sender, EventArgs e)
    {
        Version++;
        Fill();
    }

    #endregion // Event handlers
}
