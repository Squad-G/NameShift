using System;
using System.Collections.Generic;
using NameShift.Core;
using NameShift.Ui.Preview;
using Xunit;

namespace NameShift.Ui.Tests.Preview;

public sealed class PhotoDateNeedTests
{
    private static RenameItem Item(DateTime? taken) =>
        new RenameItem("a.jpg", "C:" + (char)92 + "A", ItemKind.File, ItemAttributes.None, 1, DateTime.Now, DateTime.Now, taken);

    public static IEnumerable<object[]> InRows()
    {
        yield return new object[] { Array.Empty<IRenameRule>(), false };
        yield return new object[] { new IRenameRule[] { new AutoDateRule(DateSource.Modified) }, false };
        yield return new object[] { new IRenameRule[] { new AutoDateRule(DateSource.Created) }, false };
        yield return new object[] { new IRenameRule[] { new AutoDateRule(DateSource.Taken) }, true };
        yield return new object[]
        {
            new IRenameRule[] { new ContextRecordingRule(), new AutoDateRule(DateSource.Taken) }, true,
        };
        yield return new object[]
        {
            new IRenameRule[] { new AutoDateRule(DateSource.Modified), new ContextRecordingRule() }, false,
        };
        yield return new object[] { new IRenameRule[] { new ContextRecordingRule() }, false };
    }

    [Theory]
    [MemberData(nameof(InRows))]
    public void In_Table(IRenameRule[] rules, bool expected)
    {
        Assert.Equal(expected, PhotoDateNeed.In(rules));
    }

    [Fact]
    public void In_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PhotoDateNeed.In(null!));
    }

    [Fact]
    public void HasDate_Table()
    {
        Assert.False(PhotoDateNeed.HasDate(Item(null)));
        Assert.False(PhotoDateNeed.HasDate(Item(default(DateTime))));
        Assert.True(PhotoDateNeed.HasDate(Item(new DateTime(2023, 7, 1, 10, 0, 0))));
    }

    [Fact]
    public void HasDate_NullItem_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PhotoDateNeed.HasDate(null!));
    }
}
