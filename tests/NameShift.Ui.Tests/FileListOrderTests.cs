using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NameShift.Core;
using NameShift.Ui.Files;
using Xunit;
using static NameShift.Ui.Tests.FakeFileListSource;

namespace NameShift.Ui.Tests;

public sealed class FileListOrderTests
{
    [Fact]
    public void FoldersFirst_ThenFiles_EachInNaturalOrder()
    {
        RenameItem[] items =
        {
            File("file10.txt"), Folder("b"), File("file2.txt"), Folder("A10"), File("File1.txt"), Folder("a2"),
        };

        var sorted = FileListOrder.Sort(items);

        Assert.Equal(new[] { "a2", "A10", "b", "File1.txt", "file2.txt", "file10.txt" }, sorted.Select(i => i.Name));
    }

    [Fact]
    public void SameNameIgnoringCase_IsOrderedOrdinal()
    {
        Assert.Equal(new[] { "B.txt", "b.txt" }, FileListOrder.Sort(new[] { File("b.txt"), File("B.txt") }).Select(i => i.Name));
        Assert.Equal(new[] { "B.txt", "b.txt" }, FileListOrder.Sort(new[] { File("B.txt"), File("b.txt") }).Select(i => i.Name));
    }

    private static RenameItem Dated(string name, long size, DateTime modified, ItemKind kind = ItemKind.File) =>
        new RenameItem(name, @"C:\A", kind, ItemAttributes.None, size, default, modified);

    private static string[] Names(IEnumerable<RenameItem> items) => items.Select(i => i.Name).ToArray();

    private static readonly FileListSort[] AllSorts =
    {
        new FileListSort(FileListColumn.Name, false), new FileListSort(FileListColumn.Name, true),
        new FileListSort(FileListColumn.Size, false), new FileListSort(FileListColumn.Size, true),
        new FileListSort(FileListColumn.Modified, false), new FileListSort(FileListColumn.Modified, true),
    };

    public static IEnumerable<object[]> EverySort() => AllSorts.Select(sort => new object[] { sort });

    [Fact]
    public void Name_Ascending_IsTodaysOrder()
    {
        RenameItem[] items = { File("a10.txt"), Folder("d2"), File("a2.txt"), Folder("d10"), File("A2.txt") };

        Assert.Equal(Names(FileListOrder.Sort(items)), Names(FileListOrder.Sort(items, new FileListSort(FileListColumn.Name, false))));
        Assert.Equal(new[] { "d2", "d10", "A2.txt", "a2.txt", "a10.txt" }, Names(FileListOrder.Sort(items)));
    }

    [Fact]
    public void Name_Descending_ExactReverseInsideEachGroup()
    {
        RenameItem[] items = { File("a10.txt"), Folder("d2"), File("a2.txt"), Folder("d10"), File("A2.txt") };

        var ascending = FileListOrder.Sort(items, new FileListSort(FileListColumn.Name, false));
        var descending = FileListOrder.Sort(items, new FileListSort(FileListColumn.Name, true));

        Assert.Equal(new[] { "d10", "d2", "a10.txt", "a2.txt", "A2.txt" }, Names(descending));
        Assert.Equal(Names(ascending.Where(i => i.Kind == ItemKind.Folder).Reverse()), Names(descending.Where(i => i.Kind == ItemKind.Folder)));
        Assert.Equal(Names(ascending.Where(i => i.Kind == ItemKind.File).Reverse()), Names(descending.Where(i => i.Kind == ItemKind.File)));
    }

    [Fact]
    public void Size_Ascending_Numeric_TiesByName()
    {
        RenameItem[] items = { File("b", 10), File("c", 9), File("d", 2048), File("a", 10), File("e", 2) };

        var sorted = FileListOrder.Sort(items, new FileListSort(FileListColumn.Size, false));

        Assert.Equal(new[] { "e", "c", "a", "b", "d" }, Names(sorted));
    }

    [Fact]
    public void Size_Descending_TiesStayAscending()
    {
        RenameItem[] items = { File("b", 10), File("c", 9), File("d", 2048), File("a", 10) };

        var sorted = FileListOrder.Sort(items, new FileListSort(FileListColumn.Size, true));

        Assert.Equal(new[] { "d", "a", "b", "c" }, Names(sorted));
    }

