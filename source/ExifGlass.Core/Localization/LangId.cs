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

/// <summary>
/// The key of every string the app shows; the member name is the key in <c>*.eglang.json</c>, so renaming one drops it from every pack.
/// </summary>
public enum LangId
{
    #region General

    _Open,
    _OpenSettings,
    _Retry,
    _About,
    _Settings,
    _CopyCommand,
    _ExifToolUnavailable,
    _Edit,
    _Copy,
    _Json,
    _Clear,
    _TagName,
    _Value,
    _CopyAsJson,
    _AiGeneration,
    _ShowAll,
    _AiGenerationOverline,
    _Save,
    _Discard,
    _Reload,
    _Restore,
    _NotNow,
    _Browse,
    _Reset,
    _Ok,
    _Cancel,
    _Back,
    _BackTip,
    _Download,
    _ShowFewer,
    _On,
    _Off,
    _ClearSearch,
    _Remove,
    _ShowMore,
    _ShowLess,
    _ScrollUp,
    _ScrollDown,
    _ScrollLeft,
    _ScrollRight,
    _CopyValue,
    _Close,
    _Apply,
    _CancelTip,
    _CopyJson,
    _TagsOne,
    _Tags,
    _TagsAndAi,
    _AiFieldsOne,
    _AiFields,
    _CharactersOne,
    _Characters,
    _ChangesOne,
    _Changes,
    _MoreCount,
    _FirstOfMore,

    #endregion // General

    #region Toolbar

    Toolbar_OpenFile,
    Toolbar_SearchTags,
    Toolbar_Search,
    Toolbar_ShowRail,
    Toolbar_HideRail,
    Toolbar_ShowInspector,
    Toolbar_HideInspector,
    Toolbar_Pin,
    Toolbar_Unpin,
    Toolbar_OpenFileTip,
    Toolbar_SettingsTip,
    Toolbar_AboutTip,
    Toolbar_ThemeTip,

    #endregion // Toolbar

    #region Empty window

    Empty_Title,
    Empty_Caption,
    Empty_ReleaseToInspect,
    Empty_ExifToolMissingCaption,

    #endregion // Empty window

    #region Table

    Table_SummaryNoFile,
    Table_SummaryReading,
    Table_CollapseAll,
    Table_ExpandAll,
    Table_CollapseAllTip,
    Table_ExpandAllTip,
    Table_EditTip,
    Table_Columns,
    Table_ColumnsTip,
    Table_ColumnsHeader,
    Table_Export,
    Table_Selected,
    Table_ClearAll,
    Table_RemoveFilter,
    Table_CopyRowsTip,
    Table_CopyRowsJsonTip,
    Table_ClearSelectionTip,
    Table_NoMatchTitle,
    Table_NoMatchCaption,
    Table_ClearFilters,
    Table_ColumnIndex,
    Table_ColumnTagId,
    Table_AtLeastOne,
    Table_HiddenAtWidth,
    Table_Name,
    Table_HeaderTagId,
    Table_HeaderTagName,
    Table_HeaderValue,
    Table_BadgeEdited,
    Table_BadgeDelete,
    Table_BadgeNew,
    Table_ParsedIntoTip,
    Table_InGroupsOne,
    Table_InGroups,
    Table_InView,
    Table_HidingOne,
    Table_Hiding,
    Table_SummaryFiltered,
    Table_AiSummaryFiltered,

    #endregion // Table

    #region Quick filters

    Filter_DateTime,
    Filter_Gps,
    Filter_Converted,
    Filter_HasTagId,
    Filter_Prompts,
    Filter_Models,
    Filter_Provenance,
    Filter_AiTip,
    Filter_PromptsTip,
    Filter_ModelsTip,
    Filter_ProvenanceTip,

    #endregion // Quick filters

    #region Export menu

    Export_Csv,
    Export_CsvDesc,
    Export_Text,
    Export_TextDesc,
    Export_JsonDesc,
    Export_CopyDesc,
    Export_AiParameters,
    Export_AiParametersDesc,
    Export_AiJson,
    Export_AiWorkflow,
    Export_AiWorkflowDesc,
    Export_Header,

    #endregion // Export menu

    #region Edit menu and Add tag

