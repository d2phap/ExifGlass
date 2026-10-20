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
using System.Text;
using System.Text.Json;

namespace ExifGlass.Core.Ai.Parsers;

/// <summary>
/// IPTC digital source type, the IPTC 2025.1 AI fields and Google's credit lines.
/// </summary>
public sealed class IptcParser : IAiParser
{
    #region Public methods

    public void Parse(AiParseContext context)
    {
        var dst = context.Free("DigitalSourceType", "XMP-iptcExt");
        var system = context.Free("AISystemUsed", "XMP-iptcExt");
        var version = context.Free("AISystemVersionUsed", "XMP-iptcExt");
        var prompt = context.Free("AIPromptInformation", "XMP-iptcExt");
        var writer = context.Free("AIPromptWriterName", "XMP-iptcExt");
        var credit = context.Free("Credit", "XMP-photoshop");
        var googleCredit = credit is not null && credit.Text.Contains("Google AI", StringComparison.OrdinalIgnoreCase) ? credit : null;
        if (dst is null && system is null && prompt is null && googleCredit is null) return;

        var b = new SourceBuilder(AiSourceKind.Iptc, system?.Text ?? (googleCredit is not null ? "Google AI" : "IPTC"));
        b.Use(context, dst).Use(context, system).Use(context, version).Use(context, prompt).Use(context, writer).Use(context, googleCredit);
        b.Version = version?.Text;

        var kind = DigitalSourceTypes.Kind(dst?.Text);
        if (googleCredit is not null && kind < AiKind.EditedWithAi)
        {
            kind = googleCredit.Text.StartsWith("Edited", StringComparison.OrdinalIgnoreCase) ? AiKind.EditedWithAi : AiKind.Generated;
        }

        if (kind == AiKind.None && (system is not null || prompt is not null)) kind = AiKind.UnknownAi;
        b.Kind = kind;

        b.Prompt(prompt?.Text, AiPromptRole.Positive, "Prompt information", prompt?.TableKey);
        b.Param("Prompt writer", writer?.Text, writer?.TableKey);
        b.Param("Digital source type", dst is null ? null : DigitalSourceTypes.Label(dst.Text), dst?.TableKey);
        b.Param("Credit", googleCredit?.Text, googleCredit?.TableKey);

        b.Iptc = new IptcAiInfo(dst?.Text, dst is null ? null : DigitalSourceTypes.Label(dst.Text), system?.Text, version?.Text, prompt?.Text, writer?.Text, googleCredit?.Text);
        context.Sources.Add(b.Build());
    }

    #endregion // Public methods
}


/// <summary>
/// The China GB 45438-2025 implicit label: XMP TC260:AIGC, PNG AIGC (wrapped), MP4 Keys, Matroska, ID3 and Vorbis tags.
/// </summary>
public sealed class AigcParser : IAiParser
{
    #region Public methods

    public void Parse(AiParseContext context)
    {
        var tag = context.FreeWithName("AIGC").FirstOrDefault();
        if (tag is null) return;

        using var doc = LenientJson.TryParse(tag.Text, out var warning);
        if (doc?.RootElement is not { ValueKind: JsonValueKind.Object } root) return;

        // PNG wraps it: {"AIGC": {...}}
        var label = LenientJson.Prop(root, "AIGC") is { ValueKind: JsonValueKind.Object } inner ? inner : root;
        var code = LenientJson.Str(label, "Label") ?? string.Empty;
        var meaning = code switch
        {
            "1" => "AI-generated",
            "2" => "Possibly AI-generated",
            "3" => "Suspected AI-generated",
            _ => "Unknown label " + code,
        };

        var produceId = LenientJson.Str(label, "ProduceID");
        var b = new SourceBuilder(AiSourceKind.Aigc, Producer(produceId) ?? "AIGC label");
        b.Use(context, tag);
        if (warning is not null) b.Warn(warning);
        b.Kind = code == "1" ? AiKind.Generated : AiKind.UnknownAi;

        b.Param("AIGC label", meaning, tag.TableKey);
        b.Param("Content producer", LenientJson.Str(label, "ContentProducer"), tag.TableKey);
        b.Param("Produce ID", produceId, tag.TableKey);
        b.Param("Content propagator", LenientJson.Str(label, "ContentPropagator"), tag.TableKey);
        b.Param("Propagate ID", LenientJson.Str(label, "PropagateID"), tag.TableKey);

        b.Aigc = new AigcLabel(code, meaning, LenientJson.Str(label, "ContentProducer"), produceId, LenientJson.Str(label, "ContentPropagator"),
            LenientJson.Str(label, "PropagateID"), LenientJson.Str(label, "ReservedCode1"), LenientJson.Str(label, "ReservedCode2"));
        context.Sources.Add(b.Build());
    }

