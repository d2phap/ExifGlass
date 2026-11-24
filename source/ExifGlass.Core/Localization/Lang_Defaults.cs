/*
ExifGlass - AI & EXIF metadata viewer
Copyright (C) 2023 - 2027 DUONG DIEU PHAP
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
namespace ExifGlass.Core.Localization;

public sealed partial class Lang
{
    // the built-in English of every LangId, in the enum's order; a key without one shows nothing
    private static readonly KeyValuePair<LangId, string>[] _defaultLangList = [

        #region General

        new(LangId._Open, "Open"),
        new(LangId._OpenSettings, "Open settings"),
        new(LangId._Retry, "Retry"),
        new(LangId._About, "About"),
        new(LangId._Settings, "Settings"),
        new(LangId._CopyCommand, "Copy command"),
        new(LangId._ExifToolUnavailable, "ExifTool isn't available"),
        new(LangId._Edit, "Edit"),
        new(LangId._Copy, "Copy"),
        new(LangId._Json, "JSON"),
        new(LangId._Clear, "Clear"),
        new(LangId._TagName, "Tag name"),
        new(LangId._Value, "Value"),
        new(LangId._CopyAsJson, "Copy as JSON"),
        new(LangId._AiGeneration, "AI generation"),
        new(LangId._ShowAll, "Show all"),
        new(LangId._AiGenerationOverline, "AI GENERATION"),
        new(LangId._Save, "Save"),
        new(LangId._Discard, "Discard"),
        new(LangId._Reload, "Reload"),
        new(LangId._Restore, "Restore"),
        new(LangId._NotNow, "Not now"),
        new(LangId._Browse, "Browse…"),
        new(LangId._Reset, "Reset"),
        new(LangId._Ok, "OK"),
        new(LangId._Cancel, "Cancel"),
        new(LangId._Back, "Back"),
        new(LangId._BackTip, "Back (Esc)"),
        new(LangId._Download, "Download"),
        new(LangId._ShowFewer, "Show fewer"),
        new(LangId._On, "On"),
        new(LangId._Off, "Off"),
        new(LangId._ClearSearch, "Clear search"),
        new(LangId._Remove, "Remove"),
        new(LangId._ShowMore, "Show more"),
        new(LangId._ShowLess, "Show less"),
        new(LangId._ScrollUp, "Scroll up"),
        new(LangId._ScrollDown, "Scroll down"),
        new(LangId._ScrollLeft, "Scroll left"),
        new(LangId._ScrollRight, "Scroll right"),
        new(LangId._CopyValue, "Copy value"),
        new(LangId._Close, "Close"),
        new(LangId._Apply, "Apply"),
        new(LangId._CancelTip, "Cancel (Esc)"),
        new(LangId._CopyJson, "Copy JSON"),
        new(LangId._TagsOne, "1 tag"),
        new(LangId._Tags, "{0:N0} tags"),
        new(LangId._TagsAndAi, "{0} and {1}"),
        new(LangId._AiFieldsOne, "1 AI field"),
        new(LangId._AiFields, "{0:N0} AI fields"),
        new(LangId._CharactersOne, "1 character"),
        new(LangId._Characters, "{0:N0} characters"),
        new(LangId._ChangesOne, "1 change"),
        new(LangId._Changes, "{0:N0} changes"),
        new(LangId._MoreCount, "{0} more"),
        new(LangId._FirstOfMore, "{0} (+{1} more)"),

        #endregion // General

        #region Toolbar

        new(LangId.Toolbar_OpenFile, "Open file…"),
        new(LangId.Toolbar_SearchTags, "Search tags"),
        new(LangId.Toolbar_Search, "Search"),
        new(LangId.Toolbar_ShowRail, "Show groups panel"),
        new(LangId.Toolbar_HideRail, "Hide groups panel"),
        new(LangId.Toolbar_ShowInspector, "Show inspector"),
        new(LangId.Toolbar_HideInspector, "Hide inspector"),
        new(LangId.Toolbar_Pin, "Keep window on top"),
        new(LangId.Toolbar_Unpin, "Unpin window"),
        new(LangId.Toolbar_OpenFileTip, "Open file ({0})"),
        new(LangId.Toolbar_SettingsTip, "Settings ({0})"),
        new(LangId.Toolbar_AboutTip, "About ({0})"),
        new(LangId.Toolbar_ThemeTip, "Theme: {0} (click for {1})"),

        #endregion // Toolbar

        #region Empty window

        new(LangId.Empty_Title, "Drop a file here to view its metadata"),
        new(LangId.Empty_Caption, "Or click the button below"),
        new(LangId.Empty_ReleaseToInspect, "Release to inspect"),
        new(LangId.Empty_ExifToolMissingCaption, "ExifGlass reads metadata with ExifTool. Choose its path in Settings, or retry."),

        #endregion // Empty window

        #region Table

        new(LangId.Table_SummaryNoFile, "No file"),
        new(LangId.Table_SummaryReading, "Reading metadata…"),
        new(LangId.Table_CollapseAll, "Collapse all"),
        new(LangId.Table_ExpandAll, "Expand all"),
        new(LangId.Table_CollapseAllTip, "Collapse all groups"),
        new(LangId.Table_ExpandAllTip, "Expand all groups"),
        new(LangId.Table_EditTip, "Edit metadata"),
        new(LangId.Table_Columns, "Columns"),
        new(LangId.Table_ColumnsTip, "Show or hide columns"),
        new(LangId.Table_ColumnsHeader, "COLUMNS"),
        new(LangId.Table_Export, "Export"),
        new(LangId.Table_Selected, "selected"),
        new(LangId.Table_ClearAll, "Clear all"),
        new(LangId.Table_RemoveFilter, "Remove filter"),
        new(LangId.Table_CopyRowsTip, "Copy as tab-separated name and value"),
        new(LangId.Table_CopyRowsJsonTip, "Copy as JSON with group-prefixed keys"),
        new(LangId.Table_ClearSelectionTip, "Clear the selection (Esc)"),
        new(LangId.Table_NoMatchTitle, "No tags match"),
        new(LangId.Table_NoMatchCaption, "Try a different search, or clear the filters."),
        new(LangId.Table_ClearFilters, "Clear filters"),
        new(LangId.Table_ColumnIndex, "Index"),
        new(LangId.Table_ColumnTagId, "Tag ID"),
        new(LangId.Table_AtLeastOne, "At least one"),
        new(LangId.Table_HiddenAtWidth, "Hidden at this width"),
        new(LangId.Table_Name, "Metadata tags"),
        new(LangId.Table_HeaderTagId, "TAG ID"),
        new(LangId.Table_HeaderTagName, "TAG NAME"),
        new(LangId.Table_HeaderValue, "VALUE"),
        new(LangId.Table_BadgeEdited, "Edited"),
        new(LangId.Table_BadgeDelete, "Delete"),
        new(LangId.Table_BadgeNew, "New"),
        new(LangId.Table_ParsedIntoTip, "Parsed into AI generation · jump to its fields"),
        new(LangId.Table_InGroupsOne, "in 1 group"),
        new(LangId.Table_InGroups, "in {0:N0} groups"),
        new(LangId.Table_InView, "{0} in view"),
        new(LangId.Table_HidingOne, "Hiding {0}"),
        new(LangId.Table_Hiding, "Hiding {0} groups"),
        new(LangId.Table_SummaryFiltered, "{0} of {1} tags"),
        new(LangId.Table_AiSummaryFiltered, "{0} of {1} AI fields"),

        #endregion // Table

        #region Quick filters

        new(LangId.Filter_DateTime, "Date & time"),
        new(LangId.Filter_Gps, "GPS"),
        new(LangId.Filter_Converted, "Converted"),
        new(LangId.Filter_HasTagId, "Has tag ID"),
        new(LangId.Filter_Prompts, "Prompts"),
        new(LangId.Filter_Models, "Models"),
        new(LangId.Filter_Provenance, "Provenance"),
        new(LangId.Filter_AiTip, "Show AI fields and the tags they were read from"),
        new(LangId.Filter_PromptsTip, "Show the prompts"),
        new(LangId.Filter_ModelsTip, "Show the models, LoRAs and embeddings"),
        new(LangId.Filter_ProvenanceTip, "Show Content Credentials, IPTC, the AI label and AI-training signals"),

        #endregion // Quick filters

        #region Export menu

        new(LangId.Export_Csv, "CSV"),
        new(LangId.Export_CsvDesc, "Spreadsheet-ready, one row per tag"),
        new(LangId.Export_Text, "Plain text"),
        new(LangId.Export_TextDesc, "Tab-separated, like exiftool -t"),
        new(LangId.Export_JsonDesc, "Compatible with exiftool -j -G"),
        new(LangId.Export_CopyDesc, "Straight to the clipboard"),
        new(LangId.Export_AiParameters, "Generation parameters"),
        new(LangId.Export_AiParametersDesc, "A1111 text, as .txt"),
        new(LangId.Export_AiJson, "AI data as JSON"),
        new(LangId.Export_AiWorkflow, "Workflow"),
        new(LangId.Export_AiWorkflowDesc, "Exactly as embedded, as .json"),
        new(LangId.Export_Header, "EXPORTS THE {0} IN VIEW"),

        #endregion // Export menu

        #region Edit menu and Add tag

        new(LangId.Edit_AddTagMenu, "Add tag…"),
        new(LangId.Edit_AddTagMenuDesc, "Write a new EXIF, XMP or IPTC tag"),
        new(LangId.Edit_RemoveGps, "Remove GPS data"),
        new(LangId.Edit_RemoveGpsNone, "No GPS data in this file"),
        new(LangId.Edit_RemoveAi, "Remove AI generation data"),
        new(LangId.Edit_RemoveAiDesc, "Prompts, workflows and parameters · provenance kept"),
        new(LangId.Edit_StripAll, "Strip all metadata"),
        new(LangId.Edit_StripAllDesc, "Removes every writable tag · -all="),
        new(LangId.Edit_Staged, "Already staged"),
        new(LangId.Edit_RemoveAiNone, "No AI generation data in this file"),
        new(LangId.Edit_RemoveGpsManifest, "Only in the Content Credentials, which ExifTool can't edit"),
        new(LangId.Edit_ManifestGpsNote, "The location in the Content Credentials stays: Strip all metadata removes it with the manifest"),
        new(LangId.Edit_AddTagOverline, "ADD TAG"),
        new(LangId.Edit_AddTag, "Add tag"),
        new(LangId.Edit_TagNamePlaceholder, "Tag name, e.g. Artist"),
        new(LangId.Edit_TagGroup, "Tag group"),
        new(LangId.Edit_GpsTagsOne, "1 location tag · {0}"),
        new(LangId.Edit_GpsTags, "{0:N0} location tags · {1}"),
        new(LangId.Edit_NotWritable, "ExifTool has no {0} tag it can write named {1}"),
        new(LangId.Edit_AddTagTip, "Add tag ({0})"),

        #endregion // Edit menu and Add tag

        #region Pending changes

        new(LangId.Pending_DiscardTip, "Drop every pending change"),
        new(LangId.Pending_Saving, "Saving…"),
        new(LangId.Pending_ChangesOne, "1 pending change"),
        new(LangId.Pending_Changes, "{0:N0} pending changes"),
        new(LangId.Pending_SaveWrites, "Save writes {0} · backup kept as {0}_original"),
        new(LangId.Pending_SaveWritesInContainer, "Save writes {0} · backup kept in ExifGlass's container"),

        #endregion // Pending changes

        #region Dialogs

        new(LangId.Dialog_UnsavedTitle, "Save changes to {0}?"),
        new(LangId.Dialog_UnsavedBody, "{0} not written yet. Discard drops them."),
        new(LangId.Dialog_ChangedTitle, "{0} changed on disk"),
        new(LangId.Dialog_ChangedBody, "Another app changed the file after ExifGlass read it. Save writes {0} into the file as it is now; Reload reads it again and drops them."),
        new(LangId.Dialog_RecoveryTitle, "Restore changes to {0}?"),
        new(LangId.Dialog_RecoveryKeptOne, "1 change wasn't saved"),
        new(LangId.Dialog_RecoveryKept, "{0:N0} changes weren't saved"),
        new(LangId.Dialog_RecoveryBody, "{0} when ImageGlass closed ExifGlass. Discard drops them."),
        new(LangId.Dialog_RecoveryBodyChanged, "{0} when ImageGlass closed ExifGlass, and the file has changed since. Discard drops them."),

        #endregion // Dialogs

        #region Groups panel

        new(LangId.Rail_Groups, "GROUPS"),
        new(LangId.Rail_QuickFilters, "QUICK FILTERS"),
        new(LangId.Rail_HideAll, "Hide all"),
        new(LangId.Rail_FileReading, "Reading…"),
        new(LangId.Rail_FileReadingSub, "ExifTool is parsing the file"),
        new(LangId.Rail_FileNone, "No file open"),
        new(LangId.Rail_FileNoneSub, "Open or drop a file"),
        new(LangId.Rail_HideGroup, "Hide {0} tags"),
        new(LangId.Rail_ShowGroup, "Show {0} tags"),
        new(LangId.Rail_JumpTo, "Jump to {0}"),

        #endregion // Groups panel

        #region Inspector

        new(LangId.Inspector_AiTraining, "AI training"),
        new(LangId.Inspector_NotAllowed, "Not allowed"),
        new(LangId.Inspector_DeleteTag, "Delete tag"),
        new(LangId.Inspector_UndoDelete, "Undo delete"),
        new(LangId.Inspector_CopyTagKey, "Copy tag key"),
        new(LangId.Inspector_CopyRawValue, "Copy raw value"),
        new(LangId.Inspector_CopyFormat, "Copy format"),
        new(LangId.Inspector_EditValue, "Edit value"),
        new(LangId.Inspector_Revert, "Revert"),
        new(LangId.Inspector_RawValue, "Raw (-n)"),
        new(LangId.Inspector_Format, "Format"),
        new(LangId.Inspector_TagKey, "Tag key"),
        new(LangId.Inspector_Was, "Was: {0}"),
        new(LangId.Inspector_PreviewOverline, "PREVIEW"),
        new(LangId.Inspector_OpenInOsm, "Open in OpenStreetMap"),
        new(LangId.Inspector_Camera, "Camera"),
        new(LangId.Inspector_Lens, "Lens"),
        new(LangId.Inspector_Captured, "Captured"),
        new(LangId.Inspector_Location, "Location"),
        new(LangId.Inspector_SaveBinary, "Save binary data…"),
        new(LangId.Inspector_SaveBinaryTip, "Save the tag's bytes as a file"),
        new(LangId.Inspector_GoToAi, "Go to AI generation"),
        new(LangId.Inspector_SameAsValue, "(same as value)"),
        new(LangId.Inspector_ApplyTip, "Apply ({0})"),
        new(LangId.Inspector_ParsedIntoOne, "Parsed into 1 AI field"),
        new(LangId.Inspector_ParsedInto, "Parsed into {0} AI fields"),
        new(LangId.Inspector_Aperture, "Aperture"),
        new(LangId.Inspector_Shutter, "Shutter"),
        new(LangId.Inspector_Iso, "ISO"),
        new(LangId.Inspector_Focal, "Focal"),
        new(LangId.Inspector_MaxAperture, "max f/{0}"),
        new(LangId.Inspector_FullFrame, "{0} full-frame eq."),
        new(LangId.Inspector_Look, "Look"),
        new(LangId.Inspector_FilmSimulation, "Film simulation"),
        new(LangId.Inspector_Altitude, "{0} altitude"),
        new(LangId.Inspector_Software, "Software"),
        new(LangId.Inspector_MonthNames, "Jan Feb Mar Apr May Jun Jul Aug Sep Oct Nov Dec"),
        new(LangId.Inspector_DateTime, "{0} {1}, {2} · {3}"),

        #endregion // Inspector

        #region AI generation

        new(LangId.Ai_JsonDesc, "Every parsed field with its source tag"),
        new(LangId.Ai_PromptOverline, "PROMPT"),
        new(LangId.Ai_NegativePromptOverline, "NEGATIVE PROMPT"),
        new(LangId.Ai_CopyParameters, "Copy generation parameters"),
        new(LangId.Ai_Prompt, "Prompt"),
        new(LangId.Ai_NegativePrompt, "Negative prompt"),
        new(LangId.Ai_RawText, "Raw text"),
        new(LangId.Ai_PromptInformationOverline, "PROMPT INFORMATION"),
        new(LangId.Ai_RawTextOverline, "RAW TEXT"),
        new(LangId.Ai_StylesOverline, "STYLES"),
        new(LangId.Ai_ParametersOverline, "PARAMETERS"),
        new(LangId.Ai_WorkflowOverline, "WORKFLOW"),
        new(LangId.Ai_CredentialsOverline, "CONTENT CREDENTIALS"),
        new(LangId.Ai_AigcOverline, "AI LABEL · GB 45438-2025"),
        new(LangId.Ai_TrainingOverline, "AI TRAINING"),
        new(LangId.Ai_CharacterCardOverline, "CHARACTER CARD"),
        new(LangId.Ai_AllParameters, "All parameters"),
        new(LangId.Ai_PassMain, "Pass 1"),
        new(LangId.Ai_From, "from"),
        new(LangId.Ai_Also, "Also: "),
        new(LangId.Ai_ShowSourceTag, "Show source tag"),
        new(LangId.Ai_ShowSourceTags, "Show source tags"),
        new(LangId.Ai_JumpTo, "Jump to AI generation"),
        new(LangId.Ai_CopyJson, "Copy AI data as JSON"),
        new(LangId.Ai_CopyParametersDesc, "A1111 text · pastes into \"Read generation parameters\""),
        new(LangId.Ai_CopyWorkflowJson, "Copy workflow JSON"),
        new(LangId.Ai_SaveWorkflow, "Save workflow…"),
        new(LangId.Ai_MenuHeader, "COPY OR SAVE THE AI DATA"),
        new(LangId.Ai_MenuTip, "More ways to copy or save"),
        new(LangId.Ai_CopyPrompt, "Copy prompt"),
        new(LangId.Ai_CopyNegative, "Copy negative prompt"),
        new(LangId.Ai_CopyRaw, "Copy raw text"),
        new(LangId.Ai_CopyCharacter, "Copy character prompt"),
        new(LangId.Ai_CopyHash, "Copy hash"),
        new(LangId.Ai_NotVerified, "Not verified"),
        new(LangId.Ai_NotVerifiedTip, "Not verified: ExifGlass shows what the file claims. It doesn't check the signature, so it can't say who signed it or whether the file changed since."),
        new(LangId.Ai_SignerNotShown, "Signer not shown"),
        new(LangId.Ai_ProvenanceOnly, "This file has no prompt or generation settings, only provenance."),
        new(LangId.Ai_UnknownNote, "ExifGlass found AI signals but doesn't know this format yet. The text is shown as it is."),
        new(LangId.Ai_CharacterNote, "Character data for chat apps, not image generation. It isn't counted as AI generation."),
        new(LangId.Ai_Loading, "Reading the AI data. The table is ready; AI fields appear here when parsing finishes."),
        new(LangId.Ai_UseBundledExifTool, "Use the bundled ExifTool"),
        new(LangId.Ai_RegionalPrompts, "regional prompts"),
        new(LangId.Ai_Centre, "Centre"),
        new(LangId.Ai_ClaimedBy, "Claimed by"),
        new(LangId.Ai_Actions, "Actions"),
        new(LangId.Ai_SourceType, "Source type"),
        new(LangId.Ai_Manifests, "Manifests"),
        new(LangId.Ai_RemoteManifest, "Remote manifest"),
        new(LangId.Ai_Ingredient, "Ingredient"),
        new(LangId.Ai_IngredientSuffix, "· ingredient"),
        new(LangId.Ai_NoOwnCredentials, "No Content Credentials of its own"),
        new(LangId.Ai_System, "AI system"),
        new(LangId.Ai_PromptWriter, "Prompt writer"),
        new(LangId.Ai_Credit, "Credit"),
        new(LangId.Ai_AigcLabel, "Label"),
        new(LangId.Ai_AigcProducer, "Producer"),
        new(LangId.Ai_AigcProduceId, "Produce ID"),
        new(LangId.Ai_AigcPropagator, "Propagator"),
        new(LangId.Ai_AigcPropagateId, "Propagate ID"),
        new(LangId.Ai_DataMining, "Data mining"),
        new(LangId.Ai_LearningOptOut, "Opt-out"),
        new(LangId.Ai_SourceTag, "Source tag"),
        new(LangId.Ai_ParsedAs, "Parsed as"),
        new(LangId.Ai_Length, "Length"),
        new(LangId.Ai_Creator, "Creator"),
        new(LangId.Ai_Version, "Version"),
        new(LangId.Ai_CopyCardJson, "Copy card JSON"),
        new(LangId.Ai_SaveCardJson, "Save card JSON…"),
        new(LangId.Ai_Character, "Character"),
        new(LangId.Ai_NodeOne, "node"),
        new(LangId.Ai_Nodes, "nodes"),
        new(LangId.Ai_SamplerOne, "sampler"),
        new(LangId.Ai_Samplers, "samplers"),
        new(LangId.Ai_ModelOne, "model"),
        new(LangId.Ai_Models, "models"),
        new(LangId.Ai_MoreCharacters, "{0} more, Copy copies them all"),
        new(LangId.Ai_CharactersTitle, "CHARACTERS · {0}"),
        new(LangId.Ai_ModelsTitle, "MODELS · {0}"),
        new(LangId.Ai_SamplersCount, "{0} samplers"),
        new(LangId.Ai_ManifestCountOne, "1 · this one"),
        new(LangId.Ai_ManifestCount, "{0} · the active one is the last"),
        new(LangId.Ai_AigcCode, "code {0}"),
        new(LangId.Ai_StylesNegative, "No negative prompt. The styles add: {0}"),
        new(LangId.Ai_SaveWorkflowTip, "A .json file that loads back into {0}"),
        new(LangId.Ai_CopyWorkflowTip, "Exactly as embedded · {0}"),
        new(LangId.Ai_CopiedHash, "Hash of {0}"),

        #endregion // AI generation

        #region Command bar

        new(LangId.CommandBar_Write, "Write"),
        new(LangId.CommandBar_WriteTip, "Save runs this command"),
        new(LangId.CommandBar_Expand, "Show the whole command"),
        new(LangId.CommandBar_Collapse, "Show one line"),

        #endregion // Command bar

        #region Toasts

        new(LangId.Toast_Pinned, "Pinned on top"),
        new(LangId.Toast_PinnedSub, "ExifGlass stays above other windows"),
        new(LangId.Toast_Unpinned, "Unpinned"),
        new(LangId.Toast_UnpinnedSub, "Window behaves normally"),
        new(LangId.Toast_ReadFailed, "Couldn't read that file"),
        new(LangId.Toast_Folder, "Folders can't be opened"),
        new(LangId.Toast_FolderSub, "Drop a file instead"),
        new(LangId.Toast_CommandCopied, "Command copied"),
        new(LangId.Toast_CommandCopiedSub, "Paste it into any terminal"),
        new(LangId.Toast_CopiedValue, "Copied value"),
        new(LangId.Toast_CopiedCell, "Copied cell"),
        new(LangId.Toast_CopiedTsvSub, "Tab-separated name and value"),
        new(LangId.Toast_CopiedJsonSub, "Group-prefixed keys"),
        new(LangId.Toast_CopiedAsJson, "Copied as JSON"),
        new(LangId.Toast_ExportFailed, "Export failed"),
        new(LangId.Toast_CopiedKey, "Copied tag key"),
        new(LangId.Toast_CopiedRaw, "Copied raw value"),
        new(LangId.Toast_CopiedFormat, "Copied format"),
        new(LangId.Toast_EditStaged, "Edit staged"),
        new(LangId.Toast_NoChange, "No change"),
        new(LangId.Toast_CopiedPrompt, "Copied prompt"),
        new(LangId.Toast_CopiedNegative, "Copied negative prompt"),
        new(LangId.Toast_CopiedParameters, "Copied generation parameters"),
        new(LangId.Toast_CopiedAiJson, "Copied AI data as JSON"),
        new(LangId.Toast_CopiedWorkflow, "Copied workflow JSON"),
        new(LangId.Toast_CopiedCharacter, "Copied character card"),
        new(LangId.Toast_SavedWorkflow, "Saved workflow"),
        new(LangId.Toast_SavedParameters, "Saved generation parameters"),
        new(LangId.Toast_SavedAiJson, "Saved AI data"),
        new(LangId.Toast_SavedCharacter, "Saved character card"),
        new(LangId.Toast_TagStaged, "Tag staged"),
        new(LangId.Toast_NothingSaved, "Nothing to change"),
        new(LangId.Toast_NothingSavedSub, "The file already had these values"),
        new(LangId.Toast_SavedBinary, "Saved binary data"),
        new(LangId.Toast_BinaryFailed, "Couldn't read the binary data"),
        new(LangId.Toast_RestoredSub, "Nothing is written until you save"),
        new(LangId.Toast_RecoveryGone, "Couldn't restore unsaved changes"),
        new(LangId.Toast_SettingsSaved, "Settings saved"),
        new(LangId.Toast_VersionCopied, "Version info copied"),
        new(LangId.Toast_UpdateSub, "Download it from About"),
        new(LangId.Toast_BackupKeptInContainer, "Backup kept in ExifGlass's container"),
        new(LangId.Toast_OpenedOneOf, "Opened 1 of {0:N0} files"),
        new(LangId.Toast_CopiedTags, "Copied {0}"),
        new(LangId.Toast_CopiedRowsAsJson, "Copied {0:N0} as JSON"),
        new(LangId.Toast_CopiedField, "Copied {0}"),
        new(LangId.Toast_Exported, "Exported {0}"),
        new(LangId.Toast_ExportedSub, "{0} · {1:0.0} kB"),
        new(LangId.Toast_SavedOne, "Saved 1 change"),
        new(LangId.Toast_Saved, "Saved {0:N0} changes"),
        new(LangId.Toast_BackupKept, "Backup kept as {0}_original"),
        new(LangId.Toast_SaveFailed, "Couldn't save {0}"),
        new(LangId.Toast_SavedBinarySub, "{0} · {1:0.0} kB"),
        new(LangId.Toast_RecoveryGoneSub, "{0} was moved or deleted"),
        new(LangId.Toast_RestoredOne, "Restored 1 change"),
        new(LangId.Toast_Restored, "Restored {0:N0} changes"),
        new(LangId.Toast_RestoreDroppedOne, "1 change no longer applies to the file"),
        new(LangId.Toast_RestoreDropped, "{0:N0} changes no longer apply to the file"),
        new(LangId.Toast_A1111Text, "{0} · A1111 text"),
        new(LangId.Toast_SettingsSavedArgumentsOne, "1 argument"),
        new(LangId.Toast_SettingsSavedArguments, "{0:N0} arguments"),
        new(LangId.Toast_SettingsSavedCustom, "{0} · custom ExifTool"),
        new(LangId.Toast_SettingsSavedBundled, "{0} · bundled ExifTool"),
        new(LangId.Toast_UpdateAvailable, "{0} {1} is available"),
        new(LangId.Toast_LanguageInstalledOne, "Installed 1 language pack"),
        new(LangId.Toast_LanguageInstalled, "Installed {0:N0} language packs"),
        new(LangId.Toast_LanguageIncompatible, "Made for another version of ExifGlass"),

        #endregion // Toasts

        #region File pickers

        new(LangId.Picker_OpenTitle, "Open a file"),
        new(LangId.Picker_Images, "Images"),
        new(LangId.Picker_AllFiles, "All files"),
        new(LangId.Picker_BinaryData, "Binary data"),
        new(LangId.Picker_ExifToolTitle, "Choose ExifTool"),
        new(LangId.Picker_Programs, "Programs"),
        new(LangId.Picker_LanguagePack, "ExifGlass language pack"),

        #endregion // File pickers

        #region Settings

        new(LangId.Settings_General, "GENERAL"),
        new(LangId.Settings_ExifTool, "EXIFTOOL"),
        new(LangId.Settings_CommandPreview, "COMMAND PREVIEW"),
        new(LangId.Settings_AppTheme, "App theme"),
        new(LangId.Settings_ThemeSystem, "System"),
        new(LangId.Settings_ThemeLight, "Light"),
        new(LangId.Settings_ThemeDark, "Dark"),
        new(LangId.Settings_AppThemeDesc, "System follows your OS light or dark setting."),
        new(LangId.Settings_AccentColor, "Accent color"),
        new(LangId.Settings_AccentColorDesc, "System follows your OS accent color."),
        new(LangId.Settings_AccentSystem, "System accent color"),
        new(LangId.Settings_AccentBlue, "Blue"),
        new(LangId.Settings_AccentPurple, "Purple"),
        new(LangId.Settings_AccentPink, "Pink"),
        new(LangId.Settings_AccentOrange, "Orange"),
        new(LangId.Settings_AccentGreen, "Green"),
        new(LangId.Settings_AccentYellow, "Yellow"),
        new(LangId.Settings_AccentGraphite, "Graphite"),
        new(LangId.Settings_AlwaysOnTop, "Keep window always on top"),
        new(LangId.Settings_AlwaysOnTopDesc, "Float above other apps while you compare files."),
        new(LangId.Settings_CheckUpdates, "Check for updates on startup"),
        new(LangId.Settings_CheckUpdatesDesc, "A quiet check against GitHub releases."),
        new(LangId.Settings_ExecutablePath, "Executable path"),
        new(LangId.Settings_BrowseTip, "Choose the exiftool to run"),
        new(LangId.Settings_Arguments, "Arguments"),
        new(LangId.Settings_ArgumentsKeys, "Enter to add · Backspace to remove"),
        new(LangId.Settings_ArgumentsHint, "Hover an argument to see what it does."),
        new(LangId.Settings_ResetTip, "Back to the default settings"),
        new(LangId.Settings_BundledExifTool, "Bundled ExifTool"),
        new(LangId.Settings_PathNotFound, "No file at this path. ExifGlass uses its bundled ExifTool instead."),
        new(LangId.Settings_NoExifTool, "No ExifTool was found. Choose the exiftool to run."),
        new(LangId.Settings_UseBundled, "Clear the path to use the bundled ExifTool."),
        new(LangId.Settings_SandboxNote, "This version runs its bundled ExifTool and keeps the _original backups in its own container."),
        new(LangId.Settings_AddArgument, "Add an argument"),
        new(LangId.Settings_OldExifTool, "Your ExifTool {0} can't read every AI field."),
        new(LangId.Settings_OldExifToolNeeds, "{0} need {1:0.00}."),
        new(LangId.Settings_ExifToolOnPath, "On PATH · exiftool {0}"),
        new(LangId.Settings_ExifToolBundled, "Bundled · exiftool {0}"),
        new(LangId.Settings_Language, "Language"),
        new(LangId.Settings_LanguageContributors, "Contributors: {0}"),
        new(LangId.Settings_LanguageMore, "More language options"),
        new(LangId.Settings_LanguageInstall, "Install language pack…"),
        new(LangId.Settings_LanguageExport, "Export language pack…"),

        #endregion // Settings

        #region About

        new(LangId.About_Notices, "THIRD-PARTY NOTICES"),
        new(LangId.About_CopyVersionInfo, "Copy version info"),
        new(LangId.About_Updates, "Updates"),
        new(LangId.About_NewBadge, "NEW"),
        new(LangId.About_CheckNow, "Check now"),
        new(LangId.About_NotChecked, "Not checked yet"),
        new(LangId.About_Checking, "Checking for updates…"),
        new(LangId.About_UpdateError, "Couldn't check. Check your connection."),
        new(LangId.About_Website, "Website"),
        new(LangId.About_Visit, "Visit"),
        new(LangId.About_StoreSub, "Support development · automatic updates"),
        new(LangId.About_StoreGet, "Get ExifGlass"),
        new(LangId.About_VersionLine, "Version {0} ({1}) · {2} · {3}"),
        new(LangId.About_UpToDate, "You're up to date · checked at {0}"),
        new(LangId.About_UpdateReady, "Version {0} is available"),
        new(LangId.About_UpdateReadyDate, "Version {0} is available ({1})"),

        #endregion // About

        #region Store badge

        new(LangId.Badge_StoreCaption, "Get it from"),
        new(LangId.Badge_ReleasesCaption, "Download from"),

        #endregion // Store badge

        #region Settings argument field

        new(LangId.Argument_Group, "Print the group name for each tag"),
        new(LangId.Argument_Tab, "Tab-delimited output"),
        new(LangId.Argument_Hex, "Show tag IDs in hexadecimal"),
        new(LangId.Argument_Json, "JSON output"),
        new(LangId.Argument_Long, "Long output: descriptions and raw values"),
        new(LangId.Argument_Raw, "Raw numerical values, no print conversion"),
        new(LangId.Argument_Short, "Short tag names instead of descriptions"),
        new(LangId.Argument_Duplicates, "Allow duplicate tags"),
        new(LangId.Argument_Fast, "Skip scanning for trailers, for faster reads"),
        new(LangId.Argument_Fast2, "Also skip maker notes and late chunks"),
        new(LangId.Argument_Fast3, "Read only the file system and format tags"),
        new(LangId.Argument_MinorErrors, "Ignore minor errors and warnings"),
        new(LangId.Argument_Quiet, "Quiet: suppress informational messages"),
        new(LangId.Argument_Unknown, "Extract unknown tags"),
        new(LangId.Argument_UnknownBinary, "Extract unknown binary tags too"),
        new(LangId.Argument_Embedded, "Extract embedded data (e.g. video streams)"),
        new(LangId.Argument_Embedded2, "Extract embedded data, level 2"),
        new(LangId.Argument_Embedded3, "Extract embedded data, level 3"),
        new(LangId.Argument_NoComposite, "Don't generate composite tags"),
        new(LangId.Argument_OnlyComposite, "Only generate composite tags"),
        new(LangId.Argument_Latin1, "Use Windows Latin1 encoding"),
        new(LangId.Argument_Struct, "Keep structures as JSON objects"),
        new(LangId.Argument_Flatten, "Flatten structures"),
        new(LangId.Argument_Api, "Set an ExifTool API option"),
        new(LangId.Argument_Charset, "Character set for output"),
        new(LangId.Argument_Lang, "Language of tag descriptions and values"),
        new(LangId.Argument_DateFormat, "Date and time format"),
        new(LangId.Argument_GpsFormat, "GPS coordinate format"),
        new(LangId.Argument_Separator, "Separator for list items"),
        new(LangId.Argument_ListItem, "Show one item of each list"),
        new(LangId.Argument_Exclude, "Exclude a tag"),
        new(LangId.Argument_Extension, "Process files with this extension"),
        new(LangId.Argument_HintRaw, "raw values"),
        new(LangId.Argument_HintDuplicates, "duplicates"),
        new(LangId.Argument_HintUnknown, "unknown tags"),
        new(LangId.Argument_HintUnknownBinary, "unknown binary"),
        new(LangId.Argument_HintEmbedded, "embedded"),
        new(LangId.Argument_HintAllEmbedded, "all embedded"),
        new(LangId.Argument_HintShort, "short names"),
        new(LangId.Argument_HintNoComposite, "no composite"),
        new(LangId.Argument_HintFaster, "faster"),
        new(LangId.Argument_HintFastest, "fastest"),
        new(LangId.Argument_HintMinorErrors, "minor errors"),
        new(LangId.Argument_HintQuiet, "quiet"),
        new(LangId.Argument_ReasonOutput, "Changes the output format the app reads"),
        new(LangId.Argument_ReasonWrite, "Writes or changes files"),
        new(LangId.Argument_ReasonProtocol, "Belongs to the app's own ExifTool session"),
        new(LangId.Argument_ReasonCode, "Runs Perl code"),
        new(LangId.Argument_ReasonUnknown, "Not supported in the argument field"),
        new(LangId.Argument_ReasonFileName, "A file name: the app passes the file itself"),
        new(LangId.Argument_NeedsValue, "Needs a value"),
        new(LangId.Argument_ValueLines, "A value can't span lines"),
        new(LangId.Argument_ApiNotAllowed, "The API option {0} isn't allowed here"),
        new(LangId.Argument_FileNameCharset, "The app sets the file-name character set itself"),
        new(LangId.Argument_FastHidesAi, "{0} can hide AI data in the table: late PNG text and MP4 keys aren't read (the AI card still shows it)"),
        new(LangId.Argument_ExcludeTags, "Exclude tags"),
        new(LangId.Argument_ReadTags, "Read only these tags"),
        new(LangId.Argument_ValueFor, "Value for {0}"),

        #endregion // Settings argument field

        #region Group descriptions

        new(LangId.Group_Ai, "Parsed by ExifGlass"),
        new(LangId.Group_ExifTool, "Reader"),
        new(LangId.Group_File, "File system"),
        new(LangId.Group_Exif, "Camera & capture"),
        new(LangId.Group_MakerNotes, "Vendor-specific"),
        new(LangId.Group_Xmp, "Adobe XMP"),
        new(LangId.Group_IccProfile, "Color profile"),
        new(LangId.Group_Composite, "Derived by ExifTool"),
        new(LangId.Group_Png, "PNG chunks"),
        new(LangId.Group_Iptc, "Press metadata"),
        new(LangId.Group_Jfif, "JPEG header"),
        new(LangId.Group_Photoshop, "Photoshop resources"),
        new(LangId.Group_QuickTime, "Video container"),
        new(LangId.Group_Jumbf, "C2PA Content Credentials"),
        new(LangId.Group_Mpf, "Multi-picture format"),
        new(LangId.Group_Riff, "WebP and AVI container"),
        new(LangId.Group_MakerNotesOf, "{0} maker notes"),

        #endregion // Group descriptions

        #region Reading files

        new(LangId.Read_FileNotOpened, "Another app is using it, or it can't be read"),

        #endregion // Reading files

        #region Saving files

        new(LangId.Save_ExifToolStopped, "ExifTool stopped with status {0}"),
        new(LangId.Save_NotUpdated, "ExifTool didn't update the file"),

        #endregion // Saving files

        #region macOS menu bar

        new(LangId.MacMenu_File, "File"),
        new(LangId.MacMenu_Edit, "Edit"),
        new(LangId.MacMenu_View, "View"),
        new(LangId.MacMenu_Window, "Window"),
        new(LangId.MacMenu_Settings, "Settings…"),
        new(LangId.MacMenu_Quit, "Quit ExifGlass"),
        new(LangId.MacMenu_Open, "Open…"),
        new(LangId.MacMenu_CloseWindow, "Close Window"),
        new(LangId.MacMenu_SelectAll, "Select All"),
        new(LangId.MacMenu_Find, "Find"),
        new(LangId.MacMenu_Groups, "Groups Panel"),
        new(LangId.MacMenu_Inspector, "Inspector"),
        new(LangId.MacMenu_CommandBar, "Command Bar"),
        new(LangId.MacMenu_Minimize, "Minimize"),
        new(LangId.MacMenu_Zoom, "Zoom"),

        #endregion // macOS menu bar
    ];
}
