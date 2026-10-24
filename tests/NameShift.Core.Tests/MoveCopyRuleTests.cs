using System;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class MoveCopyRuleTests
{
    private const MoveCopyMode Move = MoveCopyMode.Move;
    private const MoveCopyMode Copy = MoveCopyMode.Copy;
    private const MoveCopySource FromStart = MoveCopySource.Start;
    private const MoveCopySource FromEnd = MoveCopySource.End;
    private const MoveCopyTarget ToStart = MoveCopyTarget.Start;
    private const MoveCopyTarget ToEnd = MoveCopyTarget.End;
    private const MoveCopyTarget ToPosition = MoveCopyTarget.Position;

    private static MoveCopyRule R(MoveCopyMode m, MoveCopySource s, int c, MoveCopyTarget t, int? p = null,
        string sep = "") => new(m, s, c, t, p, sep);

    private static RenameResult RunOne(MoveCopyRule rule, string name) =>
        new RenamePipeline([rule]).Run(Items.Files(name))[0];

    private static RenameResult RunFolder(MoveCopyRule rule, string name) =>
        new RenamePipeline([rule]).Run([new RenameItem(name, @"C:\Test", ItemKind.Folder)])[0];

    private static int LoneSurrogates(string s)
    {
        var count = 0;
        for (var i = 0; i < s.Length; i++)
        {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(s[i]))
            {
                count++;
            }
        }

        return count;
    }

    [Fact]
    public void Move_StartToEnd() =>
        Assert.Equal("report_2024.txt", RunOne(R(Move, FromStart, 4, ToEnd, sep: "_"), "2024report.txt").NewName);

    [Fact]
    public void Copy_EndToStart() =>
        Assert.Equal("2024-report_2024.txt",
            RunOne(R(Copy, FromEnd, 4, ToStart, sep: "-"), "report_2024.txt").NewName);

    [Theory]
    [InlineData(3, "", "cdabef")]
    [InlineData(3, "-", "cdab-ef")]
    [InlineData(5, "", "cdefab")]
    [InlineData(99, "", "cdefab")]
    [InlineData(5, "-", "cdef-ab")]
    [InlineData(99, "-", "cdef-ab")]
    [InlineData(1, "-", "ab-cdef")]
    [InlineData(4, "-", "cdeab-f")]
    public void Move_ToPosition(int position, string separator, string expectedBase) =>
        Assert.Equal(expectedBase + ".txt",
            RunOne(R(Move, FromStart, 2, ToPosition, position, separator), "abcdef.txt").NewName);

    [Fact]
    public void PositionAtEnd_SameAsTargetEnd()
    {
        var viaTarget = RunOne(R(Move, FromStart, 2, ToEnd, sep: "-"), "abcdef.txt").NewName;

        Assert.Equal("cdef-ab.txt", viaTarget);
        Assert.Equal(viaTarget, RunOne(R(Move, FromStart, 2, ToPosition, 5, "-"), "abcdef.txt").NewName);
    }

    [Fact]
    public void PositionOne_SameAsTargetStart()
    {
        var viaTarget = RunOne(R(Move, FromStart, 2, ToStart, sep: "-"), "abcdef.txt").NewName;

        Assert.Equal("ab-cdef.txt", viaTarget);
        Assert.Equal(viaTarget, RunOne(R(Move, FromStart, 2, ToPosition, 1, "-"), "abcdef.txt").NewName);
    }

    [Fact]
    public void Copy_CountLargerThanName_TakesAll() =>
        Assert.Equal("abc-abc.txt", RunOne(R(Copy, FromStart, 10, ToEnd, sep: "-"), "abc.txt").NewName);

    [Fact]
    public void Move_CountLargerThanName_Unchanged()
    {
        var result = RunOne(R(Move, FromStart, 10, ToEnd, sep: "-"), "abc.txt");

        Assert.Equal("abc.txt", result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void CountBelowOne_Disabled(int count) => AssertDisabled(R(Move, FromStart, count, ToEnd, sep: "_"));

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-3)]
    public void PositionTarget_NullOrBelowOne_Disabled(int? position) =>
        AssertDisabled(R(Move, FromStart, 2, ToPosition, position));

    private static void AssertDisabled(MoveCopyRule rule)
    {
        Assert.False(rule.Enabled);
        Assert.Equal(RenameStatus.Unchanged, RunOne(rule, "abcdef.txt").Status);

        var parts = NameParts.Split("abcdef.txt", ItemKind.File);
        var applied = rule.Apply(parts, new ItemContext(Items.File("abcdef.txt"), 0, 1));
        Assert.Equal(parts.BaseName, applied.BaseName);
        Assert.Equal(parts.Extension, applied.Extension);
    }

    [Fact]
    public void PositionIgnored_ForStartAndEnd()
    {
        Assert.True(R(Move, FromStart, 2, ToEnd, p: null).Enabled);
        Assert.True(R(Move, FromStart, 2, ToStart, p: null).Enabled);
    }

    [Fact]
    public void MaxValues_Clamped()
    {
        Assert.Equal("abcdef-abcdef.txt",
            RunOne(R(Copy, FromEnd, int.MaxValue, ToEnd, sep: "-"), "abcdef.txt").NewName);
        Assert.Equal("cdef-ab.txt",
            RunOne(R(Move, FromStart, 2, ToPosition, int.MaxValue, "-"), "abcdef.txt").NewName);
    }

    [Fact]
    public void SameSide_SeparatorStillAdded()
    {
        Assert.Equal("ab_cd.txt", RunOne(R(Move, FromEnd, 2, ToEnd, sep: "_"), "abcd.txt").NewName);

        var result = RunOne(R(Move, FromEnd, 2, ToEnd), "abcd.txt");
        Assert.Equal("abcd.txt", result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Fact]
    public void Pair_TakenWhole_FromStart() =>
        Assert.Equal("a\U0001F600.txt", RunOne(R(Move, FromStart, 1, ToEnd), "\U0001F600a.txt").NewName);

    [Fact]
    public void Pair_PositionInsidePair_MovesAfter() =>
        Assert.Equal("\U0001F600ab.txt", RunOne(R(Move, FromStart, 1, ToPosition, 2), "a\U0001F600b.txt").NewName);

    [Fact]
    public void Pair_TakenWhole_FromEnd() =>
        Assert.Equal("\U0001F600a.txt", RunOne(R(Move, FromEnd, 1, ToStart), "a\U0001F600.txt").NewName);

    [Fact]
    public void Pair_NeverSplit_NoLoneSurrogate()
    {
        foreach (var mode in new[] { Move, Copy })
        {
            foreach (var source in new[] { FromStart, FromEnd })
            {
                for (var count = 1; count <= 5; count++)
                {
                    for (var position = 1; position <= 6; position++)
                    {
                        var result = RunOne(R(mode, source, count, ToPosition, position),
                            "\U0001F600\U0001F601x.txt");

                        Assert.Equal(0, LoneSurrogates(result.NewName));
                        Assert.NotEqual(RenameStatus.Error, result.Status);
                    }
                }
            }
        }
    }

    [Fact]
    public void Dotfile_WholeNameIsBase() =>
        Assert.Equal("tignore_.gi", RunOne(R(Move, FromStart, 3, ToEnd, sep: "_"), ".gitignore").NewName);

    [Fact]
    public void Folder_WholeNameIsBase() =>
        Assert.Equal(".2_Folder 1.2", RunFolder(R(Copy, FromEnd, 2, ToStart, sep: "_"), "Folder 1.2").NewName);

    [Fact]
    public void Extension_NeverTouched() =>
        Assert.Equal("rab.ta.gz", RunOne(R(Move, FromEnd, 1, ToStart), "ab.tar.gz").NewName);

    [Fact]
    public void InvalidSeparator_AppliedAndFlagged()
    {
        var result = RunOne(R(Move, FromStart, 1, ToEnd, sep: ":"), "ab.txt");

        Assert.Equal("b:a.txt", result.NewName);
        Assert.Equal(RenameStatus.Changed, result.Status);
        Assert.Contains(new InvalidNameCheck().Check([result])[0], c => c.Code == ConflictCode.InvalidCharacter);
    }

    [Fact]
    public void Pipeline_StageOrder()
    {
        var pipeline = new RenamePipeline([
            new AddRule(prefix: "P-"),
            R(Move, FromStart, 2, ToEnd, sep: "_"),
            new RemovePositionRule(first: 1),
        ]);

        Assert.Equal("P-cd_ab.txt", pipeline.Run(Items.Files("xabcd.txt"))[0].NewName);
    }

    [Fact]
    public void NullSeparator_Throws()
    {
        var error = Assert.Throws<ArgumentNullException>(() => new MoveCopyRule(separator: null!));

        Assert.Equal("separator", error.ParamName);
    }

    [Fact]
    public void UndefinedEnums_Throw()
    {
        Assert.Equal("mode",
            Assert.Throws<ArgumentOutOfRangeException>(() => new MoveCopyRule(mode: (MoveCopyMode)9)).ParamName);
        Assert.Equal("source",
            Assert.Throws<ArgumentOutOfRangeException>(() => new MoveCopyRule(source: (MoveCopySource)9)).ParamName);
        Assert.Equal("target",
            Assert.Throws<ArgumentOutOfRangeException>(() => new MoveCopyRule(target: (MoveCopyTarget)9)).ParamName);
    }

    [Fact]
    public void Properties_ReturnTheValuesGiven()
    {
        var rule = new MoveCopyRule(Copy, FromEnd, 3, ToPosition, 2, " - ");

        Assert.Equal(Copy, rule.Mode);
        Assert.Equal(FromEnd, rule.Source);
        Assert.Equal(3, rule.Count);
        Assert.Equal(ToPosition, rule.Target);
        Assert.Equal(2, rule.Position);
        Assert.Equal(" - ", rule.Separator);
        Assert.Equal(RuleStage.MoveCopy, rule.Stage);

        var defaults = new MoveCopyRule();
        Assert.Equal(Move, defaults.Mode);
        Assert.Equal(FromStart, defaults.Source);
        Assert.Equal(0, defaults.Count);
        Assert.Equal(ToEnd, defaults.Target);
        Assert.Null(defaults.Position);
        Assert.Equal("", defaults.Separator);
        Assert.Equal(RuleStage.MoveCopy, defaults.Stage);
        Assert.False(defaults.Enabled);
    }
}
