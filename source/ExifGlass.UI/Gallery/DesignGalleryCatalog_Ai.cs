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
using Avalonia.Controls;
using Avalonia.Layout;
using ExifGlass.Core.Ai;
using ExifGlass.UI.Controls;

namespace ExifGlass.UI.Gallery;

public static partial class DesignGalleryCatalog
{
    #region Fields and constants

    // the AI addendum's A1111 sample, written as SD WebUI 1.10.1 writes it
    private const string SAMPLE_INFOTEXT = "a lighthouse on a basalt cliff at blue hour, (volumetric fog:1.2) drifting over the sea, long exposure, 35mm film grain, highly detailed, cinematic lighting <lora:add_detail:0.8>\n"
        + "Negative prompt: lowres, blurry, oversaturated, watermark, text, jpeg artifacts, easynegative\n"
        + "Steps: 30, Sampler: DPM++ 2M, Schedule type: Karras, CFG scale: 6.5, Seed: 2841937305, Size: 1024x1536, Model hash: 31e35c80fc, Model: sd_xl_base_1.0, "
        + "VAE hash: 235745af8d, VAE: sdxl_vae, Denoising strength: 0.35, Clip skip: 2, Hires upscale: 1.5, Hires steps: 15, Hires upscaler: 4x-UltraSharp, "
        + "ADetailer model: face_yolov8n.pt, ADetailer confidence: 0.3, Lora hashes: \"add_detail: 7c6bad76eb54\", TI hashes: \"easynegative: c74b4e810b03\", Version: v1.10.1";

    private const string SAMPLE_DST = "http://cv.iptc.org/newscodes/digitalsourcetype/trainedAlgorithmicMedia";

    #endregion // Fields and constants


    #region Sections

    private static GallerySection Ai()
    {
        static AiCard Card(AiGenerationInfo? info, Action<AiCard>? setup = null)
        {
            var card = new AiCard { Width = CARD_WIDTH, Info = info };
            setup?.Invoke(card);
            return card;
        }

        static StackPanel Column(params Control[] children)
        {
            var column = new StackPanel { Spacing = 8 };
            column.Children.AddRange(children);
            return column;
        }

        static StackPanel Row(params Control[] children)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            row.Children.AddRange(children);
            return row;
        }

        SourceLink Link() => new() { SourceKey = "PNG:Parameters", OtherSources = ["XMP:XMP-iptcExt:DigitalSourceType"] };

        var opened = Card(A1111Sample(), c =>
        {
            c.IsExpanded = true;
            c.AllParametersOpen = true;
            c.AllModelsOpen = true;
        });

