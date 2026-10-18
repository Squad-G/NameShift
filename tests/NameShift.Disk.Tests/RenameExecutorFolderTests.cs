using System.IO;
using System.Linq;
using NameShift.Core;
using Xunit;

namespace NameShift.Disk.Tests;

public sealed class RenameExecutorFolderTests : ExecutorTestBase
{
    /// <summary>X\a.txt to b.txt and X to Y, the folder listed first.</summary>
    private PlannedRename[] FolderAndItemInside()
    {
        Temp.Folder("X");
        Temp.File(@"X\a.txt");
        return [Rename("X", "Y", ItemKind.Folder), Rename("a.txt", "b.txt", sub: "X")];
    }

    [Fact]
    public void Execute_Folders_AreRenamedLikeFiles()
    {
        Temp.Folder("d1");
        Temp.File(@"d1\one.txt");
        Temp.Folder("d2");
        Temp.File(@"d2\two.txt");

        var result = Executor.Execute([Rename("d1", "d2", ItemKind.Folder), Rename("d2", "d1", ItemKind.Folder)]);

        Assert.Equal(ExecutionOutcome.Succeeded, result.Outcome);
        Assert.Equal(["d1", "d2"], Temp.Listing());
        Assert.Equal(["two.txt"], Temp.Listing("d1"));
        Assert.Equal(["one.txt"], Temp.Listing("d2"));
        Assert.Equal("one.txt", Temp.Content(@"d2\one.txt"));
    }

    [Fact]
    public void Execute_FolderAndAnItemInside_RenamesInsideFirst()
    {
        var result = Executor.Execute(FolderAndItemInside());

        Assert.Equal(ExecutionOutcome.Succeeded, result.Outcome);
        Assert.Equal(
            [(Temp.Full(@"X\a.txt"), Temp.Full(@"X\b.txt")), (Temp.Full("X"), Temp.Full("Y"))],
            Disk.Moves);
        Assert.Equal(
            [(Temp.Full(@"X\a.txt"), Temp.Full(@"Y\b.txt")), (Temp.Full("X"), Temp.Full("Y"))],
            result.Renamed.Select(r => (r.OldPath, r.NewPath)));
        Assert.Equal("a.txt", Temp.Content(@"Y\b.txt"));
    }

    [Fact]
    public void Execute_NestedRollback_RestoresParentThenChild()
    {
        PlannedRename[] renames = [.. FolderAndItemInside(), Rename("z", "w")];
        Temp.File("z");
        Disk.FailMove = (from, _) => from == Temp.Full("z") ? new IOException("z failed") : null;

        var result = Executor.Execute(renames);

        Assert.Equal(ExecutionOutcome.RolledBack, result.Outcome);
        Assert.Equal(Temp.Full("z"), result.Failure!.ItemPath);
        Assert.Equal(["X", "z"], Temp.Listing());
        Assert.Equal(["a.txt"], Temp.Listing("X"));
        Assert.Equal(
            [Temp.Full(@"X\a.txt"), Temp.Full("X"), Temp.Full("z"), Temp.Full("Y"), Temp.Full(@"X\b.txt")],
            Disk.Moves.Select(move => move.From));
    }

    [Fact]
    public void Execute_ChildUndoAfterParentUndoFails_ListsBothWithRealPaths()
    {
        PlannedRename[] renames = [.. FolderAndItemInside(), Rename("z", "w")];
        Temp.File("z");
        Disk.FailMove = (from, to) =>
            from == Temp.Full("z") || (from == Temp.Full("Y") && to == Temp.Full("X")) ? new IOException("refused") : null;

        var result = Executor.Execute(renames);

        Assert.Equal(ExecutionOutcome.NeedsAttention, result.Outcome);
        Assert.Equal(
            [(Temp.Full("Y"), Temp.Full("X")), (Temp.Full(@"Y\b.txt"), Temp.Full(@"X\a.txt"))],
            result.NeedsAttention.Select(s => (s.CurrentPath, s.OriginalPath)));
        Assert.Equal("refused", result.NeedsAttention[0].SystemMessage);
        Assert.Null(result.NeedsAttention[1].SystemMessage);
        Assert.Equal(["Y", "z"], Temp.Listing());
        Assert.Equal("a.txt", Temp.Content(@"Y\b.txt"));
    }
}
