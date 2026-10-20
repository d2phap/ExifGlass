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
using System.Buffers;
using System.Collections.Frozen;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ExifGlass.Core.Ai;

/// <summary>
/// The AI exports: generation parameters as A1111 text, normalized JSON, the workflow as embedded.
/// </summary>
public static class AiExport
{
    #region Fields and constants

    // normalized ids -> A1111 infotext keys, so "Read generation parameters" in A1111/Forge fills the same settings
    private static readonly FrozenDictionary<string, string> A1111_KEYS = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [AiParameterNames.STEPS] = "Steps",
        [AiParameterNames.SAMPLER] = "Sampler",
        [AiParameterNames.SCHEDULER] = "Schedule type",
        [AiParameterNames.CFG] = "CFG scale",
        [AiParameterNames.SEED] = "Seed",
        [AiParameterNames.SIZE] = "Size",
        [AiParameterNames.MODEL_HASH] = "Model hash",
        [AiParameterNames.MODEL] = "Model",
        [AiParameterNames.VAE_HASH] = "VAE hash",
        [AiParameterNames.VAE] = "VAE",
        [AiParameterNames.DENOISE] = "Denoising strength",
        [AiParameterNames.CLIP_SKIP] = "Clip skip",
        [AiParameterNames.DISTILLED_CFG] = "Distilled CFG Scale",
        [AiParameterNames.GUIDANCE] = "Distilled CFG Scale",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // ComfyUI / k-diffusion sampler names -> A1111 labels
    private static readonly FrozenDictionary<string, string> SAMPLERS = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["euler"] = "Euler", ["euler_ancestral"] = "Euler a", ["euler_a"] = "Euler a", ["k_euler"] = "Euler", ["k_euler_ancestral"] = "Euler a",
        ["heun"] = "Heun", ["dpm_2"] = "DPM2", ["dpm_2_ancestral"] = "DPM2 a", ["lms"] = "LMS", ["k_lms"] = "LMS",
        ["dpm_fast"] = "DPM fast", ["dpm_adaptive"] = "DPM adaptive", ["dpmpp_2s_ancestral"] = "DPM++ 2S a", ["dpmpp_sde"] = "DPM++ SDE",
        ["dpmpp_sde_gpu"] = "DPM++ SDE", ["dpmpp_2m"] = "DPM++ 2M", ["dpmpp_2m_sde"] = "DPM++ 2M SDE", ["dpmpp_2m_sde_gpu"] = "DPM++ 2M SDE",
        ["dpmpp_3m_sde"] = "DPM++ 3M SDE", ["dpmpp_3m_sde_gpu"] = "DPM++ 3M SDE", ["ddim"] = "DDIM", ["uni_pc"] = "UniPC", ["lcm"] = "LCM",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, string> SCHEDULERS = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["karras"] = "Karras", ["exponential"] = "Exponential", ["sgm_uniform"] = "SGM Uniform", ["simple"] = "Simple", ["normal"] = "Normal",
        ["ddim_uniform"] = "DDIM", ["beta"] = "Beta", ["kl_optimal"] = "KL Optimal", ["polyexponential"] = "Polyexponential", ["uniform"] = "Uniform",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonWriterOptions WRITER_OPTIONS = new() { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// Copy generation parameters: the infotext exactly as embedded when the source is A1111-family, otherwise built from the parsed fields.
    /// </summary>
    public static string ToA1111(AiSourceInfo source)
    {
        if (source.Infotext is { } info) return info.Format();

        var positive = source.Prompts.FirstOrDefault(p => p.Role == AiPromptRole.Positive && p.Label.Length == 0)?.Text
            ?? source.Prompts.FirstOrDefault(p => p.Role == AiPromptRole.Positive)?.Text ?? string.Empty;
        var negative = source.Prompts.FirstOrDefault(p => p.Role == AiPromptRole.Negative && p.Label.Length == 0)?.Text
            ?? source.Prompts.FirstOrDefault(p => p.Role == AiPromptRole.Negative)?.Text;

        var pairs = new List<(string Key, string Value)>();
        foreach (var p in source.Parameters)
        {
            if (!A1111_KEYS.TryGetValue(p.Id, out var key)) continue;
            if (pairs.Any(x => x.Key == key)) continue;

            var value = p.Id switch
            {
                AiParameterNames.SAMPLER => SAMPLERS.GetValueOrDefault(p.Value, p.Value),
                AiParameterNames.SCHEDULER => SCHEDULERS.GetValueOrDefault(p.Value, p.Value),
                AiParameterNames.MODEL => Path.GetFileNameWithoutExtension(p.Value),
                _ => p.Value,
            };
            pairs.Add((key, value));
        }

        // A1111 sorts the parameters it writes in this order; it reads them in any order
        string[] order = ["Steps", "Sampler", "Schedule type", "CFG scale", "Distilled CFG Scale", "Seed", "Size", "Model hash", "Model", "VAE hash", "VAE", "Denoising strength", "Clip skip"];
        pairs = [.. pairs.OrderBy(p => Array.IndexOf(order, p.Key) is var i && i < 0 ? order.Length : i)];

        var loras = source.Models.Where(m => m.Kind == AiModelKind.Lora && m.Hash is not null).Select(m => $"{Path.GetFileNameWithoutExtension(m.Name)}: {m.Hash}").ToList();
        if (loras.Count > 0) pairs.Add(("Lora hashes", string.Join(", ", loras)));

        var version = source.GeneratorVersion is null ? source.Generator : $"{source.Generator} {source.GeneratorVersion}";
        pairs.Add(("Version", version));

        return A1111Infotext.Create(positive, negative, pairs).Format();
    }


    /// <summary>
    /// The AI card's "Copy as JSON": every source with its prompts, parameters, models, workflows and provenance, normalized.
    /// </summary>
    public static string ToJson(AiGenerationInfo info) => ToJson(info, stable: false);


    /// <summary>
    /// The same JSON; stable leaves out C2PA manifest labels, which re-signing changes (the golden outputs use it).
    /// </summary>
    public static string ToJson(AiGenerationInfo info, bool stable)
    {
        var buffer = new ArrayBufferWriter<byte>(4096);
        using (var w = new Utf8JsonWriter(buffer, WRITER_OPTIONS))
        {
            w.WriteStartObject();
            w.WriteString("generator", info.Generator);
            WriteOptional(w, "version", info.GeneratorVersion);
            w.WriteString("kind", AiGenerationInfo.KindLabel(info.Kind));
            w.WriteBoolean("isAi", info.IsAi);

            w.WriteStartArray("sources");
            foreach (var s in info.Sources) WriteSource(w, s, stable);
            w.WriteEndArray();

            if (info.Warnings.Count > 0)
            {
                w.WriteStartArray("warnings");
                foreach (var warning in info.Warnings) w.WriteStringValue(warning);
                w.WriteEndArray();
            }

            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    #endregion // Public methods


    #region Private methods

    private static void WriteOptional(Utf8JsonWriter w, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value)) w.WriteString(name, value);
    }


    private static void WriteSource(Utf8JsonWriter w, AiSourceInfo s, bool stable)
    {
        w.WriteStartObject();
        w.WriteString("source", s.Source.ToString());
        w.WriteString("generator", s.Generator);
        WriteOptional(w, "version", s.GeneratorVersion);
        w.WriteString("kind", AiGenerationInfo.KindLabel(s.Kind));

        if (s.Prompts.Count > 0)
        {
            w.WriteStartArray("prompts");
            foreach (var p in s.Prompts)
            {
                w.WriteStartObject();
                w.WriteString("role", p.Role == AiPromptRole.Positive ? "positive" : "negative");
                WriteOptional(w, "label", p.Label);
                w.WriteString("text", p.Text);
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        if (s.Parameters.Count > 0)
        {
            w.WriteStartObject("parameters");
            foreach (var p in s.Parameters) w.WriteString(p.Id, p.Value);
            w.WriteEndObject();
        }

        if (s.Models.Count > 0)
        {
            w.WriteStartArray("models");
            foreach (var m in s.Models)
            {
                w.WriteStartObject();
                w.WriteString("kind", m.Kind.ToString());
                w.WriteString("name", m.Name);
                WriteOptional(w, "weight", m.Weight);
                WriteOptional(w, "hash", m.Hash);
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        if (s.Passes.Count > 0)
        {
            w.WriteStartArray("passes");
            foreach (var pass in s.Passes)
            {
                w.WriteStartObject();
                w.WriteString("title", pass.Title);
                w.WriteStartObject("parameters");
                foreach (var p in pass.Parameters) w.WriteString(p.Id, p.Value);
                w.WriteEndObject();
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        if (s.Workflows.Count > 0)
        {
            w.WriteStartArray("workflows");
            foreach (var wf in s.Workflows)
            {
                w.WriteStartObject();
                w.WriteString("format", wf.FormatLabel);
                w.WriteNumber("nodes", wf.NodeCount);
                w.WriteNumber("links", wf.LinkCount);
                w.WriteNumber("samplers", wf.SamplerCount);
                w.WriteNumber("models", wf.ModelCount);
                w.WriteNumber("subgraphs", wf.SubgraphCount);
                w.WriteNumber("bytes", wf.Size);
                w.WriteEndObject();
            }

            w.WriteEndArray();
        }

        WriteProvenance(w, s, stable);

        w.WriteStartArray("sourceTags");
        foreach (var k in s.SourceKeys) w.WriteStringValue(k);
        w.WriteEndArray();

        if (s.Warnings.Count > 0)
        {
            w.WriteStartArray("warnings");
            foreach (var warning in s.Warnings) w.WriteStringValue(warning);
            w.WriteEndArray();
        }

        w.WriteEndObject();
    }


    private static void WriteProvenance(Utf8JsonWriter w, AiSourceInfo s, bool stable)
    {
        if (s.C2pa is { } c2pa)
        {
            w.WriteStartObject("c2pa");
            w.WriteBoolean("verified", c2pa.IsVerified);
            WriteOptional(w, "remoteManifest", c2pa.RemoteManifestUrl);
            w.WriteStartArray("manifests");
            foreach (var m in c2pa.Manifests)
            {
                w.WriteStartObject();
                if (!stable) w.WriteString("label", m.Label);
                w.WriteBoolean("active", m.IsActive);
                w.WriteNumber("claimVersion", m.ClaimVersion);
                WriteOptional(w, "claimGenerator", m.ClaimGenerator);
                WriteOptional(w, "claimGeneratorVersion", m.ClaimGeneratorVersion);
                WriteOptional(w, "title", m.Title);
                w.WriteStartArray("actions");
                foreach (var a in m.Actions)
                {
                    w.WriteStartObject();
                    w.WriteString("action", a.Action);
                    WriteOptional(w, "softwareAgent", a.SoftwareAgent);
                    WriteOptional(w, "softwareAgentVersion", a.SoftwareAgentVersion);
                    WriteOptional(w, "digitalSourceType", a.DigitalSourceType);
                    WriteOptional(w, "description", a.Description);
                    w.WriteEndObject();
                }

                w.WriteEndArray();
                w.WriteStartArray("ingredients");
                foreach (var i in m.Ingredients)
                {
                    w.WriteStartObject();
                    WriteOptional(w, "title", i.Title);
                    WriteOptional(w, "relationship", i.Relationship);
                    if (!stable) WriteOptional(w, "manifest", i.ManifestLabel);
                    else w.WriteBoolean("linked", i.ManifestLabel is not null);
                    w.WriteEndObject();
                }

                w.WriteEndArray();
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        if (s.Iptc is { } iptc)
        {
            w.WriteStartObject("iptc");
            WriteOptional(w, "digitalSourceType", iptc.DigitalSourceType);
            WriteOptional(w, "aiSystem", iptc.AiSystem);
            WriteOptional(w, "aiSystemVersion", iptc.AiSystemVersion);
            WriteOptional(w, "promptInformation", iptc.PromptInformation);
            WriteOptional(w, "promptWriter", iptc.PromptWriter);
            WriteOptional(w, "credit", iptc.Credit);
            w.WriteEndObject();
        }

        if (s.Aigc is { } aigc)
        {
            w.WriteStartObject("aigc");
            w.WriteString("label", aigc.Label);
            w.WriteString("meaning", aigc.Meaning);
            WriteOptional(w, "contentProducer", aigc.ContentProducer);
            WriteOptional(w, "produceId", aigc.ProduceId);
            WriteOptional(w, "contentPropagator", aigc.ContentPropagator);
            WriteOptional(w, "propagateId", aigc.PropagateId);
            w.WriteEndObject();
        }

        if (s.Training is { } training)
        {
            w.WriteStartObject("aiTraining");
            WriteOptional(w, "dataMining", training.DataMining);
            WriteOptional(w, "learningOptOutIn", training.LearningOptOutIn);
            w.WriteEndObject();
        }

        WriteOptional(w, "characterName", s.CharacterName);
    }

    #endregion // Private methods
}
