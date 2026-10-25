using System.IO;
using System.Linq;
using NameShift.Core;
using Xunit;

namespace NameShift.Disk.Tests;

public sealed class PartialRevertTests : JournalTestBase
{
    [Fact]
    public void Revert_OneFileRemoved_RevertsTheOthersAndKeepsIt()
    {
        RenameFiles(("a", "x"), ("b", "y"), ("c", "z"));
        var written = File.GetLastWriteTimeUtc(Temp.Full("y"));
        File.Delete(Temp.Full("y"));

        var result = Renamer.Revert();

        Assert.Equal(RevertOutcome.PartlyReverted, result.Outcome);
        var skipped = Assert.Single(result.NotReverted);
        Assert.Equal((Temp.Full("b"), Temp.Full("y")), (skipped.Entry.OldPath, skipped.Entry.NewPath));
        Assert.Equal(RevertSkipReason.ItemMissing, skipped.Reason);
        Assert.Null(skipped.BlockingPath);
        Assert.Equal(2, result.Reverted.Count);
        Assert.Equal(["a", "c"], Temp.Listing());
        var last = Renamer.LoadLastBatch();
        Assert.Equal(LastBatchState.Revertable, last.State);
        Assert.Equal(Now, last.Time);
        Assert.Equal([(Temp.Full("b"), Temp.Full("y"))], Pairs(last.Renames));

        // NS-90: the file comes back with the same size and last write time, so it is not Modified.
        Temp.File("y", "b");
        File.SetLastWriteTimeUtc(Temp.Full("y"), written);
        Assert.Equal(RevertOutcome.Reverted, Renamer.Revert().Outcome);
        Assert.Equal(["a", "b", "c"], Temp.Listing());
        Assert.Equal("b", Temp.Content("b"));
    }

    [Fact]
    public void Revert_OldNameTaken_NeverOverwrites()
    {
        RenameFiles(("a", "x"), ("b", "y"));
        Temp.File("a", "newcomer");

        var result = Renamer.Revert();

        Assert.Equal(RevertOutcome.PartlyReverted, result.Outcome);
        var skipped = Assert.Single(result.NotReverted);
        Assert.Equal(RevertSkipReason.NameTaken, skipped.Reason);
        Assert.Equal(Temp.Full("a"), skipped.BlockingPath);
        Assert.Equal("newcomer", Temp.Content("a"));
        Assert.Equal("a", Temp.Content("x"));
        Assert.Equal(["a", "b", "x"], Temp.Listing());
        Assert.DoesNotContain(Disk.Moves, move => move.To == Temp.Full("a"));
    }

    [Fact]
    public void Revert_ChainBlocked_SkipsTheDependentToo()
    {
        RenameFiles(("a", "b"), ("b", "c"));
        Temp.File("a", "newcomer");
        var journal = JournalBytes();
        Files.Writes.Clear();
        var moves = Disk.Moves.Count;

        var result = Renamer.Revert();

        Assert.Equal(RevertOutcome.NothingReverted, result.Outcome);
        // Journal order is the executor's: b reached c before a reached b.
        Assert.Equal(
            [
                (Temp.Full("b"), RevertSkipReason.WaitsForSkippedItem, Temp.Full("b")),
                (Temp.Full("a"), RevertSkipReason.NameTaken, Temp.Full("a")),
            ],
            result.NotReverted.Select(skip => (skip.Entry.OldPath, skip.Reason, skip.BlockingPath)));
        Assert.Equal(journal, JournalBytes());
        Assert.Empty(Files.Writes);
        Assert.Equal(moves, Disk.Moves.Count);
        Assert.Equal(["a", "b", "c"], Temp.Listing());
    }

