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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using ExifGlass.Core.Ai;
using System.Globalization;
using System.Windows.Input;

namespace ExifGlass.UI.Controls;

/// <summary>
/// One parameter tile of the AI card: a label and a mono value.
/// </summary>
public sealed record AiParameterView(string Label, string Value);


/// <summary>
/// One model of the AI card: name, "LoRA · 7c6bad76eb54", the weight chip, the hash it copies.
/// </summary>
public sealed record AiModelView(string Name, string Meta, string? Weight, string? Hash, IconId Icon, bool IsAccent, bool IsFirst);


/// <summary>
/// A NovelAI character prompt: its number, caption and centre; a line above every row but the first.
/// </summary>
public sealed record AiCharacterView(string Number, string Caption, string Centre, bool IsFirst)
{
    #region Properties

    public Thickness Line => IsFirst ? default : new Thickness(0, 1, 0, 0);

    public AiActionRequest Copy => new(AiAction.CopyText, Caption, $"{UiText.CHARACTER} {Number}");

    #endregion // Properties
}


/// <summary>
/// The workflow summary (D25): counts, the JSON size, the formats embedded, what Save workflow writes, and the tag it came from.
/// </summary>
public sealed record AiWorkflowView(string Nodes, string NodesLabel, string Samplers, string SamplersLabel, string Models, string ModelsLabel, string Size, string Summary, string SaveTip, string CopyTip, string? SourceKey);


/// <summary>
/// Unknown AI's raw text and the tag it came from.
/// </summary>
public sealed record AiRawView(string Text, string? SourceKey);


/// <summary>
/// The sampler switch: one title per sampler, and "2 samplers".
/// </summary>
public sealed record AiPassesView(IReadOnlyList<string> Titles, string Label);


/// <summary>
/// A C2PA ingredient: its title, and what its own manifest says.
/// </summary>
public sealed record AiIngredientView(string Title, string Detail);


/// <summary>
/// A provenance section of the AI card: Content Credentials, IPTC, the AIGC label, AI training; with the tag it came from.
/// </summary>
public sealed record AiSectionView(IReadOnlyList<AiKeyValue> Rows, string? SourceKey, IReadOnlyList<string> OtherSources)
{
    #region Properties

    /// <summary>
    /// The C2PA ingredients, each with what its own manifest claims.
    /// </summary>
    public IReadOnlyList<AiIngredientView> Ingredients { get; init; } = [];

    public bool HasIngredients => Ingredients.Count > 0;

    #endregion // Properties
}


/// <summary>
/// The AI card, extended by the AI addendum (P7): generator badge and kind chip with the tag it came from; prompt, negative prompt, characters and styles; parameters per sampler with All parameters; models and LoRAs; the workflow summary (D25); Content Credentials marked Not verified (D21), IPTC, the AIGC label and AI-training signals; Copy generation parameters and the More menu.
/// </summary>
public class AiCard : TemplatedControl
{
    #region Fields and constants

    // DS 6.11: the first eight parameters, then All parameters; the first three models, then "n more"
    private const int SHOWN_PARAMETERS = 8;
    private const int SHOWN_MODELS = 3;

    public static readonly StyledProperty<AiGenerationInfo?> InfoProperty =
        AvaloniaProperty.Register<AiCard, AiGenerationInfo?>(nameof(Info));

    public static readonly StyledProperty<bool> IsLoadingProperty =
        AvaloniaProperty.Register<AiCard, bool>(nameof(IsLoading));

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AiCard, bool>(nameof(IsExpanded));

    public static readonly StyledProperty<bool> AllParametersOpenProperty =
        AvaloniaProperty.Register<AiCard, bool>(nameof(AllParametersOpen));

    public static readonly StyledProperty<bool> AllModelsOpenProperty =
        AvaloniaProperty.Register<AiCard, bool>(nameof(AllModelsOpen));

    public static readonly StyledProperty<int> SelectedPassProperty =
        AvaloniaProperty.Register<AiCard, int>(nameof(SelectedPass));

    /// <summary>
    /// R19: what a custom ExifTool is too old to read, said in the card; null when it reads everything.
    /// </summary>
    public static readonly StyledProperty<string?> ExifToolNoteProperty =
        AvaloniaProperty.Register<AiCard, string?>(nameof(ExifToolNote));

    public static readonly StyledProperty<ICommand?> ActionCommandProperty =
        AvaloniaProperty.Register<AiCard, ICommand?>(nameof(ActionCommand));

    public static readonly DirectProperty<AiCard, string?> GeneratorProperty =
        AvaloniaProperty.RegisterDirect<AiCard, string?>(nameof(Generator), c => c.Generator);

    public static readonly DirectProperty<AiCard, string?> KindTextProperty =
        AvaloniaProperty.RegisterDirect<AiCard, string?>(nameof(KindText), c => c.KindText);

    public static readonly DirectProperty<AiCard, IconId> KindIconProperty =
        AvaloniaProperty.RegisterDirect<AiCard, IconId>(nameof(KindIcon), c => c.KindIcon);

    public static readonly DirectProperty<AiCard, BadgeKind> KindBadgeProperty =
        AvaloniaProperty.RegisterDirect<AiCard, BadgeKind>(nameof(KindBadge), c => c.KindBadge);

    public static readonly DirectProperty<AiCard, string?> SourceKeyProperty =
        AvaloniaProperty.RegisterDirect<AiCard, string?>(nameof(SourceKey), c => c.SourceKey);

    public static readonly DirectProperty<AiCard, IReadOnlyList<string>> OtherSourcesProperty =
        AvaloniaProperty.RegisterDirect<AiCard, IReadOnlyList<string>>(nameof(OtherSources), c => c.OtherSources);

    public static readonly DirectProperty<AiCard, AiRawView?> RawProperty =
        AvaloniaProperty.RegisterDirect<AiCard, AiRawView?>(nameof(Raw), c => c.Raw);

    public static readonly DirectProperty<AiCard, AiPassesView?> PassesViewProperty =
        AvaloniaProperty.RegisterDirect<AiCard, AiPassesView?>(nameof(PassesView), c => c.PassesView);

    public static readonly DirectProperty<AiCard, string?> WarningProperty =
        AvaloniaProperty.RegisterDirect<AiCard, string?>(nameof(Warning), c => c.Warning);

    public static readonly DirectProperty<AiCard, string> PromptTitleProperty =
        AvaloniaProperty.RegisterDirect<AiCard, string>(nameof(PromptTitle), c => c.PromptTitle);

