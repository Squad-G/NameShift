using Xunit;

namespace NameShift.Core.Tests;

public sealed class RemoveTidyRuleTests
{
    private static RenameResult RunOne(RemoveTidyRule rule, string name) =>
        new RenamePipeline([rule]).Run(Items.Files(name))[0];

    private static RenameResult RunFolder(RemoveTidyRule rule, string name) =>
        new RenamePipeline([rule]).Run([new RenameItem(name, @"C:\Test", ItemKind.Folder)])[0];

    private static string Spaces(int count) => new string(' ', count);

    [Fact]
    public void Run_Trim_RemovesLeadingAndTrailingWhiteSpace() =>
        Assert.Equal("a b.txt", RunOne(new RemoveTidyRule(trim: true), "  a b .txt").NewName);

    [Fact]
    public void Run_Trim_UsesCharIsWhiteSpace() =>
        // tab, no-break space and em space are all white space
        Assert.Equal("a.txt", RunOne(new RemoveTidyRule(trim: true), "\t\u00A0a\u2003.txt").NewName);

    [Theory]
    [InlineData("..a.txt", "a.txt")]
    [InlineData(".gitignore", "gitignore")]
    public void Run_LeadingDots_RemovesThem(string name, string expected) =>
        Assert.Equal(expected, RunOne(new RemoveTidyRule(leadingDots: true), name).NewName);

    [Fact]
    public void Run_LeadingDots_KeepsATrailingDotOfTheBase() =>
        Assert.Equal("a..txt", RunOne(new RemoveTidyRule(leadingDots: true), "a..txt").NewName);

    [Fact]
    public void Run_Dotfile_UnchangedWhenOff()
    {
        var result = RunOne(new RemoveTidyRule(trim: true, doubleSpaces: true), ".gitignore");

        Assert.Equal(".gitignore", result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Fact]
    public void Run_LeadingDots_OnAFolder() =>
        Assert.Equal("hidden", RunFolder(new RemoveTidyRule(leadingDots: true), ".hidden").NewName);

    [Theory]
    [InlineData("a" + "   " + "b.txt", "a b.txt")] // three spaces
    [InlineData("a b.txt", "a b.txt")] // one space
    [InlineData("a  b   c.txt", "a b c.txt")] // runs of two and three
    public void Run_DoubleSpaces_CollapsesRuns(string name, string expected) =>
        Assert.Equal(expected, RunOne(new RemoveTidyRule(doubleSpaces: true), name).NewName);

    [Theory]
    [InlineData("a\t\tb.txt")]
    [InlineData("a\u00A0\u00A0b.txt")]
    [InlineData("a \u00A0 b.txt")]
    public void Run_DoubleSpaces_LeavesOtherWhiteSpace(string name) =>
        Assert.Equal(name, RunOne(new RemoveTidyRule(doubleSpaces: true), name).NewName);

    [Fact]
    public void Run_LeadingDotsThenTrim_TrimsTheSpaceLeftBehind() =>
        Assert.Equal("a.txt", RunOne(new RemoveTidyRule(trim: true, leadingDots: true), ".. a.txt").NewName);

    [Fact]
    public void Run_TrimThenLeadingDots() =>
        Assert.Equal("a.txt", RunOne(new RemoveTidyRule(trim: true, leadingDots: true), " ..a.txt").NewName);

    [Theory]
    [InlineData(".. a.txt", " a.txt")]
    [InlineData(" ..a.txt", " ..a.txt")]
    public void Run_LeadingDotsAlone_OnlyAtTheVeryStart(string name, string expected) =>
        Assert.Equal(expected, RunOne(new RemoveTidyRule(leadingDots: true), name).NewName);

    [Fact]
    public void Run_DoubleSpacesAndTrim() =>
        Assert.Equal("a b.txt", RunOne(new RemoveTidyRule(trim: true, doubleSpaces: true), "  a   b  .txt").NewName);

    [Fact]
    public void Run_NothingToTidy_Unchanged()
    {
        var result = RunOne(new RemoveTidyRule(true, true, true), "a b.txt");

        Assert.Equal("a b.txt", result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Fact]
    public void NoOption_Disabled() => Assert.False(new RemoveTidyRule().Enabled);

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Enabled_WhenAnyOptionIsOn(bool trim, bool leadingDots, bool doubleSpaces) =>
        Assert.True(new RemoveTidyRule(trim, leadingDots, doubleSpaces).Enabled);

    [Fact]
    public void Run_FileOfSpaces_GivesDotExtension_NotFlagged()
    {
        var result = RunOne(new RemoveTidyRule(trim: true), "  .txt");

        Assert.Equal(".txt", result.NewName);
        Assert.Empty(new InvalidNameCheck().Check([result])[0]);
    }

    [Fact]
    public void Run_FolderOfSpaces_FlaggedEmptyName()
    {
        var result = RunFolder(new RemoveTidyRule(trim: true), Spaces(3));

        Assert.Equal("", result.NewName);
        Assert.Equal([new Conflict(ConflictCode.EmptyName)], new InvalidNameCheck().Check([result])[0]);
    }

    [Fact]
    public void Run_AllDotsFile_FlaggedEmptyName()
    {
        var result = RunOne(new RemoveTidyRule(leadingDots: true), "...");

        Assert.Equal("", result.NewName);
        Assert.Equal([new Conflict(ConflictCode.EmptyName)], new InvalidNameCheck().Check([result])[0]);
    }

    [Fact]
    public void Run_NeverTouchesTheExtension() =>
        Assert.Equal("a b.my" + Spaces(2) + "ext",
            RunOne(new RemoveTidyRule(doubleSpaces: true), "a" + Spaces(2) + "b.my" + Spaces(2) + "ext").NewName);

    [Fact]
    public void Run_DotBaseWithExtension()
    {
        var result = RunOne(new RemoveTidyRule(leadingDots: true), "..txt");

        Assert.Equal(".txt", result.NewName);
        Assert.Empty(new InvalidNameCheck().Check([result])[0]);
    }

    [Fact]
    public void Pipeline_ReplaceRunsBeforeTidy()
    {
        var pipeline = new RenamePipeline([new RemoveTidyRule(trim: true), new ReplaceRule("x", " ")]);

        Assert.Equal("a.txt", pipeline.Run(Items.Files("ax.txt"))[0].NewName);
    }

    [Fact]
    public void Stage_IsRemove5() => Assert.Equal(RuleStage.Remove, new RemoveTidyRule().Stage);

    [Fact]
    public void Apply_NeverThrows()
    {
        var result = RunOne(new RemoveTidyRule(true, true, true), "\uD800 . a\0.txt");

        Assert.Null(result.Error);
    }
}
