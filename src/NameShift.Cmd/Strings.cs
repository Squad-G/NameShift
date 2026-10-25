using System;
using System.Globalization;
using System.Resources;

namespace NameShift.Cmd;

/// <summary>Every text the command line writes itself, from Strings.resx. One property per key. The window's texts
/// (results, conflicts, rule errors, hints) come from NameShift.Ui.</summary>
public static class Strings
{
    private static readonly ResourceManager Manager = new ResourceManager("NameShift.Cmd.Strings", typeof(Strings).Assembly);

    /// <summary>The usage line that ends every usage error.</summary>
    public static string Usage => Get(nameof(Usage));

    public static string NoCommand => Get(nameof(NoCommand));

    /// <summary>Unknown command "{0}".</summary>
    public static string UnknownCommand => Get(nameof(UnknownCommand));

    /// <summary>Unknown option "{0}".</summary>
    public static string UnknownOption => Get(nameof(UnknownOption));

    public static string ReplaceNeedsValues => Get(nameof(ReplaceNeedsValues));

    public static string CaseNeedsValue => Get(nameof(CaseNeedsValue));

    /// <summary>"{0}" is not a value of --case: ... (lists the four values).</summary>
    public static string BadCase => Get(nameof(BadCase));

    /// <summary>"{0} can be given only once." ({0} is --replace or --case).</summary>
    public static string RepeatedOption => Get(nameof(RepeatedOption));

    public static string EmptyFind => Get(nameof(EmptyFind));

    /// <summary>Give one folder only: "{0}" and "{1}" were both given.</summary>
    public static string TwoFolders => Get(nameof(TwoFolders));

    public static string NoFolder => Get(nameof(NoFolder));

    public static string NoRule => Get(nameof(NoRule));

    /// <summary>The text of --prefix cannot be empty.</summary>
    public static string EmptyPrefix => Get(nameof(EmptyPrefix));

    /// <summary>The text of --suffix cannot be empty.</summary>
    public static string EmptySuffix => Get(nameof(EmptySuffix));

    /// <summary>The text of --insert cannot be empty.</summary>
    public static string EmptyInsert => Get(nameof(EmptyInsert));

    /// <summary>--insert needs two values: the text and the position.</summary>
    public static string InsertNeedsValues => Get(nameof(InsertNeedsValues));

    /// <summary>"{0}" is not a position of --insert: use a whole number from 1.</summary>
    public static string BadInsertPosition => Get(nameof(BadInsertPosition));

    /// <summary>--from-end needs --insert.</summary>
    public static string FromEndNeedsInsert => Get(nameof(FromEndNeedsInsert));

    /// <summary>"{0} needs a value." ({0} is the option).</summary>
    public static string OptionNeedsValue => Get(nameof(OptionNeedsValue));

    /// <summary>"{0}" is the name as typed, trimmed. Followed on the same line by SavedPresets or NoSavedPresets.</summary>
    public static string PresetNotFound => Get(nameof(PresetNotFound));

    /// <summary>{0} is the saved names in the store's order, joined by ", ".</summary>
    public static string SavedPresets => Get(nameof(SavedPresets));

    /// <summary>No preset is saved (or the folder cannot be read).</summary>
    public static string NoSavedPresets => Get(nameof(NoSavedPresets));

    /// <summary>No panel of the preset builds a rule.</summary>
    public static string PresetHasNoRules => Get(nameof(PresetHasNoRules));

    /// <summary>The success line of --save-preset.</summary>
    public static string PresetSaved => Get(nameof(PresetSaved));

    /// <summary>The name is taken and --overwrite is absent.</summary>
    public static string PresetExists => Get(nameof(PresetExists));

    /// <summary>{0} is the system message.</summary>
    public static string PresetSaveFailed => Get(nameof(PresetSaveFailed));

    /// <summary>Usage error.</summary>
    public static string PresetNameBlank => Get(nameof(PresetNameBlank));

    /// <summary>Usage error.</summary>
    public static string PresetNameTooLong => Get(nameof(PresetNameTooLong));

    /// <summary>Usage error.</summary>
    public static string OverwriteNeedsSave => Get(nameof(OverwriteNeedsSave));

