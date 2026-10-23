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
/// Collects what a parser finds and builds the immutable source.
/// </summary>
internal sealed partial class SourceBuilder(AiSourceKind source, string generator)
{
    #region Fields and constants

    private readonly List<AiPrompt> _prompts = [];
    private readonly List<(string RawKey, string? Value, string? SourceKey)> _params = [];
    private readonly List<AiModelRef> _models = [];
    private readonly List<AiSamplerPass> _passes = [];
    private readonly List<AiWorkflowSummary> _workflows = [];
    private readonly List<string> _keys = [];
    private readonly List<string> _warnings = [];

    #endregion // Fields and constants


    #region Properties

    public AiSourceKind Source { get; } = source;

    public string Generator { get; set; } = generator;

    public string? Version { get; set; }

    public AiKind Kind { get; set; } = AiKind.Generated;

    public A1111Infotext? Infotext { get; set; }

    public string? RawText { get; set; }

    public C2paInfo? C2pa { get; set; }

    public IptcAiInfo? Iptc { get; set; }

    public AigcLabel? Aigc { get; set; }

    public TrainingSignals? Training { get; set; }

    public string? CharacterName { get; set; }

    public CharacterCardInfo? Character { get; set; }

    public string? MainPassTitle { get; set; }

    public bool HasContent => _prompts.Count > 0 || _params.Count > 0 || _workflows.Count > 0 || _models.Count > 0;

    #endregion // Properties


    #region Public methods

    /// <summary>
    /// Records a tag this source read and claims it so later parsers skip it.
    /// </summary>
    public SourceBuilder Use(AiParseContext context, AiTag? tag)
    {
        if (tag is null) return this;

        context.Claim(tag);
        if (!_keys.Contains(tag.TableKey)) _keys.Add(tag.TableKey);
        if (tag.WasRepaired) Warn($"{tag.Name} was stored as UTF-8 in a Latin-1 PNG chunk; shown repaired");
        return this;
    }


    public void Prompt(string? text, AiPromptRole role, string label = "", string? sourceKey = null)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var t = text.Trim();
        if (_prompts.Any(p => p.Role == role && p.Text == t && p.Label == label)) return;
        _prompts.Add(new AiPrompt(t, role, label, sourceKey));
    }


    public void Param(string rawKey, string? value, string? sourceKey = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        _params.Add((rawKey, value, sourceKey));
    }


    public void Model(AiModelKind kind, string? name, string? weight = null, string? hash = null, string? sourceKey = null)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "None" or "none") return;

        var n = name.Trim();
        var i = _models.FindIndex(m => m.Kind == kind && m.Name.Equals(n, StringComparison.OrdinalIgnoreCase));
        if (i >= 0)
        {
            var m = _models[i];
            _models[i] = m with { Weight = m.Weight ?? weight, Hash = m.Hash ?? hash };
            return;
        }

        _models.Add(new AiModelRef(kind, n, weight, hash, sourceKey));
    }


    /// <summary>
    /// Sets the hash of a model already added, matched by name with or without its file extension.
    /// </summary>
    public void Hash(AiModelKind kind, string name, string hash)
    {
        var i = _models.FindIndex(m => m.Kind == kind && SameModel(m.Name, name));
        if (i >= 0) _models[i] = _models[i] with { Hash = _models[i].Hash ?? hash };
        else Model(kind, name, null, hash);
    }


    public void Pass(AiSamplerPass pass) => _passes.Add(pass);


    public void Workflow(AiWorkflowSummary workflow) => _workflows.Add(workflow);


    public void Warn(string warning)
    {
        if (!_warnings.Contains(warning)) _warnings.Add(warning);
    }


    /// <summary>
    /// Adds the LoRA, embedding and wildcard references written inside a prompt: &lt;lora:name:0.8&gt;, embedding:name.
    /// </summary>
    public void ModelsFromPrompt(string? prompt, string? sourceKey)
    {
        if (string.IsNullOrEmpty(prompt)) return;

        foreach (Match m in LoraTag().Matches(prompt))
        {
            Model(m.Groups[1].Value.Equals("hypernet", StringComparison.OrdinalIgnoreCase) ? AiModelKind.Other : AiModelKind.Lora,
                m.Groups[2].Value, m.Groups[3].Success ? m.Groups[3].Value : null, null, sourceKey);
        }

        foreach (Match m in EmbeddingRef().Matches(prompt))
        {
            Model(AiModelKind.Embedding, m.Groups[1].Value, null, null, sourceKey);
        }
    }


    public AiSourceInfo Build() => new()
    {
        Source = Source,
        Generator = Generator,
        GeneratorVersion = string.IsNullOrWhiteSpace(Version) ? null : Version.Trim(),
        Kind = Kind,
        Prompts = [.. _prompts],
        Parameters = AiParameterNames.Build(_params),
        Models = [.. _models],
        Passes = [.. _passes],
        Workflows = [.. _workflows],
        SourceKeys = [.. _keys],
        Warnings = [.. _warnings],
        Infotext = Infotext,
        RawText = RawText,
        C2pa = C2pa,
        Iptc = Iptc,
        Aigc = Aigc,
        Training = Training,
        CharacterName = CharacterName,
        Character = Character,
        MainPassTitle = MainPassTitle,
    };


    public static bool SameModel(string a, string b) =>
        a.Equals(b, StringComparison.OrdinalIgnoreCase) || Path.GetFileNameWithoutExtension(a).Equals(Path.GetFileNameWithoutExtension(b), StringComparison.OrdinalIgnoreCase);

    #endregion // Public methods


    #region Private methods

    [GeneratedRegex(@"<(lora|lyco|hypernet):([^:>]+)(?::([-+]?[\d.]+))?[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LoraTag();


    [GeneratedRegex(@"\bembedding:([\w\-.]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EmbeddingRef();

    #endregion // Private methods
}
