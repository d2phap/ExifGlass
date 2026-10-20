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
namespace ExifGlass.Core.Common;

/// <summary>
/// How the app was started.
/// </summary>
public enum AppMode
{
    Standalone,

    /// <summary>
    /// An ImageGlass 10 tool: --pipe &lt;name&gt; (ImageGlass.SDK).
    /// </summary>
    ImageGlass10,

    /// <summary>
    /// An ImageGlass 9 external tool: --ig-tool-pipe-code=&lt;code&gt; (ImageGlass.Tools, Windows).
    /// </summary>
    ImageGlass9,
}


/// <summary>
/// The parsed command line.
/// </summary>
public sealed record StartupOptions
{
    #region Properties

    public AppMode Mode { get; init; }

    public string? PipeName { get; init; }

    public string? ImageGlass9PipeCode { get; init; }

    /// <summary>
    /// The file to open; ImageGlass 10 mode ignores it and waits for the host's photo.
    /// </summary>
    public string? File { get; init; }

    public IReadOnlyDictionary<string, string> Overrides { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// --theme dark|light for this run.
    /// </summary>
    public string? Theme { get; init; }

    public bool Gallery { get; init; }

    public bool SelfTest { get; init; }

    /// <summary>
    /// --fixtures &lt;folder&gt;: what --selftest reads.
    /// </summary>
    public string? FixturesDirectory { get; init; }

    /// <summary>
    /// --render software: Avalonia's software renderer, the memory reference of §8.
    /// </summary>
    public bool SoftwareRendering { get; init; }

    #endregion // Properties
}


/// <summary>
/// Parses the command line without reflection.
/// </summary>
public static class CommandLine
{
    #region Fields and constants

    public const string PIPE = "--pipe";
    public const string IG9_PIPE_CODE = "--ig-tool-pipe-code=";
    public const string OVERRIDE = "-p:";
    public const string SCHEME = "exifglass:";
    public const string THEME = "--theme";
    public const string GALLERY = "--gallery";
    public const string SELFTEST = "--selftest";
    public const string FIXTURES = "--fixtures";
    public const string RENDER = "--render";

    #endregion // Fields and constants


    #region Public methods

    public static StartupOptions Parse(IReadOnlyList<string> args)
    {
        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var mode = AppMode.Standalone;
        string? pipe = null, ig9 = null, file = null, theme = null, fixtures = null;
        bool gallery = false, selftest = false, software = false;

        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (string.IsNullOrEmpty(arg)) continue;

            // ImageGlass.SDK passes exactly two tokens: --pipe <name>
            if (arg.Equals(PIPE, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Count && !args[i + 1].StartsWith('-'))
                {
                    pipe = args[++i];
                    mode = AppMode.ImageGlass10;
                }

                continue;
            }

            if (arg.StartsWith(IG9_PIPE_CODE, StringComparison.OrdinalIgnoreCase))
            {
                ig9 = arg[IG9_PIPE_CODE.Length..];
                if (ig9.Length > 0 && mode == AppMode.Standalone) mode = AppMode.ImageGlass9;
                continue;
            }

            if (arg.StartsWith(OVERRIDE, StringComparison.OrdinalIgnoreCase))
            {
                var body = arg[OVERRIDE.Length..];
                var eq = body.IndexOf('=');
                if (eq > 0 && body[..eq].Trim() is { Length: > 0 } key) overrides[key] = body[(eq + 1)..];
                continue;
            }

            if (arg.Equals(THEME, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
            {
                theme = args[++i].ToLowerInvariant();
                continue;
            }

            if (arg.Equals(FIXTURES, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
            {
                fixtures = args[++i];
                continue;
            }

            if (arg.Equals(RENDER, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
            {
                software = args[++i].Equals("software", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (arg.Equals(GALLERY, StringComparison.OrdinalIgnoreCase))
            {
                gallery = true;
                continue;
            }

            if (arg.Equals(SELFTEST, StringComparison.OrdinalIgnoreCase))
            {
                selftest = true;
                continue;
            }

            // any other flag belongs to a host or to Avalonia
            if (arg.StartsWith('-')) continue;

            file ??= NormalizeFile(arg);
        }

        return new StartupOptions
        {
            Mode = mode,
            PipeName = pipe,
            ImageGlass9PipeCode = ig9,
            File = mode == AppMode.ImageGlass10 ? null : file,
            Overrides = overrides,
            Theme = theme,
            Gallery = gallery,
            SelfTest = selftest,
            FixturesDirectory = fixtures,
            SoftwareRendering = software,
        };
    }


    /// <summary>
    /// Strips an exifglass: (or exifglass://) prefix and URL-decodes what follows.
    /// </summary>
    public static string NormalizeFile(string arg)
    {
        var value = arg.Trim().Trim('"');
        if (!value.StartsWith(SCHEME, StringComparison.OrdinalIgnoreCase)) return value;

        value = value[SCHEME.Length..];
        if (value.StartsWith("//", StringComparison.Ordinal)) value = value[2..];

        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            return value;
        }
    }

    #endregion // Public methods
}