    Edit_AddTagMenu,
    Edit_AddTagMenuDesc,
    Edit_RemoveGps,
    Edit_RemoveGpsNone,
    Edit_RemoveAi,
    Edit_RemoveAiDesc,
    Edit_StripAll,
    Edit_StripAllDesc,
    Edit_Staged,
    Edit_RemoveAiNone,
    Edit_RemoveGpsManifest,
    Edit_ManifestGpsNote,
    Edit_AddTagOverline,
    Edit_AddTag,
    Edit_TagNamePlaceholder,
    Edit_TagGroup,
    Edit_GpsTagsOne,
    Edit_GpsTags,
    Edit_NotWritable,
    Edit_AddTagTip,

    #endregion // Edit menu and Add tag

    #region Pending changes

    Pending_DiscardTip,
    Pending_Saving,
    Pending_ChangesOne,
    Pending_Changes,
    Pending_SaveWrites,
    Pending_SaveWritesInContainer,

    #endregion // Pending changes

    #region Dialogs

    Dialog_UnsavedTitle,
    Dialog_UnsavedBody,
    Dialog_ChangedTitle,
    Dialog_ChangedBody,
    Dialog_RecoveryTitle,
    Dialog_RecoveryKeptOne,
    Dialog_RecoveryKept,
    Dialog_RecoveryBody,
    Dialog_RecoveryBodyChanged,

    #endregion // Dialogs

    #region Groups panel

    Rail_Groups,
    Rail_QuickFilters,
    Rail_HideAll,
    Rail_FileReading,
    Rail_FileReadingSub,
    Rail_FileNone,
    Rail_FileNoneSub,
    Rail_HideGroup,
    Rail_ShowGroup,
    Rail_JumpTo,

    #endregion // Groups panel

    #region Inspector

    Inspector_AiTraining,
    Inspector_NotAllowed,
    Inspector_DeleteTag,
    Inspector_UndoDelete,
    Inspector_CopyTagKey,
    Inspector_CopyRawValue,
    Inspector_CopyFormat,
    Inspector_EditValue,
    Inspector_Revert,
    Inspector_RawValue,
    Inspector_Format,
    Inspector_TagKey,
    Inspector_Was,
    Inspector_PreviewOverline,
    Inspector_OpenInOsm,
    Inspector_Camera,
    Inspector_Lens,
    Inspector_Captured,
    Inspector_Location,
    Inspector_SaveBinary,
    Inspector_SaveBinaryTip,
    Inspector_GoToAi,
    Inspector_SameAsValue,
    Inspector_ApplyTip,
    Inspector_ParsedIntoOne,
    Inspector_ParsedInto,
    Inspector_Aperture,
    Inspector_Shutter,
    Inspector_Iso,
    Inspector_Focal,
    Inspector_MaxAperture,
    Inspector_FullFrame,
    Inspector_Look,
    Inspector_FilmSimulation,
    Inspector_Altitude,
    Inspector_Software,
    Inspector_MonthNames,
    Inspector_DateTime,

    #endregion // Inspector

    #region AI generation

    Ai_JsonDesc,
    Ai_PromptOverline,
    Ai_NegativePromptOverline,
    Ai_CopyParameters,
    Ai_Prompt,
    Ai_NegativePrompt,
    Ai_RawText,
    Ai_PromptInformationOverline,
    Ai_RawTextOverline,
    Ai_StylesOverline,
    Ai_ParametersOverline,
    Ai_WorkflowOverline,
    Ai_CredentialsOverline,
    Ai_AigcOverline,
    Ai_TrainingOverline,
    Ai_CharacterCardOverline,
    Ai_AllParameters,
    Ai_PassMain,
    Ai_From,
    Ai_Also,
    Ai_ShowSourceTag,
    Ai_ShowSourceTags,
    Ai_JumpTo,
    Ai_CopyJson,
    Ai_CopyParametersDesc,
    Ai_CopyWorkflowJson,
    Ai_SaveWorkflow,
    Ai_MenuHeader,
    Ai_MenuTip,
    Ai_CopyPrompt,
    Ai_CopyNegative,
    Ai_CopyRaw,
    Ai_CopyCharacter,
    Ai_CopyHash,
    Ai_NotVerified,
    Ai_NotVerifiedTip,
    Ai_SignerNotShown,
    Ai_ProvenanceOnly,
    Ai_UnknownNote,
    Ai_CharacterNote,
    Ai_Loading,
    Ai_UseBundledExifTool,
    Ai_RegionalPrompts,
    Ai_Centre,
    Ai_ClaimedBy,
    Ai_Actions,
    Ai_SourceType,
    Ai_Manifests,
    Ai_RemoteManifest,
    Ai_Ingredient,
    Ai_IngredientSuffix,
    Ai_NoOwnCredentials,
    Ai_System,
    Ai_PromptWriter,
    Ai_Credit,
    Ai_AigcLabel,
    Ai_AigcProducer,
    Ai_AigcProduceId,
    Ai_AigcPropagator,
    Ai_AigcPropagateId,
    Ai_DataMining,
    Ai_LearningOptOut,
    Ai_SourceTag,
    Ai_ParsedAs,
    Ai_Length,
    Ai_Creator,
    Ai_Version,
    Ai_CopyCardJson,
    Ai_SaveCardJson,
    Ai_Character,
    Ai_NodeOne,
    Ai_Nodes,
    Ai_SamplerOne,
    Ai_Samplers,
    Ai_ModelOne,
    Ai_Models,
    Ai_MoreCharacters,
    Ai_CharactersTitle,
    Ai_ModelsTitle,
    Ai_SamplersCount,
    Ai_ManifestCountOne,
    Ai_ManifestCount,
    Ai_AigcCode,
    Ai_StylesNegative,
    Ai_SaveWorkflowTip,
    Ai_CopyWorkflowTip,
    Ai_CopiedHash,

