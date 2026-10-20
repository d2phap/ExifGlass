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

namespace ExifGlass.Core.Ai.Parsers;

/// <summary>
/// SwarmUI: sui_image_params, sui_extra_data and sui_models; numbers often arrive as strings.
/// </summary>
public sealed class SwarmUiParser : IAiParser
{
    #region Fields and constants

    private static readonly string[] SHOWN = ["prompt", "negativeprompt", "model", "seed", "steps", "cfgscale", "width", "height", "sampler", "scheduler", "loras", "loraweights", "swarm_version"];

    #endregion // Fields and constants


    #region Public methods

    public void Parse(AiParseContext context)
    {
        foreach (var tag in new[] { context.Free("Parameters", "PNG"), context.Free("UserComment", "ExifIFD"), context.Free("Model", "IFD0") })
        {
            if (tag is null || !tag.Text.Contains("sui_image_params", StringComparison.Ordinal)) continue;

            using var doc = LenientJson.TryParse(tag.Text, out _);
            if (doc?.RootElement is not { ValueKind: JsonValueKind.Object } root || LenientJson.Prop(root, "sui_image_params") is not { } p) continue;

            context.Sources.Add(Read(context, tag, root, p));
            return;
        }
    }

    #endregion // Public methods


    #region Private methods

    private static AiSourceInfo Read(AiParseContext context, AiTag tag, JsonElement root, JsonElement p)
    {
        var key = tag.TableKey;
        var b = new SourceBuilder(AiSourceKind.SwarmUi, "SwarmUI") { Version = LenientJson.Str(p, "swarm_version") };
        b.Use(context, tag);

        b.Prompt(LenientJson.Str(p, "prompt"), AiPromptRole.Positive, string.Empty, key);
        b.Prompt(LenientJson.Str(p, "negativeprompt"), AiPromptRole.Negative, string.Empty, key);
        b.ModelsFromPrompt(LenientJson.Str(p, "prompt"), key);

        b.Param("model", LenientJson.Str(p, "model"), key);
        b.Param("sampler", LenientJson.Str(p, "sampler"), key);
        b.Param("scheduler", LenientJson.Str(p, "scheduler"), key);
        b.Param("steps", LenientJson.Str(p, "steps"), key);
        b.Param("cfgscale", LenientJson.Str(p, "cfgscale"), key);
        b.Param("seed", LenientJson.Str(p, "seed"), key);
        if (LenientJson.Str(p, "width") is { } w && LenientJson.Str(p, "height") is { } h) b.Param("Size", $"{w}x{h}", key);

        foreach (var prop in p.EnumerateObject())
        {
            if (!SHOWN.Contains(prop.Name)) b.Param(prop.Name, AiTagSet.ToText(prop.Value), key);
        }

        if (LenientJson.Prop(root, "sui_extra_data") is { ValueKind: JsonValueKind.Object } extra)
        {
            b.Param("Date", LenientJson.Str(extra, "date"), key);
            b.Param("Generation time", LenientJson.Str(extra, "generation_time"), key);
        }

        // hashes from sui_models, matched to the model and LoRAs by path without extension
        var hashes = new List<(string Name, string Param, string? Hash)>();
        if (LenientJson.Prop(root, "sui_models") is { ValueKind: JsonValueKind.Array } models)
        {
            hashes.AddRange(models.EnumerateArray().Select(m => (LenientJson.Str(m, "name") ?? string.Empty, LenientJson.Str(m, "param") ?? string.Empty, LenientJson.Str(m, "hash"))));
        }

        string? HashOf(string name, string param) => hashes.FirstOrDefault(h => h.Param == param && SourceBuilder.SameModel(h.Name, name)).Hash;

        if (LenientJson.Str(p, "model") is { } model) b.Model(AiModelKind.Checkpoint, model, null, HashOf(model, "model"), key);

        if (LenientJson.Prop(p, "loras") is { ValueKind: JsonValueKind.Array } loras)
        {
            var weights = LenientJson.Prop(p, "loraweights") is { ValueKind: JsonValueKind.Array } ws ? ws.EnumerateArray().Select(AiTagSet.ToText).ToList() : [];
            var i = 0;
            foreach (var l in loras.EnumerateArray())
            {
                var name = AiTagSet.ToText(l);
                b.Model(AiModelKind.Lora, name, i < weights.Count ? weights[i] : null, HashOf(name, "loras"), key);
                i++;
            }
        }

        return b.Build();
    }

    #endregion // Private methods
}
