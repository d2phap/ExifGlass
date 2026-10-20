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
using System.Globalization;

namespace ExifGlass.Core.ExifTool;

/// <summary>
/// An ExifTool feature the AI read needs and the version that brought it.
/// </summary>
public sealed record ExifToolFeature(string Name, decimal MinVersion);


/// <summary>
/// The AI version gate (R19): what a custom ExifTool is too old to read.
/// </summary>
public static class ExifToolVersion
{
    #region Fields and constants

    public static readonly ExifToolFeature IptcAiFields = new("IPTC 2025 AI fields", 13.40m);
    public static readonly ExifToolFeature C2paEverywhere = new("C2PA Content Credentials in every container", 12.75m);
    public static readonly ExifToolFeature LearningOptOut = new("Exif 3.1 AI-training opt-out", 13.56m);
    public static readonly ExifToolFeature PngAiTags = new("PNG Stable Diffusion tags", 12.81m);

    public static IReadOnlyList<ExifToolFeature> AiFeatures { get; } = [IptcAiFields, C2paEverywhere, LearningOptOut, PngAiTags];

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Parses "13.59" or "13.59\n"; null when it isn't a version.
    /// </summary>
    public static decimal? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var t = text.Trim();
        var space = t.IndexOfAny([' ', '\n', '\r']);
        if (space > 0) t = t[..space];

        return decimal.TryParse(t, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v) ? v : null;
    }


    /// <summary>
    /// The AI features this ExifTool version can't read; empty when it reads them all.
    /// </summary>
    public static IReadOnlyList<ExifToolFeature> MissingAiFeatures(string? version)
    {
        if (Parse(version) is not { } v) return AiFeatures;

        return [.. AiFeatures.Where(f => v < f.MinVersion).OrderByDescending(f => f.MinVersion)];
    }

    #endregion // Public methods
}