    public static readonly DirectProperty<AiCard, string?> PromptProperty =
        AvaloniaProperty.RegisterDirect<AiCard, string?>(nameof(Prompt), c => c.Prompt);

    public static readonly DirectProperty<AiCard, string?> NegativePromptProperty =
        AvaloniaProperty.RegisterDirect<AiCard, string?>(nameof(NegativePrompt), c => c.NegativePrompt);

    public static readonly DirectProperty<AiCard, string?> NegativeNoteProperty =
        AvaloniaProperty.RegisterDirect<AiCard, string?>(nameof(NegativeNote), c => c.NegativeNote);

    public static readonly DirectProperty<AiCard, IReadOnlyCollection<string>> EmbeddingsProperty =
        AvaloniaProperty.RegisterDirect<AiCard, IReadOnlyCollection<string>>(nameof(Embeddings), c => c.Embeddings);

    public static readonly DirectProperty<AiCard, IReadOnlyList<AiCharacterView>> CharactersProperty =
        AvaloniaProperty.RegisterDirect<AiCard, IReadOnlyList<AiCharacterView>>(nameof(Characters), c => c.Characters);

    public static readonly DirectProperty<AiCard, string> CharactersTitleProperty =
        AvaloniaProperty.RegisterDirect<AiCard, string>(nameof(CharactersTitle), c => c.CharactersTitle);

    public static readonly DirectProperty<AiCard, IReadOnlyList<string>> StyleNamesProperty =
        AvaloniaProperty.RegisterDirect<AiCard, IReadOnlyList<string>>(nameof(StyleNames), c => c.StyleNames);

    public static readonly DirectProperty<AiCard, IReadOnlyList<AiParameterView>> ParametersProperty =
        AvaloniaProperty.RegisterDirect<AiCard, IReadOnlyList<AiParameterView>>(nameof(Parameters), c => c.Parameters);

    public static readonly DirectProperty<AiCard, IReadOnlyList<AiKeyValue>> MoreParametersProperty =
        AvaloniaProperty.RegisterDirect<AiCard, IReadOnlyList<AiKeyValue>>(nameof(MoreParameters), c => c.MoreParameters);

    public static readonly DirectProperty<AiCard, string?> ParameterCountProperty =
        AvaloniaProperty.RegisterDirect<AiCard, string?>(nameof(ParameterCount), c => c.ParameterCount);

    public static readonly DirectProperty<AiCard, IReadOnlyList<AiModelView>> ModelsProperty =
        AvaloniaProperty.RegisterDirect<AiCard, IReadOnlyList<AiModelView>>(nameof(Models), c => c.Models);

    public static readonly DirectProperty<AiCard, IReadOnlyList<AiModelView>> MoreModelsProperty =
        AvaloniaProperty.RegisterDirect<AiCard, IReadOnlyList<AiModelView>>(nameof(MoreModels), c => c.MoreModels);

    public static readonly DirectProperty<AiCard, string?> ModelsTitleProperty =
        AvaloniaProperty.RegisterDirect<AiCard, string?>(nameof(ModelsTitle), c => c.ModelsTitle);

    public static readonly DirectProperty<AiCard, string?> MoreModelsLabelProperty =
        AvaloniaProperty.RegisterDirect<AiCard, string?>(nameof(MoreModelsLabel), c => c.MoreModelsLabel);

    public static readonly DirectProperty<AiCard, AiWorkflowView?> WorkflowProperty =
        AvaloniaProperty.RegisterDirect<AiCard, AiWorkflowView?>(nameof(Workflow), c => c.Workflow);

    public static readonly DirectProperty<AiCard, AiSectionView?> CredentialsProperty =
        AvaloniaProperty.RegisterDirect<AiCard, AiSectionView?>(nameof(Credentials), c => c.Credentials);

    public static readonly DirectProperty<AiCard, AiSectionView?> IptcProperty =
        AvaloniaProperty.RegisterDirect<AiCard, AiSectionView?>(nameof(Iptc), c => c.Iptc);

    public static readonly DirectProperty<AiCard, AiSectionView?> AigcProperty =
        AvaloniaProperty.RegisterDirect<AiCard, AiSectionView?>(nameof(Aigc), c => c.Aigc);

    public static readonly DirectProperty<AiCard, AiSectionView?> TrainingProperty =
        AvaloniaProperty.RegisterDirect<AiCard, AiSectionView?>(nameof(Training), c => c.Training);

    public static readonly DirectProperty<AiCard, string> MainActionTextProperty =
        AvaloniaProperty.RegisterDirect<AiCard, string>(nameof(MainActionText), c => c.MainActionText);

    public static readonly DirectProperty<AiCard, IconId> MainActionIconProperty =
        AvaloniaProperty.RegisterDirect<AiCard, IconId>(nameof(MainActionIcon), c => c.MainActionIcon);

    private string? _generator;
    private string? _kindText;
    private IconId _kindIcon = IconId.Sparkle;
    private BadgeKind _kindBadge = BadgeKind.Ai;
    private string? _sourceKey;
    private IReadOnlyList<string> _otherSources = [];
    private string? _warning;
    private AiRawView? _raw;
    private AiPassesView? _passesView;
    private string _promptTitle = UiText.PROMPT_OVERLINE;
    private string? _prompt;
    private string? _negative;
    private string? _negativeNote;
    private IReadOnlyCollection<string> _embeddings = [];
    private IReadOnlyList<AiCharacterView> _characters = [];
    private string _charactersTitle = string.Empty;
    private IReadOnlyList<string> _styles = [];
    private IReadOnlyList<AiParameterView> _parameters = [];
    private IReadOnlyList<AiKeyValue> _moreParameters = [];
    private string? _parameterCount;
    private IReadOnlyList<AiModelView> _models = [];
    private IReadOnlyList<AiModelView> _moreModels = [];
    private string? _modelsTitle;
    private string? _moreModelsLabel;
    private AiWorkflowView? _workflow;
    private AiSectionView? _credentials;
    private AiSectionView? _iptc;
    private AiSectionView? _aigc;
    private AiSectionView? _training;
    private string _mainActionText = UiText.COPY_PARAMETERS;
    private IconId _mainActionIcon = IconId.Copy;
    private IReadOnlyList<(string Title, IReadOnlyList<AiField> Fields)> _passFields = [];
    private Button? _more;
    private PromptText? _promptText;
    private PromptText? _negativeText;
    private GlassDropDownButton? _menuButton;

    #endregion // Fields and constants


    #region Constructors

    public AiCard()
    {
        AddHandler(Button.ClickEvent, Button_Click);
        UpdatePseudoClasses();
    }

