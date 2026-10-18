using System;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class AddRuleTests
{
    private static RenameResult RunOne(AddRule rule, string name) =>
        new RenamePipeline([rule]).Run(Items.Files(name))[0];

    [Fact]
    public void Run_Prefix_GoesBeforeTheName() =>
        Assert.Equal("new_a.txt", RunOne(new AddRule(prefix: "new_"), "a.txt").NewName);

    [Fact]
    public void Run_Suffix_GoesBeforeTheExtension() =>
        Assert.Equal("a_v2.txt", RunOne(new AddRule(suffix: "_v2"), "a.txt").NewName);

    [Fact]
    public void Run_InsertAtPosition4_KeepsThreeCharactersInFront() =>
        Assert.Equal("abc-def.txt", RunOne(new AddRule(insert: "-", insertPosition: 4), "abcdef.txt").NewName);

    [Fact]
    public void Run_InsertAtPosition1_GoesBeforeTheFirstCharacter() =>
        Assert.Equal("-abcdef.txt", RunOne(new AddRule(insert: "-", insertPosition: 1), "abcdef.txt").NewName);

    [Fact]
    public void Run_InsertPastTheEnd_IsClampedToTheEnd() =>
        Assert.Equal("abX.txt", RunOne(new AddRule(insert: "X", insertPosition: 10), "ab.txt").NewName);

    [Fact]
    public void Run_InsertFromTheEnd_KeepsThatManyCharactersAfter() =>
        Assert.Equal("abcd-ef.txt",
            RunOne(new AddRule(insert: "-", insertPosition: 2, insertFromEnd: true), "abcdef.txt").NewName);

    [Fact]
    public void Run_InsertFromTheEndPastTheStart_IsClampedToTheStart() =>
        Assert.Equal("Xab.txt",
            RunOne(new AddRule(insert: "X", insertPosition: 10, insertFromEnd: true), "ab.txt").NewName);

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-1)]
    public void Run_InsertWithoutAValidPosition_IsIgnored(int? position)
    {
        var result = RunOne(new AddRule(insert: "X", insertPosition: position), "abc.txt");

        Assert.Equal("abc.txt", result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Fact]
    public void Run_InvalidPosition_StillAppliesTheOtherParts() =>
        Assert.Equal("p_abc.txt", RunOne(new AddRule(prefix: "p_", insert: "X", insertPosition: 0), "abc.txt").NewName);

    [Fact]
    public void Run_WordSpace_SplitsCamelCaseWords() =>
        Assert.Equal("Hello World Again.txt", RunOne(new AddRule(wordSpace: true), "HelloWorldAgain.txt").NewName);

    [Fact]
    public void Run_WordSpace_SplitsAfterADigit() =>
        Assert.Equal("Photo2023 Summer.txt", RunOne(new AddRule(wordSpace: true), "Photo2023Summer.txt").NewName);

    [Fact]
    public void Run_WordSpace_LeavesUpperCaseRunsAndExistingSpacesAlone() =>
        Assert.Equal("ABCDef Ghi.txt", RunOne(new AddRule(wordSpace: true), "ABCDef Ghi.txt").NewName);

    [Fact]
    public void Run_WordSpace_LeavesTheExtensionAlone() =>
        Assert.Equal("photo Album.JpG", RunOne(new AddRule(wordSpace: true), "photoAlbum.JpG").NewName);

    [Fact]
    public void Run_EmptyTexts_LeaveTheNameUnchanged()
    {
        var result = RunOne(new AddRule(prefix: "", insert: "", insertPosition: 3, suffix: ""), "abc.txt");

        Assert.Equal("abc.txt", result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Fact]
    public void Enabled_IsFalseWhenThereIsNothingToAdd()
    {
        Assert.False(new AddRule().Enabled);
        Assert.False(new AddRule(insert: "X").Enabled);
        Assert.False(new AddRule(insertPosition: 3).Enabled);
    }

    [Fact]
    public void Enabled_IsTrueForEachPart()
    {
        Assert.True(new AddRule(prefix: "a").Enabled);
        Assert.True(new AddRule(suffix: "a").Enabled);
        Assert.True(new AddRule(wordSpace: true).Enabled);
        Assert.True(new AddRule(insert: "a", insertPosition: 1).Enabled);
    }

    [Fact]
    public void Run_AllParts_InOrder() =>
        Assert.Equal("aHelloX WorldB.txt",
            RunOne(new AddRule(prefix: "a", insert: "X", insertPosition: 6, suffix: "B", wordSpace: true), "HelloWorld.txt").NewName);

    [Fact]
    public void Run_Folder_SuffixGoesAtTheEnd()
    {
        var items = new[] { new RenameItem("My.Folder", @"C:\Test", ItemKind.Folder) };

        Assert.Equal("My.Folder_x", new RenamePipeline([new AddRule(suffix: "_x")]).Run(items)[0].NewName);
    }

    [Fact]
    public void Constructor_Defaults()
    {
        var rule = new AddRule();

        Assert.Equal("", rule.Prefix);
        Assert.Equal("", rule.Insert);
        Assert.Null(rule.InsertPosition);
        Assert.False(rule.InsertFromEnd);
        Assert.Equal("", rule.Suffix);
        Assert.False(rule.WordSpace);
        Assert.Equal(RuleStage.Add, rule.Stage);
    }

    [Fact]
    public void Constructor_RejectsNullTexts()
    {
        Assert.Throws<ArgumentNullException>(() => new AddRule(prefix: null!));
        Assert.Throws<ArgumentNullException>(() => new AddRule(insert: null!));
        Assert.Throws<ArgumentNullException>(() => new AddRule(suffix: null!));
    }
}
