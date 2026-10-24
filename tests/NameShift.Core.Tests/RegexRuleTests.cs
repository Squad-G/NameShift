using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class RegexRuleTests
{
    private static RenameResult RunOne(RegexRule rule, string name) =>
        new RenamePipeline([rule]).Run(Items.Files(name))[0];

    private static RenameResult RunFolder(RegexRule rule, string name) =>
        new RenamePipeline([rule]).Run([new RenameItem(name, @"C:\Test", ItemKind.Folder)])[0];

    [Fact]
    public void Run_NumberedGroup() =>
        Assert.Equal("Photo 042.jpg", RunOne(new RegexRule(@"IMG_(\d+)", "Photo $1"), "IMG_042.jpg").NewName);

    [Fact]
    public void Run_NamedGroup() =>
        Assert.Equal("ax12.txt", RunOne(new RegexRule(@"(?<n>\d+)", "x${n}"), "a12.txt").NewName);

    [Fact]
    public void Run_WholeMatchAndDollar() =>
        Assert.Equal("a[1]$.txt", RunOne(new RegexRule(@"\d", "[$&]$$"), "a1.txt").NewName);

    [Fact]
    public void Run_EveryMatchLeftToRight() =>
        Assert.Equal("bXbXb.txt", RunOne(new RegexRule("a", "b"), "aXaXa.txt").NewName);

    [Theory]
    [InlineData("(")]
    [InlineData("[a-")]
    [InlineData("*a")]
    public void Validate_InvalidPattern_NonEmptyMessage(string pattern)
    {
        var message = RegexRule.Validate(pattern);

        Assert.NotNull(message);
        Assert.False(string.IsNullOrWhiteSpace(message));
    }

    [Theory]
    [InlineData("(")]
    [InlineData("[a-")]
    [InlineData("*a")]
    public void Ctor_InvalidPattern_ThrowsArgumentException(string pattern)
    {
        var ex = Assert.Throws<ArgumentException>(() => new RegexRule(pattern, ""));

        Assert.Equal("pattern", ex.ParamName);
        Assert.NotNull(ex.InnerException);
    }

    [Theory]
    [InlineData(@"IMG_(\d+)")]
    [InlineData("")]
    [InlineData("a")]
    public void Validate_ValidPattern_Null(string pattern) =>
        Assert.Null(RegexRule.Validate(pattern));

    [Fact]
    public void Ctor_NullPatternOrReplacement_ThrowsArgumentNull()
    {
        var pattern = Assert.Throws<ArgumentNullException>(() => new RegexRule(null!, ""));
        var replacement = Assert.Throws<ArgumentNullException>(() => new RegexRule("a", null!));

        Assert.Equal("pattern", pattern.ParamName);
        Assert.Equal("replacement", replacement.ParamName);
    }

    [Fact]
    public void Validate_Null_ThrowsArgumentNull() =>
        Assert.Throws<ArgumentNullException>(() => RegexRule.Validate(null!));

    [Fact]
    public void Run_BaseOnly_ExtensionUntouched() =>
        Assert.Equal(".txt", RunOne(new RegexRule("txt", ""), "txt.txt").NewName);

    [Fact]
    public void Run_IncludeExtension_FixesExtension() =>
        Assert.Equal("a.jpg", RunOne(new RegexRule(@"\.jpe?g$", ".jpg", includeExtension: true), "a.jpeg").NewName);

    [Fact]
    public void Run_IncludeExtension_ResplitsTheName()
    {
        var rule = new RegexRule(@"^(.*)\.(.*)$", "$2.$1", includeExtension: true);

        Assert.Equal("txt.a", RunOne(rule, "a.txt").NewName);

        var parts = rule.Apply(new NameParts("a", "txt"), new ItemContext(Items.File("a.txt"), 0, 1));

        Assert.Equal("txt", parts.BaseName);
        Assert.Equal("a", parts.Extension);
    }

    [Fact]
    public void Run_BaseOnly_DollarAnchorsAtBaseEnd() =>
        Assert.Equal("notX.txt", RunOne(new RegexRule("e$", "X"), "note.txt").NewName);

    [Fact]
    public void Run_IgnoreCase_Matches() =>
        Assert.Equal("x_1.jpg", RunOne(new RegexRule("img", "x", ignoreCase: true), "IMG_1.jpg").NewName);

    [Fact]
    public void Run_CaseSensitiveByDefault()
    {
        var result = RunOne(new RegexRule("img", "x"), "IMG_1.jpg");

        Assert.Equal("IMG_1.jpg", result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Fact]
    public void Run_IgnoreCase_InvariantUnderTurkishCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            // The regex reads the culture when it is built, so both rules are built here.
            var dotted = new RegexRule("i", "x", ignoreCase: true);
            var dotless = new RegexRule("I", "x", ignoreCase: true);

            Assert.Equal("x.txt", RunOne(dotted, "I.txt").NewName);
            Assert.Equal("\u0131.txt", RunOne(dotless, "\u0131.txt").NewName);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Run_Timeout_ItemErrorOthersFine()
    {
        var slow = new string('a', 36) + "b.txt";
        var rule = new RegexRule("(a+)+$", "x");

        var stopwatch = Stopwatch.StartNew();
        var results = new RenamePipeline([rule]).Run(Items.Files("a.txt", slow, "b.txt"));
        stopwatch.Stop();

        Assert.Equal("x.txt", results[0].NewName);
        Assert.Equal(RenameStatus.Changed, results[0].Status);
        Assert.Equal(RenameStatus.Error, results[1].Status);
        Assert.Contains("took too long", results[1].Error!.Message);
        Assert.Equal(slow, results[1].NewName);
        Assert.Equal("b.txt", results[2].NewName);
        Assert.Equal(RenameStatus.Unchanged, results[2].Status);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"took {stopwatch.Elapsed}");
    }

    [Fact]
    public void Apply_Timeout_WrapsRegexMatchTimeout()
    {
        var slow = new string('a', 36) + "b";
        var rule = new RegexRule("(a+)+$", "x");

        var ex = Assert.Throws<InvalidOperationException>(
            () => rule.Apply(new NameParts(slow, "txt"), new ItemContext(Items.File(slow + ".txt"), 0, 1)));

        var timeout = Assert.IsType<RegexMatchTimeoutException>(ex.InnerException);
        Assert.Equal(TimeSpan.FromMilliseconds(100), timeout.MatchTimeout);
    }

    // Built in code: a name with a line break pins "no Multiline" (^ is only the start of the searched text).
    [Fact]
    public void Apply_NoMultiline_CaretOnlyAtStart() =>
        Assert.Equal(
            new NameParts("a\nb", "txt"),
            new RegexRule("^b", "x").Apply(new NameParts("a\nb", "txt"), new ItemContext(Items.File("a.txt"), 0, 1)));

    [Fact]
    public void EmptyPattern_DisabledAndUnchanged()
    {
        var rule = new RegexRule("", "x");
        var parts = rule.Apply(new NameParts("ab", "txt"), new ItemContext(Items.File("ab.txt"), 0, 1));

        Assert.False(rule.Enabled);
        Assert.Equal(new NameParts("ab", "txt"), parts);
        Assert.Equal(RenameStatus.Unchanged, RunOne(rule, "ab.txt").Status);
    }

    [Fact]
    public void Run_EmptyReplacement_RemovesMatches() =>
        Assert.Equal("ab.txt", RunOne(new RegexRule(@"\d+", ""), "a12b.txt").NewName);

    [Fact]
    public void Run_MissingGroup_KeptAsTyped() =>
        Assert.Equal("$9bc.txt", RunOne(new RegexRule("a", "$9"), "abc.txt").NewName);

    [Fact]
    public void Run_EmptyMatch_DotNetBehaviour()
    {
        var result = RunOne(new RegexRule("x*", "-"), "ab.txt");

        Assert.Equal("-a-b-.txt", result.NewName);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Run_EmptyResult_OnAFile_KeepsExtension_NotFlagged()
    {
        var result = RunOne(new RegexRule(".+", ""), "abc.txt");

        Assert.Equal(".txt", result.NewName);
        Assert.Empty(new InvalidNameCheck().Check([result])[0]);
    }

    [Fact]
    public void Run_EmptyResult_OnAFolder_FlaggedEmptyName()
    {
        var result = RunFolder(new RegexRule(".+", ""), "abc");

        Assert.Equal("", result.NewName);
        Assert.Null(result.Error);
        Assert.Equal([new Conflict(ConflictCode.EmptyName)], new InvalidNameCheck().Check([result])[0]);
    }

    [Fact]
    public void Run_InvalidCharacter_AppliedAndFlagged()
    {
        var result = RunOne(new RegexRule("-", ":"), "a-b.txt");

        Assert.Equal("a:b.txt", result.NewName);
        Assert.Equal(RenameStatus.Changed, result.Status);
        Assert.Contains(new InvalidNameCheck().Check([result])[0], c => c.Code == ConflictCode.InvalidCharacter);
    }

    [Fact]
    public void Run_Folder_WholeNameIsBase() =>
        Assert.Equal("Folder #.#", RunFolder(new RegexRule(@"\d", "#"), "Folder 1.2").NewName);

    [Fact]
    public void Run_Dotfile()
    {
        var item = new RenameItem(".gitignore", @"C:\Test", ItemKind.File, ItemAttributes.Hidden);

        var result = new RenamePipeline([new RegexRule(@"^\.", "")]).Run([item])[0];

        Assert.Equal("gitignore", result.NewName);
    }

    [Fact]
    public void Pipeline_RegexRunsBeforeReplace()
    {
        var pipeline = new RenamePipeline([new ReplaceRule("b", "c"), new RegexRule("a", "b")]);

        Assert.Equal("c.txt", pipeline.Run(Items.Files("a.txt"))[0].NewName);
    }

    [Fact]
    public void Run_SurrogatePair_OnlyMatchesReplaced() =>
        Assert.Equal("x\U0001F600x.txt", RunOne(new RegexRule("a", "x"), "a\U0001F600a.txt").NewName);

    // Lone surrogates cannot go through [InlineData]: attribute strings are stored as UTF-8 and become U+FFFD.
    [Fact]
    public void Run_LoneSurrogate_NoThrow()
    {
        var result = RunOne(new RegexRule("a", "x"), "a\uD83Da.txt");

        Assert.Equal("x\uD83Dx.txt", result.NewName);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Properties_ReturnTheValuesGiven()
    {
        var rule = new RegexRule("p", "r", ignoreCase: true, includeExtension: true);
        var defaults = new RegexRule("p", "r");

        Assert.Equal("p", rule.Pattern);
        Assert.Equal("r", rule.Replacement);
        Assert.True(rule.IgnoreCase);
        Assert.True(rule.IncludeExtension);
        Assert.Equal(RuleStage.RegEx, rule.Stage);
        Assert.False(defaults.IgnoreCase);
        Assert.False(defaults.IncludeExtension);
    }
}
