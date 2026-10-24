using System;
using System.Globalization;
using System.Resources;

namespace NameShift.Ui;

/// <summary>Every visible string of the app, from Strings.resx. One property per key.</summary>
public static class Strings
{
    private static readonly ResourceManager Manager = new ResourceManager("NameShift.Ui.Strings", typeof(Strings).Assembly);

    public static string AppTitle => Get(nameof(AppTitle));

    public static string Desktop => Get(nameof(Desktop));

    public static string Documents => Get(nameof(Documents));

    public static string Downloads => Get(nameof(Downloads));

    public static string Pictures => Get(nameof(Pictures));

    public static string Loading => Get(nameof(Loading));

    public static string DriveNotReady => Get(nameof(DriveNotReady));

    public static string AccessDenied => Get(nameof(AccessDenied));

    public static string FolderNotFound => Get(nameof(FolderNotFound));

    public static string FolderUnavailable => Get(nameof(FolderUnavailable));

    public static string PathBarLabel => Get(nameof(PathBarLabel));

    public static string PathInvalid => Get(nameof(PathInvalid));

    /// <summary>"Folder not found: {0}".</summary>
    public static string PathNotFound => Get(nameof(PathNotFound));

    /// <summary>"That is a file, not a folder: {0}".</summary>
    public static string PathNotAFolder => Get(nameof(PathNotAFolder));

    public static string PathBarHint => Get(nameof(PathBarHint));

    public static string SomethingWentWrong => Get(nameof(SomethingWentWrong));

    /// <summary>"{0} is no longer there."</summary>
    public static string ShownFolderGone => Get(nameof(ShownFolderGone));

    public static string FolderTreeName => Get(nameof(FolderTreeName));

    public static string ColumnName => Get(nameof(ColumnName));

    public static string ColumnNewName => Get(nameof(ColumnNewName));

    public static string ColumnStatus => Get(nameof(ColumnStatus));

    public static string ConflictInvalidCharacter => Get(nameof(ConflictInvalidCharacter));

    public static string ConflictReservedName => Get(nameof(ConflictReservedName));

    public static string ConflictTrailingDotOrSpace => Get(nameof(ConflictTrailingDotOrSpace));

    public static string ConflictEmptyName => Get(nameof(ConflictEmptyName));

    public static string ConflictNameTooLong => Get(nameof(ConflictNameTooLong));

    public static string ConflictDuplicateTarget => Get(nameof(ConflictDuplicateTarget));

    public static string ConflictTargetExists => Get(nameof(ConflictTargetExists));

    public static string ConflictPathTooLong => Get(nameof(ConflictPathTooLong));

    public static string ConflictSeparator => Get(nameof(ConflictSeparator));

    public static string ConflictSummaryOne => Get(nameof(ConflictSummaryOne));

    public static string ConflictSummaryMany => Get(nameof(ConflictSummaryMany));

    public static string SelectionCountOne => Get(nameof(SelectionCountOne));

    public static string SelectionCountMany => Get(nameof(SelectionCountMany));

    public static string ColumnSize => Get(nameof(ColumnSize));

    public static string ColumnModified => Get(nameof(ColumnModified));

    /// <summary>"{0} KB".</summary>
    public static string SizeKilobytes => Get(nameof(SizeKilobytes));

    public static string FolderEmpty => Get(nameof(FolderEmpty));

    public static string FileListName => Get(nameof(FileListName));

    /// <summary>"Error: {0}", a rule's error in the New Name cell.</summary>
    public static string PreviewError => Get(nameof(PreviewError));

    /// <summary>"{0}. {1}": the stage number and the panel title.</summary>
    public static string RulePanelHeading => Get(nameof(RulePanelHeading));

    public static string RulePanelReset => Get(nameof(RulePanelReset));

    /// <summary>"Reset {0}": the reset button's accessible name.</summary>
    public static string RulePanelResetName => Get(nameof(RulePanelResetName));

    public static string RulePanelActive => Get(nameof(RulePanelActive));

    public static string RulePanelsName => Get(nameof(RulePanelsName));

    public static string ReplaceTitle => Get(nameof(ReplaceTitle));

    public static string ReplaceFind => Get(nameof(ReplaceFind));

    public static string ReplaceWith => Get(nameof(ReplaceWith));

    public static string ReplaceMatchCase => Get(nameof(ReplaceMatchCase));

    public static string ReplaceKeepCase => Get(nameof(ReplaceKeepCase));