    [Fact]
    public void Revert_FileLockedDuringRevert_RevertsTheOthers()
    {
        RenameFiles(("a", "x"), ("b", "y"), ("c", "z"));

        RevertResult result;
        using (new FileStream(Temp.Full("y"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            result = Renamer.Revert();
        }

        Assert.Equal(RevertOutcome.PartlyReverted, result.Outcome);
        var skipped = Assert.Single(result.NotReverted);
        Assert.Equal(Temp.Full("y"), skipped.Entry.NewPath);
        Assert.Equal(RevertSkipReason.RenameFailed, skipped.Reason);
        Assert.NotNull(skipped.SystemMessage);
        Assert.NotNull(skipped.HResult);
        Assert.Equal(32, skipped.HResult!.Value & 0xFFFF);
        Assert.Equal(["a", "c", "y"], Temp.Listing());
        Assert.Equal([(Temp.Full("b"), Temp.Full("y"))], Pairs(Renamer.LoadLastBatch().Renames));
        Assert.False(File.Exists(PendingPath));
    }

    [Fact]
    public void Revert_LockViolation_SkippedNotThrown()
    {
        RenameFiles(("a", "x"), ("b", "y"));
        const int LockViolation = unchecked((int)0x80070021);
        Disk.FailMove = (from, _) => from == Temp.Full("y") ? new IOException("locked", LockViolation) : null;

        var result = Renamer.Revert();

        Assert.Equal(RevertOutcome.PartlyReverted, result.Outcome);
        var skipped = Assert.Single(result.NotReverted);
        Assert.Equal((Temp.Full("b"), Temp.Full("y")), (skipped.Entry.OldPath, skipped.Entry.NewPath));
        Assert.Equal(RevertSkipReason.RenameFailed, skipped.Reason);
        Assert.Equal("locked", skipped.SystemMessage);
        Assert.Equal(LockViolation, skipped.HResult);
        Assert.Equal(["a", "y"], Temp.Listing());
        Assert.Equal([(Temp.Full("b"), Temp.Full("y"))], Pairs(Renamer.LoadLastBatch().Renames));
        Assert.False(File.Exists(PendingPath));
    }

    [Fact]
    public void Revert_FolderRevertedChildSkipped_RebasesTheChild()
    {
        Temp.Folder("X");
        Temp.File(@"X\a.txt");
        Run(Changed(0, "X", "Y", ItemKind.Folder), Changed(1, "a.txt", "b.txt", sub: "X"));
        Temp.File(@"Y\a.txt", "newcomer");

        var result = Renamer.Revert();

        Assert.Equal(RevertOutcome.PartlyReverted, result.Outcome);
        Assert.Equal(RevertSkipReason.NameTaken, Assert.Single(result.NotReverted).Reason);
        Assert.Equal(["X"], Temp.Listing());
        Assert.Equal(
            [(Temp.Full(@"X\a.txt"), Temp.Full(@"X\b.txt"))],
            Pairs(Renamer.LoadLastBatch().Renames));

        File.Delete(Temp.Full(@"X\a.txt"));
        Assert.Equal(RevertOutcome.Reverted, Renamer.Revert().Outcome);
        Assert.Equal(["a.txt"], Temp.Listing("X"));
        Assert.Equal("a.txt", Temp.Content(@"X\a.txt"));
    }

    [Fact]
    public void Revert_FolderSwapWithASkippedChild_RebasesOnce()
    {
        Temp.Folder("A");
        Temp.Folder("B");
        Temp.File(@"A\f.txt");
        Run(
            Changed(0, "A", "B", ItemKind.Folder),
            Changed(1, "B", "A", ItemKind.Folder),
            Changed(2, "f.txt", "g.txt", sub: "A"));
        Assert.Equal(["g.txt"], Temp.Listing("B"));
        Temp.File(@"B\f.txt", "newcomer");

        var result = Renamer.Revert();

        Assert.Equal(RevertOutcome.PartlyReverted, result.Outcome);
        Assert.Equal(RevertSkipReason.NameTaken, Assert.Single(result.NotReverted).Reason);
        Assert.Equal(["f.txt", "g.txt"], Temp.Listing("A"));
        Assert.Empty(Temp.Listing("B"));
        var remaining = Assert.Single(Renamer.LoadLastBatch().Renames);
        Assert.Equal(Temp.Full(@"A\g.txt"), remaining.NewPath);
        Assert.Equal(Temp.Full(@"A\f.txt"), remaining.OldPath);
    }

    [Fact]
    public void Revert_RollbackFails_NeedsAttentionAndKeepsTheJournal()
    {
        string[] originals = ["f1", "f2", "f3", "f4", "f5"];
        RenameFiles(originals.Select(name => (name, "g" + name.Substring(1))).ToArray());
        var journal = JournalBytes();
        Disk.Moves.Clear();
        FileStream? lockOnF2 = null;
        Disk.BeforeMove = (call, _, _) =>
        {
            if (call == 3)
            {
                lockOnF2 = new FileStream(Temp.Full("f2"), FileMode.Open, FileAccess.Read, FileShare.None);
            }
        };
        Disk.FailMove = (from, _) => from == Temp.Full("g3") ? new IOException("g3 failed") : null;

        RevertResult result;
        try
        {
            result = Renamer.Revert();
        }
        finally
        {
            lockOnF2?.Dispose();
        }

        Assert.Equal(RevertOutcome.NeedsAttention, result.Outcome);
        Assert.Equal("g3 failed", result.Failure!.SystemMessage);
        var stranded = Assert.Single(result.NeedsAttention);
        Assert.Equal(Temp.Full("f2"), stranded.CurrentPath);
        Assert.Equal(Temp.Full("g2"), stranded.OriginalPath);
        Assert.Empty(result.Reverted);
        Assert.Equal(journal, JournalBytes());
        Assert.False(File.Exists(PendingPath));
        Assert.Equal(["f2", "g1", "g3", "g4", "g5"], Temp.Listing());

        // A later Revert skips the stranded item as missing and never overwrites it.
        Disk.BeforeMove = null;
        Disk.FailMove = null;
        var again = Renamer.Revert();
        Assert.Equal(RevertOutcome.PartlyReverted, again.Outcome);
        Assert.Equal(RevertSkipReason.ItemMissing, Assert.Single(again.NotReverted).Reason);
        Assert.Equal(originals, Temp.Listing());
    }
}
