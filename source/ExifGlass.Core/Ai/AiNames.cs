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
/// The name detector strings of Appendix E.3: AI tools, C2PA agents, credit lines and PNG keywords.
/// </summary>
public static class AiNames
{
    #region Fields and constants

    // C2PA software agents and claim generators that are AI (E.3); matched as substrings, case-insensitive
    private static readonly string[] AGENTS =
    [
        "ChatGPT", "GPT-4o", "GPT-4", "DALL·E", "DALL-E", "DALLE", "OpenAI", "gpt-image", "Sora",
        "Azure OpenAI", "Image Creator", "Microsoft Responsible AI",
        "Adobe Firefly", "Adobe_Firefly", "Firefly",
        "Google C2PA Core Generator", "Gemini", "Imagen", "Veo", "SynthID",
        "Midjourney", "Stable Diffusion", "Stability AI", "Runway", "Luma", "Kling", "Pika", "Ideogram", "Leonardo", "Flux",
    ];

    private static readonly string[] DESCRIPTIONS = ["AI Generated", "Generative AI", "Created by Google Generative AI", "Generative Fill"];

    // Software, CreatorTool, Make/Model or Artist values that name an AI tool (E.3)
    private static readonly (string Match, string Name)[] TOOLS =
    [
        ("NovelAI", "NovelAI"), ("Fooocus", "Fooocus"), ("Image Playground", "Image Playground"), ("Draw Things", "Draw Things"),
        ("Midjourney", "Midjourney"), ("DALL-E", "DALL·E"), ("DALL·E", "DALL·E"), ("Stable Diffusion", "Stable Diffusion"),
        ("ComfyUI", "ComfyUI"), ("InvokeAI", "InvokeAI"), ("Krita AI", "Krita AI Diffusion"), ("Adobe Firefly", "Adobe Firefly"),
        ("DiffusionBee", "DiffusionBee"), ("SwarmUI", "SwarmUI"), ("Leonardo.Ai", "Leonardo.Ai"), ("Ideogram", "Ideogram"),
        ("Imagen", "Imagen"), ("Gemini", "Gemini"),
    ];

    // PNG text keywords AI tools write (E.3); an unparsed one still marks the file as Unknown AI
    private static readonly string[] PNG_KEYWORDS =
    [
        "Parameters", "Prompt", "Workflow", "Invokeai_metadata", "Invokeai_workflow", "Invokeai_graph", "Sd-metadata", "Dream",
        "Fooocus_scheme", "Aesthetic_score", "Generation_data", "Negative_prompt", "Sui_image_params",
    ];

    #endregion // Fields and constants


    #region Public methods

    public static bool IsAiAgent(string? name) => name is not null && AGENTS.Any(a => name.Contains(a, StringComparison.OrdinalIgnoreCase));


    public static bool IsAiDescription(string? text) => text is not null && DESCRIPTIONS.Any(d => text.Contains(d, StringComparison.OrdinalIgnoreCase));


    /// <summary>
    /// The AI tool a Software or CreatorTool value names, or null.
    /// </summary>
    public static string? ToolName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        foreach (var (match, name) in TOOLS)
        {
            if (value.Contains(match, StringComparison.OrdinalIgnoreCase)) return name;
        }

        return null;
    }


    public static bool IsAiPngKeyword(string name) => PNG_KEYWORDS.Contains(name, StringComparer.OrdinalIgnoreCase);

    #endregion // Public methods
}
