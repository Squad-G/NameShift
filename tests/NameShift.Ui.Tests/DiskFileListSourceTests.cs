using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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

    /// <summary>A regression guard against an accidental quadratic listing or sort, not the product promise: NS-51's
    /// "lists in under 2 seconds" stays a hand check. The fastest of three warm runs normally takes 15 to 30 ms; a
    /// quadratic sort of 10,000 entries takes about 5 s, so 3 s leaves wide headroom and still catches it.</summary>
    private static readonly TimeSpan GenerousBudget = TimeSpan.FromSeconds(3);

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

    private void MakeSixEntries()
    {
        MakeFile("visible.txt");
        MakeFolder("visible-folder");
        MakeFile("hidden.txt", FileAttributes.Hidden);
        MakeFile("system.sys", FileAttributes.System);
        MakeFolder("hidden-folder", FileAttributes.Directory | FileAttributes.Hidden);
        MakeFolder("system-folder", FileAttributes.Directory | FileAttributes.System);
    }

    [Fact]
    public void ListEntries_ReturnsHiddenAndSystemEntries_WithTheirAttributes()
    {
        MakeSixEntries();

        FileListing listing = _source.ListEntries(_temp);

        Assert.Null(listing.Problem);
        Assert.Equal(6, listing.Items.Count);
        ItemAttributes AttributesOf(string name) => listing.Items.Single(i => i.Name == name).Attributes;
        Assert.Equal(ItemAttributes.None, AttributesOf("visible.txt"));
        Assert.Equal(ItemAttributes.None, AttributesOf("visible-folder"));
        Assert.Equal(ItemAttributes.Hidden, AttributesOf("hidden.txt"));
        Assert.Equal(ItemAttributes.System, AttributesOf("system.sys"));
        Assert.Equal(ItemAttributes.Hidden, AttributesOf("hidden-folder"));
        Assert.Equal(ItemAttributes.System, AttributesOf("system-folder"));
        Assert.Equal(ItemKind.Folder, listing.Items.Single(i => i.Name == "hidden-folder").Kind);
        Assert.Equal(ItemKind.File, listing.Items.Single(i => i.Name == "system.sys").Kind);
        Assert.All(listing.Items, item => Assert.Equal(_temp, item.FolderPath));
    }

    [Fact]
    public void ListEntries_ExistingNamesByFolder_EveryEntry()
    {
        MakeSixEntries();

        FileListing listing = _source.ListEntries(_temp);

        Assert.Equal(new[] { _temp }, listing.ExistingNamesByFolder.Keys);
        Assert.Equal(
            new[] { "hidden-folder", "hidden.txt", "system-folder", "system.sys", "visible-folder", "visible.txt" },
            listing.ExistingNamesByFolder[_temp].OrderBy(n => n, StringComparer.Ordinal));
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
    public void ListEntries_TenThousandFiles_StaysFast()
    {
        for (int i = 0; i < 10000; i++)
        {
            File.WriteAllBytes(Path.Combine(_temp, "file" + i + ".txt"), Array.Empty<byte>());
        }

        // Warm-up, untimed: JIT and the file-system cache. Its result is checked too.
        Assert.Equal(10000, ListAndSort().Count);

        var times = new TimeSpan[3];
        for (int run = 0; run < times.Length; run++)
        {
            var watch = Stopwatch.StartNew();
            IReadOnlyList<RenameItem> sorted = ListAndSort();
            watch.Stop();
            times[run] = watch.Elapsed;
            Assert.Equal(10000, sorted.Count);
        }

        // The fastest run: one run slowed by another test class cannot fail the guard.
        TimeSpan fastest = times.Min();
        Assert.True(
            fastest < GenerousBudget,
            string.Format(CultureInfo.InvariantCulture, "Fastest {0:N0} ms of {1:N0}, {2:N0}, {3:N0} ms; budget {4:N0} ms.",
                fastest.TotalMilliseconds, times[0].TotalMilliseconds, times[1].TotalMilliseconds,
                times[2].TotalMilliseconds, GenerousBudget.TotalMilliseconds));
    }

    private IReadOnlyList<RenameItem> ListAndSort() => FileListOrder.Sort(_source.ListEntries(_temp).Items);
}
