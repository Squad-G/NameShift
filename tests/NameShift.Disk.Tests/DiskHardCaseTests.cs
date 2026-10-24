using System;
using System.IO;
using NameShift.Core;
using Xunit;

namespace NameShift.Disk.Tests;

/// <summary>NS-67: read-only items, items in use by another program and a drive that disappears mid-batch, through
/// JournaledRenamer on a real temp folder (faults injected with HookedFileSystem).</summary>
public sealed class DiskHardCaseTests : JournalTestBase
{
    private const int PathNotFound = unchecked((int)0x80070003);
    private const int SharingViolation = unchecked((int)0x80070020);
    private const int LockViolation = unchecked((int)0x80070021);
    private const int AccessDenied = unchecked((int)0x80070005);

    private static IOException Gone() => new("gone", PathNotFound);

    // AC 1: read-only items

    [Fact]
    public void ReadOnlyFile_RenamedAndReverted_StaysReadOnly()
    {
        Temp.File("a.txt");
        File.SetAttributes(Temp.Full("a.txt"), FileAttributes.ReadOnly);

        var result = Run(Changed(0, "a.txt", "b.txt"));

        Assert.Equal(ExecutionOutcome.Succeeded, result.Execution!.Outcome);
        Assert.Null(result.JournalIssue);
        Assert.Equal(["b.txt"], Temp.Listing());
        Assert.True(File.GetAttributes(Temp.Full("b.txt")).HasFlag(FileAttributes.ReadOnly));
        Assert.Equal([(Temp.Full("a.txt"), Temp.Full("b.txt"))], Pairs(Renamer.LoadLastBatch().Renames));

        Assert.Equal(RevertOutcome.Reverted, Renamer.Revert().Outcome);
        Assert.Equal(["a.txt"], Temp.Listing());
        Assert.True(File.GetAttributes(Temp.Full("a.txt")).HasFlag(FileAttributes.ReadOnly));
        Assert.Equal("a.txt", Temp.Content("a.txt"));
    }

    [Fact]
    public void ReadOnlyFolder_RenamedAndReverted_StaysReadOnly()
    {
        Temp.Folder("X");
        Temp.File(@"X\in.txt");
        var folder = new DirectoryInfo(Temp.Full("X"));
        folder.Attributes |= FileAttributes.ReadOnly;

        var result = Run(Changed(0, "X", "Y", ItemKind.Folder));

        Assert.Equal(ExecutionOutcome.Succeeded, result.Execution!.Outcome);
        Assert.Null(result.JournalIssue);
        Assert.Equal(["Y"], Temp.Listing());
        Assert.True(new DirectoryInfo(Temp.Full("Y")).Attributes.HasFlag(FileAttributes.ReadOnly));
        Assert.Equal([(Temp.Full("X"), Temp.Full("Y"))], Pairs(Renamer.LoadLastBatch().Renames));

        Assert.Equal(RevertOutcome.Reverted, Renamer.Revert().Outcome);
        Assert.Equal(["X"], Temp.Listing());
        Assert.True(new DirectoryInfo(Temp.Full("X")).Attributes.HasFlag(FileAttributes.ReadOnly));
        Assert.Equal(["in.txt"], Temp.Listing("X"));
    }

    // AC 2: an item in use

