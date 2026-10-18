using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NameShift.Disk;
using NameShift.Ui.Preview;

namespace NameShift.Ui.Actions;

/// <summary>The words of the Rename, Revert and Reset buttons and of their results. The one place they are built.
/// An item in a sentence is its file name; an item in a dialog list is its full path.</summary>
internal static class ActionText
{
    /// <summary>The most items a dialog lists before "...and N more.".</summary>
    public const int DetailLines = 20;

    /// <summary>Why Rename is disabled, from the snapshot on screen: conflicts, rule errors, both, or "".</summary>
    public static string Hint(PreviewSnapshot snapshot)
    {
        if (snapshot == null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        bool conflicts = snapshot.HasConflicts;
        bool errors = snapshot.ErrorCount > 0;
        if (conflicts && errors)
        {
            return Strings.RenameHintBoth;
        }

        if (conflicts)
        {
            return Strings.RenameHintConflicts;
        }

        return errors ? Strings.RenameHintErrors : "";
    }

    /// <summary>"Rename 1,234 files?".</summary>
    public static string Confirm(int changedCount, CultureInfo culture) =>
        string.Format(culture, Strings.RenameConfirm, Number(changedCount, culture));

    /// <summary>"Renaming 1 file..." or "Renaming 12 files...".</summary>
    public static string Busy(int changedCount, CultureInfo culture) =>
        changedCount == 1 ? Strings.RenamingOne : string.Format(culture, Strings.RenamingMany, Number(changedCount, culture));

    /// <summary>"Reverting...".</summary>
    public static string Reverting => Strings.Reverting;

    /// <summary>"Undo the last rename: 12 names in D:\Photos" (the batch's first folder); "" unless it can be reverted.</summary>
    public static string RevertDescription(LastBatch last, CultureInfo culture)
    {
        if (last == null)
        {
            throw new ArgumentNullException(nameof(last));
        }

        if (!last.CanRevert)
        {
            return "";
        }

        string folder = last.Folders.Count > 0 ? last.Folders[0] : Path.GetDirectoryName(last.Renames[0].OldPath) ?? "";
        int count = last.Renames.Count;
        return count == 1
            ? string.Format(culture, Strings.RevertDescriptionOne, folder)
            : string.Format(culture, Strings.RevertDescriptionMany, Number(count, culture), folder);
    }

    /// <summary>The status line, and a dialog list only when items need the user's attention.</summary>
    public static ActionReport ForRename(RenameRunResult result, CultureInfo culture)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        ExecutionResult? execution = result.Execution;
        if (execution == null)
        {
            return new ActionReport(string.Format(culture, Strings.RenameNoHistory, Message(result.JournalIssue!.SystemMessage)));
        }

        string status;
        string? details = null;
        switch (execution.Outcome)
        {
            case ExecutionOutcome.Succeeded:
                int renamed = execution.Renamed.Count;
                status = renamed == 1 ? Strings.RenameDoneOne : string.Format(culture, Strings.RenameDoneMany, Number(renamed, culture));
                break;
            case ExecutionOutcome.Refused:
            case ExecutionOutcome.RolledBack:
                status = Join(Strings.NothingRenamed, FailureSentence(execution.Failure!, culture));
                break;
            case ExecutionOutcome.NeedsAttention:
                int stranded = execution.NeedsAttention.Count;
                status = stranded == 1
                    ? Strings.RenameAttentionOne
                    : string.Format(culture, Strings.RenameAttentionMany, Number(stranded, culture));
                details = string.Join(Environment.NewLine, AttentionLines(execution.Failure!, execution.NeedsAttention, culture));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(result), execution.Outcome, "Unknown execution outcome.");
        }