    [Fact]
    public void Modified_BothDirections_TiesByName_UnknownOldest()
    {
        var old = new DateTime(2020, 1, 2, 3, 4, 5);
        var recent = new DateTime(2024, 6, 7, 8, 9, 10);
        RenameItem[] items =
        {
            Dated("c", 1, recent), Dated("unknown", 1, default), Dated("b", 1, old), Dated("a", 1, recent), Dated("a2", 1, old),
        };

        var ascending = FileListOrder.Sort(items, new FileListSort(FileListColumn.Modified, false));
        var descending = FileListOrder.Sort(items, new FileListSort(FileListColumn.Modified, true));

        Assert.Equal(new[] { "unknown", "a2", "b", "a", "c" }, Names(ascending));
        Assert.Equal(new[] { "a", "c", "a2", "b", "unknown" }, Names(descending));
    }

    [Theory]
    [MemberData(nameof(EverySort))]
    public void EveryColumnAndDirection_FoldersFirst(FileListSort sort)
    {
        var stamp = new DateTime(2022, 2, 3, 4, 5, 6);
        RenameItem[] items =
        {
            Dated("z.txt", 1, stamp), Dated("b", 0, stamp, ItemKind.Folder), Dated("a.txt", 99, stamp.AddDays(5)),
            Dated("a", 0, stamp.AddDays(-5), ItemKind.Folder), Dated("m.txt", 5, default),
        };

        var sorted = FileListOrder.Sort(items, sort);

        Assert.Equal(new[] { ItemKind.Folder, ItemKind.Folder, ItemKind.File, ItemKind.File, ItemKind.File }, sorted.Select(i => i.Kind));
        if (sort.Column == FileListColumn.Size)
        {
            Assert.Equal(new[] { "a", "b" }, Names(sorted.Take(2)));
        }
    }

    [Fact]
    public void Sort_NewListSameInstances()
    {
        RenameItem[] items = { File("b"), File("a") };

        var sorted = FileListOrder.Sort(items, FileListSort.Default);

        Assert.NotSame(items, sorted);
        Assert.Same(items[1], sorted[0]);
        Assert.Same(items[0], sorted[1]);
    }

    [Fact]
    public void Sort_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => FileListOrder.Sort(null!, FileListSort.Default));
        Assert.Throws<ArgumentNullException>(() => FileListOrder.Sort(new[] { File("a") }, null!));
        Assert.Throws<ArgumentNullException>(() => FileListOrder.Sort(null!));
    }

    [Theory]
    [MemberData(nameof(EverySort))]
    public async Task TenThousand_UnderOneSecond(FileListSort sort)
    {
        var items = new List<RenameItem>();
        for (int i = 0; i < 10000; i++)
        {
            items.Add(Dated("file" + ((i * 7919) % 10000) + ".txt", (i * 31) % 5000, new DateTime(2020, 1, 1).AddMinutes((i * 13) % 20000)));
        }

        var watch = Stopwatch.StartNew();
        var sorted = await Task.Run(() => FileListOrder.Sort(items, sort));
        watch.Stop();

        Assert.Equal(10000, sorted.Count);
        Assert.True(watch.ElapsedMilliseconds < 1000, "The sort took " + watch.ElapsedMilliseconds + " ms.");
        System.Console.WriteLine("Sort " + sort + ": " + watch.ElapsedMilliseconds + " ms");
    }

    [Fact]
    public void FileListSort_ToggledAndEquality()
    {
        Assert.Equal(FileListColumn.Name, FileListSort.Default.Column);
        Assert.False(FileListSort.Default.Descending);

        FileListSort toggled = new FileListSort(FileListColumn.Size, false).Toggled();

        Assert.Equal(new FileListSort(FileListColumn.Size, true), toggled);
        Assert.Equal(new FileListSort(FileListColumn.Size, true).GetHashCode(), toggled.GetHashCode());
        Assert.NotEqual(new FileListSort(FileListColumn.Size, false), toggled);
        Assert.NotEqual(new FileListSort(FileListColumn.Name, true), toggled);
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileListSort((FileListColumn)9, false));
    }
}
