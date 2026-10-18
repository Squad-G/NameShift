using System.IO;
using System.Linq;
using System.Text;
using NameShift.Core;
using Xunit;

namespace NameShift.Disk.Tests;

public sealed class RevertTests : JournalTestBase
{
    private RevertResult RevertAll()
    {
        var result = Renamer.Revert();
        Assert.Equal(RevertOutcome.Reverted, result.Outcome);
        Assert.Empty(result.NotReverted);
        Assert.Null(result.JournalIssue);
        Assert.Equal(LastBatchState.Reverted, Renamer.LoadLastBatch().State);
        return result;
    }

    [Fact]
    public void Revert_AfterABatch_RestoresAndDisablesRevert()
    {
        RenameFiles(("a", "x"), ("b", "y"), ("c", "z"));

        var result = RevertAll();

        Assert.Equal(["a", "b", "c"], Temp.Listing());
        Assert.Equal(["a", "b", "c"], new[] { "a", "b", "c" }.Select(Temp.Content));
        Assert.Equal(
            [(Temp.Full("x"), Temp.Full("a")), (Temp.Full("y"), Temp.Full("b")), (Temp.Full("z"), Temp.Full("c"))],
            Pairs(result.Reverted));
        var last = Renamer.LoadLastBatch();
        Assert.False(last.CanRevert);
        Assert.Equal(Now, last.Time);
        Assert.Empty(last.Renames);
        Assert.Empty(last.Folders);
        Assert.DoesNotContain(Path.GetFileName(Temp.Path), Encoding.UTF8.GetString(JournalBytes()));
        Assert.Equal(["last-batch.json"], HistoryListing());
    }

    [Fact]
    public void Revert_Twice_SecondDoesNothing()
    {
        RenameFiles(("a", "x"));
        RevertAll();
        var moves = Disk.Moves.Count;
        var journal = JournalBytes();

        var second = Renamer.Revert();

        Assert.Equal(RevertOutcome.NothingToRevert, second.Outcome);
        Assert.Equal(moves, Disk.Moves.Count);
        Assert.Equal(journal, JournalBytes());
        Assert.Equal(["a"], Temp.Listing());
    }

    [Fact]
    public void Revert_AfterTwoBatches_DoesNotExposeTheFirst()
    {
        RenameFiles(("a", "b"));
        RenameFiles(("b", "c"));

        RevertAll();

        Assert.Equal(["b"], Temp.Listing());
        Assert.Equal("a", Temp.Content("b"));
        Assert.Equal(RevertOutcome.NothingToRevert, Renamer.Revert().Outcome);
        Assert.Equal(["b"], Temp.Listing());
    }

    [Fact]
    public void Revert_AfterRestart_Works()
    {
        RenameFiles(("a", "x"), ("b", "y"));

        var restarted = NewRenamer();

        Assert.Equal(LastBatchState.Revertable, restarted.LoadLastBatch().State);
        Assert.Equal(RevertOutcome.Reverted, restarted.Revert().Outcome);
        Assert.Equal(["a", "b"], Temp.Listing());
    }

    [Fact]
    public void Revert_Chain_Restores()
    {
        RenameFiles(("a", "b"), ("b", "c"));

        RevertAll();

        Assert.Equal(["a", "b"], Temp.Listing());
        Assert.Equal("a", Temp.Content("a"));
        Assert.Equal("b", Temp.Content("b"));
    }

    [Fact]
    public void Revert_Swap_RestoresContents()
    {
        RenameFiles(("a", "b"), ("b", "a"));
        Assert.Equal("a", Temp.Content("b"));
        Disk.Moves.Clear();

        RevertAll();

        Assert.Equal(["a", "b"], Temp.Listing());
        Assert.Equal("a", Temp.Content("a"));
        Assert.Equal("b", Temp.Content("b"));
        Assert.Contains(Disk.Moves, move => move.To.Contains($"~ns-{Token}-"));
    }

    [Fact]
    public void Revert_CaseOnlyFile_Restores()
    {
        RenameFiles(("photo.jpg", "Photo.jpg"));
        Assert.Equal(["Photo.jpg"], Temp.Listing());
        Disk.Moves.Clear();

        RevertAll();

        Assert.Equal(["photo.jpg"], Temp.Listing());
        Assert.Contains(Disk.Moves, move => move.To.Contains($"~ns-{Token}-"));
    }

    [Fact]
    public void Revert_CaseOnlyFolder_Restores()
    {
        Temp.Folder("dir");
        Temp.File(@"dir\inside.txt");
        Run(Changed(0, "dir", "Dir", ItemKind.Folder));
        Assert.Equal(["Dir"], Temp.Listing());
        Disk.Moves.Clear();

        RevertAll();

        Assert.Equal(["dir"], Temp.Listing());
        Assert.Equal(["inside.txt"], Temp.Listing("dir"));
        Assert.Contains(Disk.Moves, move => move.To.Contains($"~ns-{Token}-"));
    }

    [Fact]
    public void Revert_FolderWithAFileInside_Restores()
    {
        Temp.Folder("X");
        Temp.File(@"X\a.txt");
        Run(Changed(0, "X", "Y", ItemKind.Folder), Changed(1, "a.txt", "b.txt", sub: "X"));

        RevertAll();

        Assert.Equal(["X"], Temp.Listing());
        Assert.Equal(["a.txt"], Temp.Listing("X"));
        Assert.Equal("a.txt", Temp.Content(@"X\a.txt"));
    }
}
