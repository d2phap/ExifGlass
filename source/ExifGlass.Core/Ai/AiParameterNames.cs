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
using System.Collections.Frozen;
using System.Text;

namespace ExifGlass.Core.Ai;

/// <summary>
/// Normalizes the many spellings of a generation parameter into one id, label and order.
/// </summary>
public static class AiParameterNames
{
    #region Fields and constants

    public const string MODEL = "model";
    public const string SAMPLER = "sampler";
    public const string SCHEDULER = "scheduler";
    public const string STEPS = "steps";
    public const string CFG = "cfg";
    public const string SEED = "seed";
    public const string SIZE = "size";
    public const string DENOISE = "denoise";
    public const string CLIP_SKIP = "clipSkip";
    public const string VAE = "vae";
    public const string MODEL_HASH = "modelHash";
    public const string VAE_HASH = "vaeHash";
    public const string DISTILLED_CFG = "distilledCfg";
    public const string GUIDANCE = "guidance";

    private static readonly string[] ORDER = [MODEL, SAMPLER, SCHEDULER, STEPS, CFG, SEED, SIZE];

    private static readonly FrozenDictionary<string, string> LABELS = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [MODEL] = "Model",
        [SAMPLER] = "Sampler",
        [SCHEDULER] = "Scheduler",
        [STEPS] = "Steps",
        [CFG] = "CFG",
        [SEED] = "Seed",
        [SIZE] = "Size",
        [DENOISE] = "Denoise",
        [CLIP_SKIP] = "Clip skip",
        [VAE] = "VAE",
        [MODEL_HASH] = "Model hash",
        [VAE_HASH] = "VAE hash",
        [DISTILLED_CFG] = "Distilled CFG",
        [GUIDANCE] = "Guidance",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string> ALIASES = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Model"] = MODEL, ["base_model"] = MODEL, ["use_stable_diffusion_model"] = MODEL, ["ckpt_name"] = MODEL,
        ["base_model_name"] = MODEL, ["unet_name"] = MODEL,
        ["Sampler"] = SAMPLER, ["sampler_name"] = SAMPLER, ["ksamplerName"] = SAMPLER,
        ["Schedule type"] = SCHEDULER, ["Scheduler"] = SCHEDULER, ["schedule"] = SCHEDULER, ["noise_schedule"] = SCHEDULER,
        ["Steps"] = STEPS, ["num_inference_steps"] = STEPS,
        ["CFG scale"] = CFG, ["cfg"] = CFG, ["cfg_scale"] = CFG, ["cfgscale"] = CFG, ["guidance_scale"] = CFG, ["scale"] = CFG,
        ["Guidance Scale"] = CFG,
        ["Seed"] = SEED, ["noise_seed"] = SEED,
        ["Size"] = SIZE, ["resolution"] = SIZE,
        ["Denoising strength"] = DENOISE, ["denoise"] = DENOISE, ["strength"] = DENOISE,
        ["Clip skip"] = CLIP_SKIP, ["clip_skip"] = CLIP_SKIP, ["CLiP-skip"] = CLIP_SKIP,
        ["VAE"] = VAE, ["use_vae_model"] = VAE, ["vae_name"] = VAE,
        ["Model hash"] = MODEL_HASH, ["base_model_hash"] = MODEL_HASH,
        ["VAE hash"] = VAE_HASH,
        ["Distilled CFG Scale"] = DISTILLED_CFG,
        ["guidance"] = GUIDANCE,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, string> LABEL_OVERRIDES = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["aspectratio"] = "Aspect ratio",
        ["loraweights"] = "LoRA weights",
        ["negativeprompt"] = "Negative prompt",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    #endregion // Fields and constants


    #region Public methods

    /// <summary>
    /// The id and label of a raw key: "CFG scale" becomes ("cfg", "CFG"); an unknown key keeps its text as the label.
    /// </summary>
    public static (string Id, string Label) Normalize(string rawKey)
    {
        if (ALIASES.TryGetValue(rawKey, out var id)) return (id, LABELS[id]);
        if (LABELS.ContainsKey(rawKey)) return (rawKey, LABELS[rawKey]);

        return (ToId(rawKey), LABEL_OVERRIDES.GetValueOrDefault(rawKey) ?? ToLabel(rawKey));
    }


    /// <summary>
    /// The display label of a normalized id.
    /// </summary>
    public static string LabelOf(string id) => LABELS.GetValueOrDefault(id) ?? ToLabel(id);


    /// <summary>
    /// Builds fields in display order: Model, Sampler, Scheduler, Steps, CFG, Seed, Size, then the rest as found; empty values and repeats are dropped.
    /// </summary>
    public static IReadOnlyList<AiField> Build(IEnumerable<(string RawKey, string? Value, string? SourceKey)> pairs)
    {
        var fields = new List<AiField>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (rawKey, value, sourceKey) in pairs)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;

            var (id, label) = Normalize(rawKey);
            if (!seen.Add(id)) continue;

            fields.Add(new AiField(id, label, value.Trim(), rawKey, sourceKey));
        }

        return Order(fields);
    }


    /// <summary>
    /// Sorts fields into display order, keeping the found order after the fixed ones.
    /// </summary>
    public static IReadOnlyList<AiField> Order(IReadOnlyList<AiField> fields) =>
        [.. fields.Select((f, i) => (f, i)).OrderBy(x => Rank(x.f.Id)).ThenBy(x => x.i).Select(x => x.f)];

    #endregion // Public methods


    #region Private methods

    private static int Rank(string id)
    {
        var i = Array.IndexOf(ORDER, id);
        return i < 0 ? ORDER.Length : i;
    }


    // "Hires upscaler" -> "hiresUpscaler", "CFG rescale" -> "cfgRescale", "sui_extra_data" -> "suiExtraData"
    private static string ToId(string rawKey)
    {
        var words = rawKey.Split([' ', '_', '-', '/', '.'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return rawKey;

        var sb = new StringBuilder(rawKey.Length);
        for (var i = 0; i < words.Length; i++)
        {
            var w = words[i];
            if (i == 0) sb.Append(w.Length > 1 && w.All(char.IsUpper) ? w.ToLowerInvariant() : char.ToLowerInvariant(w[0]) + w[1..]);
            else sb.Append(char.ToUpperInvariant(w[0])).Append(w[1..]);
        }

        return sb.ToString();
    }


    // "cfg_rescale" -> "Cfg rescale", "noiseSeed" -> "Noise seed"; A1111 keys are already labels
    private static string ToLabel(string rawKey)
    {
        if (rawKey.Contains(' ', StringComparison.Ordinal)) return rawKey;

        var sb = new StringBuilder(rawKey.Length + 4);
        for (var i = 0; i < rawKey.Length; i++)
        {
            var c = rawKey[i];
            if (c is '_' or '-')
            {
                sb.Append(' ');
                continue;
            }

            if (char.IsUpper(c) && i > 0 && char.IsLower(rawKey[i - 1])) sb.Append(' ').Append(char.ToLowerInvariant(c));
            else sb.Append(sb.Length == 0 ? char.ToUpperInvariant(c) : c);
        }

        return sb.ToString();
    }

    #endregion // Private methods
}
