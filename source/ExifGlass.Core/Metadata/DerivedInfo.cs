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
using System.Text.RegularExpressions;

namespace ExifGlass.Core.Metadata;

/// <summary>
/// One Exposure card readout: the number counts up, the text is what it settles on.
/// </summary>
public sealed record ExposureItem(string Label, double Number, int Decimals, string Prefix, string Suffix, string Sub)
{
    #region Properties

    public string Text => Prefix + Number.ToString("F" + Decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) + Suffix;

    #endregion // Properties
}


/// <summary>
/// One Details card row; Link is set for a location.
/// </summary>
public sealed record DetailItem(string Label, string Value, string Sub = "", string? Link = null);


/// <summary>
/// The Frame card: size, ratio, megapixels and the autofocus point in percent of the frame.
/// </summary>
public sealed record FrameInfo(int Width, int Height, string RatioLabel, double Megapixels, double? FocusX, double? FocusY)
{
    #region Properties

    public bool HasFocusPoint => FocusX is not null && FocusY is not null;

    public string SizeLabel => Width > 0 && Height > 0 ? $"{Width}×{Height}" : string.Empty;

    #endregion // Properties
}


/// <summary>
/// What the inspector cards and the rail file card show, computed once per document.
/// </summary>
public sealed partial record DerivedInfo(
    IReadOnlyList<ExposureItem> Exposure,
    IReadOnlyList<DetailItem> Details,
    FrameInfo Frame,
    string MetaLine)
{
    #region Fields and constants

    private static readonly string[] MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
    private static readonly (int A, int B)[] RATIOS = [(3, 2), (2, 3), (4, 3), (3, 4), (16, 9), (9, 16), (1, 1), (5, 4), (4, 5), (21, 9)];
    private static readonly string[] DEFAULT_GROUPS = ["EXIF", "Composite", "MakerNotes", "File", "XMP", "PNG"];

    public static DerivedInfo Empty { get; } = new([], [], new FrameInfo(0, 0, "—", 0, null, null), string.Empty);

    #endregion // Fields and constants


    #region Public methods

    public static DerivedInfo From(MetadataDocument doc)
    {
        var exposure = BuildExposure(doc);
        var frame = BuildFrame(doc);
        var details = BuildDetails(doc);

        var type = doc.Find("File", "FileType")?.Value;
        var size = doc.Find("File", "FileSize")?.Value;
        var meta = string.Join(" · ", new[] { type, size, frame.SizeLabel }.Where(s => !string.IsNullOrEmpty(s)));

        return new DerivedInfo(exposure, details, frame, meta);
    }


    /// <summary>
    /// The ratio chip: "3:2" for a known ratio, otherwise "1.78:1"; "—" without a size.
    /// </summary>
    public static string RatioLabel(int width, int height)
    {
        if (width <= 0 || height <= 0) return "—";

        var r = (double)width / height;
        foreach (var (a, b) in RATIOS)
        {
            if (Math.Abs(r - (double)a / b) < 0.01) return $"{a}:{b}";
        }

        return r.ToString("F2", CultureInfo.InvariantCulture) + ":1";
    }


    /// <summary>
    /// "2026:10:12 14:03:22" becomes "Oct 12, 2026 · 14:03:22"; anything else stays as it is.
    /// </summary>
    public static string FormatDate(string value)
    {
        var m = ExifDate().Match(value);
        if (!m.Success) return value;

        var month = int.Parse(m.Groups[2].ValueSpan, CultureInfo.InvariantCulture);
        if (month is < 1 or > 12) return value;

        var day = int.Parse(m.Groups[3].ValueSpan, CultureInfo.InvariantCulture);
        return $"{MONTHS[month - 1]} {day}, {m.Groups[1].Value} · {m.Groups[4].Value}";
    }

    #endregion // Public methods


    #region Private methods

    [GeneratedRegex(@"(\d{4}):(\d\d):(\d\d) (\d\d:\d\d:\d\d)", RegexOptions.CultureInvariant)]
    private static partial Regex ExifDate();


    private static TagRecord? Find(MetadataDocument doc, string name) => doc.FindFirst(name, DEFAULT_GROUPS);


    private static double Number(TagRecord? tag)
    {
        if (tag is null) return double.NaN;

        var text = tag.RawOrValue.Trim();
        var space = text.IndexOf(' ');
        if (space > 0) text = text[..space];

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN;
    }


    private static List<ExposureItem> BuildExposure(MetadataDocument doc)
    {
        var fNumber = Find(doc, "FNumber");
        var time = Find(doc, "ExposureTime");
        var iso = Find(doc, "ISO");
        var focal = doc.Find("EXIF", "FocalLength");
        if (fNumber is null && time is null && iso is null && focal is null) return [];

        var compensation = Find(doc, "ExposureCompensation");
        var program = Find(doc, "ExposureProgram");
        var maxAperture = Find(doc, "MaxApertureValue");
        var focal35 = Find(doc, "FocalLengthIn35mmFormat");

        var t = Number(time);
        var items = new List<ExposureItem>(4)
        {
            new("Aperture", Number(fNumber), 1, "f/", string.Empty, maxAperture is null ? string.Empty : "max f/" + maxAperture.Value),
            t is > 0 and < 1
                ? new("Shutter", Math.Round(1 / t), 0, "1/", string.Empty, program?.Value ?? string.Empty)
                : new("Shutter", t, 1, string.Empty, "s", program?.Value ?? string.Empty),
            new("ISO", Number(iso), 0, string.Empty, string.Empty, compensation is null ? string.Empty : "EV " + compensation.Value),
            new("Focal", Number(focal), 0, string.Empty, " mm", focal35 is null ? string.Empty : focal35.Value + " full-frame eq."),
        };

        return items;
    }


    private static FrameInfo BuildFrame(MetadataDocument doc)
    {
        var (w, h) = ImageSize(doc);
        double? fx = null, fy = null;

        if (w > 0 && h > 0)
        {
            // Fujifilm FocusPixel "x y"; Sony FocusLocation "width height x y"
            if (doc.Find("MakerNotes", "FocusPixel") is { } pixel && Ints(pixel.RawOrValue) is [var x, var y] && x > 0 && y > 0)
            {
                fx = x * 100.0 / w;
                fy = y * 100.0 / h;
            }
            else if (doc.Find("MakerNotes", "FocusLocation") is { } location && Ints(location.RawOrValue) is [var lw, var lh, var lx, var ly] && lw > 0 && lh > 0)
            {
                fx = lx * 100.0 / lw;
                fy = ly * 100.0 / lh;
            }
        }

        var mp = w > 0 && h > 0 ? Math.Round(w * (double)h / 1_000_000, 1) : 0;
        return new FrameInfo(w, h, RatioLabel(w, h), mp, fx, fy);
    }


    private static (int Width, int Height) ImageSize(MetadataDocument doc)
    {
        if (doc.Find("Composite", "ImageSize") is { } size && Ints(size.RawOrValue.Replace('x', ' ')) is [var w, var h]) return (w, h);

        var width = Number(doc.FindFirst("ImageWidth", "File", "EXIF", "PNG", "RIFF", "QuickTime"));
        var height = Number(doc.FindFirst("ImageHeight", "File", "EXIF", "PNG", "RIFF", "QuickTime"));
        return (double.IsFinite(width) && width > 0 ? (int)width : 0, double.IsFinite(height) && height > 0 ? (int)height : 0);
    }


    private static int[] Ints(string text)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return [];
            result[i] = (int)Math.Round(v);
        }

        return result;
    }


    private static List<DetailItem> BuildDetails(MetadataDocument doc)
    {
        var details = new List<DetailItem>(6);

        var make = Find(doc, "Make");
        var model = Find(doc, "Model");
        if (make is not null || model is not null)
        {
            var camera = model is not null && make is not null && !model.Value.StartsWith(make.Value, StringComparison.OrdinalIgnoreCase)
                ? make.Value + " " + model.Value
                : (model ?? make)!.Value;
            details.Add(new DetailItem("Camera", camera));
        }

        if ((Find(doc, "LensModel") ?? doc.Find("Composite", "LensID")) is { } lens) details.Add(new DetailItem("Lens", lens.Value));

        if ((doc.Find("EXIF", "DateTimeOriginal") ?? Find(doc, "CreateDate")) is { } captured)
        {
            var offset = Find(doc, "OffsetTimeOriginal") ?? Find(doc, "OffsetTime");
            details.Add(new DetailItem("Captured", FormatDate(captured.Value), offset is null ? string.Empty : "UTC" + offset.Value));
        }

        if (doc.Find("MakerNotes", "FilmMode") is { } film) details.Add(new DetailItem("Look", film.Value, "Film simulation"));

        if (doc.Find("Composite", "GPSPosition") is { } position && Doubles(position.RawOrValue) is [var lat, var lon])
        {
            var altitude = doc.Find("EXIF", "GPSAltitude") ?? doc.Find("Composite", "GPSAltitude");
            var text = string.Create(CultureInfo.InvariantCulture, $"{Math.Abs(lat):F4}° {(lat < 0 ? 'S' : 'N')}, {Math.Abs(lon):F4}° {(lon < 0 ? 'W' : 'E')}");
            var link = string.Create(CultureInfo.InvariantCulture, $"https://www.openstreetmap.org/?mlat={lat}&mlon={lon}#map=15/{lat}/{lon}");
            details.Add(new DetailItem("Location", text, altitude is null ? string.Empty : altitude.Value + " altitude", link));
        }

        if (doc.Find("EXIF", "Software") is { } software) details.Add(new DetailItem("Software", software.Value));

        return details;
    }


    private static double[] Doubles(string text)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var result = new double[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i])) return [];
        }

        return result;
    }

    #endregion // Private methods
}
