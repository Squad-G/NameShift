using System;
using NameShift.Ui.Files;
using Xunit;

namespace NameShift.Ui.Tests.Files;

/// <summary>NS-70 AC 8: the rows the file list keeps and scrolls to when a filter changes the row count.</summary>
public sealed class FileListRowsTests
{
    [Fact]
    public void InRange_DropsRowsPastTheEnd()
    {
        Assert.Equal(new[] { 0, 3 }, FileListRows.InRange(new[] { 0, 3, 7 }, 5));
        Assert.Equal(new[] { 0, 3 }, FileListRows.InRange(new[] { 0, 3, 5 }, 5));
        Assert.Empty(FileListRows.InRange(new[] { 0, 3, 7 }, 0));
        Assert.Equal(new[] { 2 }, FileListRows.InRange(new[] { -1, 2 }, 5));
        Assert.Empty(FileListRows.InRange(Array.Empty<int>(), 5));
    }

    [Theory]
    [InlineData(new[] { 2 }, 10, 3, 2)]
    [InlineData(new[] { 4 }, 10, 3, 0)]
    [InlineData(new int[0], 10, 3, 0)]
    [InlineData(new int[0], 3, 10, null)]
    [InlineData(new int[0], 10, 0, null)]
    [InlineData(new[] { 1 }, 3, 10, 1)]
    public void ScrollTarget(int[] selected, int oldCount, int newCount, int? expected)
    {
        Assert.Equal(expected, FileListRows.ScrollTarget(selected, oldCount, newCount));
    }
}
