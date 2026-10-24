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
/// Names a binary tag's bytes for Save binary data (D12): the file type its first bytes say, else .bin.
/// </summary>
public static class BinaryData
{
    #region Public methods

    /// <summary>
    /// The extension without its dot: jpg, png, tif, gif, webp, icc, xml, json or bin.
    /// </summary>
    public static string Extension(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF])) return "jpg";
        if (data.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])) return "png";
        if (data.StartsWith("II*\0"u8) || data.StartsWith("MM\0*"u8)) return "tif";
        if (data.StartsWith("GIF8"u8)) return "gif";
        if (data.Length >= 12 && data.StartsWith("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8)) return "webp";

        // an ICC profile says "acsp" at byte 36
        if (data.Length >= 40 && data[36..40].SequenceEqual("acsp"u8)) return "icc";

        var text = data.TrimStart(" \t\r\n"u8);
        if (text.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF])) text = text[3..];
        if (text.StartsWith("<?xml"u8) || text.StartsWith("<x:xmpmeta"u8)) return "xml";
        if (text.StartsWith("{"u8) || text.StartsWith("["u8)) return "json";

        return "bin";
    }


    /// <summary>
    /// "DSCF4127_ThumbnailImage.jpg": the file's name, the tag's, the type's extension.
    /// </summary>
    public static string SuggestedName(string sourcePath, string tagName, ReadOnlySpan<byte> data) =>
        $"{Path.GetFileNameWithoutExtension(sourcePath)}_{tagName}.{Extension(data)}";

    #endregion // Public methods
}
