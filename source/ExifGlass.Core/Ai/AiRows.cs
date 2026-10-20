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

namespace ExifGlass.Core.Ai;

/// <summary>
/// One row of the synthetic "AI generation" group (D22), linked to the tag it came from.
/// </summary>
public sealed record AiRow(string Key, string Name, string Label, string Value, string? SourceKey);


/// <summary>
/// Turns the AI information into the rows of the table's AI group, each field once (Appendix B item 21).
/// </summary>
public static class AiRows
{
    #region Public methods

    public static IReadOnlyList<AiRow> Build(AiGenerationInfo? info)
    {
        var rows = new List<AiRow>();
        if (info is null || info.Sources.Count == 0) return rows;

        var keys = new Dictionary<string, int>(StringComparer.Ordinal);
        void Add(string id, string label, string? value, string? sourceKey)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (rows.Any(r => r.Label == label && r.Value == value)) return;

            var n = keys[id] = keys.GetValueOrDefault(id) + 1;
            var key = n == 1 ? $"AI:{id}" : $"AI:{id}#{n}";
            rows.Add(new AiRow(key, id, label, value, sourceKey));
        }

        var primary = info.Primary;
        var firstKey = primary?.SourceKeys.FirstOrDefault();
        Add("generator", "Generator", info.GeneratorVersion is null ? info.Generator : $"{info.Generator} {info.GeneratorVersion}", firstKey);
        Add("kind", "Kind", AiGenerationInfo.KindLabel(info.Kind), firstKey);

        foreach (var s in info.Sources)
        {
            foreach (var p in s.Prompts)
            {
                var label = (p.Role == AiPromptRole.Positive ? "Prompt" : "Negative prompt") + (p.Label.Length > 0 ? $" ({p.Label})" : string.Empty);
                Add(p.Role == AiPromptRole.Positive ? "prompt" : "negativePrompt", label, p.Text, p.SourceKey ?? s.SourceKeys.FirstOrDefault());
            }
        }

        foreach (var m in info.Models)
        {
            var value = m.Name;
            if (m.Weight is not null) value += " · " + m.Weight;
            if (m.Hash is not null) value += " · " + m.Hash;
            Add(m.Kind.ToString().ToLowerInvariant(), ModelLabel(m.Kind), value, m.SourceKey);
        }

        foreach (var s in info.Sources)
        {
            foreach (var p in s.Parameters)
            {
                if (p.Id is AiParameterNames.MODEL or AiParameterNames.VAE && info.Models.Count > 0) continue;
                Add(p.Id, p.Label, p.Value, p.SourceKey ?? s.SourceKeys.FirstOrDefault());
            }

            foreach (var pass in s.Passes)
            {
                Add("pass", pass.Title, string.Join(", ", pass.Parameters.Select(p => $"{p.Label} {p.Value}")), s.SourceKeys.FirstOrDefault());
            }

            foreach (var w in s.Workflows)
            {
                Add("workflow", "Workflow", string.Create(CultureInfo.InvariantCulture, $"{w.FormatLabel} · {w.NodeCount} nodes"), w.SourceKey);
            }

            AddProvenance(s, Add);
        }

        return rows;
    }

    #endregion // Public methods


    #region Private methods

    private static string ModelLabel(AiModelKind kind) => kind switch
    {
        AiModelKind.Checkpoint => "Model",
        AiModelKind.Unet => "Diffusion model",
        AiModelKind.TextEncoder => "Text encoder",
        AiModelKind.Vae => "VAE",
        AiModelKind.Lora => "LoRA",
        AiModelKind.Embedding => "Embedding",
        AiModelKind.Upscaler => "Upscaler",
        AiModelKind.ControlNet => "ControlNet",
        AiModelKind.Refiner => "Refiner",
        AiModelKind.Detailer => "Detailer",
        _ => "Model",
    };


    private static void AddProvenance(AiSourceInfo s, Action<string, string, string?, string?> add)
    {
        var key = s.SourceKeys.FirstOrDefault();
        if (s.C2pa is { } c2pa)
        {
            add("c2pa", "Content Credentials", $"{c2pa.Manifests.Count} manifest{(c2pa.Manifests.Count == 1 ? string.Empty : "s")} · not verified", key);
            foreach (var a in c2pa.Manifests.SelectMany(m => m.Actions).Where(a => a.DigitalSourceType is not null))
            {
                add("digitalSourceType", "Digital source type", a.DigitalSourceLabel, key);
            }
        }

        if (s.Aigc is { } aigc) add("aigcLabel", "AIGC label", aigc.Meaning, key);

        if (s.Training is { } training)
        {
            add("dataMining", "Data mining", training.DataMining, key);
            add("learningOptOut", "AI training opt-out", training.LearningOptOutIn, key);
        }

        if (s.CharacterName is { } name) add("characterCard", "Character card", name, key);
        if (s.RawText is { } raw && s.Kind == AiKind.UnknownAi && s.Parameters.Count == 0) add("raw", "AI text", raw, key);
    }

    #endregion // Private methods
}
