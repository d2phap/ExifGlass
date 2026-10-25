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
namespace ExifGlass.UI;

/// <summary>
/// The words the design-system controls show themselves (plan 5.9: all strings in one place, sentence case).
/// </summary>
public static class UiText
{
    #region Constants

    public const string SWITCH_ON = "On";
    public const string SWITCH_OFF = "Off";
    public const string CLEAR = "Clear";
    public const string COPY = "Copy";
    public const string CLEAR_SEARCH = "Clear search";
    public const string REMOVE = "Remove";
    public const string SHOW_MORE = "Show more";
    public const string SHOW_LESS = "Show less";
    public const string ADD_ARGUMENT = "Add an argument";
    public const string STORE_CAPTION = "Get it from";
    public const string STORE_TARGET = "Microsoft Store";
    public const string RELEASES_CAPTION = "Download from";
    public const string RELEASES_TARGET = "GitHub Releases";
    public const string DIALOG_CANCEL = "Cancel";
    public const string SCROLL_UP = "Scroll up";
    public const string SCROLL_DOWN = "Scroll down";
    public const string SCROLL_LEFT = "Scroll left";
    public const string SCROLL_RIGHT = "Scroll right";

    // the table's column headers are Overline: uppercase (DS 1.1)
    public const string COLUMN_INDEX = "#";
    public const string COLUMN_TAG_ID = "TAG ID";
    public const string COLUMN_TAG_NAME = "TAG NAME";
    public const string COLUMN_VALUE = "VALUE";
    public const string COPY_VALUE = "Copy value";
    public const string BADGE_EDITED = "Edited";
    public const string BADGE_DELETE = "Delete";
    public const string BADGE_NEW = "New";
    public const string AI_GROUP = "AI generation";

    // the inspector's cards (DS 6.11, S4); overlines are uppercase (DS 1.1)
    public const string CLOSE = "Close";
    public const string DELETE_TAG = "Delete tag";
    public const string UNDO_DELETE = "Undo delete";
    public const string COPY_TAG_KEY = "Copy tag key";
    public const string COPY_RAW_VALUE = "Copy raw value";
    public const string COPY_FORMAT = "Copy format";
    public const string EDIT = "Edit";
    public const string EDIT_VALUE = "Edit value";
    public const string CANCEL = "Cancel";
    public const string APPLY = "Apply";
    public const string CANCEL_TIP = "Cancel (Esc)";
    public const string REVERT = "Revert";
    public const string RAW_VALUE = "Raw (-n)";
    public const string FORMAT = "Format";
    public const string TAG_KEY = "Tag key";
    public const string WAS = "Was: {0}";
    public const string AI_GENERATION_OVERLINE = "AI GENERATION";
    public const string PROMPT_OVERLINE = "PROMPT";
    public const string NEGATIVE_PROMPT_OVERLINE = "NEGATIVE PROMPT";
    public const string COPY_PARAMETERS = "Copy generation parameters";
    public const string PREVIEW_OVERLINE = "PREVIEW";
    public const string OPEN_IN_OSM = "Open in OpenStreetMap";
    public const string DETAIL_CAMERA = "Camera";
    public const string DETAIL_LENS = "Lens";
    public const string DETAIL_CAPTURED = "Captured";
    public const string DETAIL_LOCATION = "Location";

    // editing (DS 6.11-6.13, S5)
    public const string SAVE_BINARY = "Save binary data…";
    public const string SAVE_BINARY_TIP = "Save the tag's bytes as a file";
    public const string ADD_TAG_OVERLINE = "ADD TAG";
    public const string ADD_TAG = "Add tag";
    public const string TAG_NAME_PLACEHOLDER = "Tag name, e.g. Artist";
    public const string VALUE_PLACEHOLDER = "Value";
    public const string TAG_GROUP = "Tag group";
    public const string DISCARD = "Discard";
    public const string DISCARD_TIP = "Drop every pending change";
    public const string SAVE = "Save";
    public const string SAVING = "Saving…";
    public const string WRITE = "Write";
    public const string WRITE_TIP = "Save runs this command";
    public const string COPY_COMMAND = "Copy command";
    public const string EXPAND_COMMAND = "Show the whole command";
    public const string COLLAPSE_COMMAND = "Show one line";

