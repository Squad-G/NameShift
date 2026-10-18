using System;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class ItemsTests
{
    [Fact]
    public void RenameItem_RejectsAnEmptyName()
    {
        Assert.Throws<ArgumentException>(() => new RenameItem("", @"C:\Test"));
    }

    [Fact]
    public void ItemContext_RejectsAnIndexOutsideTheList()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ItemContext(Items.File("a.txt"), 1, 1));
    }

    [Fact]
    public void RenameResult_ErrorStatusNeedsAnError()
    {
        Assert.Throws<ArgumentException>(
            () => new RenameResult(Items.File("a.txt"), 0, "a.txt", RenameStatus.Error, null));
    }
}
