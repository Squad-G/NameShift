using System.Linq;
using NameShift.Core;
using Xunit;

namespace NameShift.Disk.Tests;

public sealed class RenameExecutorOrderingTests : ExecutorTestBase
{
    [Fact]
    public void Execute_Chain_RenamesTheFreeTargetFirst()
    {
        Temp.File("a");
        Temp.File("b");

        var result = Executor.Execute([Rename("a", "b"), Rename("b", "c")]);

        Assert.Equal(ExecutionOutcome.Succeeded, result.Outcome);
        Assert.Equal([(Temp.Full("b"), Temp.Full("c")), (Temp.Full("a"), Temp.Full("b"))], Disk.Moves);
        Assert.Equal(["b", "c"], Temp.Listing());
        Assert.Equal("a", Temp.Content("b"));
        Assert.Equal("b", Temp.Content("c"));
        Assert.Equal(
            [(Temp.Full("b"), Temp.Full("c")), (Temp.Full("a"), Temp.Full("b"))],
            result.Renamed.Select(r => (r.OldPath, r.NewPath)));
    }

    [Fact]
    public void Execute_LongChainInReverseOrder_NeverOverwrites()
    {
        foreach (var name in new[] { "1", "2", "3", "4" })
        {
            Temp.File(name);
        }

        var result = Executor.Execute([Rename("1", "2"), Rename("2", "3"), Rename("3", "4"), Rename("4", "5")]);

        Assert.Equal(ExecutionOutcome.Succeeded, result.Outcome);
        Assert.Equal(4, Disk.Moves.Count);
        Assert.DoesNotContain(Disk.Moves, move => move.To.Contains("~ns-"));
        Assert.Equal(["2", "3", "4", "5"], Temp.Listing());
        Assert.Equal(["1", "2", "3", "4"], new[] { "2", "3", "4", "5" }.Select(Temp.Content));
    }

    [Fact]
    public void Execute_Swap_GoesThroughATempNameAndKeepsContents()
    {
        Temp.File("a");
        Temp.File("b");

        var result = Executor.Execute([Rename("a", "b"), Rename("b", "a")]);

        Assert.Equal(ExecutionOutcome.Succeeded, result.Outcome);
        Assert.Equal(
            [(Temp.Full("a"), TempName(1)), (Temp.Full("b"), Temp.Full("a")), (TempName(1), Temp.Full("b"))],
            Disk.Moves);
        Assert.Equal(["a", "b"], Temp.Listing());
        Assert.Equal("b", Temp.Content("a"));
        Assert.Equal("a", Temp.Content("b"));
    }

    [Fact]
    public void Execute_Rotation_OfThree()
    {
        Temp.File("a");
        Temp.File("b");
        Temp.File("c");

        var result = Executor.Execute([Rename("a", "b"), Rename("b", "c"), Rename("c", "a")]);

        Assert.Equal(ExecutionOutcome.Succeeded, result.Outcome);
        Assert.Equal(4, Disk.Moves.Count);
        Assert.Single(Disk.Moves, move => move.To.Contains("~ns-"));
        Assert.Equal(["a", "b", "c"], Temp.Listing());
        Assert.Equal(["c", "a", "b"], new[] { "a", "b", "c" }.Select(Temp.Content));
    }

    [Fact]
    public void Execute_CaseOnlyFileChange_ChangesTheNameOnDisk()
    {
        Temp.File("photo.jpg");

        var result = Executor.Execute([Rename("photo.jpg", "Photo.jpg")]);

        Assert.Equal(ExecutionOutcome.Succeeded, result.Outcome);
        Assert.Equal(
            [(Temp.Full("photo.jpg"), TempName(1)), (TempName(1), Temp.Full("Photo.jpg"))],
            Disk.Moves);
        Assert.Equal(["Photo.jpg"], Temp.Listing());
        Assert.Equal("photo.jpg", Temp.Content("Photo.jpg"));
    }

    [Fact]
    public void Execute_CaseOnlyFolderChange_ChangesTheNameOnDisk()
    {
        Temp.Folder("photos");
        Temp.File(@"photos\inside.txt");

        var result = Executor.Execute([Rename("photos", "Photos", ItemKind.Folder)]);

        Assert.Equal(ExecutionOutcome.Succeeded, result.Outcome);
        Assert.Equal(["Photos"], Temp.Listing());
        Assert.Equal(["inside.txt"], Temp.Listing("Photos"));
        Assert.Equal("inside.txt", Temp.Content(@"Photos\inside.txt"));
    }

    [Fact]
    public void Execute_SwapInAnotherCase_Works()
    {
        Temp.File("a.txt");
        Temp.File("b.txt");

        var result = Executor.Execute([Rename("a.txt", "B.txt"), Rename("b.txt", "A.txt")]);

        Assert.Equal(ExecutionOutcome.Succeeded, result.Outcome);
        Assert.Equal(["A.txt", "B.txt"], Temp.Listing());
        Assert.Equal("b.txt", Temp.Content("A.txt"));
        Assert.Equal("a.txt", Temp.Content("B.txt"));
    }

    [Fact]
    public void Execute_TempNameAlreadyTaken_PicksTheNextOne()
    {
        Temp.File("a");
        Temp.File("b");
        Temp.File($"~ns-{Token}-1", "keep");

        var result = Executor.Execute([Rename("a", "b"), Rename("b", "a")]);

        Assert.Equal(ExecutionOutcome.Succeeded, result.Outcome);
        Assert.Equal((Temp.Full("a"), TempName(2)), Disk.Moves[0]);
        Assert.DoesNotContain(Disk.Moves, move => move.From == TempName(1) || move.To == TempName(1));
        Assert.Equal("keep", Temp.Content($"~ns-{Token}-1"));
        Assert.Equal(["a", "b", $"~ns-{Token}-1"], Temp.Listing());
    }

    [Fact]
    public void Execute_Success_LeavesNoTempName()
    {
        Temp.File("a");
        Temp.File("b");
        Temp.File("photo.jpg");

        var result = Executor.Execute([Rename("a", "b"), Rename("b", "a"), Rename("photo.jpg", "PHOTO.JPG")]);

        Assert.Equal(ExecutionOutcome.Succeeded, result.Outcome);
        Assert.DoesNotContain(Temp.Listing(), name => name.StartsWith("~ns-", System.StringComparison.Ordinal));
        Assert.Equal(["PHOTO.JPG", "a", "b"], Temp.Listing());
    }
}