    // the AI surfaces (AI addendum): sentence case, no em dashes (owner at G4)
    public const string PROMPT = "Prompt";
    public const string NEGATIVE_PROMPT = "Negative prompt";
    public const string RAW_TEXT = "Raw text";
    public const string PROMPT_INFORMATION_OVERLINE = "PROMPT INFORMATION";
    public const string RAW_TEXT_OVERLINE = "RAW TEXT";
    public const string STYLES_OVERLINE = "STYLES";
    public const string PARAMETERS_OVERLINE = "PARAMETERS";
    public const string WORKFLOW_OVERLINE = "WORKFLOW";
    public const string CREDENTIALS_OVERLINE = "CONTENT CREDENTIALS";
    public const string IPTC_OVERLINE = "IPTC";
    public const string AIGC_OVERLINE = "AI LABEL · GB 45438-2025";
    public const string TRAINING_OVERLINE = "AI TRAINING";
    public const string CHARACTER_CARD_OVERLINE = "CHARACTER CARD";
    public const string ALL_PARAMETERS = "All parameters";
    public const string PASS_MAIN = "Pass 1";
    public const string FROM = "from";
    public const string ALSO = "Also: ";
    public const string SHOW_SOURCE_TAG = "Show source tag";
    public const string SHOW_SOURCE_TAGS = "Show source tags";
    public const string JUMP_TO_AI = "Jump to AI generation";
    public const string COPY_AI_JSON = "Copy AI data as JSON";
    public const string COPY_AS_JSON = "Copy as JSON";
    public const string COPY_AS_JSON_DESC = "Every parsed field with its source tag";
    public const string COPY_PARAMETERS_DESC = "A1111 text · pastes into \"Read generation parameters\"";
    public const string COPY_WORKFLOW_JSON = "Copy workflow JSON";
    public const string COPY_JSON = "Copy JSON";
    public const string SAVE_WORKFLOW = "Save workflow…";
    public const string AI_MENU_HEADER = "COPY OR SAVE THE AI DATA";
    public const string AI_MENU_TIP = "More ways to copy or save";
    public const string COPY_PROMPT = "Copy prompt";
    public const string COPY_NEGATIVE = "Copy negative prompt";
    public const string COPY_RAW = "Copy raw text";
    public const string COPY_CHARACTER = "Copy character prompt";
    public const string COPY_HASH = "Copy hash";
    public const string NOT_VERIFIED = "Not verified";
    public const string NOT_VERIFIED_TIP = "Not verified: ExifGlass shows what the file claims. It doesn't check the signature, so it can't say who signed it or whether the file changed since.";
    public const string SIGNER_NOT_SHOWN = "Signer not shown";
    public const string PROVENANCE_ONLY = "This file has no prompt or generation settings, only provenance.";
    public const string UNKNOWN_AI_NOTE = "ExifGlass found AI signals but doesn't know this format yet. The text is shown as it is.";
    public const string CHARACTER_NOTE = "Character data for chat apps, not image generation. It isn't counted as AI generation.";
    public const string AI_LOADING = "Reading the AI data. The table is ready; AI fields appear here when parsing finishes.";
    public const string USE_BUNDLED_EXIFTOOL = "Use the bundled ExifTool";
    public const string OPEN_SETTINGS = "Open settings";
    public const string REGIONAL_PROMPTS = "regional prompts";
    public const string CENTRE = "Centre";
    public const string CLAIMED_BY = "Claimed by";
    public const string ACTIONS = "Actions";
    public const string SOURCE_TYPE = "Source type";
    public const string MANIFESTS = "Manifests";
    public const string REMOTE_MANIFEST = "Remote manifest";
    public const string INGREDIENT = "Ingredient";
    public const string INGREDIENT_SUFFIX = "· ingredient";
    public const string NO_OWN_CREDENTIALS = "No Content Credentials of its own";
    public const string AI_SYSTEM = "AI system";
    public const string PROMPT_WRITER = "Prompt writer";
    public const string CREDIT = "Credit";
    public const string AIGC_LABEL = "Label";
    public const string AIGC_PRODUCER = "Producer";
    public const string AIGC_PRODUCE_ID = "Produce ID";
    public const string AIGC_PROPAGATOR = "Propagator";
    public const string AIGC_PROPAGATE_ID = "Propagate ID";
    public const string DATA_MINING = "Data mining";
    public const string LEARNING_OPT_OUT = "Opt-out";
    public const string SOURCE_TAG = "Source tag";
    public const string PARSED_AS = "Parsed as";
    public const string LENGTH = "Length";
    public const string CREATOR = "Creator";
    public const string VERSION = "Version";
    public const string COPY_CARD_JSON = "Copy card JSON";
    public const string SAVE_CARD_JSON = "Save card JSON…";
    public const string PARSED_INTO_TIP = "Parsed into AI generation · jump to its fields";
    public const string GO_TO_AI = "Go to AI generation";
    public const string CHARACTER = "Character";
    public const string JSON = "JSON";
    public const string SAME_AS_VALUE = "(same as value)";

