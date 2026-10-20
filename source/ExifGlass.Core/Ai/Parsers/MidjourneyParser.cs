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
using System.Text.RegularExpressions;

namespace ExifGlass.Core.Ai.Parsers;

/// <summary>
/// Midjourney: the prompt, its --flags and the Job ID in one Description, in PNG text and in XMP (hence -a).
/// </summary>
public sealed partial class MidjourneyParser : IAiParser
{
    #region Public methods

    public void Parse(AiParseContext context)
    {
        foreach (var tag in new[] { context.Free("Description", "PNG"), context.Free("Description", "XMP-dc"), context.Free("ImageDescription", "IFD0") })
        {
            if (tag is null) continue;

            var text = tag.Text;
            var job = JobId().Match(text);
            var flags = Flag().Matches(text);
            if (!job.Success && !flags.Any(f => KnownFlag(f.Groups[1].Value))) continue;

            context.Sources.Add(Read(context, tag, text, job, flags));
            return;
        }
    }

    #endregion // Public methods


    #region Private methods

    [GeneratedRegex(@"\s*Job ID:\s*([0-9a-fA-F-]{8,})", RegexOptions.CultureInvariant)]
    private static partial Regex JobId();


    // --ar 16:9, --stylize 250, --v 7, --no text, --style raw, --tile
    [GeneratedRegex(@"(?:^|\s)--([a-z]+)(?:\s+(?!--)((?:(?!\s--)(?!\s*Job ID:).)+))?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Flag();


    private static bool KnownFlag(string flag) => flag.ToLowerInvariant() is "ar" or "aspect" or "v" or "version" or "niji" or "s" or "stylize"
        or "c" or "chaos" or "q" or "quality" or "seed" or "style" or "w" or "weird" or "tile" or "no" or "iw" or "sref" or "cref" or "sw" or "cw"
        or "p" or "profile" or "exp" or "raw" or "draft" or "r" or "repeat" or "stop" or "personalize" or "oref" or "ow";


    private static string Label(string flag) => flag.ToLowerInvariant() switch
    {
        "ar" or "aspect" => "Aspect ratio",
        "v" or "version" => "Version",
        "niji" => "Niji",
        "s" or "stylize" => "Stylize",
        "c" or "chaos" => "Chaos",
        "q" or "quality" => "Quality",
        "seed" => "Seed",
        "style" => "Style",
        "w" or "weird" => "Weird",
        "iw" => "Image weight",
        "sref" => "Style reference",
        "cref" => "Character reference",
        "oref" => "Omni reference",
        "p" or "profile" or "personalize" => "Personalization",
        _ => char.ToUpperInvariant(flag[0]) + flag[1..].ToLowerInvariant(),
    };


    private static AiSourceInfo Read(AiParseContext context, AiTag tag, string text, Match job, MatchCollection flags)
    {
        var key = tag.TableKey;
        var b = new SourceBuilder(AiSourceKind.Midjourney, "Midjourney");
        b.Use(context, tag);

        var firstFlag = flags.Count > 0 ? flags[0].Index : text.Length;
        var end = Math.Min(firstFlag, job.Success ? job.Index : text.Length);
        b.Prompt(text[..end], AiPromptRole.Positive, string.Empty, key);

        foreach (Match f in flags)
        {
            var flag = f.Groups[1].Value.ToLowerInvariant();
            var value = f.Groups[2].Success ? f.Groups[2].Value.Trim() : "on";

            if (flag == "no") b.Prompt(value, AiPromptRole.Negative, string.Empty, key);
            else if (flag is "v" or "version") b.Version = value;
            else if (flag == "niji") b.Version = "Niji " + value;

            if (flag != "no") b.Param(Label(flag), value, key);
        }

        if (job.Success) b.Param("Job ID", job.Groups[1].Value, key);

        foreach (var name in new[] { "Author", "CreationTime" })
        {
            if (context.Free(name, "PNG") is { } extra)
            {
                b.Use(context, extra);
                b.Param(name == "Author" ? "Author" : "Created", extra.Text, extra.TableKey);
            }
        }

        if (context.Free("DigitalImageGUID", "XMP-iptcExt") is { } guid) b.Use(context, guid);
        if (context.Free("Description", "XMP-dc") is { } xmpDescription && xmpDescription.Text == text) b.Use(context, xmpDescription);

        return b.Build();
    }

    #endregion // Private methods
}