    /// <summary>"{0}: {1}": an option, then the window's own error text for its value.</summary>
    public static string OptionError => Get(nameof(OptionError));

    /// <summary>"{0}" is not a value of {1}: use {2}. ({2} is one of the three value lists).</summary>
    public static string BadChoice => Get(nameof(BadChoice));

    /// <summary>"prefix, suffix or insert": the values of --number-mode.</summary>
    public static string NumberModeValues => Get(nameof(NumberModeValues));

    /// <summary>"decimal, hex, letters or roman": the values of --number-system.</summary>
    public static string NumberSystemValues => Get(nameof(NumberSystemValues));

    /// <summary>"upper or lower": the values of --number-case.</summary>
    public static string NumberCaseValues => Get(nameof(NumberCaseValues));

    /// <summary>"Until then": where the last sentence of the window's error texts starts; the command line cuts the text from there.</summary>
    public static string SkipSentenceMarker => Get(nameof(SkipSentenceMarker));

    /// <summary>Usage error: two flags of one rule's modes ({0} lists them).</summary>
    public static string OneModeOnly => Get(nameof(OneModeOnly));

    /// <summary>Usage error: "{0} needs {1}."</summary>
    public static string NeedsOption => Get(nameof(NeedsOption));

    /// <summary>Usage error: the text of {0} is empty or only spaces.</summary>
    public static string BlankText => Get(nameof(BlankText));

    /// <summary>Usage error: the text of {0} is empty or only dots and spaces.</summary>
    public static string BlankExtension => Get(nameof(BlankExtension));

    /// <summary>Usage error: the text of {0} is empty.</summary>
    public static string EmptyOptionText => Get(nameof(EmptyOptionText));

    /// <summary>The flags of the Name panel's modes.</summary>
    public static string NameModeOptions => Get(nameof(NameModeOptions));

    /// <summary>The flags of the Extension panel's modes.</summary>
    public static string ExtensionModeOptions => Get(nameof(ExtensionModeOptions));

    /// <summary>The options --remove-match-case needs one of.</summary>
    public static string RemoveMatchCaseNeeds => Get(nameof(RemoveMatchCaseNeeds));

    /// <summary>The options --remove-keep-marker needs one of.</summary>
    public static string RemoveKeepMarkerNeeds => Get(nameof(RemoveKeepMarkerNeeds));

    /// <summary>The label of the Remove range error.</summary>
    public static string RemoveRangeOptions => Get(nameof(RemoveRangeOptions));

    /// <summary>Usage error: --regex has fewer than two values.</summary>
    public static string RegexNeedsValues => Get(nameof(RegexNeedsValues));

    /// <summary>The flags of the filter's kinds, for <see cref="OneModeOnly"/>.</summary>
    public static string FilterKindOptions => Get(nameof(FilterKindOptions));

    /// <summary>Usage error: a mask in the folder text and --mask.</summary>
    public static string MaskTwice => Get(nameof(MaskTwice));

    /// <summary>The filters left no file: a line, exit code 0.</summary>
    public static string NoFilesMatch => Get(nameof(NoFilesMatch));

    /// <summary>One file planned with the Photo date source has no photo date (error stream).</summary>
    public static string PhotoDateMissingOne => Get(nameof(PhotoDateMissingOne));

    /// <summary>Several files have no photo date; {0} is the count (error stream).</summary>
    public static string PhotoDateMissingMany => Get(nameof(PhotoDateMissingMany));

    /// <summary>The flags of the Move or copy panel's modes.</summary>
    public static string MoveModeOptions => Get(nameof(MoveModeOptions));

    /// <summary>The values of --move-from.</summary>
    public static string MoveFromValues => Get(nameof(MoveFromValues));

    /// <summary>The values of --move-to.</summary>
    public static string MoveToValues => Get(nameof(MoveToValues));

    /// <summary>The values of --date-source.</summary>
    public static string DateSourceValues => Get(nameof(DateSourceValues));

    /// <summary>The values of --date-placement and --folder-placement.</summary>
    public static string PlacementValues => Get(nameof(PlacementValues));

    /// <summary>"The folder {0} contains a quote: ...": what cmd makes of a quoted folder that ends with a backslash.</summary>
    public static string QuoteInFolder => Get(nameof(QuoteInFolder));