    #endregion // AI generation

    #region Command bar

    CommandBar_Write,
    CommandBar_WriteTip,
    CommandBar_Expand,
    CommandBar_Collapse,

    #endregion // Command bar

    #region Toasts

    Toast_Pinned,
    Toast_PinnedSub,
    Toast_Unpinned,
    Toast_UnpinnedSub,
    Toast_ReadFailed,
    Toast_Folder,
    Toast_FolderSub,
    Toast_CommandCopied,
    Toast_CommandCopiedSub,
    Toast_CopiedValue,
    Toast_CopiedCell,
    Toast_CopiedTsvSub,
    Toast_CopiedJsonSub,
    Toast_CopiedAsJson,
    Toast_ExportFailed,
    Toast_CopiedKey,
    Toast_CopiedRaw,
    Toast_CopiedFormat,
    Toast_EditStaged,
    Toast_NoChange,
    Toast_CopiedPrompt,
    Toast_CopiedNegative,
    Toast_CopiedParameters,
    Toast_CopiedAiJson,
    Toast_CopiedWorkflow,
    Toast_CopiedCharacter,
    Toast_SavedWorkflow,
    Toast_SavedParameters,
    Toast_SavedAiJson,
    Toast_SavedCharacter,
    Toast_TagStaged,
    Toast_NothingSaved,
    Toast_NothingSavedSub,
    Toast_SavedBinary,
    Toast_BinaryFailed,
    Toast_RestoredSub,
    Toast_RecoveryGone,
    Toast_SettingsSaved,
    Toast_VersionCopied,
    Toast_UpdateSub,
    Toast_BackupKeptInContainer,
    Toast_OpenedOneOf,
    Toast_CopiedTags,
    Toast_CopiedRowsAsJson,
    Toast_CopiedField,
    Toast_Exported,
    Toast_ExportedSub,
    Toast_SavedOne,
    Toast_Saved,
    Toast_BackupKept,
    Toast_SaveFailed,
    Toast_SavedBinarySub,
    Toast_RecoveryGoneSub,
    Toast_RestoredOne,
    Toast_Restored,
    Toast_RestoreDroppedOne,
    Toast_RestoreDropped,
    Toast_A1111Text,
    Toast_SettingsSavedArgumentsOne,
    Toast_SettingsSavedArguments,
    Toast_SettingsSavedCustom,
    Toast_SettingsSavedBundled,
    Toast_UpdateAvailable,
    Toast_LanguageInstalledOne,
    Toast_LanguageInstalled,
    Toast_LanguageIncompatible,
    Toast_LanguageInstallFailed,

    #endregion // Toasts

    #region File pickers

    Picker_OpenTitle,
    Picker_Images,
    Picker_AllFiles,
    Picker_BinaryData,
    Picker_ExifToolTitle,
    Picker_Programs,
    Picker_LanguagePack,

    #endregion // File pickers

    #region Settings