    #endregion // Constructors


    #region Properties

    public AiGenerationInfo? Info
    {
        get => GetValue(InfoProperty);
        set => SetValue(InfoProperty, value);
    }

    /// <summary>
    /// The AI read is still running for a file whose table shows AI tags: the card pulses (AI addendum state-ai-loading).
    /// </summary>
    public bool IsLoading
    {
        get => GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    /// <summary>
    /// Shows the whole prompt and negative prompt (Show more).
    /// </summary>
    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public bool AllParametersOpen
    {
        get => GetValue(AllParametersOpenProperty);
        set => SetValue(AllParametersOpenProperty, value);
    }

    public bool AllModelsOpen
    {
        get => GetValue(AllModelsOpenProperty);
        set => SetValue(AllModelsOpenProperty, value);
    }

    /// <summary>
    /// The sampler the parameters show when there are several (ComfyUI base and refiner).
    /// </summary>
    public int SelectedPass
    {
        get => GetValue(SelectedPassProperty);
        set => SetValue(SelectedPassProperty, value);
    }

    public string? ExifToolNote
    {
        get => GetValue(ExifToolNoteProperty);
        set => SetValue(ExifToolNoteProperty, value);
    }

    /// <summary>
    /// Runs every copy, save and jump the card offers, with an AiActionRequest.
    /// </summary>
    public ICommand? ActionCommand
    {
        get => GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    /// <summary>
    /// Gets the badge: "SD WebUI v1.10.1".
    /// </summary>
    public string? Generator
    {
        get => _generator;
        private set => SetAndRaise(GeneratorProperty, ref _generator, value);
    }

    public string? KindText
    {
        get => _kindText;
        private set => SetAndRaise(KindTextProperty, ref _kindText, value);
    }

    public IconId KindIcon
    {
        get => _kindIcon;
        private set => SetAndRaise(KindIconProperty, ref _kindIcon, value);
    }

    /// <summary>
    /// Gets Ai for Generated, Edited with AI and Composite; Neutral for Unknown AI (AI addendum question 5).
    /// </summary>
    public BadgeKind KindBadge
    {
        get => _kindBadge;
        private set => SetAndRaise(KindBadgeProperty, ref _kindBadge, value);
    }

    /// <summary>
    /// Gets the tag the card leads with, after "from".
    /// </summary>
    public string? SourceKey
    {
        get => _sourceKey;
        private set => SetAndRaise(SourceKeyProperty, ref _sourceKey, value);
    }

    public IReadOnlyList<string> OtherSources
    {
        get => _otherSources;
        private set => SetAndRaise(OtherSourcesProperty, ref _otherSources, value);
    }

    /// <summary>
    /// Gets what a parser couldn't read, said in a warning note.
    /// </summary>
    public string? Warning
    {
        get => _warning;
        private set => SetAndRaise(WarningProperty, ref _warning, value);
    }

    /// <summary>
    /// Gets Unknown AI's raw text, or null.
    /// </summary>
    public AiRawView? Raw
    {
        get => _raw;
        private set => SetAndRaise(RawProperty, ref _raw, value);
    }

    /// <summary>
    /// Gets the samplers' titles when there are several; the segmented control switches between them.
    /// </summary>
    public AiPassesView? PassesView
    {
        get => _passesView;
        private set => SetAndRaise(PassesViewProperty, ref _passesView, value);
    }

    /// <summary>
    /// Gets "PROMPT", or "PROMPT INFORMATION" for the IPTC field.
    /// </summary>
    public string PromptTitle
    {
        get => _promptTitle;
        private set => SetAndRaise(PromptTitleProperty, ref _promptTitle, value);
    }

    public string? Prompt
    {
        get => _prompt;
        private set => SetAndRaise(PromptProperty, ref _prompt, value);
    }

    public string? NegativePrompt
    {
        get => _negative;
        private set => SetAndRaise(NegativePromptProperty, ref _negative, value);
    }

    /// <summary>
    /// Gets "No negative prompt. The styles add: ..." for Fooocus, whose styles write the negative prompt.
    /// </summary>
    public string? NegativeNote
    {
        get => _negativeNote;
        private set => SetAndRaise(NegativeNoteProperty, ref _negativeNote, value);
    }

    public IReadOnlyCollection<string> Embeddings
    {
        get => _embeddings;
        private set => SetAndRaise(EmbeddingsProperty, ref _embeddings, value);
    }

    public IReadOnlyList<AiCharacterView> Characters
    {
        get => _characters;
        private set => SetAndRaise(CharactersProperty, ref _characters, value);
    }

    public string CharactersTitle
    {
        get => _charactersTitle;
        private set => SetAndRaise(CharactersTitleProperty, ref _charactersTitle, value);
    }

    /// <summary>
    /// Gets the Fooocus styles, shown as badges.
    /// </summary>
    public IReadOnlyList<string> StyleNames
    {
        get => _styles;
        private set => SetAndRaise(StyleNamesProperty, ref _styles, value);
    }

    public IReadOnlyList<AiParameterView> Parameters
    {
        get => _parameters;
        private set => SetAndRaise(ParametersProperty, ref _parameters, value);
    }

    public IReadOnlyList<AiKeyValue> MoreParameters
    {
        get => _moreParameters;
        private set => SetAndRaise(MoreParametersProperty, ref _moreParameters, value);
    }

    public string? ParameterCount
    {
        get => _parameterCount;
        private set => SetAndRaise(ParameterCountProperty, ref _parameterCount, value);
    }

    public IReadOnlyList<AiModelView> Models
    {
        get => _models;
        private set => SetAndRaise(ModelsProperty, ref _models, value);
    }

    public IReadOnlyList<AiModelView> MoreModels
    {
        get => _moreModels;
        private set => SetAndRaise(MoreModelsProperty, ref _moreModels, value);
    }

    public string? ModelsTitle
    {
        get => _modelsTitle;
        private set => SetAndRaise(ModelsTitleProperty, ref _modelsTitle, value);
    }

    public string? MoreModelsLabel
    {
        get => _moreModelsLabel;
        private set => SetAndRaise(MoreModelsLabelProperty, ref _moreModelsLabel, value);
    }

    public AiWorkflowView? Workflow
    {
        get => _workflow;
        private set => SetAndRaise(WorkflowProperty, ref _workflow, value);
    }

    /// <summary>
    /// Gets C2PA Content Credentials: claimed by, actions, source type, manifests; never verified (D21).
    /// </summary>
    public AiSectionView? Credentials
    {
        get => _credentials;
        private set => SetAndRaise(CredentialsProperty, ref _credentials, value);
    }

    public AiSectionView? Iptc
    {
        get => _iptc;
        private set => SetAndRaise(IptcProperty, ref _iptc, value);
    }

    public AiSectionView? Aigc
    {
        get => _aigc;
        private set => SetAndRaise(AigcProperty, ref _aigc, value);
    }

    public AiSectionView? Training
    {
        get => _training;
        private set => SetAndRaise(TrainingProperty, ref _training, value);
    }

    /// <summary>
    /// Gets "Copy generation parameters", or "Copy AI data as JSON" for provenance alone.
    /// </summary>
    public string MainActionText
    {
        get => _mainActionText;
        private set => SetAndRaise(MainActionTextProperty, ref _mainActionText, value);
    }

    public IconId MainActionIcon
    {
        get => _mainActionIcon;
        private set => SetAndRaise(MainActionIconProperty, ref _mainActionIcon, value);
    }

    #endregion // Properties


    #region Public methods

    public void Appear() => Card.PlayAppear(this);


    /// <summary>
    /// The generator badge's text: the name and its version.
    /// </summary>
    public static string GeneratorLabel(AiGenerationInfo info)
    {
        var name = info.Generator.Length > 0 ? info.Generator : AiGenerationInfo.KindLabel(info.Kind);
        return info.GeneratorVersion is { Length: > 0 } version ? $"{name} {version}" : name;
    }


    /// <summary>
    /// The kind chip's icon (AI addendum question 5): sparkle, edited, composite, question, character.
    /// </summary>
    public static IconId KindIconOf(AiKind kind) => kind switch
    {
        AiKind.EditedWithAi => IconId.AiEdited,
        AiKind.Composite => IconId.AiComposite,
        AiKind.UnknownAi => IconId.AiQuestion,
        AiKind.CharacterCard => IconId.AiCharacter,
        _ => IconId.Sparkle,
    };


    public static BadgeKind KindBadgeOf(AiKind kind) => kind is AiKind.UnknownAi or AiKind.CharacterCard ? BadgeKind.Neutral : BadgeKind.Ai;


    /// <summary>
    /// The workflow Save workflow writes: the UI workflow, which loads back into ComfyUI or InvokeAI; else the first one.
    /// </summary>
    public static AiWorkflowSummary? SavedWorkflow(AiGenerationInfo? info) =>
        info?.Workflows.FirstOrDefault(w => w.Format is AiWorkflowFormat.ComfyUiWorkflow or AiWorkflowFormat.InvokeAiWorkflow) ?? info?.Workflows.FirstOrDefault();


    /// <summary>
    /// Every source tag of the file, the card's own first.
    /// </summary>
    public static IReadOnlyList<string> SourceTags(AiGenerationInfo info)
    {
        var keys = new List<string>();
        foreach (var s in info.Primary is { } p ? info.Sources.Where(s => s == p).Concat(info.Sources.Where(s => s != p)) : info.Sources)
        {
            foreach (var key in s.SourceKeys)
            {
                if (!keys.Contains(key)) keys.Add(key);
            }
        }

        return keys;
    }

    #endregion // Public methods


    #region Protected methods

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (_promptText is not null) _promptText.PropertyChanged -= Prompt_PropertyChanged;
        if (_negativeText is not null) _negativeText.PropertyChanged -= Prompt_PropertyChanged;
        if (_more is not null) _more.Click -= More_Click;

        base.OnApplyTemplate(e);

        _more = e.NameScope.Find<Button>("PART_More");
        _promptText = e.NameScope.Find<PromptText>("PART_Prompt");
        _negativeText = e.NameScope.Find<PromptText>("PART_Negative");
        _menuButton = e.NameScope.Find<GlassDropDownButton>("PART_Menu");
        if (_more is not null) _more.Click += More_Click;
        if (_promptText is not null) _promptText.PropertyChanged += Prompt_PropertyChanged;
        if (_negativeText is not null) _negativeText.PropertyChanged += Prompt_PropertyChanged;
        BuildMenu();
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsExpandedProperty) PseudoClasses.Set(":expanded", IsExpanded);
        else if (change.Property == AllModelsOpenProperty) MoreModelsLabel = MoreModels.Count == 0 ? null : AllModelsOpen ? UiText.SHOW_LESS : UiText.MoreCount(MoreModels.Count);
        else if (change.Property == SelectedPassProperty) ShowPass(SelectedPass);
        else if (change.Property == IsLoadingProperty) UpdatePseudoClasses();
        else if (change.Property == InfoProperty)
        {
            Update(Info);
            BuildMenu();
        }
    }

