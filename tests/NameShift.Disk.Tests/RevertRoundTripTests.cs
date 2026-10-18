using System.Linq;
using NameShift.Core;
using Xunit;

namespace NameShift.Disk.Tests;

/// <summary>The executor's output feeds NS-9's Revert: executing the inverses restores the start.</summary>
public sealed class RevertRoundTripTests : ExecutorTestBase
{
    private void ExecuteThenRevert(params PlannedRename[] renames)
    {
        var done = Executor.Execute(renames);
        Assert.Equal(ExecutionOutcome.Succeeded, done.Outcome);

        var reverted = Executor.Execute(done.Renamed.Select(r => r.Inverse()).ToList());

        Assert.Equal(ExecutionOutcome.Succeeded, reverted.Outcome);
        Assert.Equal(
            done.Renamed.Select(r => (r.NewPath, r.OldPath)).OrderBy(p => p.OldPath, System.StringComparer.Ordinal),
            reverted.Renamed.Select(r => (r.OldPath, r.NewPath)).OrderBy(p => p.NewPath, System.StringComparer.Ordinal));
    }

    [Fact]
    public void Inverse_OfAChain_RestoresTheOldNames()
    {
        Temp.File("a");
        Temp.File("b");

        ExecuteThenRevert(Rename("a", "b"), Rename("b", "c"));

        Assert.Equal(["a", "b"], Temp.Listing());
        Assert.Equal("a", Temp.Content("a"));
        Assert.Equal("b", Temp.Content("b"));
    }

    [Fact]
    public void Inverse_OfASwap_Restores()
    {
        Temp.File("a");
        Temp.File("b");

        ExecuteThenRevert(Rename("a", "b"), Rename("b", "a"));

        Assert.Equal(["a", "b"], Temp.Listing());
        Assert.Equal("a", Temp.Content("a"));
        Assert.Equal("b", Temp.Content("b"));
    }

    [Fact]
    public void Inverse_OfACaseOnlyChange_Restores()
    {
        Temp.File("photo.jpg");

        ExecuteThenRevert(Rename("photo.jpg", "Photo.jpg"));

        Assert.Equal(["photo.jpg"], Temp.Listing());
    }

    [Fact]
    public void Inverse_OfAFolderAndAnItemInside_Restores()
    {
        Temp.Folder("X");
        Temp.File(@"X\a.txt");

        ExecuteThenRevert(Rename("X", "Y", ItemKind.Folder), Rename("a.txt", "b.txt", sub: "X"));

        Assert.Equal(["X"], Temp.Listing());
        Assert.Equal(["a.txt"], Temp.Listing("X"));
        Assert.Equal("a.txt", Temp.Content(@"X\a.txt"));
    }
}
