using System;
using System.Collections.Generic;
using System.Linq;
using NameShift.Core;
using NameShift.Ui.Files;
using Xunit;
using UiStrings = NameShift.Ui.Strings;

namespace NameShift.Cmd.Tests;

/// <summary>The filter options of <c>apply</c> in the parser (usage errors, the filter built) and the mask taken from
/// the folder text. A tilde in a folder text stands for a backslash and a caret for a double quote, so that no
/// escape sequence is written in this file.</summary>
public sealed class FilterParserTests
{
    private static readonly string[] ValuedWords =
    {
        FilterReader.MaskOption, FilterReader.RegexOption, FilterReader.MinLengthOption, FilterReader.MaxLengthOption,
    };

    private static readonly string[] Switches =
    {
        FilterReader.IgnoreCaseOption, FilterReader.IncludeHiddenOption,
    };

    private static readonly string[] Kinds =
    {
        FilterReader.FilesOnlyOption, FilterReader.FoldersOnlyOption, FilterReader.IncludeFoldersOption,
    };

    private static string Tail => " " + Strings.Usage + " " + Strings.HelpPointer;

    private static string Fmt(string format, params object[] values) => string.Format(Cmd.EnUs, format, values);

    private static string Real(string text) => text.Replace('~', (char)92).Replace('^', '"');

    /// <summary>The folder D:, P, a rule, then the joined options.</summary>
    private static ParsedCommand Parse(string joined) => ParseIn("D:~P", joined);

    private static ParsedCommand ParseIn(string folder, string joined)
    {
        var args = new List<string> { "apply", Real(folder), "--case", "lower" };
        args.AddRange(joined.Split('|'));
        return CommandLineParser.Parse(args);
    }

    private static ApplyOptions Ok(string joined)
    {
        ParsedCommand command = Parse(joined);
        Assert.True(command.Kind == CommandKind.Apply, command.Error);
        return command.Options!;
    }

    private static void Fails(string expected, string joined)
    {
        ParsedCommand command = Parse(joined);
        Assert.Equal(CommandKind.Error, command.Kind);
        Assert.Equal(expected + Tail, command.Error);
    }

    private static string Once(string word)
    {
        switch (word)
        {
            case FilterReader.MaskOption:
                return word + "|*.jpg";
            case FilterReader.RegexOption:
                return word + "|a";
            case FilterReader.MinLengthOption:
            case FilterReader.MaxLengthOption:
                return word + "|3";
            default:
                return word;
        }
    }

    [Fact]
    public void Mask_FillsTheModel()
    {
        ApplyOptions options = Ok("--mask|*.jpg;*.png");

        Assert.Equal("*.jpg;*.png", options.Filter.Mask);
        Assert.True(options.Filtered);
        Assert.False(options.Filter.IncludeFolders);
    }

    [Fact]
    public void NoFilterOption_IsNotFiltered_FilesOnly()
    {
        ApplyOptions options = Ok("--dry-run");

        Assert.False(options.Filtered);
        Assert.False(options.Filter.IncludeFolders);
        Assert.True(options.Filter.IncludeFiles);
        Assert.False(options.Filter.IncludeHidden);
    }

    [Fact]
    public void Pattern_And_IgnoreCase_FillTheModel()
    {
        ApplyOptions options = Ok("--filter-regex|^IMG_[0-9]+$|--filter-ignore-case");

        Assert.Equal("^IMG_[0-9]+$", options.Filter.Pattern);
        Assert.True(options.Filter.IgnoreCase);
        Assert.Null(options.Filter.Error);
    }

    [Fact]
    public void IgnoreCase_WithoutPattern_NeedsOption()
    {
        Fails(Fmt(Strings.NeedsOption, "--filter-ignore-case", "--filter-regex"), "--filter-ignore-case");
    }

    [Fact]
    public void BadPattern_NamesTheWindowsText()
    {
        string windowText = new FilterPanelModel { Pattern = "(" }.Error!;

        Fails(Fmt(Strings.OptionError, "--filter-regex", windowText), "--filter-regex|(");
    }