    #endregion // Protected methods


    #region Private methods

    private void Update(AiGenerationInfo? info)
    {
        var primary = info?.Primary;
        Generator = info is null ? null : GeneratorLabel(info);
        KindText = info is null ? null : AiGenerationInfo.KindLabel(info.Kind);
        KindIcon = KindIconOf(info?.Kind ?? AiKind.Generated);
        KindBadge = KindBadgeOf(info?.Kind ?? AiKind.Generated);

        var tags = info is null ? [] : SourceTags(info);
        SourceKey = tags.Count > 0 ? tags[0] : null;
        OtherSources = tags.Count > 1 ? [.. tags.Skip(1)] : [];

        var warnings = info is null ? [] : info.Warnings.Concat(info.Sources.SelectMany(s => s.Warnings)).Distinct().ToList();
        Warning = warnings.Count > 0 ? string.Join("\n", warnings) : null;

        var unknown = primary is { Kind: AiKind.UnknownAi } && primary.Prompts.Count == 0 && primary.RawText is not null;
        Raw = unknown ? new AiRawView(primary!.RawText!, primary.SourceKeys.FirstOrDefault()) : null;

        UpdatePrompts(info);
        UpdateParameters(primary, info);
        UpdateModels(info);
        UpdateWorkflow(info);
        UpdateProvenance(info);

        var generation = primary is { IsGeneration: true };
        MainActionText = generation ? UiText.COPY_PARAMETERS : UiText.COPY_AI_JSON;
        MainActionIcon = generation ? IconId.Copy : IconId.Braces;

        IsExpanded = false;
        AllParametersOpen = false;
        AllModelsOpen = false;
        UpdatePseudoClasses();
    }


