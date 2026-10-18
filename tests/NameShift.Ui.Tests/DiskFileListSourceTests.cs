using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NameShift.Core;
using NameShift.Ui.Files;
using NameShift.Ui.Folders;
using Xunit;

namespace NameShift.Ui.Tests;

public sealed class DiskFileListSourceTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "NameShift.Ui.Tests", Guid.NewGuid().ToString("N"));
    private readonly DiskFileListSource _source = new DiskFileListSource();

    public DiskFileListSourceTests()
    {
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        foreach (string path in Directory.GetFileSystemEntries(_temp, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        Directory.Delete(_temp, true);
    }

    private string MakeFile(string name, FileAttributes attributes = FileAttributes.Normal)
    {
        string path = Path.Combine(_temp, name);
        File.WriteAllText(path, "");
        File.SetAttributes(path, attributes);
        return path;
    }

    private string MakeFolder(string name, FileAttributes attributes = FileAttributes.Directory)
    {
        string path = Path.Combine(_temp, name);
        Directory.CreateDirectory(path);
        File.SetAttributes(path, attributes);
        return path;
    }

    [Fact]
    public void ListEntries_ReturnsFilesAndSubFolders_WithTheirFacts()
    {
        string file = Path.Combine(_temp, "a.txt");
        File.WriteAllText(file, "hello");
        var created = new DateTime(2025, 3, 4, 5, 6, 7, DateTimeKind.Local);
        var modified = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Local);
        File.SetCreationTime(file, created);
        File.SetLastWriteTime(file, modified);
        File.SetAttributes(file, FileAttributes.ReadOnly);
        MakeFolder("v1.2");

        FileListing listing = _source.ListEntries(_temp);

        Assert.Null(listing.Problem);
        Assert.Equal(2, listing.Items.Count);
        RenameItem item = listing.Items.Single(i => i.Kind == ItemKind.File);
        Assert.Equal("a.txt", item.Name);
        Assert.Equal(_temp, item.FolderPath);
        Assert.Equal(ItemAttributes.ReadOnly, item.Attributes);
        Assert.Equal(5, item.Size);
        Assert.Equal(created, item.Created);
        Assert.Equal(modified, item.Modified);
        RenameItem folder = listing.Items.Single(i => i.Kind == ItemKind.Folder);
        Assert.Equal(0, folder.Size);
        NameParts parts = NameParts.Split(folder.Name, ItemKind.Folder);
        Assert.Equal("v1.2", parts.BaseName);
        Assert.Equal("", parts.Extension);
    }

    [Fact]
    public void ListEntries_SkipsHiddenAndSystemEntries()
    {
        MakeFile("visible.txt");
        MakeFolder("visible-folder");
        MakeFile("hidden.txt", FileAttributes.Hidden);
        MakeFile("system.sys", FileAttributes.System);
        MakeFolder("hidden-folder", FileAttributes.Directory | FileAttributes.Hidden);
        MakeFolder("system-folder", FileAttributes.Directory | FileAttributes.System);

        FileListing listing = _source.ListEntries(_temp);

        Assert.Equal(new[] { "visible-folder", "visible.txt" }, listing.Items.Select(i => i.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData(FileAttributes.Normal, true)]
    [InlineData(FileAttributes.ReadOnly, true)]
    [InlineData(FileAttributes.Archive, true)]
    [InlineData(FileAttributes.Hidden, false)]
    [InlineData(FileAttributes.System, false)]
    [InlineData(FileAttributes.Hidden | FileAttributes.System, false)]
    public void IsShown_HiddenOrSystem_IsFalse(FileAttributes attributes, bool expected)
    {
        Assert.Equal(expected, DiskFileListSource.IsShown(attributes));
    }

    [Fact]
    public void ListEntries_MissingFolder_IsNotFound()
    {
        FileListing listing = _source.ListEntries(Path.Combine(_temp, "missing"));

        Assert.Equal(FolderProblem.NotFound, listing.Problem);
        Assert.Empty(listing.Items);
    }

    [Fact]
    public void ListEntries_OnAFile_IsAProblem_NotACrash()
    {
        FileListing listing = _source.ListEntries(MakeFile("plain.txt"));

        Assert.NotNull(listing.Problem);
        Assert.Empty(listing.Items);
    }

    [Fact]
    public void ListEntries_TenThousandFiles_UnderTwoSeconds()
    {
        for (int i = 0; i < 10000; i++)
        {
            File.WriteAllBytes(Path.Combine(_temp, "file" + i + ".txt"), Array.Empty<byte>());
        }

        var watch = Stopwatch.StartNew();
        FileListing listing = _source.ListEntries(_temp);
        var sorted = FileListOrder.Sort(listing.Items);
        watch.Stop();

        Assert.Equal(10000, sorted.Count);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), watch.Elapsed.ToString());
    }
}