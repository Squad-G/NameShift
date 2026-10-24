using System;
using System.Globalization;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class RemoveTextRuleTests
{
    private static RenameResult RunOne(RemoveTextRule rule, string name) =>
        new RenamePipeline([rule]).Run(Items.Files(name))[0];

    private static RenameResult RunFolder(RemoveTextRule rule, string name) =>
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
    [InlineData("-_", "a-b_c.txt", "abc.txt")]
    [InlineData(" ", "a b c.txt", "abc.txt")]
    [InlineData("-_", "--a__.txt", "a.txt")]
    public void Run_Characters_RemovesEachListedCharacter(string characters, string name, string expected) =>
        Assert.Equal(expected, RunOne(new RemoveTextRule(characters: characters), name).NewName);

    [Theory]
    [InlineData("my old file.txt", "my  file.txt")]
    [InlineData("my OLD file.txt", "my  file.txt")]
    public void Run_Text_RemovesIgnoringCaseByDefault(string name, string expected) =>
        Assert.Equal(expected, RunOne(new RemoveTextRule(text: "old"), name).NewName);

    [Fact]
    public void Run_Text_MatchCase_LeavesOtherCase()
    {
        var result = RunOne(new RemoveTextRule(text: "old", matchCase: true), "my OLD file.txt");

        Assert.Equal("my OLD file.txt", result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Fact]
    public void Run_Text_LeftToRightWithoutOverlap() =>
        Assert.Equal("a.txt", RunOne(new RemoveTextRule(text: "aa"), "aaa.txt").NewName);

    [Theory]
    [InlineData("concatenate.txt", "concatenate.txt")]
    [InlineData("my cat.txt", "my .txt")]
    [InlineData("cat_dog.txt", "_dog.txt")]
    [InlineData("dog_cat.txt", "dog_.txt")]
    [InlineData("a-cat.txt", "a-.txt")]
    [InlineData("cat.txt", ".txt")]
    [InlineData("CAT x.txt", " x.txt")]
    [InlineData("cat1.txt", "cat1.txt")]
    [InlineData("1cat.txt", "1cat.txt")]
    public void Run_WholeWord_OnlyAtBoundaries(string name, string expected) =>
        Assert.Equal(expected, RunOne(new RemoveTextRule(text: "cat", wholeWord: true), name).NewName);

    [Fact]
    public void Run_WholeWord_MatchCaseOn_ExactOnly() =>
        Assert.Equal(
            "CAT x.txt",
            RunOne(new RemoveTextRule(text: "cat", wholeWord: true, matchCase: true), "CAT x.txt").NewName);

    [Theory]
    [InlineData("cat", "cats cat.txt", "cats .txt")]
    [InlineData("a a", "ba a a.txt", "ba .txt")]
    public void Run_WholeWord_SkipsAPartialMatch_ThenFindsTheWord(string text, string name, string expected) =>
        Assert.Equal(expected, RunOne(new RemoveTextRule(text: text, wholeWord: true), name).NewName);

    [Fact]
    public void Run_WholeWord_DigitsAreWordCharacters() =>
        Assert.Equal("a1 .txt", RunOne(new RemoveTextRule(text: "1", wholeWord: true), "a1 1.txt").NewName);

    [Theory]
    [InlineData("\U0001D400cat.txt", "\U0001D400cat.txt")]
    [InlineData("cat\U0001D400.txt", "cat\U0001D400.txt")]
    [InlineData("\U0001F600cat\U0001F600.txt", "\U0001F600\U0001F600.txt")]
    public void Run_WholeWord_ReadsASurrogatePairNeighbourAsOneCharacter(string name, string expected) =>
        Assert.Equal(expected, RunOne(new RemoveTextRule(text: "cat", wholeWord: true), name).NewName);

    [Fact]
    public void Run_Crop_Before_RemovesTheMarkerByDefault() =>
        Assert.Equal("Final.txt", RunOne(new RemoveTextRule(cropBefore: " - "), "Report - Final.txt").NewName);

    [Fact]
    public void Run_Crop_After_RemovesTheMarkerByDefault() =>
        Assert.Equal("Report.txt", RunOne(new RemoveTextRule(cropAfter: " - "), "Report - Final.txt").NewName);

    [Fact]
    public void Run_Crop_Before_KeepMarker() =>
        Assert.Equal(
            " - Final.txt",
            RunOne(new RemoveTextRule(cropBefore: " - ", keepMarker: true), "Report - Final.txt").NewName);

    [Fact]
    public void Run_Crop_After_KeepMarker() =>
        Assert.Equal(
            "Report - .txt",
            RunOne(new RemoveTextRule(cropAfter: " - ", keepMarker: true), "Report - Final.txt").NewName);

    [Fact]
    public void Run_Crop_Before_UsesTheFirstOccurrence() =>
        Assert.Equal("b - c.txt", RunOne(new RemoveTextRule(cropBefore: " - "), "a - b - c.txt").NewName);

    [Fact]
    public void Run_Crop_After_UsesTheFirstOccurrence() =>
        Assert.Equal("a.txt", RunOne(new RemoveTextRule(cropAfter: " - "), "a - b - c.txt").NewName);

    [Fact]
    public void Run_BothCrops_AfterRunsOnTheResultOfBefore() =>
        Assert.Equal("keep.txt", RunOne(new RemoveTextRule(cropBefore: "[", cropAfter: "]"), "x[keep]y.txt").NewName);

    [Fact]
    public void Run_BothCrops_AfterMarkerBeforeTheBeforeMarker_IsIgnored() =>
        Assert.Equal("b.txt", RunOne(new RemoveTextRule(cropBefore: "[", cropAfter: "]"), "a]x[b.txt").NewName);

    [Theory]
    [InlineData("#", "")]
    [InlineData("", "#")]
    public void Run_EmptyOrMissing_DoesNothing(string cropBefore, string cropAfter)
    {
        var result = RunOne(new RemoveTextRule(cropBefore: cropBefore, cropAfter: cropAfter), "Report - Final.txt");

        Assert.Equal("Report - Final.txt", result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Fact]
    public void Run_OnlyFlags_DoesNothing() =>
        Assert.Equal(
            "a b.txt",
            RunOne(new RemoveTextRule(wholeWord: true, matchCase: true, keepMarker: true), "a b.txt").NewName);

    [Fact]
    public void Enabled_OnlyWhenAPartHasText()
    {
        Assert.False(new RemoveTextRule().Enabled);
        Assert.False(new RemoveTextRule(wholeWord: true).Enabled);
        Assert.False(new RemoveTextRule(matchCase: true).Enabled);
        Assert.False(new RemoveTextRule(keepMarker: true).Enabled);
        Assert.True(new RemoveTextRule(characters: "a").Enabled);
        Assert.True(new RemoveTextRule(text: "a").Enabled);
        Assert.True(new RemoveTextRule(cropBefore: "a").Enabled);
        Assert.True(new RemoveTextRule(cropAfter: "a").Enabled);
    }

    [Fact]
    public void Run_MatchCaseOff_AppliesToCharactersTextAndMarkers()
    {
        Assert.Equal("MG-1.jpg", RunOne(new RemoveTextRule(characters: "i"), "IMG-1.jpg").NewName);
        Assert.Equal("b.txt", RunOne(new RemoveTextRule(cropBefore: "x"), "aXb.txt").NewName);
        Assert.Equal("a.txt", RunOne(new RemoveTextRule(cropAfter: "x"), "aXb.txt").NewName);
    }

    [Theory]
    [InlineData("i", "", "", "IMG-1.jpg")]
    [InlineData("", "x", "", "aXb.txt")]
    [InlineData("", "", "x", "aXb.txt")]
    public void Run_MatchCaseOn_ExactOnly(string characters, string cropBefore, string cropAfter, string name)
    {
        var rule = new RemoveTextRule(characters: characters, matchCase: true, cropBefore: cropBefore, cropAfter: cropAfter);

        Assert.Equal(name, RunOne(rule, name).NewName);
    }

    [Fact]
    public void Run_IgnoreCase_WhateverTheCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            Assert.Equal(" x.txt", RunOne(new RemoveTextRule(text: "title"), "TITLE x.txt").NewName);
            Assert.Equal("x.txt", RunOne(new RemoveTextRule(cropBefore: "title "), "TITLE x.txt").NewName);
            Assert.Equal("TTLE.txt", RunOne(new RemoveTextRule(characters: "i"), "TITLE.txt").NewName);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Theory]
    [InlineData("txt", "", "a.txt.bak", "a..bak")]
    [InlineData("", "t", "test.txt", "es.txt")]
    public void Run_NeverTouchesTheExtension(string text, string characters, string name, string expected) =>
        Assert.Equal(expected, RunOne(new RemoveTextRule(characters: characters, text: text), name).NewName);

    [Fact]
    public void Run_EmptyResult_OnAFile_KeepsExtension_NotFlagged()
    {
        var result = RunOne(new RemoveTextRule(characters: "-_"), "-_.txt");

        Assert.Equal(".txt", result.NewName);
        Assert.Empty(new InvalidNameCheck().Check([result])[0]);
    }

    [Fact]
    public void Run_EmptyResult_OnAFolder_FlaggedEmptyName()
    {
        var result = RunFolder(new RemoveTextRule(characters: "-"), "---");

        Assert.Equal("", result.NewName);
        Assert.Null(result.Error);
        Assert.Equal([new Conflict(ConflictCode.EmptyName)], new InvalidNameCheck().Check([result])[0]);
    }

    [Fact]
    public void Run_Characters_RemovesAWholeSurrogatePair() =>
        Assert.Equal("ab.txt", RunOne(new RemoveTextRule(characters: "\U0001F600"), "a\U0001F600b.txt").NewName);

    [Fact]
    public void Run_Characters_SameHighSurrogate_OtherEmoji_Untouched()
    {
        var result = RunOne(new RemoveTextRule(characters: "\U0001F600"), "a\U0001F601.txt");

        Assert.Equal("a\U0001F601.txt", result.NewName);
        Assert.Equal(0, LoneSurrogates(result.NewName));
    }

    [Fact]
    public void Run_Text_HalfOfAPair_IsNotMatched()
    {
        var result = RunOne(new RemoveTextRule(text: "\uD83D"), "a\U0001F600b.txt");

        Assert.Equal("a\U0001F600b.txt", result.NewName);
        Assert.Equal(0, LoneSurrogates(result.NewName));
    }

    [Fact]
    public void Run_Crop_MarkerThatEndsInsideAPair_IsNotMatched()
    {
        var result = RunOne(new RemoveTextRule(cropBefore: "\uD83D"), "a\U0001F600b.txt");

        Assert.Equal("a\U0001F600b.txt", result.NewName);
        Assert.Equal(0, LoneSurrogates(result.NewName));
    }

    [Fact]
    public void Run_Text_StartingInsideAPair_IsNotMatched()
    {
        var result = RunOne(new RemoveTextRule(text: "\uDE00"), "a\U0001F600b.txt");

        Assert.Equal("a\U0001F600b.txt", result.NewName);
        Assert.Equal(0, LoneSurrogates(result.NewName));
    }

    [Fact]
    public void Run_Crop_MarkerThatStartsInsideAPair_IsNotMatched()
    {
        var result = RunOne(new RemoveTextRule(cropAfter: "\uDE00"), "a\U0001F600b.txt");

        Assert.Equal("a\U0001F600b.txt", result.NewName);
        Assert.Equal(0, LoneSurrogates(result.NewName));
    }

    [Fact]
    public void Run_Dotfile_WholeNameIsTheBase() =>
        Assert.Equal(".ignore", RunOne(new RemoveTextRule(text: "git"), ".gitignore").NewName);

    [Fact]
    public void Run_Folder_WholeNameIsTheBase() =>
        Assert.Equal(".2", RunFolder(new RemoveTextRule(cropBefore: "1"), "Folder 1.2").NewName);

    [Fact]
    public void Pipeline_ReplaceRunsBeforeRemove() =>
        Assert.Equal(
            ".txt",
            new RenamePipeline([new RemoveTextRule(text: "b"), new ReplaceRule("a", "b")]).Run(Items.Files("ab.txt"))[0].NewName);

    [Fact]
    public void Run_PartsRunInOrder_CropsTextCharacters() =>
        Assert.Equal(
            "e.txt",
            RunOne(new RemoveTextRule(characters: "p", text: "ke", cropBefore: "[", cropAfter: "]"), "x[keep]y.txt").NewName);

    [Fact]
    public void Run_PartsRunInOrder_TextBeforeCharacters() =>
        Assert.Equal("ab.txt", RunOne(new RemoveTextRule(characters: "x", text: "ab"), "axb.txt").NewName);

    [Theory]
    [InlineData("characters")]
    [InlineData("text")]
    [InlineData("cropBefore")]
    [InlineData("cropAfter")]
    public void Constructor_NullString_Throws(string argument)
    {
        var thrown = Assert.Throws<ArgumentNullException>(() => argument switch
        {
            "characters" => new RemoveTextRule(characters: null!),
            "text" => new RemoveTextRule(text: null!),
            "cropBefore" => new RemoveTextRule(cropBefore: null!),
            _ => new RemoveTextRule(cropAfter: null!),
        });

        Assert.Equal(argument, thrown.ParamName);
    }

    [Fact]
    public void Constructor_Defaults_AreEmptyAndOff()
    {
        var rule = new RemoveTextRule();

        Assert.Equal("", rule.Characters);
        Assert.Equal("", rule.Text);
        Assert.Equal("", rule.CropBefore);
        Assert.Equal("", rule.CropAfter);
        Assert.False(rule.WholeWord);
        Assert.False(rule.MatchCase);
        Assert.False(rule.KeepMarker);
    }

    [Fact]
    public void Stage_IsRemove5()
    {
        Assert.Equal(RuleStage.Remove, new RemoveTextRule().Stage);
        Assert.Equal(5, (int)RuleStage.Remove);
    }

    [Theory]
    [InlineData("\uD800")]
    [InlineData("\0")]
    public void Apply_NeverThrows_OnOddValues(string odd)
    {
        var whole = new RemoveTextRule(odd, odd, wholeWord: true, cropBefore: odd, cropAfter: odd, keepMarker: true);
        var plain = new RemoveTextRule(odd, odd, cropBefore: odd, cropAfter: odd);

        Assert.True(LoneSurrogates(RunOne(whole, "a\uD800b.txt").NewName) <= 1);
        Assert.True(LoneSurrogates(RunOne(plain, "a\uD800b.txt").NewName) <= 1);
        Assert.Equal(0, LoneSurrogates(RunOne(plain, "a\U0001F600b.txt").NewName));
    }
}
