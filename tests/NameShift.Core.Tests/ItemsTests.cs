using System;
using System.Linq;
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

    [Fact]
    public void RenameItem_Taken_DefaultsToNull()
    {
        Assert.Null(new RenameItem("a.txt", @"C:\Test").Taken);
        Assert.Null(new RenameItem("a.txt", @"C:\Test", ItemKind.File, ItemAttributes.None, 1, default, default).Taken);
    }

    [Fact]
    public void RenameItem_Taken_IsKeptAsGiven()
    {
        var taken = new DateTime(2023, 7, 1, 10, 0, 0, DateTimeKind.Utc);

        var item = new RenameItem("a.jpg", @"C:\Test", taken: taken);

        Assert.Equal(taken.Ticks, item.Taken!.Value.Ticks);
        Assert.Equal(DateTimeKind.Utc, item.Taken.Value.Kind);
    }

    [Fact]
    public void RenameItem_WithTaken_CopiesEveryOtherFact()
    {
        var t1 = new DateTime(2023, 7, 1, 10, 0, 0);
        var t2 = new DateTime(2022, 5, 6, 7, 8, 9);
        var created = new DateTime(2020, 1, 2, 3, 4, 5);
        var modified = new DateTime(2024, 3, 9, 14, 5, 7);
        var original = new RenameItem("pics", @"C:\Test\Sub", ItemKind.Folder,
            ItemAttributes.Hidden | ItemAttributes.ReadOnly, 42, created, modified, t1);

        var withT2 = original.WithTaken(t2);
        var withNull = original.WithTaken(null);

        Assert.NotSame(original, withT2);
        Assert.NotSame(original, withNull);
        foreach (var copy in new[] { withT2, withNull })
        {
            Assert.Equal("pics", copy.Name);
            Assert.Equal(@"C:\Test\Sub", copy.FolderPath);
            Assert.Equal(ItemKind.Folder, copy.Kind);
            Assert.Equal(ItemAttributes.Hidden | ItemAttributes.ReadOnly, copy.Attributes);
            Assert.Equal(42L, copy.Size);
            Assert.Equal(created, copy.Created);
            Assert.Equal(modified, copy.Modified);
        }

        Assert.Equal(t2, withT2.Taken);
        Assert.Null(withNull.Taken);
        Assert.Equal(t1, original.Taken);

        foreach (var property in typeof(RenameItem).GetProperties().Where(p => p.Name != nameof(RenameItem.Taken)))
        {
            Assert.Equal(property.GetValue(original), property.GetValue(withT2));
        }
    }
}