    /// <summary>"{0}" is not a valid folder path.</summary>
    public static string InvalidFolder => Get(nameof(InvalidFolder));

    /// <summary>"{0} is a file, not a folder.".</summary>
    public static string FolderIsFile => Get(nameof(FolderIsFile));

    /// <summary>"Folder not found: {0}".</summary>
    public static string FolderNotFound => Get(nameof(FolderNotFound));

    /// <summary>"Cannot list {0}: {1}": the folder, then the window's folder problem text.</summary>
    public static string CannotList => Get(nameof(CannotList));

    /// <summary>"--quiet and --verbose cannot be used together."</summary>
    public static string QuietWithVerbose => Get(nameof(QuietWithVerbose));

    /// <summary>"--quiet and --dry-run cannot be used together."</summary>
    public static string QuietWithDryRun => Get(nameof(QuietWithDryRun));

    /// <summary>"{0}: unchanged": a file name the rules leave as it is (--verbose).</summary>
    public static string UnchangedLine => Get(nameof(UnchangedLine));

    public static string NothingToRename => Get(nameof(NothingToRename));

    /// <summary>"{0} -&gt; {1}": the old and the new file name of a dry run.</summary>
    public static string DryRunLine => Get(nameof(DryRunLine));

    /// <summary>"{0}: skipped ({1})": a name undo would skip in a dry run, and why.</summary>
    public static string DryRunSkippedLine => Get(nameof(DryRunSkippedLine));

    /// <summary>"{0}: {1}": a file name, then its conflicts or its rule error.</summary>
    public static string ItemLine => Get(nameof(ItemLine));

    /// <summary>"For help, run: NameShiftCmd --help": ends every usage error.</summary>
    public static string HelpPointer => Get(nameof(HelpPointer));

    /// <summary>"NameShift hit an unexpected problem. No file was renamed.": a crash before the renamer was called.</summary>
    public static string UnexpectedBeforeRename => Get(nameof(UnexpectedBeforeRename));

    /// <summary>"NameShift hit an unexpected problem. No file was changed.": a crash before undo reverted anything.</summary>
    public static string UnexpectedBeforeUndo => Get(nameof(UnexpectedBeforeUndo));

    /// <summary>"The undo history cannot be used...": the last batch is interrupted or unreadable.</summary>
    public static string UndoHistoryUnusable => Get(nameof(UndoHistoryUnusable));

    /// <summary>"undo takes no folder or rule options, only --dry-run.": a usage error.</summary>
    public static string UndoOnlyDryRun => Get(nameof(UndoOnlyDryRun));

    /// <summary>"NameShift {0}": the version line.</summary>
    public static string VersionLine => Get(nameof(VersionLine));

    /// <summary>The usage of apply, two lines, in the helps.</summary>
    public static string HelpUsageApply => Get(nameof(HelpUsageApply));

    /// <summary>The usage of undo, two lines, in the helps.</summary>
    public static string HelpUsageUndo => Get(nameof(HelpUsageUndo));

    /// <summary>The main help between its usage and the apply options: commands, global options, examples.</summary>
    public static string HelpMain => Get(nameof(HelpMain));

    /// <summary>The options of apply and one example.</summary>
    public static string HelpApply => Get(nameof(HelpApply));

    /// <summary>The options of undo and one example.</summary>
    public static string HelpUndo => Get(nameof(HelpUndo));

    /// <summary>"Exit codes:": the title of the exit code table.</summary>
    public static string HelpExitCodes => Get(nameof(HelpExitCodes));

    /// <summary>The meaning of exit code 0.</summary>
    public static string ExitCode0 => Get(nameof(ExitCode0));

    /// <summary>The meaning of exit code 1.</summary>
    public static string ExitCode1 => Get(nameof(ExitCode1));

    /// <summary>The meaning of exit code 2.</summary>
    public static string ExitCode2 => Get(nameof(ExitCode2));

    /// <summary>The meaning of exit code 3.</summary>
    public static string ExitCode3 => Get(nameof(ExitCode3));

    /// <summary>The meaning of exit code 4.</summary>
    public static string ExitCode4 => Get(nameof(ExitCode4));

    private static string Get(string key) =>
        Manager.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException("Missing string resource: " + key);
}