    [Theory]
    [InlineData("--filter-regex")]
    [InlineData("--mask")]
    public void EmptyText_IsRefused(string option)
    {
        Fails(Fmt(Strings.EmptyOptionText, option), option + "|");
    }

    [Fact]
    public void WhiteSpaceMask_IsRefused_WhiteSpacePatternIsAllowed()
    {
        Fails(Fmt(Strings.EmptyOptionText, "--mask"), "--mask| ");
        Assert.Equal(" ", Ok("--filter-regex| ").Filter.Pattern);
    }

    [Theory]
    [InlineData("--min-length", "x")]
    [InlineData("--min-length", "-1")]
    [InlineData("--min-length", "1.5")]
    [InlineData("--max-length", "x")]
    [InlineData("--max-length", "-1")]
    [InlineData("--max-length", "1.5")]
    public void BadLength_NamesItsOption_WithTheWindowsText(string option, string value)
    {
        Fails(Fmt(Strings.OptionError, option, UiStrings.FilterLengthInvalid), option + "|" + value);
    }

    [Theory]
    [InlineData("--min-length", "")]
    [InlineData("--min-length", " ")]
    [InlineData("--max-length", "")]
    [InlineData("--max-length", " ")]
    public void EmptyLength_IsRefused(string option, string value)
    {
        Fails(Fmt(Strings.EmptyOptionText, option), option + "|" + value);
    }

    [Fact]
    public void MinAboveMax_GivesTheRangeText()
    {
        Fails(Fmt(Strings.OptionError, "--min-length", ItemFilter.LengthRangeError), "--min-length|5|--max-length|3");
    }

    [Fact]
    public void Lengths_WithSpaces_Accepted()
    {
        ApplyOptions options = Ok("--min-length| 3 ");

        Assert.Equal(3, options.Filter.MinLength);
        Assert.Null(options.Filter.MaxLength);
    }

    [Fact]
    public void Lengths_Equal_AreAccepted()
    {
        ApplyOptions options = Ok("--min-length|4|--max-length|4");

        Assert.Equal(4, options.Filter.MinLength);
        Assert.Equal(4, options.Filter.MaxLength);
    }

    [Fact]
    public void Kinds_FillTheModel()
    {
        ItemFilter filesOnly = Ok("--files-only").Filter;
        Assert.True(filesOnly.IncludeFiles);
        Assert.False(filesOnly.IncludeFolders);
        ItemFilter foldersOnly = Ok("--folders-only").Filter;
        Assert.False(foldersOnly.IncludeFiles);
        Assert.True(foldersOnly.IncludeFolders);
        ItemFilter both = Ok("--include-folders").Filter;
        Assert.True(both.IncludeFiles);
        Assert.True(both.IncludeFolders);
        Assert.True(Ok("--include-hidden").Filter.IncludeHidden);
    }

    public static IEnumerable<object[]> KindPairs() =>
        from first in Kinds
        from second in Kinds
        where first != second
        select new object[] { first, second };

    [Theory]
    [MemberData(nameof(KindPairs))]
    public void TwoKinds_OneModeOnly(string first, string second)
    {
        Fails(Fmt(Strings.OneModeOnly, Strings.FilterKindOptions), first + "|" + second);
    }

    [Fact]
    public void TwoKinds_SentenceIsTheStorys()
    {
        Fails("Give only one of --files-only, --folders-only or --include-folders.", "--files-only|--folders-only");
    }

    [Theory]
    [InlineData("--files-only")]
    [InlineData("--folders-only")]
    [InlineData("--include-folders")]
    public void SameKindTwice_IsRepeated(string word)
    {
        Fails(Fmt(Strings.RepeatedOption, word), word + "|" + word);
    }

    [Theory]
    [InlineData("--mask")]
    [InlineData("--filter-regex")]
    [InlineData("--min-length")]
    [InlineData("--max-length")]
    public void ValuedWord_Twice_IsRepeated(string word)
    {
        Fails(Fmt(Strings.RepeatedOption, word), Once(word) + "|" + Once(word));
    }