    #endregion // Public methods


    #region Private methods

    // producer ids are registration codes; some services prefix their own name
    private static string? Producer(string? produceId) => produceId?.ToLowerInvariant() switch
    {
        { } id when id.StartsWith("kling", StringComparison.Ordinal) => "Kling",
        { } id when id.StartsWith("doubao", StringComparison.Ordinal) => "Doubao",
        { } id when id.StartsWith("jimeng", StringComparison.Ordinal) => "Jimeng",
        _ => null,
    };

    #endregion // Private methods
}


/// <summary>
/// AI-training signals: PLUS DataMining and Exif 3.1 LearningOptOutIn; they say nothing about how the image was made.
/// </summary>
public sealed class TrainingSignalsParser : IAiParser
{
    #region Public methods

    public void Parse(AiParseContext context)
    {
        var mining = context.Free("DataMining", "XMP-plus");
        var optOut = context.Free("LearningOptOutIn", "ExifIFD");
        if (mining is null && optOut is null) return;

        var b = new SourceBuilder(AiSourceKind.TrainingSignals, "AI-training signals") { Kind = AiKind.None };
        b.Use(context, mining).Use(context, optOut);
        b.Training = new TrainingSignals(mining?.Text, optOut?.Text);
        context.Sources.Add(b.Build());
    }

    #endregion // Public methods
}


/// <summary>
/// LLM character cards (PNG chara / ccv3, base64 JSON): labelled as such, never as image generation.
/// </summary>
public sealed class CharacterCardParser : IAiParser
{
    #region Public methods

    public void Parse(AiParseContext context)
    {
        var v3 = context.Free("Ccv3", "PNG");
        var v2 = context.Free("Chara", "PNG");
        if (v3 is null && v2 is null) return;

        var b = new SourceBuilder(AiSourceKind.CharacterCard, "Character card") { Kind = AiKind.CharacterCard };
        b.Use(context, v3).Use(context, v2);

        foreach (var tag in new[] { v3, v2 })
        {
            if (tag is null || Decode(tag.Text) is not { } json) continue;

            using var doc = LenientJson.TryParse(json, out _);
            if (doc?.RootElement is not { ValueKind: JsonValueKind.Object } root) continue;

            var data = LenientJson.Prop(root, "data") ?? root;
            b.CharacterName = LenientJson.Str(data, "name");
            b.Version = LenientJson.Str(root, "spec");
            break;
        }

        context.Sources.Add(b.Build());
    }

    #endregion // Public methods


    #region Private methods

    private static string? Decode(string base64)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(base64.Trim()));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    #endregion // Private methods
}


/// <summary>
/// An AI tool named in Software, CreatorTool, Make or Model when no parser found its data: Unknown AI with the name.
/// </summary>
public sealed class ToolNameParser : IAiParser
{
    #region Public methods

    public void Parse(AiParseContext context)
    {
        if (context.HasGeneration) return;

        foreach (var (group, name) in new[] { ("IFD0", "Software"), ("PNG", "Software"), ("XMP-xmp", "CreatorTool"), ("IFD0", "Make"), ("IFD0", "Model"), ("IFD0", "Artist") })
        {
            if (context.Free(name, group) is not { } tag || AiNames.ToolName(tag.Text) is not { } tool) continue;

            var b = new SourceBuilder(AiSourceKind.ToolName, tool) { Kind = AiKind.UnknownAi, RawText = tag.Text };
            b.Use(context, tag);
            b.Param(name, tag.Text, tag.TableKey);
            context.Sources.Add(b.Build());
            return;
        }
    }

    #endregion // Public methods
}


/// <summary>
/// PNG text chunks with AI keywords that no parser understood: Unknown AI with the raw text (R13).
/// </summary>
public sealed class UnknownAiParser : IAiParser
{
    #region Public methods

    public void Parse(AiParseContext context)
    {
        var tags = context.Tags.InGroup("PNG").Where(t => !context.IsClaimed(t) && AiNames.IsAiPngKeyword(t.Name) && t.Text.Trim().Length > 0).ToList();
        if (tags.Count == 0) return;

        var b = new SourceBuilder(AiSourceKind.UnknownAi, "Unknown AI") { Kind = AiKind.UnknownAi, RawText = string.Join("\n\n", tags.Select(t => t.Text)) };
        foreach (var t in tags)
        {
            b.Use(context, t);
            b.Param(t.Name, t.Text.Length > 200 ? t.Text[..200] + "…" : t.Text, t.TableKey);
        }

        b.Warn("AI data in a format ExifGlass doesn't know yet; the raw text is shown");
        context.Sources.Add(b.Build());
    }

    #endregion // Public methods
}
