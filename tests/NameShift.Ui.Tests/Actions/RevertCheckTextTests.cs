using System;
using System.Globalization;
using System.Linq;
using NameShift.Disk;
using NameShift.Ui.Actions;
using Xunit;

namespace NameShift.Ui.Tests.Actions;

public sealed class RevertCheckTextTests
{
    private static readonly CultureInfo EnUs = new CultureInfo("en-US");

    private static SkippedRevert Skip(string name, RevertSkipReason reason)
    {
        string path = @"C:\A\" + name;
        CompletedRename entry = DiskResults.Done(@"C:\A\old_" + name, path);
        bool blocked = reason == RevertSkipReason.NameTaken || reason == RevertSkipReason.WaitsForSkippedItem;
        return DiskResults.Skip(
            entry,
            reason,
            blocked ? @"C:\A\old_" + name : null,
            reason == RevertSkipReason.RenameFailed ? "In use." : null);
    }

    private static RevertCheck Check(params SkippedRevert[] skipped) => new RevertCheck(skipped, anyRevertable: true);

    private static RevertCheck Many(int count) =>
        Check(Enumerable.Range(0, count).Select(i => Skip("f" + i + ".txt", RevertSkipReason.Modified)).ToArray());

    [Fact]
    public void Lines_OneOfEachReason()
    {
        RevertCheck check = Check(
            Skip("a.txt", RevertSkipReason.Modified),
            Skip("b.txt", RevertSkipReason.ItemMissing),
            Skip("c.txt", RevertSkipReason.NameTaken),
            Skip("d.txt", RevertSkipReason.WaitsForSkippedItem));

        Assert.Equal(
            new[]
            {
                "a.txt: changed since the rename",
                "b.txt: no longer there",
                "c.txt: its old name is now used by another file",
                "d.txt: waits for another file",
            },
            RevertCheckText.Lines(check, EnUs));
    }

    [Fact]
    public void Lines_KeepTheJournalOrder()
    {
        RevertCheck check = Check(
            Skip("z.txt", RevertSkipReason.ItemMissing),
            Skip("a.txt", RevertSkipReason.Modified));

        Assert.Equal(new[] { "z.txt: no longer there", "a.txt: changed since the rename" }, RevertCheckText.Lines(check, EnUs));
    }

    [Theory]
    [InlineData(0, 0, "")]
    [InlineData(10, 10, "")]
    [InlineData(11, 10, "and 1 more")]
    [InlineData(25, 10, "and 15 more")]
    public void Lines_TenLimit(int skipped, int listed, string more)
    {
        var lines = RevertCheckText.Lines(Many(skipped), EnUs);

        Assert.Equal(listed + (more.Length > 0 ? 1 : 0), lines.Count);
        Assert.Equal(Enumerable.Range(0, listed).Select(i => "f" + i + ".txt: changed since the rename"), lines.Take(listed));
        if (more.Length > 0)
        {
            Assert.Equal(more, lines[listed]);
        }
    }

    [Fact]
    public void Lines_Grouping_Culture()
    {
        Assert.Equal("and 1,234 more", RevertCheckText.Lines(Many(1244), EnUs).Last());
    }

    [Fact]
    public void Lines_RenameFailed_HasAWord()
    {
        RevertCheck check = Check(Skip("a.txt", RevertSkipReason.RenameFailed));

        Assert.Equal("a.txt: could not be renamed", RevertCheckText.Lines(check, EnUs).Single());
        Assert.Equal("could not be renamed", RevertCheckText.Reason(RevertSkipReason.RenameFailed));
    }

    [Fact]
    public void Reason_UnknownValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RevertCheckText.Reason((RevertSkipReason)999));
    }

    [Fact]
    public void Lines_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RevertCheckText.Lines(null!, EnUs));
        Assert.Throws<ArgumentNullException>(() => RevertCheckText.Lines(RevertCheck.Empty, null!));
    }
}

public sealed class RevertDialogOptionsTests
{
    private static SkippedRevert Skip(RevertSkipReason reason)
    {
        bool blocked = reason == RevertSkipReason.NameTaken;
        return DiskResults.Skip(DiskResults.Done(@"C:\A\o.txt", @"C:\A\n.txt"), reason, blocked ? @"C:\A\o.txt" : null);
    }

    [Theory]
    [InlineData(RevertSkipReason.Modified, false, false, true, false)]
    [InlineData(RevertSkipReason.Modified, true, true, true, false)]
    [InlineData(RevertSkipReason.ItemMissing, true, true, false, false)]
    [InlineData(RevertSkipReason.ItemMissing, false, false, false, true)]
    [InlineData(RevertSkipReason.NameTaken, false, false, false, true)]
    [InlineData(RevertSkipReason.NameTaken, true, true, false, false)]
    public void Table(RevertSkipReason reason, bool anyRevertable, bool rest, bool include, bool closeOnly)
    {
        var options = RevertDialogOptions.For(new RevertCheck(new[] { Skip(reason) }, anyRevertable));

        Assert.Equal(rest, options.ShowRevertRest);
        Assert.Equal(include, options.ShowIncludeModified);
        Assert.Equal(closeOnly, options.CloseOnly);
    }

    [Fact]
    public void Null_Throws() => Assert.Throws<ArgumentNullException>(() => RevertDialogOptions.For(null!));
}