    [Theory]
    [InlineData("--filter-ignore-case")]
    [InlineData("--include-hidden")]
    public void Switch_Twice_IsRepeated(string word)
    {
        string first = word == "--filter-ignore-case" ? "--filter-regex|a|" : "";
        Fails(Fmt(Strings.RepeatedOption, word), first + word + "|" + word);
    }

    [Theory]
    [InlineData("--mask")]
    [InlineData("--filter-regex")]
    [InlineData("--min-length")]
    [InlineData("--max-length")]
    public void ValuedWord_WithoutValue_NeedsOne(string word)
    {
        Fails(Fmt(Strings.OptionNeedsValue, word), word + "|--dry-run");
    }

    [Fact]
    public void EveryNewWord_IsAnOptionWord()
    {
        foreach (string word in ValuedWords.Concat(Switches).Concat(Kinds))
        {
            Assert.Contains(word, CommandLineParser.ApplyOptionWords);
            Assert.True(OptionArgs.IsOptionWord(word), word);
        }
    }

    [Fact]
    public void FilterAlone_IsStillNoRule()
    {
        ParsedCommand command = CommandLineParser.Parse(new[] { "apply", Real("D:~P"), "--mask", "*.jpg" });

        Assert.Equal(Strings.NoRule + Tail, command.Error);
    }

    [Theory]
    [InlineData("D:~Pics~*.jpg", "D:~Pics", "*.jpg")]
    [InlineData("D:~Pics~*", "D:~Pics", "*")]
    [InlineData("D:~*.jpg", "D:~", "*.jpg")]
    [InlineData("*.jpg", ".", "*.jpg")]
    [InlineData("D:/Pics/a?.txt", "D:/Pics", "a?.txt")]
    [InlineData("~~srv~share~*.jpg", "~~srv~share", "*.jpg")]
    [InlineData("D:~Pi?s~*.jpg", "D:~Pi?s", "*.jpg")]
    [InlineData("D:~*~x", "D:~*~x", null)]
    [InlineData("D:~Pics~", "D:~Pics~", null)]
    [InlineData("D:*.jpg", "D:*.jpg", null)]
    [InlineData("~*.jpg", "~*.jpg", null)]
    [InlineData("/*.jpg", "/*.jpg", null)]
    [InlineData("D:~Pics", "D:~Pics", null)]
    [InlineData("D:~Pics~*.jpg^", "D:~Pics~*.jpg^", null)]
    [InlineData("^D:~Pics~*.jpg", "^D:~Pics~*.jpg", null)]
    public void FolderMask_Split(string text, string folder, string? mask)
    {
        var result = FolderMask.Split(Real(text));

        Assert.Equal(Real(folder), result.Folder);
        Assert.Equal(mask, result.Mask);
    }

    [Fact]
    public void FolderMask_InTheFolderText_FillsTheFilter()
    {
        ParsedCommand command = ParseIn("D:~Pics~*.jpg", "--dry-run");

        Assert.Equal(CommandKind.Apply, command.Kind);
        Assert.Equal(Real("D:~Pics"), command.Options!.FolderText);
        Assert.Equal("*.jpg", command.Options.Filter.Mask);
        Assert.True(command.Options.Filtered);
    }

    [Fact]
    public void FolderMask_WithMaskOption_IsTwice()
    {
        ParsedCommand command = ParseIn("D:~Pics~*.jpg", "--mask|*.png");

        Assert.Equal(CommandKind.Error, command.Kind);
        Assert.Equal(Strings.MaskTwice + Tail, command.Error);
        Assert.Equal("Give the mask once: in the folder or with --mask." + Tail, command.Error);
    }

    [Fact]
    public void FolderMask_KeepsTheOtherFilterOptions()
    {
        ParsedCommand command = ParseIn("D:~Pics~*.jpg", "--min-length|3|--include-folders");

        Assert.Equal("*.jpg", command.Options!.Filter.Mask);
        Assert.Equal(3, command.Options.Filter.MinLength);
        Assert.True(command.Options.Filter.IncludeFolders);
    }
}
