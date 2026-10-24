using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NameShift.Core;
using NameShift.Disk;
using NameShift.Ui.Actions;
using NameShift.Ui.Preview;
using Xunit;
using static NameShift.Ui.Tests.Actions.DiskResults;

namespace NameShift.Ui.Tests.Actions;

public sealed class ActionTextTests
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    private static readonly string NL = Environment.NewLine;

    private static PreviewSnapshot Snapshot(bool conflict, bool error)
    {
        RenameItem[] items = { FakeFileListSource.File("a.txt"), FakeFileListSource.File("b.txt") };
        var results = new[]
        {
            new RenameResult(items[0], 0, "x.txt", RenameStatus.Changed, null),
            error
                ? new RenameResult(items[1], 1, "b.txt", RenameStatus.Error, new RenameError(RuleStage.Replace, "Replace", "boom"))
                : new RenameResult(items[1], 1, "y.txt", RenameStatus.Changed, null),
        };
        IReadOnlyList<Conflict> none = Array.Empty<Conflict>();
        IReadOnlyList<Conflict> first = conflict ? new[] { new Conflict(ConflictCode.TargetExists) } : none;
        return new PreviewSnapshot(items, results, new ConflictReport(new[] { first, none }));
    }

    // Hint (AC 2)

    [Fact]
    public void Hint_NoConflictNoError_IsEmpty() => Assert.Equal("", ActionText.Hint(Snapshot(false, false)));

    [Fact]
    public void Hint_Conflicts() => Assert.Equal("Fix the conflicts to rename.", ActionText.Hint(Snapshot(true, false)));

    [Fact]
    public void Hint_RuleErrors() => Assert.Equal("Fix the rule errors to rename.", ActionText.Hint(Snapshot(false, true)));

    [Fact]
    public void Hint_Both() => Assert.Equal("Fix the conflicts and rule errors to rename.", ActionText.Hint(Snapshot(true, true)));

    [Fact]
    public void Hint_EmptySnapshot_IsEmpty() => Assert.Equal("", ActionText.Hint(PreviewSnapshot.Empty));

    // Confirmation, busy text, Revert's description

    [Fact]
    public void Confirm_FormatsTheCount() => Assert.Equal("Rename 1,234 files?", ActionText.Confirm(1234, En));

    [Fact]
    public void Busy_OneAndMany()
    {
        Assert.Equal("Renaming 1 file...", ActionText.Busy(1, En));
        Assert.Equal("Renaming 12 files...", ActionText.Busy(12, En));
        Assert.Equal("Renaming 5,000 files...", ActionText.Busy(5000, En));
        Assert.Equal("Reverting...", ActionText.Reverting);
    }

    [Fact]
    public void RevertDescription_OneMany_EmptyUnlessRevertable()
    {
        Assert.Equal(@"Undo the last rename: 1 name in D:\Photos", ActionText.RevertDescription(Last(LastBatchState.Revertable, 1, @"D:\Photos"), En));
        Assert.Equal(@"Undo the last rename: 1,200 names in D:\Photos", ActionText.RevertDescription(Last(LastBatchState.Revertable, 1200, @"D:\Photos"), En));
        foreach (LastBatchState state in new[] { LastBatchState.None, LastBatchState.Reverted, LastBatchState.Interrupted, LastBatchState.Unreadable })
        {
            Assert.Equal("", ActionText.RevertDescription(Last(state), En));
        }
    }

    // Rename (AC 3, 4, 9)

    [Fact]
    public void RenameSucceeded_One() => Assert.Equal("1 file renamed.", ActionText.ForRename(Succeeded(1), En).Status);

    [Fact]
    public void RenameSucceeded_Many() => Assert.Equal("1,234 files renamed.", ActionText.ForRename(Succeeded(1234), En).Status);

    [Fact]
    public void SuccessHasNoDetails()
    {
        Assert.Null(ActionText.ForRename(Succeeded(3), En).Details);
        Assert.Null(ActionText.ForRevert(Revert(RevertOutcome.Reverted, Renames(3)), En).Details);
    }

    [Fact]
    public void Refused_HasConflicts()
    {
        ActionReport report = ActionText.ForRename(Failed(ExecutionOutcome.Refused, Failure(FailureReason.HasConflicts)), En);

        Assert.Equal("Nothing was renamed. Fix the conflicts first.", report.Status);
        Assert.Null(report.Details);
    }

    [Fact]
    public void Refused_HasRuleErrors() =>
        Assert.Equal(
            "Nothing was renamed. Fix the rule errors first.",
            ActionText.ForRename(Failed(ExecutionOutcome.Refused, Failure(FailureReason.HasRuleErrors)), En).Status);

    [Fact]
    public void Refused_SourceMissing() =>
        Assert.Equal(
            "Nothing was renamed. b.txt is no longer there.",
            ActionText.ForRename(Failed(ExecutionOutcome.Refused, Failure(FailureReason.SourceMissing, @"C:\A\b.txt")), En).Status);

    [Fact]
    public void RolledBack_TargetInTheWay() =>
        Assert.Equal(
            "Nothing was renamed. b.txt cannot be renamed: x_b.txt already exists.",
            ActionText.ForRename(
                Failed(ExecutionOutcome.RolledBack, Failure(FailureReason.TargetInTheWay, @"C:\A\b.txt", @"C:\A\x_b.txt")), En).Status);

    [Fact]
    public void RolledBack_RenameFailed_ShowsSystemMessage()
    {
        ActionReport report = ActionText.ForRename(
            Failed(
                ExecutionOutcome.RolledBack,
                Failure(FailureReason.RenameFailed, @"C:\A\b.txt", systemMessage: "  The process cannot access the file because it is being used by another process.\r\n")),
            En);

        Assert.Equal(
            "Nothing was renamed. b.txt could not be renamed: The process cannot access the file because it is being used by another process.",
            report.Status);
        Assert.Null(report.Details);
    }

    [Fact]
    public void RolledBack_ItemInUse_SaysInUse()
    {
        ActionReport report = ActionText.ForRename(
            Failed(
                ExecutionOutcome.RolledBack,
                Failure(FailureReason.ItemInUse, @"C:\A\b.txt", systemMessage: "The process cannot access the file because it is being used by another process.")),
            En);

        Assert.Equal("Nothing was renamed. b.txt is in use by another program: close it and try again.", report.Status);
        Assert.Null(report.Details);
    }

    [Fact]
    public void ItemInUse_StringValue() =>
        Assert.Equal("{0} is in use by another program: close it and try again.", Strings.ItemInUse);

    [Fact]
    public void NeedsAttention_ItemInUse_FirstLineIsTheSentence()
    {
        ExecutionFailure failure = Failure(FailureReason.ItemInUse, @"C:\A\c.txt", systemMessage: "Busy.");

        ActionReport report = ActionText.ForRename(NeedsAttention(failure, Stranded(@"C:\A\~ns-1234.tmp", @"C:\A\a.txt")), En);

        Assert.Equal(
            "c.txt is in use by another program: close it and try again.",
            report.Details!.Split(new[] { NL }, StringSplitOptions.None)[0]);
    }

    [Fact]
    public void NotStarted_HistoryNotSaved()
    {
        ActionReport report = ActionText.ForRename(NotStarted("Access to the path is denied."), En);

        Assert.Equal(
            "Nothing was renamed, because the history needed for Revert could not be saved: Access to the path is denied.",
            report.Status);
        Assert.Null(report.Details);
    }

    [Fact]
    public void Succeeded_HistoryNotSavedAfter_AddsRevertNotAvailable() =>
        Assert.Equal(
            "3 files renamed. Revert is not available, because the history could not be saved: The disk is full.",
            ActionText.ForRename(Succeeded(3, Journal(JournalWritePhase.AfterFinish)), En).Status);

    [Fact]
    public void RolledBack_HistoryNotSavedAfter_AddsRevertNotAvailable() =>
        Assert.Equal(
            "Nothing was renamed. b.txt is no longer there. Revert is not available, because the history could not be saved: The disk is full.",
            ActionText.ForRename(
                Failed(ExecutionOutcome.RolledBack, Failure(FailureReason.SourceMissing, @"C:\A\b.txt"), Journal(JournalWritePhase.AfterFinish)),
                En).Status);

    [Fact]
    public void NeedsAttention_StatusCount_DetailsListCurrentPathAndOldName()
    {
        ExecutionFailure failure = Failure(FailureReason.RenameFailed, @"C:\A\c.txt", systemMessage: "Access is denied.");

        ActionReport one = ActionText.ForRename(NeedsAttention(failure, Stranded(@"C:\A\~ns-1234.tmp", @"C:\A\a.txt")), En);
        ActionReport two = ActionText.ForRename(
            NeedsAttention(failure, Stranded(@"C:\A\~ns-1234.tmp", @"C:\A\a.txt"), Stranded(@"C:\A\x_b.txt", @"C:\A\b.txt")), En);

        Assert.Equal("The rename failed and 1 item could not get its old name back. Check it now.", one.Status);
        Assert.Equal("The rename failed and 2 items could not get their old names back. Check them now.", two.Status);
        Assert.Equal(
            "c.txt could not be renamed: Access is denied." + NL + NL
            + "Not back to their old names:" + NL
            + @"C:\A\~ns-1234.tmp  (was a.txt)" + NL
            + @"C:\A\x_b.txt  (was b.txt)",
            two.Details);
    }

    [Fact]
    public void Details_MoreThan20_First20ThenAndNMore()
    {
        CompletedRename[] entries = Renames(25);
        RevertResult result = Revert(
            RevertOutcome.NothingReverted,
            notReverted: entries.Select(e => Skip(e, RevertSkipReason.ItemMissing)).ToArray());

        string[] lines = ActionText.ForRevert(result, En).Details!.Split(new[] { NL }, StringSplitOptions.None);

        Assert.Equal(22, lines.Length);
        Assert.Equal("Not restored:", lines[0]);
        Assert.Equal(@"C:\A\x_f0.txt: it is no longer where the rename left it.", lines[1]);
        Assert.Equal(@"C:\A\x_f19.txt: it is no longer where the rename left it.", lines[20]);
        Assert.Equal("...and 5 more.", lines[21]);
    }

    [Fact]
    public void Details_Exactly20_NoMoreLine()
    {
        CompletedRename[] entries = Renames(20);
        RevertResult result = Revert(
            RevertOutcome.NothingReverted,
            notReverted: entries.Select(e => Skip(e, RevertSkipReason.ItemMissing)).ToArray());

        Assert.Equal(21, ActionText.ForRevert(result, En).Details!.Split(new[] { NL }, StringSplitOptions.None).Length);
    }

    // Revert (AC 5)

    [Fact]
    public void Reverted_One() => Assert.Equal("1 name restored.", ActionText.ForRevert(Revert(RevertOutcome.Reverted, Renames(1)), En).Status);

    [Fact]
    public void Reverted_Many() => Assert.Equal("1,500 names restored.", ActionText.ForRevert(Revert(RevertOutcome.Reverted, Renames(1500)), En).Status);

    [Fact]
    public void PartlyReverted_ListsEachSkipReason()
    {
        CompletedRename[] entries = Renames(6);
        RevertResult result = Revert(
            RevertOutcome.PartlyReverted,
            entries.Take(2).ToArray(),
            new[]
            {
                Skip(entries[2], RevertSkipReason.ItemMissing),
                Skip(entries[3], RevertSkipReason.NameTaken, @"C:\A\f3.txt"),
                Skip(entries[4], RevertSkipReason.WaitsForSkippedItem, @"C:\A\x_f3.txt"),
                Skip(entries[5], RevertSkipReason.RenameFailed, systemMessage: " Access is denied. "),
            });

        ActionReport report = ActionText.ForRevert(result, En);

        Assert.Equal("2 of 6 names restored. The others could not be restored; Revert stays available for them.", report.Status);
        Assert.Equal(
            "Not restored:" + NL
            + @"C:\A\x_f2.txt: it is no longer where the rename left it." + NL
            + @"C:\A\x_f3.txt: f3.txt now has its old name." + NL
            + @"C:\A\x_f4.txt: its old name is still used by x_f3.txt." + NL
            + @"C:\A\x_f5.txt: Access is denied.",
            report.Details);
    }

    [Fact]
    public void NothingReverted()
    {
        RevertResult result = Revert(RevertOutcome.NothingReverted, notReverted: new[] { Skip(Renames(1)[0], RevertSkipReason.ItemMissing) });

        ActionReport report = ActionText.ForRevert(result, En);

        Assert.Equal("No name could be restored; Revert stays available.", report.Status);
        Assert.Equal("Not restored:" + NL + @"C:\A\x_f0.txt: it is no longer where the rename left it.", report.Details);
    }

    [Fact]
    public void RevertNotStarted()
    {
        ActionReport report = ActionText.ForRevert(
            Revert(RevertOutcome.NotStarted, journalIssue: Journal(JournalWritePhase.BeforeStart, "Access is denied.")), En);

        Assert.Equal("Nothing was reverted, because the history could not be saved: Access is denied.", report.Status);
        Assert.Null(report.Details);
    }

    [Fact]
    public void NothingToRevert()
    {
        ActionReport report = ActionText.ForRevert(Revert(RevertOutcome.NothingToRevert), En);

        Assert.Equal("There is nothing to revert.", report.Status);
        Assert.Null(report.Details);
    }

    [Fact]
    public void Reverted_HistoryNotSavedAfter_AddsRevertNotAvailable() =>
        Assert.Equal(
            "3 names restored. Revert is not available, because the history could not be saved: The disk is full.",
            ActionText.ForRevert(Revert(RevertOutcome.Reverted, Renames(3), journalIssue: Journal(JournalWritePhase.AfterFinish)), En).Status);

    [Fact]
    public void RevertNeedsAttention_ListsStrandedThenSkipped()
    {
        CompletedRename[] entries = Renames(2);
        RevertResult result = Revert(
            RevertOutcome.NeedsAttention,
            notReverted: new[] { Skip(entries[1], RevertSkipReason.ItemMissing) },
            failure: Failure(FailureReason.SourceMissing, @"C:\A\x_f0.txt"),
            needsAttention: new[] { Stranded(@"C:\A\~ns-1.tmp", @"C:\A\x_f0.txt") });

        ActionReport report = ActionText.ForRevert(result, En);

        Assert.Equal("Revert failed and 1 item could not get its name back. Check it now.", report.Status);
        Assert.Equal(
            "x_f0.txt is no longer there." + NL + NL
            + "Not back to their old names:" + NL
            + @"C:\A\~ns-1.tmp  (was x_f0.txt)" + NL + NL
            + "Not restored:" + NL
            + @"C:\A\x_f1.txt: it is no longer where the rename left it.",
            report.Details);
    }

    [Fact]
    public void RevertNeedsAttention_Many_NoSkips()
    {
        RevertResult result = Revert(
            RevertOutcome.NeedsAttention,
            failure: Failure(FailureReason.SourceMissing, @"C:\A\x_f0.txt"),
            needsAttention: new[] { Stranded(@"C:\A\~ns-1.tmp", @"C:\A\x_f0.txt"), Stranded(@"C:\A\~ns-2.tmp", @"C:\A\x_f1.txt") });

        ActionReport report = ActionText.ForRevert(result, En);

        Assert.Equal("Revert failed and 2 items could not get their names back. Check them now.", report.Status);
        Assert.DoesNotContain("Not restored:", report.Details);
    }

    [Fact]
    public void ActionReport_RequiresAStatus()
    {
        Assert.Throws<ArgumentNullException>(() => new ActionReport(null!));
        Assert.Null(new ActionReport("x").Details);
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => ActionText.Hint(null!));
        Assert.Throws<ArgumentNullException>(() => ActionText.RevertDescription(null!, En));
        Assert.Throws<ArgumentNullException>(() => ActionText.ForRename(null!, En));
        Assert.Throws<ArgumentNullException>(() => ActionText.ForRevert(null!, En));
        Assert.Throws<ArgumentNullException>(() => new JournaledBatchRenamer(null!));
    }
}
