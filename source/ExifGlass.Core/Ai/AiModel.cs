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
namespace ExifGlass.Core.Ai;

/// <summary>
/// What a file is, from its AI data: ordered so that a larger value is the stronger AI claim.
/// </summary>
public enum AiKind
{
    None = 0,

    /// <summary>
    /// Provenance says a camera or a person made it (digitalCapture, humanEdits...).
    /// </summary>
    Captured,

    /// <summary>
    /// An LLM character card: AI-related, but not image generation.
    /// </summary>
    CharacterCard,

    /// <summary>
    /// AI signals without a known schema; the raw text is shown.
    /// </summary>
    UnknownAi,

    EditedWithAi,

    Composite,

    Generated,
}


/// <summary>
/// The generator family or standard a source was parsed from.
/// </summary>
public enum AiSourceKind
{
    A1111,
    ComfyUi,
    InvokeAi,
    NovelAi,
    Fooocus,
    SwarmUi,
    EasyDiffusion,
    DrawThings,
    Midjourney,
    TensorArt,
    C2pa,
    Iptc,
    Aigc,
    TrainingSignals,
    ToolName,
    CharacterCard,
    UnknownAi,
}


public enum AiPromptRole
{
    Positive,
    Negative,
}


public enum AiModelKind
{
    Checkpoint,
    Unet,
    TextEncoder,
    Vae,
    Lora,
    Embedding,
    Upscaler,
    ControlNet,
    Refiner,
    Detailer,
    Other,
}


public enum AiWorkflowFormat
{
    ComfyUiApi,
    ComfyUiWorkflow,
    InvokeAiWorkflow,
    InvokeAiGraph,
}


/// <summary>
/// One parsed value: a normalized id ("cfg"), the label shown ("CFG"), the text, the raw key and the tag it came from.
/// </summary>
public sealed record AiField(string Id, string Label, string Value, string RawKey, string? SourceKey = null);


/// <summary>
/// One prompt with its weight and LoRA syntax kept; Label tells regional or per-pass prompts apart.
/// </summary>
public sealed record AiPrompt(string Text, AiPromptRole Role, string Label = "", string? SourceKey = null);


/// <summary>
/// A model, VAE, LoRA, embedding, upscaler or ControlNet with its weight and hash.
/// </summary>
public sealed record AiModelRef(AiModelKind Kind, string Name, string? Weight = null, string? Hash = null, string? SourceKey = null);


/// <summary>
/// One sampler of a multi-pass generation (base, refiner, hires, detailer), kept apart from the others.
/// </summary>
public sealed record AiSamplerPass(string Title, IReadOnlyList<AiField> Parameters, IReadOnlyList<AiPrompt> Prompts);


/// <summary>
/// The workflow summary of the AI card (D25) and the JSON exactly as embedded.
/// </summary>
public sealed record AiWorkflowSummary(
    AiWorkflowFormat Format,
    string FormatLabel,
    int NodeCount,
    int LinkCount,
    int SamplerCount,
    int ModelCount,
    int SubgraphCount,
    long Size,
    string Json,
    string? SourceKey);


/// <summary>
/// An action of a C2PA manifest.
/// </summary>
public sealed record C2paAction(string Action, string? SoftwareAgent, string? SoftwareAgentVersion, string? DigitalSourceType, string? Description)
{
    #region Properties

    public string? DigitalSourceLabel => DigitalSourceType is null ? null : DigitalSourceTypes.Label(DigitalSourceType);

    #endregion // Properties
}


/// <summary>
/// An ingredient of a C2PA manifest, linked to its own manifest by label when it has one.
/// </summary>
public sealed record C2paIngredient(string? Title, string? Relationship, string? Format, string? ManifestLabel);


/// <summary>
/// One C2PA manifest as ExifTool shows it; never verified (D21).
/// </summary>
public sealed record C2paManifest(
    string Label,
    int ClaimVersion,
    string? ClaimGenerator,
    string? ClaimGeneratorVersion,
    string? Title,
    string? Format,
    IReadOnlyList<C2paAction> Actions,
    IReadOnlyList<C2paIngredient> Ingredients,
    IReadOnlyList<string> Authors,
    bool IsActive,
    bool HasExifAssertion);


/// <summary>
/// C2PA Content Credentials: the active manifest first, then the ones its ingredients point to.
/// </summary>
public sealed record C2paInfo(IReadOnlyList<C2paManifest> Manifests, string? RemoteManifestUrl)
{
    #region Properties

    /// <summary>
    /// Always false in 3.0: ExifTool decodes neither the signer nor the signature (D21, R15).
    /// </summary>
    public bool IsVerified => false;

    public C2paManifest? Active => Manifests.FirstOrDefault(m => m.IsActive);