    private void UpdatePrompts(AiGenerationInfo? info)
    {
        var primary = info?.Primary;
        var prompts = primary?.Prompts ?? [];
        var positive = prompts.FirstOrDefault(p => p.Role == AiPromptRole.Positive && p.Label.Length == 0)
            ?? prompts.FirstOrDefault(p => p.Role == AiPromptRole.Positive && !IsCharacter(p))
            ?? info?.Prompt;
        Prompt = positive?.Text;
        PromptTitle = positive?.Label == "Prompt information" ? UiText.PROMPT_INFORMATION_OVERLINE : UiText.PROMPT_OVERLINE;

        var negative = prompts.FirstOrDefault(p => p.Role == AiPromptRole.Negative && p.Label.Length == 0);
        NegativePrompt = negative?.Text;

        // Fooocus writes no negative prompt of its own; its styles add one
        var expanded = prompts.FirstOrDefault(p => p.Role == AiPromptRole.Negative && p.Label == "Expanded");
        NegativeNote = negative is null && expanded is not null ? UiText.StylesNegative(expanded.Text) : null;

        Embeddings = info is null ? [] : [.. info.Models.Where(m => m.Kind == AiModelKind.Embedding).Select(m => m.Name)];

        var characters = prompts.Where(IsCharacter).ToList();
        Characters = [.. characters.Select((p, i) => new AiCharacterView((i + 1).ToString(CultureInfo.InvariantCulture), p.Text, Centre(p.Label), i == 0))];
        CharactersTitle = UiText.CharactersTitle(characters.Count);

        StyleNames = primary?.Parameter("styles") is { } styles ? AiRows.ParseList(styles.Value) : [];
    }


    private void UpdateParameters(AiSourceInfo? primary, AiGenerationInfo? info)
    {
        var hasModel = info?.Models.Any(m => m.Kind is AiModelKind.Checkpoint or AiModelKind.Unet) == true;
        var hasVae = info?.Models.Any(m => m.Kind == AiModelKind.Vae) == true;
        bool Shown(AiField p) => !(p.Id is AiParameterNames.MODEL or AiParameterNames.MODEL_HASH && hasModel)
            && !(p.Id is AiParameterNames.VAE or AiParameterNames.VAE_HASH && hasVae) && p.Id != "styles";

        var passes = new List<(string Title, IReadOnlyList<AiField> Fields)>();
        if (primary is not null && !primary.IsProvenance)
        {
            passes.Add((primary.MainPassTitle ?? UiText.PASS_MAIN, [.. primary.Parameters.Where(Shown)]));
            foreach (var pass in primary.Passes) passes.Add((pass.Title, [.. pass.Parameters.Where(Shown)]));
        }

        _passFields = passes;
        PassesView = passes.Count > 1 ? new AiPassesView([.. passes.Select(p => p.Title)], UiText.Samplers(passes.Count)) : null;
        if (SelectedPass != 0) SelectedPass = 0;
        else ShowPass(0);
    }


    private void ShowPass(int index)
    {
        var fields = index >= 0 && index < _passFields.Count ? _passFields[index].Fields : [];
        Parameters = [.. fields.Take(SHOWN_PARAMETERS).Select(p => new AiParameterView(p.Label, p.Value))];

        // labelled prompts (ADetailer, Hires) have no section of their own: they join the rest
        var more = fields.Skip(SHOWN_PARAMETERS).Select(p => new AiKeyValue(p.Label, p.Value, IsMono: IsMonoValue(p.Value))).ToList();
        if (index == 0 && Info?.Primary is { } primary)
        {
            foreach (var p in primary.Prompts.Where(p => p.Label.Length > 0 && !IsCharacter(p) && p.Label is not ("Prompt information" or "Expanded")))
            {
                more.Add(new AiKeyValue((p.Role == AiPromptRole.Positive ? UiText.PROMPT : UiText.NEGATIVE_PROMPT) + " · " + p.Label, p.Text));
            }
        }

        MoreParameters = more;
        ParameterCount = more.Count > 0 ? (Parameters.Count + more.Count).ToString(CultureInfo.InvariantCulture) : null;
        PseudoClasses.Set(":parameters", Parameters.Count > 0);
        PseudoClasses.Set(":more-parameters", more.Count > 0);
    }


    private void UpdateModels(AiGenerationInfo? info)
    {
        var models = info is null ? [] : AiRows.OrderModels(info.Models);
        var views = models.Select((m, i) => ModelView(m, i == 0 || i == SHOWN_MODELS)).ToList();
        Models = [.. views.Take(SHOWN_MODELS)];
        MoreModels = [.. views.Skip(SHOWN_MODELS)];
        ModelsTitle = models.Count > 0 ? UiText.ModelsTitle(models.Count) : null;
        MoreModelsLabel = MoreModels.Count > 0 ? UiText.MoreCount(MoreModels.Count) : null;
    }


    private static AiModelView ModelView(AiModelRef m, bool first)
    {
        var type = AiRows.ModelLabel(m.Kind);
        var meta = m.Hash is { } hash ? $"{type} · {hash}" : type;
        var icon = m.Kind switch
        {
            AiModelKind.Lora => IconId.AiLora,
            AiModelKind.Embedding => IconId.AiLabel,
            AiModelKind.Upscaler => IconId.ArrowMaximizeVertical,
            _ => IconId.AiModel,
        };

        return new AiModelView(m.Name, meta, m.Weight, m.Hash, icon, m.Kind is AiModelKind.Lora or AiModelKind.Embedding, first);
    }


    private void UpdateWorkflow(AiGenerationInfo? info)
    {
        var saved = SavedWorkflow(info);
        if (saved is null || info is null)
        {
            Workflow = null;
            return;
        }

        var app = saved.Format is AiWorkflowFormat.InvokeAiWorkflow or AiWorkflowFormat.InvokeAiGraph ? "InvokeAI" : "ComfyUI";
        var summary = string.Join(" + ", info.Workflows.Select(w => w.FormatLabel).Distinct());
        Workflow = new AiWorkflowView(
            Count(saved.NodeCount), UiText.Plural(saved.NodeCount, "node"),
            Count(saved.SamplerCount), UiText.Plural(saved.SamplerCount, "sampler"),
            Count(saved.ModelCount), UiText.Plural(saved.ModelCount, "model"),
            AiRows.FormatSize(saved.Size), summary, UiText.SaveWorkflowTip(app), UiText.CopyWorkflowTip(AiRows.FormatSize(saved.Size)), saved.SourceKey);
    }