    #endregion // Constants


    #region Public methods

    /// <summary>
    /// Writes a shortcut as the OS does: "Ctrl+K" on Windows and Linux, Command-K on macOS (DS 6.2).
    /// </summary>
    public static string Shortcut(string key, bool shift = false)
    {
        if (OperatingSystem.IsMacOS()) return (shift ? "⇧" : "") + "⌘" + key;

        return (shift ? "Ctrl+Shift+" : "Ctrl+") + key;
    }


    /// <summary>
    /// A column header's tooltip, as the prototype's title: "Sort by Tag name".
    /// </summary>
    public static string SortBy(string column) => $"Sort by {column}";


    public static string ApplyTip => $"{APPLY} ({Shortcut("Enter")})";


    /// <summary>
    /// The Add-tag card's button tooltip: "Add tag (Ctrl+Enter)".
    /// </summary>
    public static string AddTagTip => $"{ADD_TAG} ({Shortcut("Enter")})";


    /// <summary>
    /// "1 node", "15 nodes": the noun alone, for a count shown above it.
    /// </summary>
    public static string Plural(int n, string noun) => n == 1 ? noun : noun + "s";


    public static string ParsedInto(int n) => n == 1 ? "Parsed into 1 AI field" : $"Parsed into {n} AI fields";


    public static string Characters(int n) => n == 1 ? "1 character" : n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " characters";


    public static string MoreCharacters(int n) => $"{Characters(n)} more, Copy copies them all";


    public static string CharactersTitle(int n) => $"CHARACTERS · {n}";


    public static string ModelsTitle(int n) => $"MODELS · {n}";


    public static string MoreCount(int n) => $"{n} more";


    public static string Samplers(int n) => $"{n} samplers";


    public static string ManifestCount(int n) => n == 1 ? "1 · this one" : $"{n} · the active one is the last";


    public static string AigcCode(string code) => $"code {code}";


    public static string StylesNegative(string text) => $"No negative prompt. The styles add: {text}";


    public static string SaveWorkflowTip(string app) => $"A .json file that loads back into {app}";


    public static string CopyWorkflowTip(string size) => $"Exactly as embedded · {size}";


    public static string CopiedHash(string model) => $"Hash of {model}";


    public static string SourceTagsDescription(IReadOnlyList<string> tags) => (tags.Count == 1 ? "1 tag" : $"{tags.Count} tags") + (tags.Count > 0 ? " · " + string.Join(", ", tags) : string.Empty);

    #endregion // Public methods
}
