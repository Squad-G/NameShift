using System;
using System.Linq;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class NumberingRuleTests
{
    private static RenameResult[] Run(NumberingRule rule, params string[] names) =>
        new RenamePipeline([rule]).Run(Items.Files(names)).ToArray();

    private static string[] Names(RenameResult[] results) => results.Select(r => r.NewName).ToArray();

    [Fact]
    public void Run_SuffixWithPad2AndSeparator_NumbersInListOrder() =>
        Assert.Equal(["a_01.txt", "b_02.txt", "c_03.txt"],
            Names(Run(new NumberingRule(pad: 2, separator: "_"), "a.txt", "b.txt", "c.txt")));

    [Fact]
    public void Run_PrefixStart10Step5_CountsByFive() =>
        Assert.Equal(["10_a.txt", "15_b.txt"],
            Names(Run(new NumberingRule(NumberingMode.Prefix, start: 10, step: 5, separator: "_"), "a.txt", "b.txt")));

    [Fact]
    public void Run_InsertAtPosition2_KeepsOneCharacterInFront() =>
        Assert.Equal("a1bcdef.txt",
            Run(new NumberingRule(NumberingMode.Insert, insertPosition: 2), "abcdef.txt")[0].NewName);

    [Fact]
    public void Run_InsertWithSeparator_PutsTheSeparatorAfterTheNumber() =>
        Assert.Equal("abc1_def.txt",
            Run(new NumberingRule(NumberingMode.Insert, separator: "_", insertPosition: 4), "abcdef.txt")[0].NewName);

    [Fact]
    public void Run_InsertPastTheEnd_IsClampedToTheEnd() =>
        Assert.Equal("ab1.txt",
            Run(new NumberingRule(NumberingMode.Insert, insertPosition: 10), "ab.txt")[0].NewName);

    [Theory]
    [InlineData(3)]
    [InlineData(10)]
    public void Run_InsertAtOrPastTheEnd_AddsNoSeparator(int position) =>
        Assert.Equal("ab1.txt",
            Run(new NumberingRule(NumberingMode.Insert, separator: "_", insertPosition: position), "ab.txt")[0].NewName);

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Run_InsertWithoutAValidPosition_DoesNothing(int? position)
    {
        var rule = new NumberingRule(NumberingMode.Insert, insertPosition: position);

        var result = Run(rule, "abc.txt")[0];

        Assert.False(rule.Enabled);
        Assert.Equal("abc.txt", result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Fact]
    public void Run_Pad0With120Files_DoesNotPad()
    {
        var names = Enumerable.Range(1, 120).Select(i => $"x{i:D3}-.txt").ToArray();

        var results = Run(new NumberingRule(), names);

        Assert.Equal("x001-1.txt", results[0].NewName);
        Assert.Equal("x010-10.txt", results[9].NewName);
        Assert.Equal("x120-120.txt", results[119].NewName);
    }

    [Fact]
    public void Run_NumberWiderThanPad_IsNotCut() =>
        Assert.Equal("a100.txt", Run(new NumberingRule(start: 100, pad: 2), "a.txt")[0].NewName);

    [Fact]
    public void Run_OnlyTheGivenItems_StartAtStartInListOrder() =>
        Assert.Equal(["d_1.txt", "b_2.txt"], Names(Run(new NumberingRule(separator: "_"), "d.txt", "b.txt")));

    [Fact]
    public void Run_NegativeStep_CountsDown() =>
        Assert.Equal(["a_3.txt", "b_2.txt", "c_1.txt"],
            Names(Run(new NumberingRule(start: 3, step: -1, separator: "_"), "a.txt", "b.txt", "c.txt")));

    [Fact]
    public void Run_NumberBelowZero_IsAnErrorAndKeepsTheName()
    {
        var results = Run(new NumberingRule(step: -1, separator: "_"), "a.txt", "b.txt", "c.txt");

        Assert.Equal("a_1.txt", results[0].NewName);
        Assert.Equal(RenameStatus.Changed, results[0].Status);
        Assert.Equal("b_0.txt", results[1].NewName);
        Assert.Equal(RenameStatus.Changed, results[1].Status);
        Assert.Equal("c.txt", results[2].NewName);
        Assert.Equal(RenameStatus.Error, results[2].Status);
        Assert.Equal(RuleStage.Numbering, results[2].Error!.Stage);
        Assert.Equal("NumberingRule", results[2].Error!.RuleName);
        Assert.Contains("below zero", results[2].Error!.Message);
    }

    [Fact]
    public void Run_NegativeStart_IsAnError()
    {
        var result = Run(new NumberingRule(start: -1), "a.txt")[0];

        Assert.Equal("a.txt", result.NewName);
        Assert.Equal(RenameStatus.Error, result.Status);
    }

    [Fact]
    public void Run_NoSeparatorGiven_DefaultsToNone() =>
        Assert.Equal("a1.txt", Run(new NumberingRule(), "a.txt")[0].NewName);

    [Fact]
    public void Run_ExtensionIsNeverTouched() =>
        Assert.Equal("a.tar_1.gz", Run(new NumberingRule(separator: "_"), "a.tar.gz")[0].NewName);

    [Fact]
    public void Run_Folder_NumberGoesAtTheEndOfTheWholeName()
    {
        var folder = new RenameItem("My.Folder", @"C:\Test", ItemKind.Folder);

        var result = new RenamePipeline([new NumberingRule(pad: 2, separator: "_")]).Run([folder])[0];

        Assert.Equal("My.Folder_01", result.NewName);
    }

    [Fact]
    public void Run_MaxPad_IsAccepted()
    {
        var rule = new NumberingRule(pad: NumberingRule.MaxPad);

        Assert.Equal(255, rule.Pad);
        Assert.Equal("a" + new string('0', 254) + "1.txt", Run(rule, "a.txt")[0].NewName);
    }

    [Fact]
    public void Run_StepZero_GivesEveryItemTheStart() =>
        Assert.Equal(["a5.txt", "b5.txt"], Names(Run(new NumberingRule(start: 5, step: 0), "a.txt", "b.txt")));

    [Fact]
    public void Run_LargeValues_DoNotOverflow() =>
        Assert.Equal(["a2147483647.txt", "b2147483648.txt"],
            Names(Run(new NumberingRule(start: int.MaxValue), "a.txt", "b.txt")));

    [Fact]
    public void Constructor_Defaults()
    {
        var rule = new NumberingRule();

        Assert.Equal(NumberingMode.Suffix, rule.Mode);
        Assert.Equal(1, rule.Start);
        Assert.Equal(1, rule.Step);
        Assert.Equal(0, rule.Pad);
        Assert.Equal("", rule.Separator);
        Assert.Null(rule.InsertPosition);
        Assert.Equal(RuleStage.Numbering, rule.Stage);
        Assert.True(rule.Enabled);
    }

    [Fact]
    public void Constructor_RejectsInvalidArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NumberingRule(pad: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NumberingRule(pad: NumberingRule.MaxPad + 1));
        Assert.Throws<ArgumentNullException>(() => new NumberingRule(separator: null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NumberingRule((NumberingMode)99));
    }

    [Fact]
    public void Enabled_IsTrueInPrefixAndSuffixModesAndWithAValidInsertPosition()
    {
        Assert.True(new NumberingRule(NumberingMode.Prefix).Enabled);
        Assert.True(new NumberingRule(NumberingMode.Suffix).Enabled);
        Assert.True(new NumberingRule(NumberingMode.Insert, insertPosition: 1).Enabled);
    }
}
