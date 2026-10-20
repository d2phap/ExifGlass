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
/// Interns the strings that repeat across reads: groups, names, descriptions and IDs.
/// </summary>
public sealed class StringPool
{
    #region Fields and constants

    private const int MAX_COUNT = 50_000;
    private const int MAX_LENGTH = 256;

    private readonly Lock _lock = new();
    private readonly HashSet<string> _set = new(StringComparer.Ordinal);
    private readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> _lookup;

    #endregion // Fields and constants


    #region Constructors

    public StringPool() => _lookup = _set.GetAlternateLookup<ReadOnlySpan<char>>();

    #endregion // Constructors


    #region Properties

    public int Count
    {
        get
        {
            lock (_lock) return _set.Count;
        }
    }

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Returns the pooled copy of a short string; long ones are never pooled.
    /// </summary>
    public string Get(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty) return string.Empty;
        if (value.Length > MAX_LENGTH) return value.ToString();

        lock (_lock)
        {
            if (_lookup.TryGetValue(value, out var pooled)) return pooled;

            // a pool that only grows would hold every value of every file ever opened
            if (_set.Count >= MAX_COUNT) _set.Clear();

            var text = value.ToString();
            _set.Add(text);
            return text;
        }
    }

    #endregion // Public methods
}