        return new GallerySection("c-ai", "AI generation", "AI addendum (D28, signed off at G1): the AI card at 304 with the kind chip and the tag it came from (question 1), prompt 4 lines and negative 2 with their tints, characters and styles, parameters per sampler with All parameters, models (3, then n more), the workflow summary (D25), Content Credentials Not verified in a neutral badge (D21, question 6), IPTC, the AIGC label, AI training, notes; the selected AI field under it (question 4, prompts in the UI font, question 14); the character card (question 9); the source link; the rail's AI badge (question 13).",
        [
            new("A1111", Card(A1111Sample()), "one tag feeds it all: from PNG:Parameters +1"),
            new("A1111, opened", opened, "Show more, All parameters and every model open"),
            new("ComfyUI, two samplers", Card(ComfySample()), "the sampler switch, the workflow summary"),
            new("NovelAI, characters", Card(NovelAiSample()), "regional prompts with their centres"),
            new("Fooocus, styles", Card(FooocusSample()), "no negative prompt of its own; the styles add one"),
            new("C2PA, provenance only", Card(C2paSample()), "Copy AI data as JSON; no signer"),
            new("IPTC, AIGC label, AI training", Card(ProvenanceSample()), "several sources in one card (Appendix B item 21)"),
            new("Unknown AI", Card(UnknownSample()), "neutral tile, chip and badge; the raw text"),
            new("loading", Card(null, c => c.IsLoading = true), "a slow AI read for a file with AI tags"),
            new("old ExifTool, a parse warning", Card(WarningSample(), c => c.ExifToolNote = "Your ExifTool 12.40 can't read every AI field. IPTC 2025 AI fields need 13.40. C2PA Content Credentials in every container need 12.75."), "R19"),
            new("selected AI field, prompt", new AiFieldCard { Width = CARD_WIDTH, View = new AiFieldView("AI:prompt", "Prompt", A1111Sample().Prompt!.Text, "PNG:Parameters", "A1111 infotext", true, ["easynegative"]) }, "UI font with the prompt tints"),
            new("selected AI field, a value", new AiFieldCard { Width = CARD_WIDTH, View = new AiFieldView("AI:seed", "Seed", "2841937305", "PNG:Parameters", "A1111 infotext", false, []) }),
            new("character card", new CharacterCard { Width = CARD_WIDTH, Source = CharacterSample() }, "neutral; not AI generation"),
            new("notes", Column(
                new InlineNote { Width = CARD_WIDTH, Text = UiText.PROVENANCE_ONLY },
                new InlineNote { Width = CARD_WIDTH, Kind = NoteKind.Info, Text = UiText.UNKNOWN_AI_NOTE },
                new InlineNote { Width = CARD_WIDTH, Kind = NoteKind.Warning, Icon = IconId.AiWarning, Text = "The UI workflow isn't valid JSON (character 18,204)." },
                new InlineNote { Width = CARD_WIDTH, Kind = NoteKind.Danger, Icon = IconId.Delete, Text = "Prompts, workflows and parameters are removed when you save." }), "plain, info, warning, danger"),
            new("source link", Row(Link(), Link().Hover(), Link().Pressed(), Link().FocusVisible()), "rest, :pointerover (AccentText), :pressed, :focus-visible"),
            new("file card, AI badge", new FileCard { Width = RAIL_CONTENT, Title = "00042-2841937305.png", Meta = "PNG · 2.1 MB · 1024×1536", Extension = "PNG", Segments = SEGMENTS, AiBadge = "SD WebUI v1.10.1", AiKind = AiKind.Generated }, "under the meta, jumps to the AI group"),
            new("file card, character card", new FileCard { Width = RAIL_CONTENT, Title = "captain.png", Meta = "PNG · 412 kB · 400×600", Extension = "PNG", Segments = SEGMENTS, AiBadge = "Character card", AiKind = AiKind.CharacterCard }, "neutral"),
        ]);
    }

    #endregion // Sections


    #region Helpers

    private static AiGenerationInfo A1111Sample()
    {
        using var tags = AiTagSet.FromPairs([("PNG:Main:Parameters", SAMPLE_INFOTEXT), ("XMP:XMP-iptcExt:Main:DigitalSourceType", SAMPLE_DST)]);
        return AiEngine.Parse(tags)!;
    }


    private static AiField Field(string id, string label, string value) => new(id, label, value, label);


    private static AiGenerationInfo ComfySample()
    {
        AiField[] main = [Field("sampler", "Sampler", "dpmpp_2m"), Field("scheduler", "Scheduler", "karras"), Field("steps", "Steps", "24 of 30"), Field("cfg", "CFG", "6.5"), Field("seed", "Seed", "156680208700286"), Field("size", "Size", "1024x1536"), Field("denoise", "Denoise", "1.0"), Field("noise", "Noise", "enable"), Field("upscale", "Upscale", "4x-UltraSharp · ×1.5")];
        AiField[] refiner = [Field("sampler", "Sampler", "euler"), Field("scheduler", "Scheduler", "karras"), Field("steps", "Steps", "30 (from 24)"), Field("cfg", "CFG", "6.5"), Field("seed", "Seed", "156680208700286")];
        const string WORKFLOW = "{\"nodes\": []}";

        return new AiGenerationInfo([new AiSourceInfo
        {
            Source = AiSourceKind.ComfyUi,
            Generator = "ComfyUI",
            Kind = AiKind.Generated,
            Prompts = [new AiPrompt("isometric diorama of a tiny ramen shop at night, neon signs, rain puddles, warm interior light, (tilt-shift:1.1), highly detailed", AiPromptRole.Positive), new AiPrompt("blurry, lowres, text, watermark, deformed", AiPromptRole.Negative)],
            Parameters = main,
            MainPassTitle = "Base",
            Passes = [new AiSamplerPass("Refiner", refiner, [])],
            Models = [new AiModelRef(AiModelKind.Checkpoint, "sd_xl_base_1.0.safetensors"), new AiModelRef(AiModelKind.Refiner, "sd_xl_refiner_1.0.safetensors"), new AiModelRef(AiModelKind.Lora, "add_detail.safetensors", "0.8")],
            Workflows = [new AiWorkflowSummary(AiWorkflowFormat.ComfyUiWorkflow, "ComfyUI workflow 0.4", 15, 18, 2, 4, 0, 38912, WORKFLOW, "PNG:Workflow"), new AiWorkflowSummary(AiWorkflowFormat.ComfyUiApi, "ComfyUI API prompt", 12, 14, 2, 4, 0, 6144, WORKFLOW, "PNG:Prompt")],
            SourceKeys = ["PNG:Prompt", "PNG:Workflow"],
        }]);
    }


    private static AiGenerationInfo NovelAiSample() => new([new AiSourceInfo
    {
        Source = AiSourceKind.NovelAi,
        Generator = "NovelAI",
        GeneratorVersion = "V4.5",
        Kind = AiKind.Generated,
        Prompts =
        [
            new AiPrompt("2girls, rainy alley at night, neon reflections, umbrellas, cinematic, best quality", AiPromptRole.Positive),
            new AiPrompt("lowres, bad anatomy, blurry, watermark", AiPromptRole.Negative),
            new AiPrompt("girl, short silver hair, yellow raincoat, holding umbrella", AiPromptRole.Positive, "Character 1 (0.3, 0.5)"),
            new AiPrompt("girl, long black hair, red scarf, looking back", AiPromptRole.Positive, "Character 2 (0.7, 0.5)"),
        ],
        Parameters = [Field("sampler", "Sampler", "k_euler_ancestral"), Field("scheduler", "Scheduler", "karras"), Field("steps", "Steps", "28"), Field("cfg", "CFG", "5.0"), Field("seed", "Seed", "2222"), Field("size", "Size", "832x1216")],
        Models = [new AiModelRef(AiModelKind.Checkpoint, "NovelAI Diffusion V4.5", Hash: "4BDE2A90")],
        SourceKeys = ["PNG:Comment", "PNG:Software"],
    }]);


    private static AiGenerationInfo FooocusSample() => new([new AiSourceInfo
    {
        Source = AiSourceKind.Fooocus,
        Generator = "Fooocus",
        GeneratorVersion = "2.5.5",
        Kind = AiKind.Generated,
        Prompts = [new AiPrompt("a cat café ☕, cozy morning light, film photo", AiPromptRole.Positive), new AiPrompt("unrealistic, saturated", AiPromptRole.Negative, "Expanded")],
        Parameters = [Field("styles", "Styles", "['Fooocus V2', 'Fooocus Enhance', 'Fooocus Sharp']"), Field("performance", "Performance", "Speed"), Field("steps", "Steps", "30"), Field("cfg", "CFG", "4"), Field("seed", "Seed", "123"), Field("size", "Size", "1152x896")],
        Models = [new AiModelRef(AiModelKind.Checkpoint, "juggernautXL_v8Rundiffusion", Hash: "aeb7e9e689"), new AiModelRef(AiModelKind.Lora, "sd_xl_offset_example-lora_1.0", "0.1", "4852686128")],
        SourceKeys = ["PNG:Parameters", "PNG:Fooocus_scheme"],
    }]);


    private static AiGenerationInfo C2paSample()
    {
        var active = new C2paManifest("urn:a", 2, "ChatGPT", null, "image.png", null, [new C2paAction("c2pa.opened", null, null, null, null)], [new C2paIngredient(null, "parentOf", null, "urn:b")], [], true, false);
        var created = new C2paManifest("urn:b", 2, "ChatGPT", null, "image.png", null, [new C2paAction("c2pa.created", "GPT-4o", null, SAMPLE_DST, null)], [], [], false, false);
        return new AiGenerationInfo([new AiSourceInfo
        {
            Source = AiSourceKind.C2pa,
            Generator = "ChatGPT",
            Kind = AiKind.Generated,
            C2pa = new C2paInfo([active, created], null),
            SourceKeys = ["JUMBF:CBOR:Actions", "JUMBF:CBOR:Claim_generator_info"],
        }]);
    }


    private static AiGenerationInfo ProvenanceSample() => new(
    [
        new AiSourceInfo
        {
            Source = AiSourceKind.Iptc,
            Generator = "Imagen",
            GeneratorVersion = "4.0",
            Kind = AiKind.Generated,
            Prompts = [new AiPrompt("Aerial view of rice terraces at sunrise, mist in the valleys, photorealistic", AiPromptRole.Positive, "Prompt information")],
            Iptc = new IptcAiInfo(SAMPLE_DST, "Trained algorithmic media", "Imagen", "4.0", null, "Mai Tran", null),
            SourceKeys = ["XMP:XMP-iptcExt:AISystemUsed", "XMP:XMP-iptcExt:DigitalSourceType", "XMP:XMP-iptcExt:AIPromptInformation"],
        },
        new AiSourceInfo
        {
            Source = AiSourceKind.Aigc,
            Generator = "Kling",
            Kind = AiKind.Generated,
            Aigc = new AigcLabel("1", "AI-generated", "kling", "3f9c2a41b7d84e0f9a1c", null, null, null, null),
            SourceKeys = ["QuickTime:Keys:AIGC"],
        },
        new AiSourceInfo
        {
            Source = AiSourceKind.TrainingSignals,
            Generator = "AI-training signals",
            Training = new TrainingSignals("Prohibited for AI/ML training", null),
            SourceKeys = ["XMP:XMP-plus:DataMining"],
        },
    ]);


    private static AiGenerationInfo UnknownSample() => new([new AiSourceInfo
    {
        Source = AiSourceKind.UnknownAi,
        Generator = "Unknown generator",
        Kind = AiKind.UnknownAi,
        RawText = "{\"app\":\"Imaginator\",\"ver\":\"2.1\",\"p\":\"a ceramic owl on a windowsill, soft daylight\",\"st\":24,\"g\":5.5,\"sd\":90210,\"sz\":[1024,1024]}",
        SourceKeys = ["PNG:Generation_data"],
    }]);


    private static AiGenerationInfo WarningSample() => new([new AiSourceInfo
    {
        Source = AiSourceKind.ComfyUi,
        Generator = "ComfyUI",
        Kind = AiKind.Generated,
        Prompts = [new AiPrompt("isometric diorama of a tiny ramen shop at night", AiPromptRole.Positive)],
        Parameters = [Field("sampler", "Sampler", "dpmpp_2m"), Field("steps", "Steps", "30")],
        Warnings = ["The UI workflow isn't valid JSON (character 18,204); prompts and settings come from the API prompt"],
        SourceKeys = ["PNG:Prompt", "PNG:Workflow"],
    }]);


    private static AiSourceInfo CharacterSample() => new()
    {
        Source = AiSourceKind.CharacterCard,
        Generator = "Character card",
        GeneratorVersion = "chara_card_v3",
        Kind = AiKind.CharacterCard,
        CharacterName = "Captain Ilsa Varn",
        Character = new CharacterCardInfo("Captain Ilsa Varn", "A weathered airship captain who trades in rumours and spare parts along the northern trade winds. Dry humour, fiercely loyal to her crew.",
            "@inkwell", "1.2", ["adventure", "airship", "steampunk"], "chara_card_v3", "{}"),
        SourceKeys = ["PNG:Ccv3", "PNG:Chara"],
    };

    #endregion // Helpers
}
