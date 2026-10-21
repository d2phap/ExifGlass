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
using Avalonia.Media;

namespace ExifGlass.UI.Tokens;

/// <summary>
/// Metadata group colours (plan 5.2, D17): named groups have their own, any other group a stable pick from a ring, so none shows grey.
/// </summary>
public static partial class GroupPalette
{
    #region Properties

    /// <summary>
    /// Gets the groups with a colour of their own.
    /// </summary>
    public static IReadOnlyCollection<string> NamedGroups => Named.Keys;


    /// <summary>
    /// Gets the number of ring colours other groups draw from.
    /// </summary>
    public static int RingSize => Ring.Length;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Gets a group's colour in a theme.
    /// </summary>
    public static Color Get(string group, bool dark)
    {
        var pair = Named.TryGetValue(group, out var named) ? named : Ring[RingIndex(group)];
        return dark ? pair.Dark : pair.Light;
    }


    /// <summary>
    /// Gets the ring slot of a group without a colour of its own.
    /// </summary>
    public static int RingIndex(string group) => (int)(Hash(group) % (uint)Ring.Length);


    /// <summary>
    /// Gets a ring colour by slot.
    /// </summary>
    public static Color GetRing(int index, bool dark) => dark ? Ring[index].Dark : Ring[index].Light;

    #endregion // Public methods


    #region Private methods

    // FNV-1a over the upper-case name: stable across runs and platforms, unlike string.GetHashCode
    private static uint Hash(string text)
    {
        var hash = 2166136261u;
        foreach (var ch in text)
        {
            hash ^= char.ToUpperInvariant(ch);
            hash *= 16777619u;
        }

        return hash;
    }

    #endregion // Private methods
}
