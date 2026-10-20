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
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ExifGlass.Core.Ai.Parsers;

/// <summary>
/// A1111 infotext and the tools that write it: A1111, Forge, reForge, SD.Next, Krita AI Diffusion, Civitai.
/// </summary>
public sealed partial class A1111Parser : IAiParser
{
    #region Fields and constants

    // where infotext lives (Appendix E.1); IFD0:UserComment is Fooocus's and is read by FooocusParser first
    private static readonly (string Group, string Name)[] SOURCES =
    [
        ("PNG", "Parameters"), ("ExifIFD", "UserComment"), ("IFD0", "UserComment"), ("File", "Comment"), ("PNG", "Comment"),
    ];

    private static readonly string[] PROMPT_KEYS = ["Hires prompt", "Hires negative prompt", "Raw prompt", "Raw negative prompt"];
    private static readonly string[] HIDDEN_KEYS = ["Civitai resources", "Civitai metadata", "Hashes", "Lora hashes", "TI hashes", "Lora weights"];

    #endregion // Fields and constants


    #region Public methods

    public void Parse(AiParseContext context)
    {
        foreach (var (group, name) in SOURCES)
        {
            var tag = context.Free(name, group);
            if (tag is null) continue;

            if (A1111Infotext.Parse(tag.Text) is not { } info) continue;

            var b = Build(context, info, tag.TableKey);
            b.Use(context, tag);
            if (b.Generator == "Fooocus") b.Use(context, context.Free("Fooocus_scheme", "PNG"));

            // Civitai on-site images: IFD0 Artist "ai" and a GUID Software
            if (context.Tags.Get("IFD0", "Artist") is { Text: "ai" } artist && context.Tags.Get("IFD0", "Software") is { } software && Guid.TryParse(software.Text, out _))
            {
                b.Generator = "Civitai";
                b.Version = null;
                b.Use(context, artist).Use(context, software);
            }

            context.Sources.Add(b.Build());
            return;
        }

        if (context.SidecarText is { } sidecar && A1111Infotext.Parse(sidecar) is { } fromSidecar)
        {
            var b = Build(context, fromSidecar, null);
            b.Warn("Read from the .txt file next to the image");
            context.Sources.Add(b.Build());
        }
    }


    /// <summary>
    /// The generator and version from the infotext's Version and App keys.
    /// </summary>
    public static (string Generator, string? Version) DetectGenerator(A1111Infotext info)
    {
        var version = info.Get("Version")?.Value;

        if (info.Get("App")?.Value is { } app && app.Contains("SD.Next", StringComparison.OrdinalIgnoreCase)) return ("SD.Next", version);
        if (info.Get("Civitai resources") is not null || info.Get("Civitai metadata") is not null) return ("Civitai", null);
        if (info.Get("Model hash")?.Value == "unknown") return ("Krita AI Diffusion", null);

        if (version is null) return ("A1111 format", null);
        if (version.StartsWith("Fooocus", StringComparison.OrdinalIgnoreCase)) return ("Fooocus", version[7..].Trim().TrimStart('v'));
        if (version.Equals("classic", StringComparison.OrdinalIgnoreCase)) return ("Forge Classic", null);
        if (version.StartsWith("neo-", StringComparison.OrdinalIgnoreCase)) return ("Forge Neo", version[4..]);

        if (ForgeVersion().Match(version) is { Success: true } forge) return ("Forge", forge.Groups[1].Value);
        if (ReForgeVersion().Match(version) is { Success: true } reforge) return ("reForge", reforge.Groups[1].Value);
        if (A1111Version().Match(version) is { Success: true } a1111) return ("AUTOMATIC1111", a1111.Groups[1].Value);

        return ("A1111 format", version);
    }

    #endregion // Public methods


    #region Private methods

    // Forge "f2.0.1v1.10.1-previous-669-gdfdcbab6"; reForge "1.0.0v2-v1.10.1RC"; A1111 "v1.10.1", "v1.10.1-96-g<hash>", "1.10.1"
    [GeneratedRegex(@"^f(\d+(?:\.\d+)*)v", RegexOptions.CultureInvariant)]
    private static partial Regex ForgeVersion();