    private void UpdateProvenance(AiGenerationInfo? info)
    {
        Credentials = null;
        Iptc = null;
        Aigc = null;
        Training = null;
        if (info is null) return;

        if (info.Sources.FirstOrDefault(s => s.C2pa is not null) is { C2pa: { } c2pa } c2paSource)
        {
            Credentials = CredentialsView(c2pa, c2paSource) with { Ingredients = IngredientViews(c2pa) };
        }

        if (info.Sources.FirstOrDefault(s => s.Iptc is not null) is { Iptc: { } iptc } iptcSource)
        {
            var rows = new List<AiKeyValue>();
            if (iptc.DigitalSourceType is { } dst) rows.Add(new AiKeyValue(UiText.SOURCE_TYPE, DigitalSourceTypes.Label(dst), DigitalSourceTypes.Term(dst)));
            if (iptc.AiSystem is { } system) rows.Add(new AiKeyValue(UiText.AI_SYSTEM, iptc.AiSystemVersion is null ? system : $"{system} {iptc.AiSystemVersion}"));
            if (iptc.PromptWriter is { } writer) rows.Add(new AiKeyValue(UiText.PROMPT_WRITER, writer));
            if (iptc.Credit is { } credit) rows.Add(new AiKeyValue(UiText.CREDIT, credit));
            if (rows.Count > 0) Iptc = Section(rows, iptcSource);
        }

        if (info.Sources.FirstOrDefault(s => s.Aigc is not null) is { Aigc: { } aigc } aigcSource)
        {
            List<AiKeyValue> rows = [new(UiText.AIGC_LABEL, aigc.Meaning, UiText.AigcCode(aigc.Label))];
            if (aigc.ContentProducer is { } producer) rows.Add(new AiKeyValue(UiText.AIGC_PRODUCER, producer, IsMono: true));
            if (aigc.ProduceId is { } produceId) rows.Add(new AiKeyValue(UiText.AIGC_PRODUCE_ID, produceId, IsMono: true));
            if (aigc.ContentPropagator is { } propagator) rows.Add(new AiKeyValue(UiText.AIGC_PROPAGATOR, propagator, IsMono: true));
            if (aigc.PropagateId is { } propagateId) rows.Add(new AiKeyValue(UiText.AIGC_PROPAGATE_ID, propagateId, IsMono: true));
            Aigc = Section(rows, aigcSource);
        }

        if (info.Sources.FirstOrDefault(s => s.Training is not null) is { Training: { } training } trainingSource)
        {
            var rows = new List<AiKeyValue>();
            if (training.DataMining is { } mining) rows.Add(new AiKeyValue(UiText.DATA_MINING, mining));
            if (training.LearningOptOutIn is { } optOut) rows.Add(new AiKeyValue(UiText.LEARNING_OPT_OUT, optOut));
            Training = Section(rows, trainingSource);
        }
    }


    private static AiSectionView CredentialsView(C2paInfo c2pa, AiSourceInfo source)
    {
        var rows = new List<AiKeyValue>();
        var active = c2pa.Active ?? c2pa.Manifests.FirstOrDefault();
        if (active?.ClaimGenerator is { } claim) rows.Add(new AiKeyValue(UiText.CLAIMED_BY, active.ClaimGeneratorVersion is null ? claim : $"{claim} {active.ClaimGeneratorVersion}"));

        var first = true;
        foreach (var a in active?.Actions ?? [])
        {
            var text = AiRows.ActionLabel(a.Action);
            if (a.Description is { Length: > 0 } description) text += " · " + description;
            if (a.SoftwareAgent is { } agent) text += " by " + agent;
            rows.Add(new AiKeyValue(first ? UiText.ACTIONS : string.Empty, text));
            first = false;
        }

        foreach (var a in active?.Actions.Where(a => a.DigitalSourceLabel is not null) ?? []) rows.Add(new AiKeyValue(UiText.SOURCE_TYPE, a.DigitalSourceLabel!));
        if (c2pa.Manifests.Count > 0) rows.Add(new AiKeyValue(UiText.MANIFESTS, UiText.ManifestCount(c2pa.Manifests.Count)));
        if (c2pa.RemoteManifestUrl is { } url) rows.Add(new AiKeyValue(UiText.REMOTE_MANIFEST, url, IsMono: true));

        return Section(rows, source);
    }


    // each ingredient of the active manifest, with what its own manifest claims
    private static IReadOnlyList<AiIngredientView> IngredientViews(C2paInfo c2pa)
    {
        var active = c2pa.Active;
        if (active is null) return [];

        var views = new List<AiIngredientView>();
        foreach (var ingredient in active.Ingredients)
        {
            var own = ingredient.ManifestLabel is { } label ? c2pa.Manifests.FirstOrDefault(m => m.Label == label) : null;
            var title = ingredient.Title ?? own?.Title ?? UiText.INGREDIENT;
            string detail;
            if (own is null) detail = UiText.NO_OWN_CREDENTIALS;
            else
            {
                var action = own.Actions.FirstOrDefault(a => a.DigitalSourceType is not null) ?? own.Actions.FirstOrDefault();
                detail = action is null ? own.ClaimGenerator ?? string.Empty : AiRows.ActionLabel(action.Action) + " by " + (action.SoftwareAgent ?? own.ClaimGenerator ?? "?");
                if (action?.DigitalSourceLabel is { } dst) detail += " · " + dst;
            }

            views.Add(new AiIngredientView(title, detail));
        }

        return views;
    }


    private static AiSectionView Section(IReadOnlyList<AiKeyValue> rows, AiSourceInfo source) =>
        new(rows, source.SourceKeys.FirstOrDefault(), source.SourceKeys.Count > 1 ? [.. source.SourceKeys.Skip(1)] : []);


    private void UpdatePseudoClasses()
    {
        var info = Info;
        PseudoClasses.Set(":loading", IsLoading && info is null);
        PseudoClasses.Set(":unknown", info?.Kind == AiKind.UnknownAi);
        PseudoClasses.Set(":prompt", !string.IsNullOrEmpty(Prompt));
        PseudoClasses.Set(":negative", !string.IsNullOrEmpty(NegativePrompt));
        PseudoClasses.Set(":negative-note", NegativeNote is not null);
        PseudoClasses.Set(":characters", Characters.Count > 0);
        PseudoClasses.Set(":styles", StyleNames.Count > 0);
        PseudoClasses.Set(":models", Models.Count > 0);
        PseudoClasses.Set(":more-models", MoreModels.Count > 0);

        // provenance alone (a C2PA manifest's fields) has no generation parameters to copy
        PseudoClasses.Set(":copyable", info?.Primary is { IsGeneration: true });
        PseudoClasses.Set(":provenance-only", info is not null && info.Primary is not { IsGeneration: true } && info.Kind != AiKind.UnknownAi);
    }


