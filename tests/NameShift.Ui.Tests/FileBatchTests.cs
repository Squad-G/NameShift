using System;
using NameShift.Core;
using NameShift.Ui.Files;
using Xunit;

namespace NameShift.Ui.Tests;

public sealed class FileBatchTests
{
    private static readonly RenameItem[] Three =
    {
        new RenameItem("a.txt", @"C:\A"),
        new RenameItem("b.txt", @"C:\A"),
        new RenameItem("c.txt", @"C:\A"),
    };

    [Fact]
    public void WholeList_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => FileBatch.WholeList(null!));
    }

    [Fact]
    public void WholeList_KeepsTheReference_AndMapsEveryRowToItself()
    {
        FileBatch batch = FileBatch.WholeList(Three);

        Assert.Same(Three, batch.Items);
        Assert.Same(Three, batch.ListItems);
        Assert.True(batch.IsWholeList);
        Assert.Equal(3, batch.Count);
        Assert.Equal(new[] { 0, 1, 2 }, batch.Rows);
        for (int row = 0; row < 3; row++)
        {
            Assert.True(batch.TryGetIndex(row, out int index));
            Assert.Equal(row, index);
        }
    }

    [Fact]
    public void TryGetIndex_OutOfRange_IsFalse()
    {
        FileBatch batch = FileBatch.WholeList(Three);

        Assert.False(batch.TryGetIndex(-1, out _));
        Assert.False(batch.TryGetIndex(3, out _));
    }

    [Fact]
    public void Empty_HasNoItems()
    {
        Assert.Equal(0, FileBatch.Empty.Count);
        Assert.True(FileBatch.Empty.IsWholeList);
        Assert.False(FileBatch.Empty.TryGetIndex(0, out _));
    }
}
