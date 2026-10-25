using System;
using NameShift.Ui.Files;
using Xunit;

namespace NameShift.Ui.Tests.Files;

/// <summary>NS-78 AC 3: the file list's column map, with and without the Folder column.</summary>
public sealed class FileListColumnsTests
{
    [Fact]
    public void Order_WithoutFolder() =>
        Assert.Equal(
            new[] { ShownColumn.Name, ShownColumn.NewName, ShownColumn.Status, ShownColumn.Size, ShownColumn.Modified },
            FileListColumns.For(false).Order);

    [Fact]
    public void Order_WithFolder() =>
        Assert.Equal(
            new[] { ShownColumn.Name, ShownColumn.Folder, ShownColumn.NewName, ShownColumn.Status, ShownColumn.Size, ShownColumn.Modified },
            FileListColumns.For(true).Order);

    [Theory]
    [InlineData(false, ShownColumn.Name, 0)]
    [InlineData(false, ShownColumn.Folder, -1)]
    [InlineData(false, ShownColumn.NewName, 1)]
    [InlineData(false, ShownColumn.Status, 2)]
    [InlineData(false, ShownColumn.Size, 3)]
    [InlineData(false, ShownColumn.Modified, 4)]
    [InlineData(true, ShownColumn.Name, 0)]
    [InlineData(true, ShownColumn.Folder, 1)]
    [InlineData(true, ShownColumn.NewName, 2)]
    [InlineData(true, ShownColumn.Status, 3)]
    [InlineData(true, ShownColumn.Size, 4)]
    [InlineData(true, ShownColumn.Modified, 5)]
    public void IndexOf(bool showFolder, ShownColumn column, int expected)
    {
        FileListColumns columns = FileListColumns.For(showFolder);

        Assert.Equal(expected, columns.IndexOf(column));
        if (expected >= 0)
        {
            Assert.Equal(column, columns.ColumnAt(expected));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SortColumnAt(bool showFolder)
    {
        FileListColumns columns = FileListColumns.For(showFolder);

        Assert.Equal(FileListColumn.Name, columns.SortColumnAt(columns.IndexOf(ShownColumn.Name)));
        Assert.Equal(FileListColumn.Size, columns.SortColumnAt(columns.IndexOf(ShownColumn.Size)));
        Assert.Equal(FileListColumn.Modified, columns.SortColumnAt(columns.IndexOf(ShownColumn.Modified)));
        Assert.Null(columns.SortColumnAt(columns.IndexOf(ShownColumn.NewName)));
        Assert.Null(columns.SortColumnAt(columns.IndexOf(ShownColumn.Status)));
        if (showFolder)
        {
            Assert.Null(columns.SortColumnAt(columns.IndexOf(ShownColumn.Folder)));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => columns.SortColumnAt(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => columns.SortColumnAt(columns.Order.Count));
        Assert.Throws<ArgumentOutOfRangeException>(() => columns.ColumnAt(columns.Order.Count));
    }

    [Fact]
    public void SortColumnAt_FolderIsNotSortable()
    {
        // The literal index: a click on the second header, where Folder sits when shown.
        Assert.Null(FileListColumns.For(true).SortColumnAt(1));
        Assert.Null(FileListColumns.For(false).SortColumnAt(1));
        Assert.Equal(FileListColumn.Size, FileListColumns.For(true).SortColumnAt(4));
        Assert.Equal(FileListColumn.Size, FileListColumns.For(false).SortColumnAt(3));
    }

    [Theory]
    [InlineData(false, FileListColumn.Name, 0)]
    [InlineData(true, FileListColumn.Name, 0)]
    [InlineData(false, FileListColumn.Size, 3)]
    [InlineData(true, FileListColumn.Size, 4)]
    [InlineData(false, FileListColumn.Modified, 4)]
    [InlineData(true, FileListColumn.Modified, 5)]
    public void SortArrowIndex(bool showFolder, FileListColumn sort, int expected) =>
        Assert.Equal(expected, FileListColumns.For(showFolder).SortArrowIndex(sort));

    [Fact]
    public void For_ReturnsTheSameInstancePerFlag()
    {
        Assert.Same(FileListColumns.For(true), FileListColumns.For(true));
        Assert.Same(FileListColumns.For(false), FileListColumns.For(false));
        Assert.NotSame(FileListColumns.For(true), FileListColumns.For(false));
        Assert.True(FileListColumns.For(true).ShowFolder);
        Assert.False(FileListColumns.For(false).ShowFolder);
    }
}