    [GeneratedRegex(@"^(\d+(?:\.\d+)*)v\d.*RC", RegexOptions.CultureInvariant)]
    private static partial Regex ReForgeVersion();


    [GeneratedRegex(@"^v?(\d+\.\d+(?:\.\d+)?)", RegexOptions.CultureInvariant)]
    private static partial Regex A1111Version();


    // "ADetailer prompt 2nd" -> unit label "ADetailer 2nd"
    [GeneratedRegex(@"^ADetailer (negative )?prompt( \d+(?:st|nd|rd|th))?$", RegexOptions.CultureInvariant)]
    private static partial Regex ADetailerPrompt();


    [GeneratedRegex(@"^ADetailer model( \d+(?:st|nd|rd|th))?$", RegexOptions.CultureInvariant)]
    private static partial Regex ADetailerModel();


    [GeneratedRegex(@"Model: ([^,\[]+?)\s*(?:\[([0-9a-fA-F]+)\])?(?:,|$)", RegexOptions.CultureInvariant)]
    private static partial Regex ControlNetModel();


    private static SourceBuilder Build(AiParseContext context, A1111Infotext info, string? sourceKey)
    {
        var (generator, version) = DetectGenerator(info);
        var b = new SourceBuilder(AiSourceKind.A1111, generator) { Version = version, Infotext = info };

        b.Prompt(info.Positive, AiPromptRole.Positive, string.Empty, sourceKey);
        b.Prompt(info.Negative, AiPromptRole.Negative, string.Empty, sourceKey);
        b.ModelsFromPrompt(info.Positive, sourceKey);
        b.ModelsFromPrompt(info.Negative, sourceKey);

        foreach (var p in info.Params)
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (PROMPT_KEYS.Contains(p.Key))
            {
                var label = p.Key.StartsWith("Hires", StringComparison.Ordinal) ? "Hires" : "Raw";
                b.Prompt(p.Value, p.Key.Contains("negative", StringComparison.Ordinal) ? AiPromptRole.Negative : AiPromptRole.Positive, label, sourceKey);
                continue;
            }

            if (ADetailerPrompt().Match(p.Key) is { Success: true } ad)
            {
                b.Prompt(p.Value, ad.Groups[1].Success ? AiPromptRole.Negative : AiPromptRole.Positive, "ADetailer" + ad.Groups[2].Value, sourceKey);
                continue;
            }

            if (!HIDDEN_KEYS.Contains(p.Key)) b.Param(p.Key, p.Value, sourceKey);
        }

        // SD.Next writes "Template: ..." and similar lines after the parameters
        foreach (var line in info.TrailingLines)
        {
            var colon = line.IndexOf(':');
            if (colon > 0) b.Param(line[..colon].Trim(), line[(colon + 1)..].Trim(), sourceKey);
        }