    // the More menu changes with the file: no generation parameters, no workflow
    private void BuildMenu()
    {
        if (_menuButton is null) return;

        var info = Info;
        var flyout = _menuButton.Flyout as MenuFlyout;
        if (flyout is null)
        {
            flyout = new MenuFlyout();
            MenuMotion.Attach(flyout);
            _menuButton.Flyout = flyout;
        }

        flyout.Items.Clear();
        if (info is null) return;

        flyout.Items.Add(new GlassMenuHeader { Text = UiText.AI_MENU_HEADER });
        if (info.Primary is { IsGeneration: true }) flyout.Items.Add(MenuItem(AiAction.CopyGenerationParameters, IconId.Copy, UiText.COPY_PARAMETERS, UiText.COPY_PARAMETERS_DESC));
        flyout.Items.Add(MenuItem(AiAction.CopyJson, IconId.Braces, UiText.COPY_AS_JSON, UiText.COPY_AS_JSON_DESC));
        if (SavedWorkflow(info) is { } workflow)
        {
            var app = workflow.Format is AiWorkflowFormat.InvokeAiWorkflow or AiWorkflowFormat.InvokeAiGraph ? "InvokeAI" : "ComfyUI";
            flyout.Items.Add(MenuItem(AiAction.CopyWorkflowJson, IconId.AiWorkflow, UiText.COPY_WORKFLOW_JSON, UiText.CopyWorkflowTip(AiRows.FormatSize(workflow.Size))));
            flyout.Items.Add(MenuItem(AiAction.SaveWorkflow, IconId.Save, UiText.SAVE_WORKFLOW, UiText.SaveWorkflowTip(app)));
        }

        flyout.Items.Add(new Separator());
        var tags = SourceTags(info);
        flyout.Items.Add(MenuItem(AiAction.ShowSourceTags, IconId.AiSource, UiText.SHOW_SOURCE_TAGS, UiText.SourceTagsDescription(tags.Select(AiRows.SourceLabel).ToList())));
    }


    private GlassMenuItem MenuItem(AiAction action, IconId glyph, string header, string description)
    {
        var item = new GlassMenuItem { Glyph = glyph, Header = header, Description = description };
        item.Click += (_, _) => Run(Request(action));
        return item;
    }


    private AiActionRequest Request(AiAction action) => action switch
    {
        AiAction.CopyPrompt => new(action, Prompt, UiText.PROMPT),
        AiAction.CopyNegativePrompt => new(action, NegativePrompt, UiText.NEGATIVE_PROMPT),
        AiAction.CopyText => new(action, Raw?.Text, UiText.RAW_TEXT),
        AiAction.ShowSourceTag => new(action, Key: SourceKey),
        _ => new(action),
    };


    private void Run(AiActionRequest request)
    {
        if (ActionCommand is { } command && command.CanExecute(request)) command.Execute(request);
    }


    private static bool IsCharacter(AiPrompt p) => p.Label.StartsWith("Character ", StringComparison.Ordinal);


    // "Character 1 (0.5, 0.5)" -> "Centre 0.5, 0.5"
    private static string Centre(string label)
    {
        var open = label.IndexOf('(', StringComparison.Ordinal);
        return open > 0 && label.EndsWith(')') ? UiText.CENTRE + " " + label[(open + 1)..^1] : string.Empty;
    }


    private static bool IsMonoValue(string value) => value.Length < 40 && value.Any(char.IsDigit);


    private static string Count(int n) => n.ToString(CultureInfo.InvariantCulture);

    #endregion // Private methods


    #region Event handlers

    // every button of the card says what it does with its Tag: an AiAction, or a whole request (copy buttons, source links)
    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        var request = (e.Source as Control)?.Tag switch
        {
            AiActionRequest r => r,
            AiAction a => Request(a),
            _ => null,
        };
        if (request is null) return;

        Run(request);
        e.Handled = true;
    }


    private void More_Click(object? sender, RoutedEventArgs e) => IsExpanded = !IsExpanded;


    // Show more only when a clamp hides text (PromptText measures it)
    private void Prompt_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == PromptText.IsClampedProperty) PseudoClasses.Set(":clamped", _promptText?.IsClamped == true || _negativeText?.IsClamped == true);
    }

    #endregion // Event handlers
}


/// <summary>
/// What the selected AI field card shows (AI addendum question 4): the field, its value, the tag it came from and how it was read.
/// </summary>
public sealed record AiFieldView(string Key, string Name, string Value, string? SourceKey, string? ParsedAs, bool IsPrompt, IReadOnlyCollection<string> Embeddings)
{
    #region Properties

    /// <summary>
    /// Gets "-PNG:Parameters", the source as an ExifTool argument.
    /// </summary>
    public string? SourceArgument => SourceKey is null ? null : "-" + AiRows.SourceLabel(SourceKey);

    public string Length => UiText.Characters(Value.Length);

    public string ShownValue => ShownText.Well(Value);

    #endregion // Properties
}


/// <summary>
/// The selected AI field card (AI addendum question 4): on top of the inspector's cards (owner at G7), Accent2 40% border and a 3 px Accent2 ring; AI generation chip and Close; the field's name; the value (prompts in the UI font with their tints, question 14); Source tag with copy, Parsed as, Length; Show source tag in Edit's place.
/// </summary>
public class AiFieldCard : TemplatedControl
{
    #region Fields and constants

    public static readonly StyledProperty<AiFieldView?> ViewProperty =
        AvaloniaProperty.Register<AiFieldCard, AiFieldView?>(nameof(View));

    public static readonly StyledProperty<ICommand?> ActionCommandProperty =
        AvaloniaProperty.Register<AiFieldCard, ICommand?>(nameof(ActionCommand));

    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<AiFieldCard, ICommand?>(nameof(CloseCommand));

    private Button? _show;

    #endregion // Fields and constants


    #region Constructors

    public AiFieldCard()
    {
        AddHandler(Button.ClickEvent, Button_Click);
        UpdatePseudoClasses();
    }

    #endregion // Constructors


    #region Properties

