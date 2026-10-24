using Xunit;

namespace NameShift.Core.Tests;

public sealed class RemoveClassesRuleTests
{
    private static RenameResult RunOne(RemoveClassesRule rule, string name) =>
        new RenamePipeline([rule]).Run(Items.Files(name))[0];

    private static RenameResult RunFolder(RemoveClassesRule rule, string name) =>
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

    [Theory]
    [InlineData("a1b2.txt", "ab.txt")]
    [InlineData("a\u0663b.txt", "ab.txt")]
    public void Run_Digits_RemovesDecimalDigits(string name, string expected) =>
        Assert.Equal(expected, RunOne(new RemoveClassesRule(digits: true), name).NewName);

    [Theory]
    [InlineData("a\u00BD.txt")]
    [InlineData("a\u2163.txt")]
    public void Run_Digits_KeepsOtherNumerals(string name)
    {
        var result = RunOne(new RemoveClassesRule(digits: true), name);

        Assert.Equal(name, result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Theory]
    [InlineData("a!b@c#.txt", "abc.txt")]
    [InlineData("a-_.+\u20AC\u00A9b.txt", "ab.txt")]
    [InlineData("a(b)c[d].txt", "abcd.txt")]
    [InlineData("a\u201Cb\u201Dc\u00ABd\u00BBe.txt", "abcde.txt")]
    [InlineData("a^b.txt", "ab.txt")]
    public void Run_Symbols_RemovesPunctuationAndSymbols(string name, string expected) =>
        Assert.Equal(expected, RunOne(new RemoveClassesRule(symbols: true), name).NewName);

    [Fact]
    public void Run_Symbols_DotInsideTheBaseGoes_ExtensionDotStays() =>
        Assert.Equal("ab.txt", RunOne(new RemoveClassesRule(symbols: true), "a.b.txt").NewName);

    [Fact]
    public void Run_Symbols_KeepsLettersDigitsSpacesAndMarks()
    {
        var result = RunOne(new RemoveClassesRule(symbols: true), "a 1e\u0301.txt");

        Assert.Equal("a 1e\u0301.txt", result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Fact]
    public void Run_Accents_Precomposed() =>
        Assert.Equal("cafe.txt", RunOne(new RemoveClassesRule(accents: true), "caf\u00E9.txt").NewName);

    [Fact]
    public void Run_Accents_Combining() =>
        Assert.Equal("cafe.txt", RunOne(new RemoveClassesRule(accents: true), "cafe\u0301.txt").NewName);

    [Theory]
    [InlineData("\u00DF.txt")]
    [InlineData("\u00F8.txt")]
    [InlineData("\u0111.txt")]
    [InlineData("\u00E6.txt")]
    public void Run_Accents_NoDecomposition_Stays(string name)
    {
        var result = RunOne(new RemoveClassesRule(accents: true), name);

        Assert.Equal(name, result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Theory]
    [InlineData("\u00D1and\u00FA.txt", "Nandu.txt")]
    [InlineData("\u1EC7.txt", "e.txt")]
    public void Run_Accents_SpanishAndVietnamese(string name, string expected) =>
        Assert.Equal(expected, RunOne(new RemoveClassesRule(accents: true), name).NewName);

    [Fact]
    public void Run_Accents_DecomposedHangul_Unchanged()
    {
        var result = RunOne(new RemoveClassesRule(accents: true), "\u1112\u1161\u11AB.txt");

        Assert.Equal(RenameStatus.Unchanged, result.Status);
        Assert.Equal("\u1112\u1161\u11AB.txt", result.NewName);
    }

    [Fact]
    public void Run_Accents_JapaneseVoicedMark_Unchanged()
    {
        var result = RunOne(new RemoveClassesRule(accents: true), "\u304B\u3099.txt");

        Assert.Equal(RenameStatus.Unchanged, result.Status);
        Assert.Equal("\u304B\u3099.txt", result.NewName);
    }

    // Their FormD differs from the input, so a rule that returns the decomposed text when no accent went fails.
    [Theory]
    [InlineData("\u304C.txt")]
    [InlineData("\uD55C.txt")]
    public void Run_Accents_NoAccentToRemove_ReturnsTheOriginalNotTheDecomposition(string name)
    {
        var result = RunOne(new RemoveClassesRule(accents: true), name);

        Assert.Equal(RenameStatus.Unchanged, result.Status);
        Assert.Equal(name, result.NewName);
    }

    [Theory]
    [InlineData("\uD55C\u00E9.txt", "\uD55Ce.txt")]
    [InlineData("\u00E9\u304C.txt", "e\u304C.txt")]
    public void Run_Accents_OtherScriptsSurviveTheRoundTrip(string name, string expected) =>
        Assert.Equal(expected, RunOne(new RemoveClassesRule(accents: true), name).NewName);

    // Lone surrogates cannot go through [InlineData]: attribute strings are stored as UTF-8 and become U+FFFD.
    [Fact]
    public void Run_Accents_LoneSurrogate_DoesNothing_NoThrow()
    {
        var result = RunOne(new RemoveClassesRule(accents: true), "a\uD83D\u00E9.txt");

        Assert.Null(result.Error);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
        Assert.Equal("a\uD83D\u00E9.txt", result.NewName);
    }

    [Fact]
    public void Run_LoneSurrogate_OtherOptionsStillApply()
    {
        var result = RunOne(new RemoveClassesRule(accents: true, digits: true), "a1\uD83D.txt");

        Assert.Null(result.Error);
        Assert.Equal("a\uD83D.txt", result.NewName);
    }

    [Fact]
    public void Run_High_RemovesAboveAscii() =>
        Assert.Equal("ab.txt", RunOne(new RemoveClassesRule(highCharacters: true), "a\u20ACb.txt").NewName);

    [Fact]
    public void Run_High_RemovesASurrogatePairWhole()
    {
        var result = RunOne(new RemoveClassesRule(highCharacters: true), "a\U0001F600b.txt");

        Assert.Equal("ab.txt", result.NewName);
        Assert.Equal(0, LoneSurrogates(result.NewName));
    }

    [Fact]
    public void Run_AccentsBeforeHigh() =>
        Assert.Equal(
            "cafe.txt",
            RunOne(new RemoveClassesRule(accents: true, highCharacters: true), "caf\u00E9\u20AC.txt").NewName);

    [Fact]
    public void Run_HighAlone_DropsTheAccentedLetter() =>
        Assert.Equal("caf.txt", RunOne(new RemoveClassesRule(highCharacters: true), "caf\u00E9.txt").NewName);

    [Fact]
    public void Run_DigitsAndSymbols() =>
        Assert.Equal("a.txt", RunOne(new RemoveClassesRule(digits: true, symbols: true), "a_1-2.txt").NewName);

    [Fact]
    public void Run_EmptyResult_OnAFile_KeepsExtension_NotFlagged()
    {
        var result = RunOne(new RemoveClassesRule(digits: true), "123.txt");

        Assert.Equal(".txt", result.NewName);
        Assert.Empty(new InvalidNameCheck().Check([result])[0]);
    }

    [Fact]
    public void Run_EmptyResult_OnAFolder_FlaggedEmptyName()
    {
        var result = RunFolder(new RemoveClassesRule(digits: true), "123");

        Assert.Equal("", result.NewName);
        Assert.Null(result.Error);
        Assert.Equal([new Conflict(ConflictCode.EmptyName)], new InvalidNameCheck().Check([result])[0]);
    }

    [Theory]
    [InlineData(true, "a1.mp3", "a.mp3")]
    [InlineData(false, "a.\u00E9xt", "a.\u00E9xt")]
    public void Run_NeverTouchesTheExtension(bool digits, string name, string expected)
    {
        var rule = digits ? new RemoveClassesRule(digits: true) : new RemoveClassesRule(highCharacters: true);

        Assert.Equal(expected, RunOne(rule, name).NewName);
    }

    [Fact]
    public void NoOption_DisabledAndUnchanged()
    {
        var rule = new RemoveClassesRule();
        var parts = new NameParts("a1_\u00E9", "txt");
        var context = new ItemContext(Items.File("a1_\u00E9.txt"), 0, 1);

        Assert.False(rule.Enabled);
        Assert.Equal(parts, rule.Apply(parts, context));
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    public void Enabled_WhenAnyOptionIsOn(bool digits, bool symbols, bool accents, bool highCharacters) =>
        Assert.True(new RemoveClassesRule(digits, symbols, accents, highCharacters).Enabled);

    [Fact]
    public void Run_Dotfile_Symbols() =>
        Assert.Equal("gitignore", RunOne(new RemoveClassesRule(symbols: true), ".gitignore").NewName);

    [Fact]
    public void Stage_IsRemove5() =>
        Assert.Equal(RuleStage.Remove, new RemoveClassesRule().Stage);

    [Theory]
    [InlineData("è.txt", "e.txt")]
    [InlineData("à.txt", "a.txt")]
    [InlineData("aͯ.txt", "a.txt")]
    [InlineData("a˿.txt", "a˿.txt")]
    [InlineData("aͰ.txt", "aͰ.txt")]
    public void Run_Accents_RangeBoundaries(string name, string expected) =>
        Assert.Equal(expected, RunOne(new RemoveClassesRule(accents: true), name).NewName);

    [Fact]
    public void Run_High_BoundaryIsAfterU007F() =>
        Assert.Equal(
            "a\u007Fbc.txt",
            RunOne(new RemoveClassesRule(highCharacters: true), "a\u007Fb\u0080c.txt").NewName);

    [Fact]
    public void Run_Accents_UsesCanonicalFormsNotCompatibility() =>
        Assert.Equal("ﬁe.txt", RunOne(new RemoveClassesRule(accents: true), "ﬁé.txt").NewName);

    // U+FFFE makes string.Normalize throw; the accent step then does nothing and the other options still apply.
    [Fact]
    public void Run_Accents_InvalidCodePoint_DoesNothing_NoThrow()
    {
        var result = RunOne(new RemoveClassesRule(accents: true), "a￾́b.txt");

        Assert.Null(result.Error);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
        Assert.Equal("a￾́b.txt", result.NewName);
    }

    [Fact]
    public void Run_InvalidCodePoint_OtherOptionsStillApply()
    {
        var result = RunOne(new RemoveClassesRule(true, true, true), "a1￾́_b.txt");

        Assert.Null(result.Error);
        Assert.Equal("a￾́b.txt", result.NewName);
    }

    [Fact]
    public void Apply_NeverThrows_AllOptions()
    {
        const string input = "\uD800x\uDC00\u0301\u0663.txt";
        var result = RunOne(new RemoveClassesRule(true, true, true, true), input);

        Assert.Null(result.Error);
        Assert.True(LoneSurrogates(result.NewName) <= LoneSurrogates(input));
    }
}
