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
namespace ExifGlass.Core.Metadata;

/// <summary>
/// Matches table rows to the AI read's source keys, including struct tags the table read flattens.
/// </summary>
public sealed class AiSourceMatcher
{
    #region Fields and constants

    private readonly IReadOnlySet<string> _keys;
    private readonly List<(string Group0, string Group1, string Name)> _structs = [];

    #endregion // Fields and constants


    #region Constructors

    public AiSourceMatcher(IEnumerable<string> sourceKeys)
    {
        var keys = new HashSet<string>(sourceKeys, StringComparer.Ordinal);
        _keys = keys;

        // the table read has no -struct: C2PA "Actions" shows as ActionsAction, "Claim_generator_info" as Claim_Generator_InfoName
        foreach (var key in keys)
        {
            var k = TagKey.Parse(key);
            if (k.Group0 is "JUMBF" or "XMP") _structs.Add((k.Group0, k.Group1, Normalize(k.Name)));
        }
    }

    #endregion // Constructors


    #region Public methods

    public bool Matches(TagRecord tag)
    {
        if (_keys.Contains(tag.Key)) return true;
        if (_structs.Count == 0) return false;

        var name = Normalize(tag.Name);
        foreach (var (g0, g1, prefix) in _structs)
        {
            if (g0 == tag.Group0 && g1 == tag.Group1 && name.StartsWith(prefix, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    #endregion // Public methods


    #region Private methods

    private static string Normalize(string name) => name.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    #endregion // Private methods
}
