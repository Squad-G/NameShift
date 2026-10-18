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

    private static string Get(string key) =>
        Manager.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException("Missing string resource: " + key);
}