        AddModels(b, info, sourceKey);
        return b;
    }


    private static void AddModels(SourceBuilder b, A1111Infotext info, string? key)
    {
        b.Model(AiModelKind.Checkpoint, info.Get("Model")?.Value, null, NullIfUnknown(info.Get("Model hash")?.Value), key);
        b.Model(AiModelKind.Vae, IsAutoVae(info.Get("VAE")?.Value) ? null : info.Get("VAE")?.Value, null, info.Get("VAE hash")?.Value, key);
        b.Model(AiModelKind.Upscaler, info.Get("Hires upscaler")?.Value, null, null, key);
        b.Model(AiModelKind.Refiner, info.Get("Refiner")?.Value, null, null, key);

        foreach (var p in info.Params)
        {
            if (ADetailerModel().IsMatch(p.Key)) b.Model(AiModelKind.Detailer, p.Value, null, null, key);
            else if (p.Key.StartsWith("Module ", StringComparison.Ordinal))
            {
                b.Model(p.Value.Equals("ae", StringComparison.OrdinalIgnoreCase) ? AiModelKind.Vae : AiModelKind.TextEncoder, p.Value, null, null, key);
            }
            else if (p.Key.StartsWith("ControlNet", StringComparison.Ordinal) && ControlNetModel().Match(p.Value) is { Success: true } cn)
            {
                b.Model(AiModelKind.ControlNet, cn.Groups[1].Value, null, cn.Groups[2].Success ? cn.Groups[2].Value : null, key);
            }
        }

        foreach (var (name, hash) in NameHashList(info.Get("Lora hashes")?.Value)) b.Hash(AiModelKind.Lora, name, hash);
        foreach (var (name, hash) in NameHashList(info.Get("TI hashes")?.Value)) b.Hash(AiModelKind.Embedding, name, hash);

        if (info.Get("Lora weights")?.Value is { } weights)
        {
            foreach (var (name, weight) in NameHashList(weights)) b.Model(AiModelKind.Lora, name, weight, null, key);
        }

        AddCivitaiResources(b, info.Get("Civitai resources")?.RawValue, key);
        AddHashes(b, info.Get("Hashes")?.RawValue);
    }


    private static string? NullIfUnknown(string? hash) => hash is null or "unknown" ? null : hash;


    // "Default (model)", "Automatic", "None": the checkpoint's own VAE, not a model of its own
    private static bool IsAutoVae(string? vae) => vae is null || vae.StartsWith("Default", StringComparison.OrdinalIgnoreCase)
        || vae.StartsWith("Automatic", StringComparison.OrdinalIgnoreCase) || vae.Equals("None", StringComparison.OrdinalIgnoreCase);


    // "add_detail: 7c6bad76eb54, other: 1234"
    private static IEnumerable<(string Name, string Hash)> NameHashList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) yield break;

        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = part.LastIndexOf(':');
            if (colon > 0) yield return (part[..colon].Trim(), part[(colon + 1)..].Trim());
        }
    }


    // Civitai resources: [{"type":"checkpoint","modelName":...,"modelVersionName":...,"weight":0.8}]
    private static void AddCivitaiResources(SourceBuilder b, string? raw, string? key)
    {
        using var doc = LenientJson.TryParse(raw, out _);
        if (doc?.RootElement is not { ValueKind: JsonValueKind.Array } list) return;

        foreach (var r in list.EnumerateArray())
        {
            var type = LenientJson.Str(r, "type")?.ToLowerInvariant();
            var name = LenientJson.Str(r, "modelName") ?? LenientJson.Str(r, "name");
            var versionName = LenientJson.Str(r, "modelVersionName");
            var full = versionName is null ? name : $"{name} ({versionName})";
            var kind = type switch
            {
                "checkpoint" => AiModelKind.Checkpoint,
                "lora" or "locon" or "lycoris" or "dora" => AiModelKind.Lora,
                "embed" or "embedding" or "textualinversion" => AiModelKind.Embedding,
                "vae" => AiModelKind.Vae,
                "upscaler" => AiModelKind.Upscaler,
                "controlnet" => AiModelKind.ControlNet,
                _ => AiModelKind.Other,
            };

            var id = LenientJson.Str(r, "modelVersionId");
            b.Model(kind, full, LenientJson.Str(r, "weight"), id is null ? null : "Civitai " + id, key);
        }
    }


    // Civitai extension: Hashes: {"vae": "...", "lora:add_detail": "...", "model": "..."}
    private static void AddHashes(SourceBuilder b, string? raw)
    {
        using var doc = LenientJson.TryParse(raw, out _);
        if (doc?.RootElement is not { ValueKind: JsonValueKind.Object } map) return;

        foreach (var p in map.EnumerateObject())
        {
            var hash = AiTagSet.ToText(p.Value);
            var colon = p.Name.IndexOf(':');
            var prefix = colon < 0 ? p.Name : p.Name[..colon];
            var name = colon < 0 ? null : p.Name[(colon + 1)..];

            switch (prefix)
            {
                case "lora" or "lyco" when name is not null: b.Hash(AiModelKind.Lora, name, hash); break;
                case "embed" when name is not null: b.Hash(AiModelKind.Embedding, name, hash); break;
                default: break;
            }
        }
    }

    #endregion // Private methods
}