    public static string ReplaceIncludeExtension => Get(nameof(ReplaceIncludeExtension));

    public static string CaseTitle => Get(nameof(CaseTitle));

    public static string CaseMode => Get(nameof(CaseMode));

    public static string CaseExceptions => Get(nameof(CaseExceptions));

    public static string CaseExceptionsHint => Get(nameof(CaseExceptionsHint));

    public static string CaseModeSame => Get(nameof(CaseModeSame));

    public static string CaseModeLower => Get(nameof(CaseModeLower));

    public static string CaseModeUpper => Get(nameof(CaseModeUpper));

    public static string CaseModeTitle => Get(nameof(CaseModeTitle));

    public static string CaseModeSentence => Get(nameof(CaseModeSentence));

    public static string NameTitle => Get(nameof(NameTitle));

    public static string NameMode => Get(nameof(NameMode));

    public static string NameText => Get(nameof(NameText));

    public static string NameModeKeep => Get(nameof(NameModeKeep));

    public static string NameModeFixed => Get(nameof(NameModeFixed));

    public static string NameModeRemove => Get(nameof(NameModeRemove));

    public static string NameModeReverse => Get(nameof(NameModeReverse));

    public static string ExtensionTitle => Get(nameof(ExtensionTitle));

    public static string ExtensionMode => Get(nameof(ExtensionMode));

    public static string ExtensionText => Get(nameof(ExtensionText));

    public static string ExtensionModeSame => Get(nameof(ExtensionModeSame));

    public static string ExtensionModeLower => Get(nameof(ExtensionModeLower));

    public static string ExtensionModeUpper => Get(nameof(ExtensionModeUpper));

    public static string ExtensionModeFixed => Get(nameof(ExtensionModeFixed));

    public static string ExtensionModeExtra => Get(nameof(ExtensionModeExtra));

    public static string ExtensionModeRemove => Get(nameof(ExtensionModeRemove));

    public static string ExtensionNote => Get(nameof(ExtensionNote));

    public static string AddTitle => Get(nameof(AddTitle));

    public static string AddPrefix => Get(nameof(AddPrefix));

    public static string AddInsert => Get(nameof(AddInsert));

    public static string AddPosition => Get(nameof(AddPosition));

    public static string AddPositionFromEnd => Get(nameof(AddPositionFromEnd));

    public static string AddFromEnd => Get(nameof(AddFromEnd));

    public static string AddSuffix => Get(nameof(AddSuffix));

    public static string AddWordSpace => Get(nameof(AddWordSpace));

    public static string AddWordSpaceHint => Get(nameof(AddWordSpaceHint));

    public static string AddPositionInvalid => Get(nameof(AddPositionInvalid));

    public static string NumberingTitle => Get(nameof(NumberingTitle));

    public static string NumberingMode => Get(nameof(NumberingMode));

    public static string NumberingModePrefix => Get(nameof(NumberingModePrefix));

    public static string NumberingModeSuffix => Get(nameof(NumberingModeSuffix));

    public static string NumberingModeInsert => Get(nameof(NumberingModeInsert));

    public static string NumberingStart => Get(nameof(NumberingStart));

    public static string NumberingStep => Get(nameof(NumberingStep));

    public static string NumberingPad => Get(nameof(NumberingPad));

    public static string NumberingPadHint => Get(nameof(NumberingPadHint));

    public static string NumberingSeparator => Get(nameof(NumberingSeparator));

    public static string NumberingSeparatorHint => Get(nameof(NumberingSeparatorHint));

    public static string NumberingPosition => Get(nameof(NumberingPosition));

    public static string NumberingPositionHint => Get(nameof(NumberingPositionHint));

    public static string NumberingStartInvalid => Get(nameof(NumberingStartInvalid));

    public static string NumberingStepInvalid => Get(nameof(NumberingStepInvalid));

    /// <summary>"Type a whole number from 0 to {0}. Until then numbering is skipped.".</summary>
    public static string NumberingPadInvalid => Get(nameof(NumberingPadInvalid));

    public static string NumberingPositionInvalid => Get(nameof(NumberingPositionInvalid));

    public static string ButtonRename => Get(nameof(ButtonRename));

    public static string ButtonRevert => Get(nameof(ButtonRevert));

    public static string ButtonReset => Get(nameof(ButtonReset));

    public static string ResetDescription => Get(nameof(ResetDescription));

    /// <summary>"Undo the last rename: 1 name in {0}".</summary>
    public static string RevertDescriptionOne => Get(nameof(RevertDescriptionOne));