    [Fact]
    public void LockedFile_RollsBack_ItemInUse_NoJournal()
    {
        Temp.File("a");
        Temp.File("b");
        Temp.File("c");

        RenameRunResult result;
        using (new FileStream(Temp.Full("b"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            result = Run(Changed(0, "a", "x"), Changed(1, "b", "y"), Changed(2, "c", "z"));
        }

        Assert.Equal(ExecutionOutcome.RolledBack, result.Execution!.Outcome);
        var failure = result.Execution.Failure!;
        Assert.Equal(FailureReason.ItemInUse, failure.Reason);
        Assert.Equal(Temp.Full("b"), failure.ItemPath);
        Assert.NotNull(failure.SystemMessage);
        Assert.Equal(32, failure.HResult!.Value & 0xFFFF);
        Assert.Equal(["a", "b", "c"], Temp.Listing());
        Assert.Equal("a", Temp.Content("a"));
        Assert.False(File.Exists(LastBatchPath));
        Assert.False(File.Exists(PendingPath));
    }

    [Fact]
    public void LockViolation_IsItemInUse()
    {
        Temp.File("a");
        Temp.File("b");
        Disk.FailMove = (_, _) => Disk.Moves.Count == 2 ? new IOException("locked", LockViolation) : null;

        var result = Run(Changed(0, "a", "x"), Changed(1, "b", "y"));

        Assert.Equal(ExecutionOutcome.RolledBack, result.Execution!.Outcome);
        Assert.Equal(FailureReason.ItemInUse, result.Execution.Failure!.Reason);
        Assert.Equal(Temp.Full("b"), result.Execution.Failure.ItemPath);
        Assert.Equal(LockViolation, result.Execution.Failure.HResult);
        Assert.Equal(["a", "b"], Temp.Listing());
        Assert.False(File.Exists(LastBatchPath));
    }

    [Theory]
    [InlineData(AccessDenied)]
    [InlineData(PathNotFound)]
    public void OtherFailure_StaysRenameFailed(int hResult)
    {
        Temp.File("a");
        Temp.File("b");
        Disk.FailMove = (_, _) => Disk.Moves.Count == 2 ? new IOException("failed", hResult) : null;

        var result = Run(Changed(0, "a", "x"), Changed(1, "b", "y"));

        Assert.Equal(ExecutionOutcome.RolledBack, result.Execution!.Outcome);
        Assert.Equal(FailureReason.RenameFailed, result.Execution.Failure!.Reason);
        Assert.Equal("failed", result.Execution.Failure.SystemMessage);
        Assert.Equal(hResult, result.Execution.Failure.HResult);
        Assert.Equal(["a", "b"], Temp.Listing());
    }

    [Fact]
    public void TargetAppearsWithSharingViolation_StaysTargetInTheWay()
    {
        Temp.File("a");
        Disk.FailMove = (_, to) =>
        {
            File.WriteAllText(to, "intruder");
            return new IOException("in use", SharingViolation);
        };

        var result = Run(Changed(0, "a", "x"));

        Assert.Equal(ExecutionOutcome.RolledBack, result.Execution!.Outcome);
        Assert.Equal(FailureReason.TargetInTheWay, result.Execution.Failure!.Reason);
        Assert.Equal(Temp.Full("x"), result.Execution.Failure.BlockingPath);
        Assert.Equal("a", Temp.Content("a"));
        Assert.Equal("intruder", Temp.Content("x"));
    }

    // AC 4: the first item restored, the third never touched

    [Fact]
    public void ThreeItems_SecondLocked_FirstRestored_ThirdUntouched()
    {
        Temp.File("a");
        Temp.File("b");
        Temp.File("c");

        RenameRunResult result;
        using (new FileStream(Temp.Full("b"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            result = Run(Changed(0, "a", "x"), Changed(1, "b", "y"), Changed(2, "c", "z"));
        }

        Assert.Equal(ExecutionOutcome.RolledBack, result.Execution!.Outcome);
        Assert.Equal(["a", "b", "c"], Temp.Listing());
        Assert.Equal("a", Temp.Content("a"));
        Assert.Equal("c", Temp.Content("c"));
        Assert.Equal(
            [(Temp.Full("a"), Temp.Full("x")), (Temp.Full("b"), Temp.Full("y")), (Temp.Full("x"), Temp.Full("a"))],
            Disk.Moves);
    }

    // AC 5 and 6: the drive disappears

    [Fact]
    public void DriveGone_OnSecondMove_RollsBack()
    {
        Temp.File("a");
        Temp.File("b");
        Temp.File("c");
        Disk.FailMove = (_, _) => Disk.Moves.Count == 2 ? Gone() : null;

        var result = Run(Changed(0, "a", "x"), Changed(1, "b", "y"), Changed(2, "c", "z"));

        Assert.Equal(ExecutionOutcome.RolledBack, result.Execution!.Outcome);
        Assert.Equal(FailureReason.RenameFailed, result.Execution.Failure!.Reason);
        Assert.Equal(PathNotFound, result.Execution.Failure.HResult);
        Assert.Equal(
            [(Temp.Full("a"), Temp.Full("x")), (Temp.Full("b"), Temp.Full("y")), (Temp.Full("x"), Temp.Full("a"))],
            Disk.Moves);
        Assert.Equal(["a", "b", "c"], Temp.Listing());
        Assert.Equal("a", Temp.Content("a"));
        Assert.False(File.Exists(LastBatchPath));
        Assert.False(File.Exists(PendingPath));
    }

    [Fact]
    public void DriveGone_UndoAlsoFails_NeedsAttention()
    {
        Temp.File("a");
        Temp.File("b");
        Temp.File("c");
        Disk.FailMove = (_, _) => Disk.Moves.Count >= 2 ? Gone() : null;

        var result = Run(Changed(0, "a", "x"), Changed(1, "b", "y"), Changed(2, "c", "z"));

        Assert.Equal(ExecutionOutcome.NeedsAttention, result.Execution!.Outcome);
        Assert.Equal(FailureReason.RenameFailed, result.Execution.Failure!.Reason);
        var stranded = Assert.Single(result.Execution.NeedsAttention);
        Assert.Equal(Temp.Full("x"), stranded.CurrentPath);
        Assert.Equal(Temp.Full("a"), stranded.OriginalPath);
        Assert.Equal("gone", stranded.SystemMessage);
        Assert.Equal(
            [(Temp.Full("a"), Temp.Full("x")), (Temp.Full("b"), Temp.Full("y")), (Temp.Full("x"), Temp.Full("a"))],
            Disk.Moves);
        var last = Renamer.LoadLastBatch();
        Assert.Equal(LastBatchState.Interrupted, last.State);
        Assert.False(last.CanRevert);
        Assert.Equal(["b", "c", "x"], Temp.Listing());
        Assert.Equal("a", Temp.Content("x"));
        Assert.Equal("b", Temp.Content("b"));
        Assert.Equal("c", Temp.Content("c"));
    }

    // AC 7: unchanged items with long names are never moved nor journaled

    [Fact]
    public void UnchangedLongNames_NoMove_NotJournaled()
    {
        Temp.File("a.txt");
        var long220 = new string('l', 216) + ".txt";
        var long250 = new string('m', 246) + ".txt";

        var result = Renamer.Rename(
            [
                Changed(0, "a.txt", "b.txt"),
                new RenameResult(new RenameItem(long220, Temp.Path), 1, long220, RenameStatus.Unchanged, null),
                new RenameResult(new RenameItem(long250, Temp.Path), 2, long250, RenameStatus.Unchanged, null),
            ],
            NoConflicts(3));

        Assert.Equal(220, long220.Length);
        Assert.Equal(250, long250.Length);
        Assert.Equal(ExecutionOutcome.Succeeded, result.Execution!.Outcome);
        Assert.Null(result.JournalIssue);
        Assert.Equal([(Temp.Full("a.txt"), Temp.Full("b.txt"))], Disk.Moves);
        Assert.Equal([(Temp.Full("a.txt"), Temp.Full("b.txt"))], Pairs(Renamer.LoadLastBatch().Renames));
        Assert.Equal(["b.txt"], Temp.Listing());
    }
}
