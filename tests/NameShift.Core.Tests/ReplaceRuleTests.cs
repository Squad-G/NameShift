using System;
using System.Globalization;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class ReplaceRuleTests
{
    private static RenameResult RunOne(ReplaceRule rule, string name) =>
        new RenamePipeline([rule]).Run(Items.Files(name))[0];

    [Fact]
    public void Run_ReplacesASpaceWithAnUnderscore() =>
        Assert.Equal("Holiday_01.jpg", RunOne(new ReplaceRule(" ", "_"), "Holiday 01.jpg").NewName);

    [Fact]
    public void Run_IgnoresCaseByDefault() =>
        Assert.Equal("pic.JPG", RunOne(new ReplaceRule("img", "pic"), "Img.JPG").NewName);

    [Fact]
    public void Run_WithMatchCase_LeavesADifferentCaseAlone()
    {
        var result = RunOne(new ReplaceRule("img", "pic", matchCase: true), "Img.JPG");

        Assert.Equal("Img.JPG", result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Fact]
    public void Constructor_MatchCaseIsOffByDefault()
    {
        var rule = new ReplaceRule("a", "b");

        Assert.False(rule.MatchCase);
        Assert.False(rule.IncludeExtension);
        Assert.Equal(RuleStage.Replace, rule.Stage);
    }

    [Fact]
    public void Run_ReplacesEveryOccurrenceLeftToRight() =>
        Assert.Equal("bbbbbb.txt", RunOne(new ReplaceRule("a", "bb"), "aaa.txt").NewName);

    [Fact]
    public void Run_DoesNotRescanInsertedText() =>
        Assert.Equal("aaaa.txt", RunOne(new ReplaceRule("a", "aa"), "aa.txt").NewName);

    [Fact]
    public void Run_MatchesDoNotOverlap() =>
        Assert.Equal("xba.txt", RunOne(new ReplaceRule("aba", "x"), "ababa.txt").NewName);

    [Fact]
    public void Enabled_IsFalseForAnEmptyFind() =>
        Assert.False(new ReplaceRule("", "x").Enabled);

    [Fact]
    public void Apply_WithAnEmptyFind_ReturnsTheNameUnchanged()
    {
        var context = new ItemContext(Items.File("a.txt"), 0, 1);

        var result = new ReplaceRule("", "x").Apply(new NameParts("a", "txt"), context);

        Assert.Equal(new NameParts("a", "txt"), result);
    }

    [Fact]
    public void Run_WithAnEmptyFind_LeavesTheNameUnchanged()
    {
        var result = RunOne(new ReplaceRule("", "x"), "a.txt");

        Assert.Equal("a.txt", result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Fact]
    public void Run_WithAnEmptyReplacement_RemovesTheFoundText() =>
        Assert.Equal("ab.txt", RunOne(new ReplaceRule("-", ""), "a-b.txt").NewName);

    [Fact]
    public void Run_ByDefault_LeavesTheExtensionAlone()
    {
        var result = RunOne(new ReplaceRule("txt", "doc"), "x.txt");

        Assert.Equal("x.txt", result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Fact]
    public void Run_WithIncludeExtension_ReplacesInTheExtension() =>
        Assert.Equal("x.doc", RunOne(new ReplaceRule("txt", "doc", includeExtension: true), "x.txt").NewName);

    [Theory]
    [InlineData(false, "a_tar.gz")]
    [InlineData(true, "a_tar_gz")]
    public void Run_WithIncludeExtension_MatchesAcrossTheDot(bool includeExtension, string expected) =>
        Assert.Equal(expected, RunOne(new ReplaceRule(".", "_", includeExtension: includeExtension), "a.tar.gz").NewName);

    [Fact]
    public void Run_WithIncludeExtension_AFolderStaysWhole()
    {
        var item = new RenameItem("v1.2", @"C:\Test", ItemKind.Folder);

        var results = new RenamePipeline([new ReplaceRule(".", "_", includeExtension: true)]).Run([item]);

        Assert.Equal("v1_2", results[0].NewName);
    }

    [Theory]
    [InlineData("v1.2", ItemKind.Folder, "v1.2", "", "1", "x", "vx.2", "")]
    [InlineData("abc.txt", ItemKind.File, "abc", "txt", "txt", "x.y", "abc.x", "y")]
    public void Apply_WithIncludeExtension_SplitsTheResultAgain(
        string name, ItemKind kind, string baseName, string extension, string find, string with,
        string expectedBaseName, string expectedExtension)
    {
        var context = new ItemContext(new RenameItem(name, @"C:\Test", kind), 0, 1);

        var result = new ReplaceRule(find, with, includeExtension: true).Apply(new NameParts(baseName, extension), context);

        Assert.Equal(new NameParts(expectedBaseName, expectedExtension), result);
    }

    [Theory]
    [InlineData("abc.txt", ".", "_", "abc.txt")]
    [InlineData("photo (1).jpg", "(1)", "_1", "photo _1.jpg")]
    [InlineData("x+y$^[z].txt", "+y$^[", "_", "x_z].txt")]
    public void Run_MatchesRegexCharactersLiterally(string name, string find, string with, string expected) =>
        Assert.Equal(expected, RunOne(new ReplaceRule(find, with), name).NewName);

    [Fact]
    public void Run_InsertsTheReplacementAsTyped() =>
        Assert.Equal("bobStack.log", RunOne(new ReplaceRule("dump", "bob"), "DumpStack.log").NewName);

    [Fact]
    public void Run_IgnoresCaseWhateverTheCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            Assert.Equal("x.txt", RunOne(new ReplaceRule("title", "x"), "TITLE.txt").NewName);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Constructor_RejectsNullFindOrWith()
    {
        Assert.Throws<ArgumentNullException>(() => new ReplaceRule(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => new ReplaceRule("x", null!));
    }
}