    /// <summary>"Undo the last rename: {0} names in {1}".</summary>
    public static string RevertDescriptionMany => Get(nameof(RevertDescriptionMany));

    public static string RenameHintConflicts => Get(nameof(RenameHintConflicts));

    public static string RenameHintErrors => Get(nameof(RenameHintErrors));

    public static string RenameHintBoth => Get(nameof(RenameHintBoth));

    public static string RenameHintLocked => Get(nameof(RenameHintLocked));

    /// <summary>"Rename {0} files?".</summary>
    public static string RenameConfirm => Get(nameof(RenameConfirm));

    public static string RenamingOne => Get(nameof(RenamingOne));

    /// <summary>"Renaming {0} files...".</summary>
    public static string RenamingMany => Get(nameof(RenamingMany));

    public static string Reverting => Get(nameof(Reverting));

    public static string WaitForAction => Get(nameof(WaitForAction));

    public static string PreviewChanged => Get(nameof(PreviewChanged));

    public static string RenameDoneOne => Get(nameof(RenameDoneOne));

    /// <summary>"{0} files renamed.".</summary>
    public static string RenameDoneMany => Get(nameof(RenameDoneMany));

    public static string NothingRenamed => Get(nameof(NothingRenamed));

    public static string FixConflictsFirst => Get(nameof(FixConflictsFirst));

    public static string FixRuleErrorsFirst => Get(nameof(FixRuleErrorsFirst));

    /// <summary>"{0} is no longer there.".</summary>
    public static string ItemGone => Get(nameof(ItemGone));

    /// <summary>"{0} cannot be renamed: {1} already exists.".</summary>
    public static string ItemBlocked => Get(nameof(ItemBlocked));

    /// <summary>"{0} could not be renamed: {1}".</summary>
    public static string ItemFailed => Get(nameof(ItemFailed));

    /// <summary>"Nothing was renamed, because the history needed for Revert could not be saved: {0}".</summary>
    public static string RenameNoHistory => Get(nameof(RenameNoHistory));

    /// <summary>"Revert is not available, because the history could not be saved: {0}".</summary>
    public static string HistoryNotSaved => Get(nameof(HistoryNotSaved));

    public static string RenameAttentionOne => Get(nameof(RenameAttentionOne));

    /// <summary>"The rename failed and {0} items could not get their old names back. Check them now.".</summary>
    public static string RenameAttentionMany => Get(nameof(RenameAttentionMany));

    public static string AttentionHeader => Get(nameof(AttentionHeader));

    /// <summary>"{0}  (was {1})".</summary>
    public static string AttentionLine => Get(nameof(AttentionLine));

    public static string NothingToRevert => Get(nameof(NothingToRevert));

    public static string RevertDoneOne => Get(nameof(RevertDoneOne));

    /// <summary>"{0} names restored.".</summary>
    public static string RevertDoneMany => Get(nameof(RevertDoneMany));

    /// <summary>"{0} of {1} names restored. The others could not be restored; Revert stays available for them.".</summary>
    public static string RevertPartly => Get(nameof(RevertPartly));

    public static string RevertNone => Get(nameof(RevertNone));

    /// <summary>"Nothing was reverted, because the history could not be saved: {0}".</summary>
    public static string RevertNoHistory => Get(nameof(RevertNoHistory));

    public static string RevertAttentionOne => Get(nameof(RevertAttentionOne));

    /// <summary>"Revert failed and {0} items could not get their names back. Check them now.".</summary>
    public static string RevertAttentionMany => Get(nameof(RevertAttentionMany));

    public static string SkippedHeader => Get(nameof(SkippedHeader));

    /// <summary>"{0}: it is no longer where the rename left it.".</summary>
    public static string SkipItemMissing => Get(nameof(SkipItemMissing));

    /// <summary>"{0}: {1} now has its old name.".</summary>
    public static string SkipNameTaken => Get(nameof(SkipNameTaken));

    /// <summary>"{0}: its old name is still used by {1}.".</summary>
    public static string SkipWaits => Get(nameof(SkipWaits));

    /// <summary>"{0}: {1}".</summary>
    public static string SkipFailed => Get(nameof(SkipFailed));

    /// <summary>"...and {0} more.".</summary>
    public static string DetailsMore => Get(nameof(DetailsMore));

    public static string ActionUnexpected => Get(nameof(ActionUnexpected));

    private static string Get(string key) =>
        Manager.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException("Missing string resource: " + key);
}
