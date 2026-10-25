using System.IO;
using Xunit;

namespace NameShift.Disk.Tests;

/// <summary>NS-91 AC 6, 7: the history folder's pure part, with a fake temp path (never the user's own folder).</summary>
public sealed class JournaledRenamerFolderTests
{
    [Theory]
    [InlineData(@"C:\Users\u\AppData\Local")]
    [InlineData(@"C:\Users\u\AppData\Local\")]
    public void HistoryRootFor_Normal_IsTodaysPath(string profileFolder)
    {
        using var temp = new TempFolder();

        Assert.Equal(@"C:\Users\u\AppData\Local\NameShift\history", JournaledRenamer.HistoryRootFor(profileFolder, temp.Path));
    }

    [Fact]
    public void EmptyProfile_ConstructsWithoutThrowing()
    {
        using var temp = new TempFolder();
        string root = JournaledRenamer.HistoryRootFor("", temp.Path);

        var renamer = new JournaledRenamer(root);

        Assert.Equal(Path.Combine(temp.Path, "NameShift", "history"), root);
        Assert.Equal(LastBatchState.None, renamer.LoadLastBatch().State);
        Assert.Empty(temp.Listing());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData(@"relative\profile")]
    public void NoProfileFolder_HistoryUnderTemp(string? profileFolder)
    {
        using var temp = new TempFolder();

        Assert.Equal(Path.Combine(temp.Path, "NameShift", "history"), JournaledRenamer.HistoryRootFor(profileFolder, temp.Path));
        Assert.Empty(temp.Listing());
    }
}
