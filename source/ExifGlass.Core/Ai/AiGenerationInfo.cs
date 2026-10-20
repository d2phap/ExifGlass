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
/// The AI data of one file: every source the parsers found, merged into the view the AI card and AI group show.
/// </summary>
public sealed class AiGenerationInfo
{
    #region Constructors

    public AiGenerationInfo(IReadOnlyList<AiSourceInfo> sources, IReadOnlyList<string>? warnings = null, TimeSpan parseTime = default)
    {
        Sources = sources;
        Warnings = warnings ?? [];
        ParseTime = parseTime;

        Primary = sources.FirstOrDefault(s => s.IsGeneration && s.Kind >= AiKind.UnknownAi)
            ?? sources.OrderByDescending(s => s.Kind).FirstOrDefault();
        Kind = sources.Count == 0 ? AiKind.None : sources.Max(s => s.Kind);

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in sources)
        {
            keys.UnionWith(s.SourceKeys);
        }

        SourceKeys = keys;
    }

    #endregion // Constructors


    #region Properties

    public IReadOnlyList<AiSourceInfo> Sources { get; }

    /// <summary>
    /// The source the card leads with: the first one with prompts or parameters, else the strongest provenance.
    /// </summary>
    public AiSourceInfo? Primary { get; }

    /// <summary>
    /// The strongest kind over every source; provenance can raise it (a ComfyUI file with a C2PA manifest).
    /// </summary>
    public AiKind Kind { get; }

    /// <summary>
    /// True when the file was made or edited with AI; false for provenance only or a character card.
    /// </summary>
    public bool IsAi => Kind >= AiKind.UnknownAi;

    public string Generator => Primary?.Generator ?? string.Empty;

    public string? GeneratorVersion => Primary?.GeneratorVersion;

    public AiPrompt? Prompt => Primary?.Prompts.FirstOrDefault(p => p.Role == AiPromptRole.Positive)
        ?? Sources.SelectMany(s => s.Prompts).FirstOrDefault(p => p.Role == AiPromptRole.Positive);

    public AiPrompt? NegativePrompt => Primary?.Prompts.FirstOrDefault(p => p.Role == AiPromptRole.Negative);

    public IReadOnlyList<AiField> Parameters => Primary?.Parameters ?? [];

    public IReadOnlyList<AiModelRef> Models => [.. Sources.SelectMany(s => s.Models).DistinctBy(m => (m.Kind, m.Name, m.Weight))];

    public IReadOnlyList<AiWorkflowSummary> Workflows => [.. Sources.SelectMany(s => s.Workflows)];

    public string? Seed => Primary?.Parameter(AiParameterNames.SEED)?.Value;

    public string? Size => Primary?.Parameter(AiParameterNames.SIZE)?.Value;

    public C2paInfo? C2pa => Sources.FirstOrDefault(s => s.C2pa is not null)?.C2pa;

    public IptcAiInfo? Iptc => Sources.FirstOrDefault(s => s.Iptc is not null)?.Iptc;

    public AigcLabel? Aigc => Sources.FirstOrDefault(s => s.Aigc is not null)?.Aigc;

    public TrainingSignals? Training => Sources.FirstOrDefault(s => s.Training is not null)?.Training;

    /// <summary>
    /// Table keys of every tag any source read; the AI quick filter and "Show source tag" use them.
    /// </summary>
    public IReadOnlySet<string> SourceKeys { get; }

    public IReadOnlyList<string> Warnings { get; }

    public TimeSpan ParseTime { get; }

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// One line for the self-test and logs: "ComfyUI · Generated · 3 prompts · seed 156680208700286".
    /// </summary>
    public string Summary()
    {
        if (Sources.Count == 0) return "no AI data";

        var parts = new List<string> { Generator.Length > 0 ? Generator : "AI", KindLabel(Kind) };
        if (GeneratorVersion is { Length: > 0 } v) parts[0] += " " + v;

        var prompts = Sources.Sum(s => s.Prompts.Count);
        if (prompts > 0) parts.Add(prompts == 1 ? "1 prompt" : $"{prompts} prompts");
        if (Seed is { } seed) parts.Add("seed " + seed);
        if (Models.Count > 0) parts.Add(Models.Count == 1 ? "1 model" : $"{Models.Count} models");
        if (Workflows.Count > 0) parts.Add("workflow " + Workflows[0].NodeCount + " nodes");

        var provenance = Sources.Where(s => !s.IsGeneration || s.Source is AiSourceKind.C2pa or AiSourceKind.Iptc or AiSourceKind.Aigc).Select(s => s.Generator).Distinct().ToList();
        if (provenance.Count > 0 && Primary is { IsGeneration: true }) parts.Add("+ " + string.Join(", ", provenance.Where(p => p != Generator)));

        return string.Join(" · ", parts.Where(p => p != "+ "));
    }


    /// <summary>
    /// The kind chip text.
    /// </summary>
    public static string KindLabel(AiKind kind) => kind switch
    {
        AiKind.Generated => "Generated",
        AiKind.Composite => "Composite",
        AiKind.EditedWithAi => "Edited with AI",
        AiKind.UnknownAi => "Unknown AI",
        AiKind.CharacterCard => "Character card",
        AiKind.Captured => "Not AI",
        _ => "None",
    };

    #endregion // Public methods
}