    Settings_General,
    Settings_ExifTool,
    Settings_CommandPreview,
    Settings_AppTheme,
    Settings_ThemeSystem,
    Settings_ThemeLight,
    Settings_ThemeDark,
    Settings_AppThemeDesc,
    Settings_AccentColor,
    Settings_AccentColorDesc,
    Settings_AccentSystem,
    Settings_AccentBlue,
    Settings_AccentPurple,
    Settings_AccentPink,
    Settings_AccentOrange,
    Settings_AccentGreen,
    Settings_AccentYellow,
    Settings_AccentGraphite,
    Settings_AlwaysOnTop,
    Settings_AlwaysOnTopDesc,
    Settings_CheckUpdates,
    Settings_CheckUpdatesDesc,
    Settings_ExecutablePath,
    Settings_BrowseTip,
    Settings_Arguments,
    Settings_ArgumentsKeys,
    Settings_ArgumentsHint,
    Settings_ResetTip,
    Settings_BundledExifTool,
    Settings_PathNotFound,
    Settings_NoExifTool,
    Settings_UseBundled,
    Settings_SandboxNote,
    Settings_AddArgument,
    Settings_OldExifTool,
    Settings_OldExifToolNeeds,
    Settings_ExifToolOnPath,
    Settings_ExifToolBundled,
    Settings_Language,
    Settings_LanguageContributors,
    Settings_LanguageMore,
    Settings_LanguageInstall,
    Settings_LanguageExport,

    #endregion // Settings

    #region About

    About_Notices,
    About_CopyVersionInfo,
    About_Updates,
    About_NewBadge,
    About_CheckNow,
    About_NotChecked,
    About_Checking,
    About_UpdateError,
    About_Website,
    About_Visit,
    About_StoreSub,
    About_StoreGet,
    About_VersionLine,
    About_UpToDate,
    About_UpdateReady,
    About_UpdateReadyDate,

    #endregion // About

    #region Store badge

    Badge_StoreCaption,
    Badge_ReleasesCaption,

    #endregion // Store badge

    #region Settings argument field

    Argument_Group,
    Argument_Tab,
    Argument_Hex,
    Argument_Json,
    Argument_Long,
    Argument_Raw,
    Argument_Short,
    Argument_Duplicates,
    Argument_Fast,
    Argument_Fast2,
    Argument_Fast3,
    Argument_MinorErrors,
    Argument_Quiet,
    Argument_Unknown,
    Argument_UnknownBinary,
    Argument_Embedded,
    Argument_Embedded2,
    Argument_Embedded3,
    Argument_NoComposite,
    Argument_OnlyComposite,
    Argument_Latin1,
    Argument_Struct,
    Argument_Flatten,
    Argument_Api,
    Argument_Charset,
    Argument_Lang,
    Argument_DateFormat,
    Argument_GpsFormat,
    Argument_Separator,
    Argument_ListItem,
    Argument_Exclude,
    Argument_Extension,
    Argument_HintRaw,
    Argument_HintDuplicates,
    Argument_HintUnknown,
    Argument_HintUnknownBinary,
    Argument_HintEmbedded,
    Argument_HintAllEmbedded,
    Argument_HintShort,
    Argument_HintNoComposite,
    Argument_HintFaster,
    Argument_HintFastest,
    Argument_HintMinorErrors,
    Argument_HintQuiet,
    Argument_ReasonOutput,
    Argument_ReasonWrite,
    Argument_ReasonProtocol,
    Argument_ReasonCode,
    Argument_ReasonUnknown,
    Argument_ReasonFileName,
    Argument_NeedsValue,
    Argument_ValueLines,
    Argument_ApiNotAllowed,
    Argument_FileNameCharset,
    Argument_FastHidesAi,
    Argument_ExcludeTags,
    Argument_ReadTags,
    Argument_ValueFor,

    #endregion // Settings argument field

    #region Group descriptions

    Group_Ai,
    Group_ExifTool,
    Group_File,
    Group_Exif,
    Group_MakerNotes,
    Group_Xmp,
    Group_IccProfile,
    Group_Composite,
    Group_Png,
    Group_Iptc,
    Group_Jfif,
    Group_Photoshop,
    Group_QuickTime,
    Group_Jumbf,
    Group_Mpf,
    Group_Riff,
    Group_MakerNotesOf,

    #endregion // Group descriptions

    #region Reading files

    Read_FileNotOpened,

    #endregion // Reading files

    #region Saving files

    Save_ExifToolStopped,
    Save_NotUpdated,

    #endregion // Saving files

    #region macOS menu bar

    MacMenu_File,
    MacMenu_Edit,
    MacMenu_View,
    MacMenu_Window,
    MacMenu_Settings,
    MacMenu_Quit,
    MacMenu_Open,
    MacMenu_CloseWindow,
    MacMenu_SelectAll,
    MacMenu_Find,
    MacMenu_Groups,
    MacMenu_Inspector,
    MacMenu_CommandBar,
    MacMenu_Minimize,
    MacMenu_Zoom,

    #endregion // macOS menu bar
}