    /// <summary>
    /// A stds.exif assertion can hold GPS that -gps:all= doesn't remove (S9).
    /// </summary>
    public bool HasExifAssertion => Manifests.Any(m => m.HasExifAssertion);

    #endregion // Properties
}


/// <summary>
/// IPTC digital source type, the IPTC 2025.1 AI fields and credit lines.
/// </summary>
public sealed record IptcAiInfo(
    string? DigitalSourceType,
    string? DigitalSourceLabel,
    string? AiSystem,
    string? AiSystemVersion,
    string? PromptInformation,
    string? PromptWriter,
    string? Credit);


/// <summary>
/// The China GB 45438-2025 implicit label.
/// </summary>
public sealed record AigcLabel(
    string Label,
    string Meaning,
    string? ContentProducer,
    string? ProduceId,
    string? ContentPropagator,
    string? PropagateId,
    string? ReservedCode1,
    string? ReservedCode2);


/// <summary>
/// AI-training signals: PLUS DataMining and Exif 3.1 LearningOptOutIn.
/// </summary>
public sealed record TrainingSignals(string? DataMining, string? LearningOptOutIn)
{
    #region Properties

    /// <summary>
    /// True when a signal says no: "Prohibited", "Opt-out", "Not allowed".
    /// </summary>
    public bool IsOptOut => Says(DataMining, "prohibit") || Says(DataMining, "not allowed") || Says(LearningOptOutIn, "opt-out") || Says(LearningOptOutIn, "optout");

    #endregion // Properties


    #region Private methods

    private static bool Says(string? value, string word) => value?.Contains(word, StringComparison.OrdinalIgnoreCase) == true;

    #endregion // Private methods
}


/// <summary>
/// An LLM character card (Tier 2): what its JSON says about the character, and the JSON itself.
/// </summary>
public sealed record CharacterCardInfo(string? Name, string? Description, string? Creator, string? CharacterVersion, IReadOnlyList<string> Tags, string? Spec, string Json);


/// <summary>
/// Everything one parser read from one generator or standard.
/// </summary>
public sealed class AiSourceInfo
{
    #region Properties

    public required AiSourceKind Source { get; init; }

    /// <summary>
    /// The generator or standard name shown on the badge: "ComfyUI", "Forge", "C2PA".
    /// </summary>
    public required string Generator { get; init; }

    public string? GeneratorVersion { get; init; }

    public AiKind Kind { get; init; }

    public IReadOnlyList<AiPrompt> Prompts { get; init; } = [];

    /// <summary>
    /// Parameters in display order: Model, Sampler, Scheduler, Steps, CFG, Seed, Size, then the rest.
    /// </summary>
    public IReadOnlyList<AiField> Parameters { get; init; } = [];

    public IReadOnlyList<AiModelRef> Models { get; init; } = [];

    public IReadOnlyList<AiSamplerPass> Passes { get; init; } = [];

    public IReadOnlyList<AiWorkflowSummary> Workflows { get; init; } = [];

    /// <summary>
    /// Table keys of every tag this source read.
    /// </summary>
    public IReadOnlyList<string> SourceKeys { get; init; } = [];

    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// The parsed A1111 infotext, kept so Copy generation parameters gives it back unchanged.
    /// </summary>
    public A1111Infotext? Infotext { get; init; }

    /// <summary>
    /// The raw text shown for Unknown AI.
    /// </summary>
    public string? RawText { get; init; }

    public C2paInfo? C2pa { get; init; }

    public IptcAiInfo? Iptc { get; init; }

    public AigcLabel? Aigc { get; init; }

    public TrainingSignals? Training { get; init; }

    public string? CharacterName { get; init; }

    public CharacterCardInfo? Character { get; init; }

    /// <summary>
    /// The first sampler's title when the source has several (ComfyUI); the others are in Passes.
    /// </summary>
    public string? MainPassTitle { get; init; }

    /// <summary>
    /// True for C2PA, IPTC, the AIGC label and AI-training signals: what a file claims, not how it was generated.
    /// </summary>
    public bool IsProvenance => Source is AiSourceKind.C2pa or AiSourceKind.Iptc or AiSourceKind.Aigc or AiSourceKind.TrainingSignals;

    /// <summary>
    /// True for a source with prompts or a workflow, or a generator's parameters.
    /// </summary>
    public bool IsGeneration => Prompts.Count > 0 || Workflows.Count > 0 || (!IsProvenance && Parameters.Count > 0);

    #endregion // Properties


    #region Public methods

    public AiField? Parameter(string id) => Parameters.FirstOrDefault(p => p.Id == id);

    public override string ToString() => $"{Generator} ({Source}, {Kind})";

    #endregion // Public methods
}