        return new ActionReport(WithJournalIssue(status, result.JournalIssue, culture), details);
    }

    /// <summary>The status line, and a dialog list only when items were not restored or need the user's attention.</summary>
    public static ActionReport ForRevert(RevertResult result, CultureInfo culture)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        string status;
        var lines = new List<string>();
        switch (result.Outcome)
        {
            case RevertOutcome.NothingToRevert:
                status = Strings.NothingToRevert;
                break;
            case RevertOutcome.Reverted:
                int reverted = result.Reverted.Count;
                status = reverted == 1 ? Strings.RevertDoneOne : string.Format(culture, Strings.RevertDoneMany, Number(reverted, culture));
                break;
            case RevertOutcome.PartlyReverted:
                status = string.Format(
                    culture,
                    Strings.RevertPartly,
                    Number(result.Reverted.Count, culture),
                    Number(result.Reverted.Count + result.NotReverted.Count, culture));
                lines.AddRange(SkippedLines(result.NotReverted, culture));
                break;
            case RevertOutcome.NothingReverted:
                status = Strings.RevertNone;
                lines.AddRange(SkippedLines(result.NotReverted, culture));
                break;
            case RevertOutcome.NotStarted:
                return new ActionReport(string.Format(culture, Strings.RevertNoHistory, Message(result.JournalIssue!.SystemMessage)));
            case RevertOutcome.NeedsAttention:
                int stranded = result.NeedsAttention.Count;
                status = stranded == 1
                    ? Strings.RevertAttentionOne
                    : string.Format(culture, Strings.RevertAttentionMany, Number(stranded, culture));
                lines.AddRange(AttentionLines(result.Failure!, result.NeedsAttention, culture));
                if (result.NotReverted.Count > 0)
                {
                    lines.Add("");
                    lines.AddRange(SkippedLines(result.NotReverted, culture));
                }

                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "Unknown revert outcome.");
        }

        string? details = lines.Count == 0 ? null : string.Join(Environment.NewLine, lines);
        return new ActionReport(WithJournalIssue(status, result.JournalIssue, culture), details);
    }

    /// <summary>The sentence that says why a batch was refused or stopped.</summary>
    private static string FailureSentence(ExecutionFailure failure, CultureInfo culture)
    {
        switch (failure.Reason)
        {
            case FailureReason.HasConflicts:
                return Strings.FixConflictsFirst;
            case FailureReason.HasRuleErrors:
                return Strings.FixRuleErrorsFirst;
            case FailureReason.SourceMissing:
                return string.Format(culture, Strings.ItemGone, Name(failure.ItemPath!));
            case FailureReason.TargetInTheWay:
                return string.Format(culture, Strings.ItemBlocked, Name(failure.ItemPath!), Name(failure.BlockingPath!));
            case FailureReason.RenameFailed:
                return string.Format(culture, Strings.ItemFailed, Name(failure.ItemPath!), Message(failure.SystemMessage));
            default:
                throw new ArgumentOutOfRangeException(nameof(failure), failure.Reason, "Unknown failure reason.");
        }
    }

    /// <summary>The failure, a blank line, the header, then one line per stranded item (capped).</summary>
    private static IEnumerable<string> AttentionLines(ExecutionFailure failure, IReadOnlyList<StrandedItem> stranded, CultureInfo culture)
    {
        var lines = new List<string> { FailureSentence(failure, culture), "", Strings.AttentionHeader };
        var items = new List<string>(stranded.Count);
        foreach (StrandedItem item in stranded)
        {
            items.Add(string.Format(culture, Strings.AttentionLine, item.CurrentPath, Name(item.OriginalPath)));
        }

        lines.AddRange(Capped(items, culture));
        return lines;
    }

    /// <summary>The header, then one line per entry not restored (capped).</summary>
    private static IEnumerable<string> SkippedLines(IReadOnlyList<SkippedRevert> skipped, CultureInfo culture)
    {
        var lines = new List<string> { Strings.SkippedHeader };
        var items = new List<string>(skipped.Count);
        foreach (SkippedRevert skip in skipped)
        {
            items.Add(SkipLine(skip, culture));
        }

        lines.AddRange(Capped(items, culture));
        return lines;
    }

    private static string SkipLine(SkippedRevert skip, CultureInfo culture)
    {
        string item = skip.Entry.NewPath;
        switch (skip.Reason)
        {
            case RevertSkipReason.ItemMissing:
                return string.Format(culture, Strings.SkipItemMissing, item);
            case RevertSkipReason.NameTaken:
                return string.Format(culture, Strings.SkipNameTaken, item, Name(skip.BlockingPath!));
            case RevertSkipReason.WaitsForSkippedItem:
                return string.Format(culture, Strings.SkipWaits, item, Name(skip.BlockingPath!));
            case RevertSkipReason.RenameFailed:
                return string.Format(culture, Strings.SkipFailed, item, Message(skip.SystemMessage));
            default:
                throw new ArgumentOutOfRangeException(nameof(skip), skip.Reason, "Unknown skip reason.");
        }
    }

    /// <summary>The first DetailLines lines, then "...and N more." for the rest.</summary>
    private static IEnumerable<string> Capped(List<string> items, CultureInfo culture)
    {
        if (items.Count <= DetailLines)
        {
            return items;
        }

        List<string> shown = items.GetRange(0, DetailLines);
        shown.Add(string.Format(culture, Strings.DetailsMore, Number(items.Count - DetailLines, culture)));
        return shown;
    }

    /// <summary>Adds "Revert is not available..." when the journal could not be written after the moves.</summary>
    private static string WithJournalIssue(string status, JournalWriteFailure? issue, CultureInfo culture) =>
        issue != null && issue.Phase == JournalWritePhase.AfterFinish
            ? Join(status, string.Format(culture, Strings.HistoryNotSaved, Message(issue.SystemMessage)))
            : status;

    private static string Join(string first, string second) => first + " " + second;

    /// <summary>The file or folder name of a full path; the path itself when it has none (a drive root).</summary>
    private static string Name(string path)
    {
        string name = Path.GetFileName(path);
        return name.Length == 0 ? path : name;
    }

    /// <summary>A system message, trimmed of surrounding white space; "" when there is none.</summary>
    private static string Message(string? systemMessage) => (systemMessage ?? "").Trim();

    private static string Number(int value, CultureInfo culture) => value.ToString("N0", culture);
}
