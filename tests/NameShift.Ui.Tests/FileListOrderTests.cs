using System.Linq;
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
}