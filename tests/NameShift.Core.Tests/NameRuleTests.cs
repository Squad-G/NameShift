using System;
using System.Collections.Generic;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class NameRuleTests
{
    private static RenameResult RunOne(NameRule rule, string name) =>
        new RenamePipeline([rule]).Run(Items.Files(name))[0];

    private static RenameResult RunFolder(NameRule rule, string name) =>
        new RenamePipeline([rule]).Run([new RenameItem(name, @"C:\Test", ItemKind.Folder)])[0];

    private static NameParts ApplyTo(NameRule rule, string baseName, string extension) =>
        rule.Apply(new NameParts(baseName, extension), new ItemContext(Items.File("a.txt"), 0, 1));

    [Fact]
    public void Run_Fixed_ReplacesTheBaseName_KeepsTheExtension() =>
        Assert.Equal("holiday.jpg", RunOne(new NameRule(NameMode.Fixed, "holiday"), "IMG_1.jpg").NewName);

    [Fact]
    public void Run_Fixed_OnAFolder_ReplacesTheWholeName() =>
        Assert.Equal("holiday", RunFolder(new NameRule(NameMode.Fixed, "holiday"), "Old").NewName);

    [Fact]
    public void Fixed_EmptyValue_Disabled_Unchanged()
    {
        var rule = new NameRule(NameMode.Fixed, "");

        Assert.False(rule.Enabled);
        Assert.Equal(new NameParts("a", "txt"), ApplyTo(rule, "a", "txt"));
        Assert.Equal("a.txt", RunOne(rule, "a.txt").NewName);
    }

    [Fact]
    public void Fixed_NonEmpty_Enabled() =>
        Assert.True(new NameRule(NameMode.Fixed, "x").Enabled);

    [Fact]
    public void Remove_And_Reverse_AreEnabled()
    {
        Assert.True(new NameRule(NameMode.Remove).Enabled);
        Assert.True(new NameRule(NameMode.Reverse).Enabled);
    }

    [Fact]
    public void Apply_Remove_OnAFile_EmptyBaseKeepsExtension() =>
        Assert.Equal(new NameParts("", "txt"), ApplyTo(new NameRule(NameMode.Remove), "a", "txt"));

    [Fact]
    public void Run_Remove_OnAFile_GivesDotExtension_NotFlagged()
    {
        var result = RunOne(new NameRule(NameMode.Remove), "a.txt");

        Assert.Equal(".txt", result.NewName);
        Assert.Empty(new InvalidNameCheck().Check([result])[0]);
    }

    [Fact]
    public void Run_Remove_OnAFolder_FlaggedEmptyName()
    {
        var result = RunFolder(new NameRule(NameMode.Remove), "Old");

        Assert.Equal("", result.NewName);
        Assert.Null(result.Error);
        Assert.Equal([new Conflict(ConflictCode.EmptyName)], new InvalidNameCheck().Check([result])[0]);
    }

    [Fact]
    public void Pipeline_RemoveThenAddPrefix_GivesPrefixDotExtension() =>
        Assert.Equal(
            "x.txt",
            new RenamePipeline([new NameRule(NameMode.Remove), new AddRule(prefix: "x")]).Run(Items.Files("a.txt"))[0].NewName);

    [Theory]
    [InlineData("abc123.txt", "321cba.txt")]
    [InlineData("a.tar.gz", "rat.a.gz")]
    public void Run_Reverse_ReversesTheBaseNameOnly(string name, string expected) =>
        Assert.Equal(expected, RunOne(new NameRule(NameMode.Reverse), name).NewName);

    [Fact]
    public void Keep_Disabled_Unchanged()
    {
        var rule = new NameRule(NameMode.Keep);

        Assert.False(rule.Enabled);
        Assert.Equal(new NameParts("a", "txt"), ApplyTo(rule, "a", "txt"));
        Assert.Equal("a.txt", RunOne(rule, "a.txt").NewName);
    }

    [Fact]
    public void Keep_IgnoresTheValue() =>
        Assert.Equal(new NameParts("a", "txt"), ApplyTo(new NameRule(NameMode.Keep, "zzz"), "a", "txt"));

    [Fact]
    public void Run_Reverse_KeepsACombiningAccentOnItsLetter() =>
        Assert.Equal("xe\u0301.txt", RunOne(new NameRule(NameMode.Reverse), "e\u0301x.txt").NewName);

    [Fact]
    public void Run_Reverse_KeepsASurrogatePairWhole()
    {
        var name = RunOne(new NameRule(NameMode.Reverse), "a\U0001F600b.txt").NewName;

        Assert.Equal("b\U0001F600a.txt", name);
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsHighSurrogate(name[i]))
            {
                Assert.True(i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]));
            }
            else if (char.IsLowSurrogate(name[i]))
            {
                Assert.True(i > 0 && char.IsHighSurrogate(name[i - 1]));
            }
        }
    }

    [Theory]
    [InlineData("a.txt")]
    [InlineData("aba.txt")]
    public void Run_Reverse_OneCharOrPalindrome_Unchanged(string name)
    {
        var result = RunOne(new NameRule(NameMode.Reverse), name);

        Assert.Equal(name, result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Fact]
    public void Apply_Reverse_EmptyBase_Unchanged() =>
        Assert.Equal(new NameParts("", "txt"), ApplyTo(new NameRule(NameMode.Reverse), "", "txt"));

    [Fact]
    public void Run_Reverse_TwoChars_Swapped() =>
        Assert.Equal("ba.txt", RunOne(new NameRule(NameMode.Reverse), "ab.txt").NewName);

    [Fact]
    public void Run_Fixed_InvalidCharacter_AppliedAsTyped_ThenFlagged()
    {
        var result = RunOne(new NameRule(NameMode.Fixed, "a?b"), "x.jpg");

        Assert.Equal("a?b.jpg", result.NewName);
        Assert.Contains(
            new Conflict(ConflictCode.InvalidCharacter, "?"),
            new InvalidNameCheck().Check([result])[0]);
    }

    [Fact]
    public void Run_Fixed_SameNameForTwoFiles_DuplicateFlagged()
    {
        var results = new RenamePipeline([new NameRule(NameMode.Fixed, "x")]).Run(Items.Files("a.txt", "b.txt"));
        var check = new DuplicateTargetCheck(new Dictionary<string, IReadOnlyCollection<string>>
        {
            [@"C:\Test"] = new[] { "a.txt", "b.txt" },
        });

        Assert.Equal("x.txt", results[0].NewName);
        Assert.Equal("x.txt", results[1].NewName);
        var flags = check.Check(results);
        Assert.Equal([new Conflict(ConflictCode.DuplicateTarget)], flags[0]);
        Assert.Equal([new Conflict(ConflictCode.DuplicateTarget)], flags[1]);
    }

    [Fact]
    public void Constructor_UnknownMode_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new NameRule((NameMode)99));

    [Fact]
    public void Constructor_NullValue_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new NameRule(NameMode.Fixed, null!));

    [Fact]
    public void Stage_IsName2()
    {
        var rule = new NameRule(NameMode.Keep);

        Assert.Equal(RuleStage.Name, rule.Stage);
        Assert.Equal(2, (int)rule.Stage);
    }

    [Fact]
    public void Value_KeptAsTyped()
    {
        var rule = new NameRule(NameMode.Fixed, "  x  ");

        Assert.Equal("  x  ", rule.Value);
        Assert.Equal("  x  .txt", RunOne(rule, "a.txt").NewName);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData(" \t ")]
    public void Fixed_WhitespaceOnly_CountsAsEmpty_Disabled_Unchanged(string value)
    {
        var rule = new NameRule(NameMode.Fixed, value);

        Assert.False(rule.Enabled);
        Assert.Equal(new NameParts("a", "jpg"), ApplyTo(rule, "a", "jpg"));
        Assert.Equal("a.jpg", RunOne(rule, "a.jpg").NewName);
        Assert.Equal("Old", RunFolder(rule, "Old").NewName);
    }

    [Fact]
    public void Fixed_SpacesAroundText_AppliedAsTyped_NotTrimmed()
    {
        var rule = new NameRule(NameMode.Fixed, " x ");

        Assert.True(rule.Enabled);
        Assert.Equal(" x .jpg", RunOne(rule, "a.jpg").NewName);
    }

    [Fact]
    public void Run_Reverse_MixedCase_KeepsEachCase() =>
        Assert.Equal("1bA.txt", RunOne(new NameRule(NameMode.Reverse), "Ab1.txt").NewName);

    [Fact]
    public void Run_Remove_OnAFileWithNoExtension_FlaggedEmptyName()
    {
        var result = RunOne(new NameRule(NameMode.Remove), "README");

        Assert.Equal("", result.NewName);
        Assert.Equal([new Conflict(ConflictCode.EmptyName)], new InvalidNameCheck().Check([result])[0]);
    }

    [Fact]
    public void Run_Reverse_OnAFolder_ReversesTheWholeName() =>
        Assert.Equal("2.1v", RunFolder(new NameRule(NameMode.Reverse), "v1.2").NewName);
}
