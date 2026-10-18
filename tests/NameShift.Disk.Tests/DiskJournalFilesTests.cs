using System;
using System.IO;
using System.Text;
using Xunit;

namespace NameShift.Disk.Tests;

public sealed class DiskJournalFilesTests : IDisposable
{
    private readonly TempFolder history = new();
    private readonly DiskJournalFiles files = new();

    private string Folder => history.Full(@"history\deep");

    private string FilePath => Path.Combine(Folder, "f.json");

    [Fact]
    public void WriteAtomic_NewFile_CreatesTheFolderAndFile()
    {
        files.WriteAtomic(FilePath, Encoding.UTF8.GetBytes("one"));

        Assert.Equal("one", File.ReadAllText(FilePath));
        Assert.Equal(["f.json"], history.Listing(@"history\deep"));
    }

    [Fact]
    public void WriteAtomic_Existing_Replaces()
    {
        files.WriteAtomic(FilePath, Encoding.UTF8.GetBytes("one"));

        files.WriteAtomic(FilePath, Encoding.UTF8.GetBytes("two"));

        Assert.Equal("two", File.ReadAllText(FilePath));
        Assert.Equal(["f.json"], history.Listing(@"history\deep"));
    }

    [Fact]
    public void WriteAtomic_DestinationLocked_KeepsTheOldFileAndLeavesNoTemp()
    {
        files.WriteAtomic(FilePath, Encoding.UTF8.GetBytes("old"));

        using (new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.ThrowsAny<Exception>(() => files.WriteAtomic(FilePath, Encoding.UTF8.GetBytes("new")));
        }

        Assert.Equal("old", File.ReadAllText(FilePath));
        Assert.Equal(["f.json"], history.Listing(@"history\deep"));
    }

    [Fact]
    public void Delete_Missing_IsNotAnError()
    {
        files.Delete(FilePath);

        Assert.False(files.Exists(FilePath));
    }

    public void Dispose() => history.Dispose();
}