    public AiFieldView? View
    {
        get => GetValue(ViewProperty);
        set => SetValue(ViewProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    #endregion // Properties


    #region Public methods

    public void Appear() => Card.PlayAppear(this);


    /// <summary>
    /// Enter on an AI row (Appendix B item 10): keyboard focus moves to Show source tag.
    /// </summary>
    public bool FocusFirst() => _show?.Focus(Avalonia.Input.NavigationMethod.Tab) == true;

    #endregion // Public methods


    #region Protected methods

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _show = e.NameScope.Find<Button>("PART_ShowSource");
    }


    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ViewProperty) UpdatePseudoClasses();
    }

    #endregion // Protected methods


    #region Private methods

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":prompt", View?.IsPrompt == true);
        PseudoClasses.Set(":source", View?.SourceKey is not null);
    }

    #endregion // Private methods


    #region Event handlers

    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        if (View is not { } view || (e.Source as Control)?.Tag is not AiAction action) return;

        AiActionRequest request = action switch
        {
            AiAction.CopyText => new(action, view.Value, view.Name),
            AiAction.CopyTagKey => new(action, view.SourceArgument, view.SourceArgument),
            _ => new(action, Key: view.SourceKey),
        };
        if (ActionCommand is { } command && command.CanExecute(request)) command.Execute(request);
        e.Handled = true;
    }

    #endregion // Event handlers
}


/// <summary>
/// An LLM character card (AI addendum question 9): a neutral card of its own, not counted as AI generation; name, description, creator, version and tags, Copy card JSON and Save.
/// </summary>
public class CharacterCard : TemplatedControl
{
    #region Fields and constants

    public static readonly StyledProperty<AiSourceInfo?> SourceProperty =
        AvaloniaProperty.Register<CharacterCard, AiSourceInfo?>(nameof(Source));

    public static readonly StyledProperty<ICommand?> ActionCommandProperty =
        AvaloniaProperty.Register<CharacterCard, ICommand?>(nameof(ActionCommand));

    public static readonly DirectProperty<CharacterCard, IReadOnlyList<AiKeyValue>> RowsProperty =
        AvaloniaProperty.RegisterDirect<CharacterCard, IReadOnlyList<AiKeyValue>>(nameof(Rows), c => c.Rows);

    public static readonly DirectProperty<CharacterCard, string?> CharacterNameProperty =
        AvaloniaProperty.RegisterDirect<CharacterCard, string?>(nameof(CharacterName), c => c.CharacterName);

    public static readonly DirectProperty<CharacterCard, string?> DescriptionProperty =
        AvaloniaProperty.RegisterDirect<CharacterCard, string?>(nameof(Description), c => c.Description);

    public static readonly DirectProperty<CharacterCard, string?> SpecProperty =
        AvaloniaProperty.RegisterDirect<CharacterCard, string?>(nameof(Spec), c => c.Spec);

    public static readonly DirectProperty<CharacterCard, IReadOnlyList<string>> TagsProperty =
        AvaloniaProperty.RegisterDirect<CharacterCard, IReadOnlyList<string>>(nameof(Tags), c => c.Tags);

    public static readonly DirectProperty<CharacterCard, string?> SourceKeyProperty =
        AvaloniaProperty.RegisterDirect<CharacterCard, string?>(nameof(SourceKey), c => c.SourceKey);

    public static readonly DirectProperty<CharacterCard, IReadOnlyList<string>> OtherSourcesProperty =
        AvaloniaProperty.RegisterDirect<CharacterCard, IReadOnlyList<string>>(nameof(OtherSources), c => c.OtherSources);

    private IReadOnlyList<AiKeyValue> _rows = [];
    private string? _name;
    private string? _description;
    private string? _spec;
    private IReadOnlyList<string> _tags = [];
    private string? _sourceKey;
    private IReadOnlyList<string> _otherSources = [];

    #endregion // Fields and constants


    #region Constructors

    public CharacterCard()
    {
        AddHandler(Button.ClickEvent, Button_Click);
    }

    #endregion // Constructors


    #region Properties

    /// <summary>
    /// The character card source of the AI data.
    /// </summary>
    public AiSourceInfo? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    /// <summary>
    /// Gets Creator and Version.
    /// </summary>
    public IReadOnlyList<AiKeyValue> Rows
    {
        get => _rows;
        private set => SetAndRaise(RowsProperty, ref _rows, value);
    }

    public string? CharacterName
    {
        get => _name;
        private set => SetAndRaise(CharacterNameProperty, ref _name, value);
    }

    public string? Description
    {
        get => _description;
        private set => SetAndRaise(DescriptionProperty, ref _description, value);
    }

    /// <summary>
    /// Gets the card's spec, the badge: "chara_card_v3".
    /// </summary>
    public string? Spec
    {
        get => _spec;
        private set => SetAndRaise(SpecProperty, ref _spec, value);
    }

    public IReadOnlyList<string> Tags
    {
        get => _tags;
        private set => SetAndRaise(TagsProperty, ref _tags, value);
    }

    public string? SourceKey
    {
        get => _sourceKey;
        private set => SetAndRaise(SourceKeyProperty, ref _sourceKey, value);
    }

    public IReadOnlyList<string> OtherSources
    {
        get => _otherSources;
        private set => SetAndRaise(OtherSourcesProperty, ref _otherSources, value);
    }

    #endregion // Properties


    #region Public methods

    public void Appear() => Card.PlayAppear(this);

    #endregion // Public methods


    #region Protected methods

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != SourceProperty) return;

        var source = Source;
        var card = source?.Character;
        CharacterName = card?.Name ?? source?.CharacterName;
        Description = card?.Description;
        Spec = source?.GeneratorVersion;
        Tags = card?.Tags ?? [];
        SourceKey = source?.SourceKeys.FirstOrDefault();
        OtherSources = source is { SourceKeys.Count: > 1 } ? [.. source.SourceKeys.Skip(1)] : [];

        var rows = new List<AiKeyValue>();
        if (card?.Creator is { Length: > 0 } creator) rows.Add(new AiKeyValue(UiText.CREATOR, creator));
        if (card?.CharacterVersion is { Length: > 0 } version) rows.Add(new AiKeyValue(UiText.VERSION, version));
        Rows = rows;
        PseudoClasses.Set(":rows", rows.Count > 0);
        PseudoClasses.Set(":tags", card?.Tags.Count > 0);
        PseudoClasses.Set(":description", !string.IsNullOrWhiteSpace(card?.Description));
    }

    #endregion // Protected methods


    #region Event handlers

    private void Button_Click(object? sender, RoutedEventArgs e)
    {
        var request = (e.Source as Control)?.Tag switch
        {
            AiActionRequest r => r,
            AiAction a => new AiActionRequest(a),
            _ => null,
        };
        if (request is null) return;

        if (ActionCommand is { } command && command.CanExecute(request)) command.Execute(request);
        e.Handled = true;
    }

    #endregion // Event handlers
}
