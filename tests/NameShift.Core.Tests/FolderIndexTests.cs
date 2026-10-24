using System;
using System.Globalization;
using System.Linq;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class FolderIndexTests
{
    private static RenameItem F(string folder, string name = "a.txt") => new(name, folder);

    private static int[] FolderIndexes(params RenameItem[] items)
    {
        var rule = new RecordingRule();
        new RenamePipeline([rule]).Run(items);
        return rule.Calls.Select(call => call.Context.FolderIndex).ToArray();
    }

    [Fact]
    public void Interleaved_EachFolderCountsItsOwn()
    {
        var items = new[] { F(@"C:\x", "a.txt"), F(@"C:\y", "a.txt"), F(@"C:\x", "b.txt") };

        Assert.Equal([0, 0, 1], FolderIndexes(items));

        var rule = new NumberingRule(separator: "_", breakPerFolder: true);
        var names = new RenamePipeline([rule]).Run(items).Select(r => r.NewName).ToArray();
        Assert.Equal(["a_1.txt", "a_1.txt", "b_2.txt"], names);
    }

    [Fact]
    public void CaseAndTrailingSeparators_OneFolder() =>
        Assert.Equal([0, 1, 2, 3], FolderIndexes(F(@"C:\Photos"), F(@"c:\photos\"), F(@"C:\PHOTOS"), F(@"C:\Photos/")));

    [Fact]
    public void OrdinalIgnoreCase_NonAscii() =>
        Assert.Equal([0, 1], FolderIndexes(F(@"C:\" + "\u00c9t\u00e9"), F(@"c:\" + "\u00e9T\u00c9")));

    [Fact]
    public void PrefixOfAnotherFolder_TwoFolders() =>
        Assert.Equal([0, 0, 1], FolderIndexes(F(@"C:\a"), F(@"C:\a b"), F(@"C:\a")));

    [Fact]
    public void DifferentFolders_NotMerged() =>
        Assert.Equal([0, 0, 0, 0], FolderIndexes(F(@"C:\a"), F(@"C:\b"), F(@"D:\a"), F(@"C:\a\b")));

    [Fact]
    public void EmptyFolderPath_OneFolder() =>
        Assert.Equal([0, 1, 2], FolderIndexes(F(""), F(""), F("")));

    [Fact]
    public void TenThousandItems_HundredFolders()
    {
        var items = Enumerable.Range(0, 10000)
            .Select(i => F(@"C:\d" + (i % 100).ToString(CultureInfo.InvariantCulture),
                "f" + i.ToString(CultureInfo.InvariantCulture) + ".txt"))
            .ToArray();
        var rule = new RecordingRule();

        new RenamePipeline([rule]).Run(items);

        Assert.Equal(10000, rule.Calls.Count);
        for (var i = 0; i < rule.Calls.Count; i++)
        {
            Assert.Equal(i, rule.Calls[i].Context.Index);
            Assert.Equal(i / 100, rule.Calls[i].Context.FolderIndex);
        }
    }

    [Fact]
    public void Context_WithoutFolderIndex_EqualsIndex() =>
        Assert.Equal(2, new ItemContext(Items.File("a.txt"), 2, 5).FolderIndex);

    [Fact]
    public void Context_FolderIndexAboveIndex_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new ItemContext(Items.File("a.txt"), 2, 5, 3));

        Assert.Equal("folderIndex", ex.ParamName);
    }

    [Fact]
    public void Context_NegativeFolderIndex_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ItemContext(Items.File("a.txt"), 2, 5, -1));

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Context_FolderIndexInRange_Kept(int folderIndex) =>
        Assert.Equal(folderIndex, new ItemContext(Items.File("a.txt"), 2, 5, folderIndex).FolderIndex);

    [Fact]
    public void Context_ExistingChecksStillFirst()
    {
        var item = Items.File("a.txt");

        var count = Assert.Throws<ArgumentOutOfRangeException>(() => new ItemContext(item, 0, 0, 5));
        var index = Assert.Throws<ArgumentOutOfRangeException>(() => new ItemContext(item, 3, 3, 0));

        Assert.Equal("count", count.ParamName);
        Assert.Equal("index", index.ParamName);
    }
}
