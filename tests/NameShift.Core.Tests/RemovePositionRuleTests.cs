using Xunit;

namespace NameShift.Core.Tests;

public sealed class RemovePositionRuleTests
{
    private static RenameResult RunOne(RemovePositionRule rule, string name) =>
        new RenamePipeline([rule]).Run(Items.Files(name))[0];

    private static RenameResult RunFolder(RemovePositionRule rule, string name) =>
        new RenamePipeline([rule]).Run([new RenameItem(name, @"C:\Test", ItemKind.Folder)])[0];

    private static bool HasLoneSurrogate(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(text[i]))
            {
                return true;
            }
        }

        return false;
    }

    [Fact]
    public void Run_First_RemovesFromTheStart() =>
        Assert.Equal("0001.jpg", RunOne(new RemovePositionRule(first: 4), "IMG_0001.jpg").NewName);

    [Fact]
    public void Run_Last_RemovesFromTheEnd() =>
        Assert.Equal("file.txt", RunOne(new RemovePositionRule(last: 5), "file_copy.txt").NewName);

    [Fact]
    public void Run_Range_RemovesInclusive1Based() =>
        Assert.Equal("aef.txt", RunOne(new RemovePositionRule(from: 2, to: 4), "abcdef.txt").NewName);

    [Fact]
    public void Run_FirstLongerThanBase_OnAFile_GivesDotExtension_NotFlagged()
    {
        var result = RunOne(new RemovePositionRule(first: 10), "abc.txt");

        Assert.Equal(".txt", result.NewName);
        Assert.Empty(new InvalidNameCheck().Check([result])[0]);
    }

    [Fact]
    public void Run_FirstLongerThanBase_OnAFolder_FlaggedEmptyName()
    {
        var result = RunFolder(new RemovePositionRule(first: 10), "abc");

        Assert.Equal("", result.NewName);
        Assert.Equal([new Conflict(ConflictCode.EmptyName)], new InvalidNameCheck().Check([result])[0]);
    }

    [Fact]
    public void Run_LastLongerThanBase_EmptiesTheBase() =>
        Assert.Equal(".txt", RunOne(new RemovePositionRule(last: 99), "abc.txt").NewName);

    [Theory]
    [InlineData(0, 3)]
    [InlineData(-1, 3)]
    [InlineData(4, 2)]
    [InlineData(null, 3)]
    [InlineData(2, null)]
    public void Range_Ignored(int? from, int? to)
    {
        var rule = new RemovePositionRule(from: from, to: to);

        Assert.False(rule.Enabled);
        Assert.Equal("abcdef.txt", RunOne(rule, "abcdef.txt").NewName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(null)]
    public void Part_ZeroNegativeOrNull_RemovesNothing(int? count)
    {
        Assert.Equal("abcdef.txt", RunOne(new RemovePositionRule(first: count), "abcdef.txt").NewName);
        Assert.Equal("abcdef.txt", RunOne(new RemovePositionRule(last: count), "abcdef.txt").NewName);
    }

    [Fact]
    public void Enabled_FalseWhenNoPartApplies()
    {
        Assert.False(new RemovePositionRule().Enabled);
        Assert.False(new RemovePositionRule(0, 0).Enabled);
        Assert.False(new RemovePositionRule(-1, -1, 5, 2).Enabled);
    }

    [Theory]
    [InlineData(1, null, null, null)]
    [InlineData(null, 1, null, null)]
    [InlineData(null, null, 1, 1)]
    public void Enabled_TrueWhenAnyPartApplies(int? first, int? last, int? from, int? to) =>
        Assert.True(new RemovePositionRule(first, last, from, to).Enabled);

    [Fact]
    public void Run_RangeToPastTheEnd_IsClamped() =>
        Assert.Equal("abc.txt", RunOne(new RemovePositionRule(from: 4, to: 99), "abcdef.txt").NewName);

    [Fact]
    public void Run_RangeFromPastTheEnd_RemovesNothing()
    {
        var result = RunOne(new RemovePositionRule(from: 9, to: 12), "abcdef.txt");

        Assert.Equal("abcdef.txt", result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Fact]
    public void Run_RangeFromExactlyOneAfterTheEnd_RemovesNothing() =>
        Assert.Equal("abcdef.txt", RunOne(new RemovePositionRule(from: 7, to: 9), "abcdef.txt").NewName);

    [Fact]
    public void Run_RangeToExactlyTheLength_RemovesTheTail() =>
        Assert.Equal("abcde.txt", RunOne(new RemovePositionRule(from: 6, to: 6), "abcdef.txt").NewName);

    [Fact]
    public void Run_HugeValues_NoOverflow()
    {
        Assert.Equal(".txt", RunOne(new RemovePositionRule(first: int.MaxValue), "abcdef.txt").NewName);
        Assert.Equal("a.txt", RunOne(new RemovePositionRule(from: 2, to: int.MaxValue), "abcdef.txt").NewName);
        Assert.Equal(".txt", RunOne(new RemovePositionRule(last: int.MaxValue), "abcdef.txt").NewName);
        Assert.Equal("abcdef.txt", RunOne(new RemovePositionRule(from: int.MaxValue, to: int.MaxValue), "abcdef.txt").NewName);
    }

    [Fact]
    public void Run_AllThreeParts_ChainInOrder() =>
        Assert.Equal("def.txt", RunOne(new RemovePositionRule(2, 2, 1, 1), "abcdefgh.txt").NewName);

    [Fact]
    public void Run_First_NeverSplitsASurrogatePair() =>
        Assert.Equal("a.txt", RunOne(new RemovePositionRule(first: 1), "\U0001F600a.txt").NewName);

    [Fact]
    public void Run_Last_NeverSplitsASurrogatePair() =>
        Assert.Equal("a.txt", RunOne(new RemovePositionRule(last: 1), "a\U0001F600.txt").NewName);

    [Theory]
    [InlineData(2, 2, "ab.txt")]
    [InlineData(1, 2, "b.txt")]
    [InlineData(3, 3, "ab.txt")]
    [InlineData(2, 3, "ab.txt")]
    public void Run_Range_NeverSplitsASurrogatePair(int from, int to, string expected)
    {
        var result = RunOne(new RemovePositionRule(from: from, to: to), "a\U0001F600b.txt");

        Assert.Equal(expected, result.NewName);
        Assert.False(HasLoneSurrogate(result.NewName));
    }

    [Fact]
    public void Run_Range_StartInsideOnePair_EndInsideAnother()
    {
        // a, pair (2-3), b, pair (5-6), c: from 3 to 5 starts inside the first pair and ends inside the second.
        var result = RunOne(new RemovePositionRule(from: 3, to: 5), "a\U0001F600b\U0001F601c.txt");

        Assert.Equal("ac.txt", result.NewName);
        Assert.False(HasLoneSurrogate(result.NewName));
    }

    [Fact]
    public void Run_Dotfile_WholeNameIsTheBase() =>
        Assert.Equal("gitignore", RunOne(new RemovePositionRule(first: 1), ".gitignore").NewName);

    [Fact]
    public void Run_Folder_WholeNameIsTheBase() =>
        Assert.Equal("Folder 1", RunFolder(new RemovePositionRule(last: 2), "Folder 1.2").NewName);

    [Fact]
    public void Run_NeverTouchesTheExtension() =>
        Assert.Equal("abc.JPG", RunOne(new RemovePositionRule(last: 3), "abcdef.JPG").NewName);

    [Fact]
    public void Stage_IsRemove5()
    {
        var rule = new RemovePositionRule();

        Assert.Equal(RuleStage.Remove, rule.Stage);
        Assert.Equal(5, (int)rule.Stage);
    }

    [Fact]
    public void Properties_KeepTheGivenValues()
    {
        var rule = new RemovePositionRule(-1, 0, 7, 3);

        Assert.Equal(-1, rule.First);
        Assert.Equal(0, rule.Last);
        Assert.Equal(7, rule.From);
        Assert.Equal(3, rule.To);
    }
}
