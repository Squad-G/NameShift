using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NameShift.Core;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Cmd.Tests;

public sealed class CommandLineParserTests
{
    /// <summary>The arguments of one command line, written with | between them (an empty entry is an empty argument).</summary>
    private static string[] Args(string joined) => joined.Split('|');

    /// <summary>The options of an apply command line that must parse.</summary>
    private static ApplyOptions Applied(string[] args)
    {
        ParsedCommand command = CommandLineParser.Parse(args);
        Assert.True(command.Kind == CommandKind.Apply, command.Error);
        return command.Options!;
    }

    private static string Text(string key, params object[] values)
    {
        PropertyInfo property = typeof(Strings).GetProperty(key, BindingFlags.Public | BindingFlags.Static)
            ?? throw new ArgumentException("No string " + key, nameof(key));
        return string.Format(CultureInfo.InvariantCulture, (string)property.GetValue(null)!, values);
    }

    [Theory]
    [InlineData(@"apply|D:\P|--case|lower|--replace|a|b|--dry-run", "a", "b", CaseMode.Lower, true)]
    [InlineData(@"apply|--dry-run|--replace|a|b|D:\P|--case|LOWER", "a", "b", CaseMode.Lower, true)]
    [InlineData(@"APPLY|--case|upper|D:\P", null, null, CaseMode.Upper, false)]
    [InlineData(@"apply|D:\P|--Case|Title|--DRY-RUN|--dry-run", null, null, CaseMode.Title, true)]
    [InlineData(@"apply|D:\P|--REPLACE|x|y|--case|sentence", "x", "y", CaseMode.Sentence, false)]
    public void Parses_AnyOrder(string joined, string? find, string? with, CaseMode caseMode, bool dryRun)
    {
        ApplyOptions options = Applied(Args(joined));

        Assert.Equal(@"D:\P", options.FolderText);
        Assert.Equal(find, options.Find);
        Assert.Equal(with, options.With);
        Assert.Equal(caseMode, options.Case);
        Assert.Equal(dryRun, options.DryRun);
    }

    [Theory]
    [InlineData(@"apply|-q|D:\P|--case|lower", "Quiet", false)]
    [InlineData(@"apply|D:\P|--case|lower|--quiet", "Quiet", false)]
    [InlineData(@"apply|--verbose|D:\P|--replace|a|b", "Verbose", false)]
    [InlineData(@"apply|D:\P|-v|--case|lower|--dry-run", "Verbose", true)]
    [InlineData(@"apply|D:\P|-Q|--case|lower", "Quiet", false)]
    [InlineData(@"apply|D:\P|--VERBOSE|--case|lower", "Verbose", false)]
    [InlineData(@"apply|D:\P|--case|lower", "Normal", false)]
    public void Verbosity_Parses_AnyPosition(string joined, string level, bool dryRun)
    {
        ApplyOptions options = Applied(Args(joined));

        Assert.Equal((OutputLevel)Enum.Parse(typeof(OutputLevel), level), options.Output);
        Assert.Equal(dryRun, options.DryRun);
        Assert.Equal(@"D:\P", options.FolderText);
    }

    [Fact]
    public void Verbosity_Parses_UnderTurkishCulture()
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo turkish = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentCulture = turkish;
            CultureInfo.CurrentUICulture = turkish;

            Assert.Equal(OutputLevel.Quiet, Applied(Args(@"apply|D:\P|-q|--case|lower")).Output);
            Assert.Equal(OutputLevel.Verbose, Applied(Args(@"apply|D:\P|-v|--case|lower")).Output);
            Assert.Equal(OutputLevel.Verbose, Applied(Args(@"apply|D:\P|--VERBOSE|--case|lower")).Output);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }

    [Theory]
    [InlineData("-v")]
    [InlineData("-q")]
    public void ReplaceText_CannotBeAVerbosityWord(string word)
    {
        string messageStart = Strings.ReplaceNeedsValues;

        ParsedCommand asWith = CommandLineParser.Parse(new[] { "apply", @"D:\P", "--replace", "a", word });
        ParsedCommand asFind = CommandLineParser.Parse(new[] { "apply", @"D:\P", "--replace", word, "b" });

        Assert.Equal(CommandKind.Error, asWith.Kind);
        Assert.StartsWith(messageStart, asWith.Error, StringComparison.Ordinal);
        Assert.Equal(CommandKind.Error, asFind.Kind);
        Assert.StartsWith(messageStart, asFind.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceText_OtherDashWords_StillParse()
    {
        ApplyOptions options = Applied(new[] { "apply", @"D:\P", "--replace", "a", "-x" });

        Assert.Equal("-x", options.With);
        Assert.Equal(OutputLevel.Normal, options.Output);
    }

    [Fact]
    public void ApplyOptions_UndefinedOutputLevel_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ApplyOptions(@"D:\P", null, null, CaseMode.Lower, false, (OutputLevel)7));
    }

    [Fact]
    public void ReplaceValues_MayStartWithADash()
    {
        ApplyOptions options = Applied(Args(@"apply|D:P|--replace|-|_"));

        Assert.Equal("-", options.Find);
        Assert.Equal("_", options.With);
        Assert.Null(options.Case);
    }

    [Theory]
    [InlineData("--x", "y")]
    [InlineData("-", "--dry")]
    [InlineData("--dry-run-x", "--case-x")]
    public void ReplaceValues_MayLookLikeOtherOptions(string find, string with)
    {
        ApplyOptions options = Applied(new[] { "apply", @"D:P", "--replace", find, with });

        Assert.Equal(find, options.Find);
        Assert.Equal(with, options.With);
        Assert.False(options.DryRun);
    }

    [Fact]
    public void ReplaceWith_MayBeEmpty()
    {
        ApplyOptions options = Applied(Args(@"apply|D:P|--replace|IMG_|"));

        Assert.Equal("IMG_", options.Find);
        Assert.Equal("", options.With);
    }

    [Fact]
    public void BuildRules_UsesThePanelDefaults()
    {
        ApplyOptions options = Applied(Args(@"apply|D:\P|--replace|IMG|Trip|--case|title"));

        IRenameRule[] rules = options.BuildRules().ToArray();

        Assert.Equal(2, rules.Length);
        ReplaceRule replace = Assert.IsType<ReplaceRule>(rules[0]);
        Assert.Equal("IMG", replace.Find);
        Assert.Equal("Trip", replace.With);
        Assert.False(replace.MatchCase);
        Assert.False(replace.IncludeExtension);
        Assert.False(replace.KeepCase);
        CaseRule caseRule = Assert.IsType<CaseRule>(rules[1]);
        Assert.Equal(CaseMode.Title, caseRule.Mode);
        Assert.Empty(caseRule.Exceptions);
    }

    [Fact]
    public void BuildRules_OnlyTheGivenRules()
    {
        ApplyOptions options = Applied(Args(@"apply|D:\P|--case|lower"));

        Assert.IsType<CaseRule>(Assert.Single(options.BuildRules()));
    }

    [Fact]
    public void UsageErrors_NoArguments()
    {
        ParsedCommand command = CommandLineParser.Parse(Array.Empty<string>());

        Assert.Equal(CommandKind.Error, command.Kind);
        Assert.Null(command.Options);
        Assert.Equal(Strings.NoCommand + " " + Strings.Usage + " " + Strings.HelpPointer, command.Error);
    }

    /// <summary>Every usage error of the parser: the command line, the message's key and its values.</summary>
    public static IEnumerable<object?[]> UsageCases() => new[]
    {
        new object?[] { @"rename|D:\P", "UnknownCommand", "rename", null },
        new object?[] { @"apply|D:\P|--case|lower|--x", "UnknownOption", "--x", null },
        new object?[] { @"apply|D:\P|--replace|a", "ReplaceNeedsValues", null, null },
        new object?[] { @"apply|D:\P|--case", "CaseNeedsValue", null, null },
        new object?[] { @"apply|D:\P|--case|sideways", "BadCase", "sideways", null },
        new object?[] { @"apply|D:\P|--case|lower|--case|upper", "RepeatedOption", "--case", null },
        new object?[] { @"apply|D:\P|--replace|a|b|--replace|c|d", "RepeatedOption", "--replace", null },
        new object?[] { @"apply|D:\P|--replace||y", "EmptyFind", null, null },
        new object?[] { @"apply|D:\P|D:\Q|--case|lower", "TwoFolders", @"D:\P", @"D:\Q" },
        new object?[] { @"apply|--case|lower", "NoFolder", null, null },
        new object?[] { @"apply|D:\P", "NoRule", null, null },
        new object?[] { @"apply|D:\P|--preset", "OptionNeedsValue", "--preset", null },
        new object?[] { @"apply|D:\P|--preset|--dry-run", "OptionNeedsValue", "--preset", null },
        new object?[] { @"apply|D:\P|--preset|  ", "OptionNeedsValue", "--preset", null },
        new object?[] { @"apply|D:\P|--case|lower|--save-preset", "OptionNeedsValue", "--save-preset", null },
        new object?[] { @"apply|D:\P|--case|lower|--save-preset|-q", "OptionNeedsValue", "--save-preset", null },
        new object?[] { @"apply|D:\P|--case|lower|--save-preset|  ", "PresetNameBlank", null, null },
        new object?[] { @"apply|D:\P|--case|lower|--save-preset|" + new string('x', 101), "PresetNameTooLong", null, null },
        new object?[] { @"apply|D:\P|--case|lower|--overwrite", "OverwriteNeedsSave", null, null },
        new object?[] { @"apply|D:\P|--save-preset|X", "NoRule", null, null },
        new object?[] { @"apply|D:\P|--preset|A|--preset|B", "RepeatedOption", "--preset", null },
        new object?[] { @"apply|D:\P|--case|lower|--save-preset|A|--save-preset|B", "RepeatedOption", "--save-preset", null },
        new object?[] { @"apply|D:\P|--case|lower|--save-preset|A|--overwrite|--overwrite", "RepeatedOption", "--overwrite", null },
        new object?[] { @"apply|D:\P|--dry-run", "NoRule", null, null },
        new object?[] { @"apply|D:\P|-", "UnknownOption", "-", null },
        new object?[] { @"apply|D:\P|--x|--case", "UnknownOption", "--x", null },
        new object?[] { @"apply||--case|lower", "NoFolder", null, null },
        new object?[] { @"apply| |--case|lower", "NoFolder", null, null },
        new object?[] { "apply|\t|--case|lower", "NoFolder", null, null },
        new object?[] { @"apply|D:\P||--case|lower", "NoFolder", null, null },
        new object?[] { @"apply|D:\P|--replace|IMG|--dry-run", "ReplaceNeedsValues", null, null },
        new object?[] { @"apply|D:\P|--replace|IMG|--DRY-RUN", "ReplaceNeedsValues", null, null },
        new object?[] { @"apply|D:\P|--replace|IMG|--case|lower", "ReplaceNeedsValues", null, null },
        new object?[] { @"apply|D:\P|--replace|--dry-run|x", "ReplaceNeedsValues", null, null },
        new object?[] { @"apply|D:\P|--replace|--replace|x", "ReplaceNeedsValues", null, null },
        new object?[] { @"apply|D:\P|--case|lower|-q|-q", "RepeatedOption", "--quiet", null },
        new object?[] { @"apply|D:\P|--case|lower|--quiet|--quiet", "RepeatedOption", "--quiet", null },
        new object?[] { @"apply|D:\P|--case|lower|-q|--quiet", "RepeatedOption", "--quiet", null },
        new object?[] { @"apply|D:\P|--case|lower|-v|--verbose", "RepeatedOption", "--verbose", null },
        new object?[] { @"apply|D:\P|--case|lower|--verbose|-v", "RepeatedOption", "--verbose", null },
        new object?[] { @"apply|D:\P|--case|lower|-q|-v", "QuietWithVerbose", null, null },
        new object?[] { @"apply|D:\P|--case|lower|--verbose|--quiet", "QuietWithVerbose", null, null },
        new object?[] { @"apply|D:\P|--case|lower|--quiet|--dry-run", "QuietWithDryRun", null, null },
        new object?[] { @"apply|D:\P|--case|lower|--dry-run|-q", "QuietWithDryRun", null, null },
        new object?[] { @"apply|--quiet|--dry-run", "QuietWithDryRun", null, null },
        new object?[] { @"apply|D:\P|--case|lower|-q|-v|--dry-run", "QuietWithVerbose", null, null },
        new object?[] { @"apply|-q|-v", "QuietWithVerbose", null, null },
        new object?[] { @"apply|D:\P|--replace|IMG|-v", "ReplaceNeedsValues", null, null },
        new object?[] { @"apply|D:\P|--replace|-q|x", "ReplaceNeedsValues", null, null },
        new object?[] { @"apply|D:\P|--case|-q", "BadCase", "-q", null },
        new object?[] { @"apply|D:\P|--prefix|", "EmptyPrefix", null, null },
        new object?[] { @"apply|D:\P|--suffix|", "EmptySuffix", null, null },
        new object?[] { @"apply|D:\P|--insert||2", "EmptyInsert", null, null },
        new object?[] { @"apply|D:\P|--insert|X", "InsertNeedsValues", null, null },
        new object?[] { @"apply|D:\P|--insert|X|--dry-run", "InsertNeedsValues", null, null },
        new object?[] { @"apply|D:\P|--insert|X|0", "BadInsertPosition", "0", null },
        new object?[] { @"apply|D:\P|--insert|X|a", "BadInsertPosition", "a", null },
        new object?[] { @"apply|D:\P|--from-end|--case|lower", "FromEndNeedsInsert", null, null },
        new object?[] { @"apply|D:\P|--case|lower|--prefix", "OptionNeedsValue", "--prefix", null },
        new object?[] { @"apply|D:\P|--prefix|--dry-run", "OptionNeedsValue", "--prefix", null },
        new object?[] { @"apply|D:\P|--number-start|-q", "OptionNeedsValue", "--number-start", null },
        new object?[] { @"apply|D:\P|--prefix|a|--prefix|b", "RepeatedOption", "--prefix", null },
        new object?[] { @"apply|D:\P|--suffix|a|--suffix|b", "RepeatedOption", "--suffix", null },
        new object?[] { @"apply|D:\P|--insert|X|2|--insert|Y|3", "RepeatedOption", "--insert", null },
        new object?[] { @"apply|D:\P|--from-end|--from-end", "RepeatedOption", "--from-end", null },
        new object?[] { @"apply|D:\P|--word-space|--word-space", "RepeatedOption", "--word-space", null },
        new object?[] { @"apply|D:\P|--number|--number", "RepeatedOption", "--number", null },
        new object?[] { @"apply|D:\P|--number-mode|prefix|--number-mode|suffix", "RepeatedOption", "--number-mode", null },
        new object?[] { @"apply|D:\P|--number-start|1|--number-start|2", "RepeatedOption", "--number-start", null },
        new object?[] { @"apply|D:\P|--number-step|1|--number-step|2", "RepeatedOption", "--number-step", null },
        new object?[] { @"apply|D:\P|--number-pad|1|--number-pad|2", "RepeatedOption", "--number-pad", null },
        new object?[] { @"apply|D:\P|--number-separator|-|--number-separator|.", "RepeatedOption", "--number-separator", null },
        new object?[] { @"apply|D:\P|--number-position|1|--number-position|2", "RepeatedOption", "--number-position", null },
        new object?[] { @"apply|D:\P|--number-system|hex|--number-system|roman", "RepeatedOption", "--number-system", null },
        new object?[] { @"apply|D:\P|--number-case|upper|--number-case|lower", "RepeatedOption", "--number-case", null },
        new object?[] { @"undo|C:\x", "UndoOnlyDryRun", null, null },
        new object?[] { @"undo|--case|lower", "UndoOnlyDryRun", null, null },
        new object?[] { @"undo|--replace|a|b", "UndoOnlyDryRun", null, null },
        new object?[] { @"undo|-q", "UndoOnlyDryRun", null, null },
        new object?[] { @"undo|--quiet", "UndoOnlyDryRun", null, null },
        new object?[] { @"undo|-v", "UndoOnlyDryRun", null, null },
        new object?[] { @"undo|--verbose", "UndoOnlyDryRun", null, null },
        new object?[] { @"undo|apply", "UndoOnlyDryRun", null, null },
        new object?[] { @"undo|", "UndoOnlyDryRun", null, null },
        new object?[] { @"undo|--dry-run|--dry-run", "RepeatedOption", "--dry-run", null },
    };

    /// <summary>The words of the parser's list: a word of the list as a value of --replace means a missing value.</summary>
    [Fact]
    public void Words_DriveTheOptionGuard()
    {
        ParsedCommand command = CommandLineParser.Parse(Args(@"apply|D:\P|--replace|--version|x"));

        Assert.Equal(CommandKind.Error, command.Kind);
        Assert.StartsWith(Strings.ReplaceNeedsValues, command.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("help")]
    [InlineData("apply")]
    [InlineData("x")]
    public void ReplaceValues_MayBeVerbsOrHelpWords(string find)
    {
        ApplyOptions options = Applied(new[] { "apply", @"D:\P", "--replace", find, "y" });

        Assert.Equal(find, options.Find);
    }

    [Fact]
    public void Words_AreTheDispatchedWords()
    {
        Assert.Equal(
            new[]
            {
                "apply", "undo", "help", "--help", "-h", "/?", "--version", "--replace", "--case", "--dry-run",
                "--quiet", "-q", "--verbose", "-v", "--prefix", "--suffix", "--insert", "--from-end", "--word-space",
                "--number", "--number-mode", "--number-start", "--number-step", "--number-pad", "--number-separator",
                "--number-position", "--number-system", "--number-case", "--name-fixed", "--name-empty",
                "--name-reverse", "--ext-lower", "--ext-upper", "--ext-fixed", "--ext-extra", "--ext-remove",
                "--remove-first", "--remove-last", "--remove-from", "--remove-to", "--remove-chars", "--remove-text",
                "--remove-before", "--remove-after", "--remove-whole-word", "--remove-match-case",
                "--remove-keep-marker", "--remove-digits", "--remove-symbols", "--remove-accents",
                "--remove-high-chars", "--remove-trim", "--remove-leading-dots", "--remove-double-spaces",
                "--regex", "--regex-ignore-case", "--regex-extension", "--move", "--copy", "--move-from", "--move-to",
                "--move-position", "--move-separator", "--date", "--date-source", "--date-format",
                "--date-placement", "--date-separator", "--folder-name", "--folder-levels", "--folder-placement",
                "--folder-separator", "--mask", "--filter-regex", "--filter-ignore-case", "--min-length",
                "--max-length", "--include-hidden", "--files-only", "--folders-only", "--include-folders",
                "--preset", "--save-preset", "--overwrite",
            },
            CommandLineParser.Words);
        Assert.Equal(CommandLineParser.Words, CommandLineParser.GlobalWords.Concat(CommandLineParser.ApplyOptionWords));
    }

    [Fact]
    public void Preset_Parsed()
    {
        ApplyOptions options = Applied(new[] { "apply", "--preset", "  Photos ", @"D:\P" });

        Assert.Equal("Photos", options.PresetName);
        Assert.Equal(@"D:\P", options.FolderText);
        Assert.Null(options.SaveName);
        Assert.False(options.Overwrite);
        Assert.Null(options.Find);
        Assert.Null(options.Case);
        Assert.Empty(options.GivenPanels);
    }

    [Fact]
    public void SavePreset_Trimmed()
    {
        ApplyOptions options = Applied(new[] { "apply", @"D:\P", "--case", "lower", "--save-preset", " Mine ", "--overwrite" });

        Assert.Equal("Mine", options.SaveName);
        Assert.True(options.Overwrite);
        Assert.Null(options.PresetName);
    }

    [Fact]
    public void SavePreset_100Chars_Ok()
    {
        string name = new string('n', 100);

        ApplyOptions options = Applied(new[] { "apply", @"D:\P", "--case", "lower", "--save-preset", "  " + name + "  " });

        Assert.Equal(name, options.SaveName);
    }

    [Fact]
    public void Preset_SatisfiesRuleCheck()
    {
        Assert.Equal("X", Applied(new[] { "apply", @"D:\P", "--preset", "X" }).PresetName);
    }

    [Fact]
    public void ReplaceAndCase_AreModels()
    {
        ApplyOptions options = Applied(new[] { "apply", @"D:\P", "--replace", "a", "b", "--case", "upper" });

        Assert.Equal(2, options.GivenPanels.Count);
        ReplacePanelModel replace = Assert.IsType<ReplacePanelModel>(options.GivenPanels[0]);
        Assert.Equal("a", replace.Find);
        Assert.Equal("b", replace.With);
        Assert.True(replace.Enabled);
        CasePanelModel casePanel = Assert.IsType<CasePanelModel>(options.GivenPanels[1]);
        Assert.Equal(CaseMode.Upper, casePanel.Mode);
        Assert.True(casePanel.Enabled);
    }

    [Fact]
    public void Overwrite_WithoutSaveName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ApplyOptions(@"D:\P", null, null, CaseMode.Lower, false, overwrite: true));
    }

    [Fact]
    public void UndoOptionWords_AreDryRun()
    {
        Assert.Equal(new[] { "--dry-run" }, CommandLineParser.UndoOptionWords);
        Assert.Equal(1, CommandLineParser.Words.Count(w => w == "--dry-run"));
    }

    [Theory]
    [InlineData("undo", false)]
    [InlineData("UNDO", false)]
    [InlineData("undo|--dry-run", true)]
    [InlineData("undo|--DRY-RUN", true)]
    public void Undo_Parses(string joined, bool dryRun)
    {
        ParsedCommand command = CommandLineParser.Parse(Args(joined));

        Assert.Equal(CommandKind.Undo, command.Kind);
        Assert.Equal(dryRun, command.UndoDryRun);
        Assert.Null(command.Options);
        Assert.Equal("", command.Error);
    }

    [Theory]
    [InlineData("undo|--help")]
    [InlineData(@"undo|C:\x|-h")]
    [InlineData("undo|--dry-run|--dry-run|/?")]
    public void Undo_HelpAnywhere_WinsOverErrors(string joined)
    {
        Assert.Equal(CommandKind.UndoHelp, CommandLineParser.Parse(Args(joined)).Kind);
    }

    [Fact]
    public void Apply_RepeatedDryRun_StillAccepted()
    {
        // Parses_AnyOrder already holds a repeated --dry-run; this one pins it with --case.
        ApplyOptions options = Applied(Args(@"apply|C:\x|--case|lower|--dry-run|--dry-run"));

        Assert.True(options.DryRun);
    }

    [Fact]
    public void ParsedCommand_UndoDryRun_IsFalseForOtherKinds()
    {
        Assert.False(CommandLineParser.Parse(Args("help")).UndoDryRun);
        Assert.False(CommandLineParser.Parse(Args(@"apply|D:\P|--case|lower|--dry-run")).UndoDryRun);
        Assert.False(CommandLineParser.Parse(Args("undo|--help")).UndoDryRun);
    }

    [Theory]
    [MemberData(nameof(UsageCases))]
    public void UsageErrors(string joined, string key, string? value0, string? value1)
    {
        object[] values = new[] { value0, value1 }.Where(v => v != null).Cast<object>().ToArray();

        ParsedCommand command = CommandLineParser.Parse(Args(joined));
        string error = command.Error;

        Assert.Equal(CommandKind.Error, command.Kind);
        Assert.Null(command.Options);
        Assert.Equal(Text(key, values) + " " + Strings.Usage + " " + Strings.HelpPointer, error);
        Assert.StartsWith(Text(key, values), error, StringComparison.Ordinal);
        Assert.EndsWith(Strings.HelpPointer, error, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", error, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", error, StringComparison.Ordinal);
    }

    [Fact]
    public void AddOptions_FillTheAddModel()
    {
        ApplyOptions options = Applied(Args(@"apply|D:\P|--prefix|p_|--suffix|_s|--insert|X|3|--from-end|--word-space"));

        AddPanelModel add = Assert.IsType<AddPanelModel>(options.Panels.Add);
        Assert.Equal("p_", add.Prefix);
        Assert.Equal("_s", add.Suffix);
        Assert.Equal("X", add.Insert);
        Assert.Equal("3", add.InsertPositionText);
        Assert.True(add.InsertFromEnd);
        Assert.True(add.WordSpace);
        Assert.True(add.Enabled);
        Assert.Null(options.Panels.Numbering);
        Assert.Same(add, Assert.Single(options.Panels.Given));
    }

    [Fact]
    public void Number_Alone_Defaults()
    {
        ApplyOptions options = Applied(Args(@"apply|D:\P|--number"));

        NumberingPanelModel numbering = Assert.IsType<NumberingPanelModel>(options.Panels.Numbering);
        Assert.True(numbering.IsDefault);
        Assert.True(numbering.Enabled);
        Assert.Null(options.Panels.Add);
        Assert.Same(numbering, Assert.Single(options.Panels.Given));
        Assert.IsType<NumberingRule>(Assert.Single(options.BuildRules()));
    }

    [Fact]
    public void NoPanelOption_LeavesThePanelsNull()
    {
        ApplyOptions options = Applied(Args(@"apply|D:\P|--case|lower"));

        Assert.Null(options.Panels.Add);
        Assert.Null(options.Panels.Numbering);
        Assert.Empty(options.Panels.Given);
    }

    [Fact]
    public void NumberOptions_FillTheModel()
    {
        ApplyOptions options = Applied(Args(
            @"apply|D:\P|--number-mode|insert|--number-start|5|--number-step|3|--number-pad|4|--number-separator|-"
            + "|--number-position|2|--number-system|hex|--number-case|lower"));

        NumberingPanelModel numbering = Assert.IsType<NumberingPanelModel>(options.Panels.Numbering);
        Assert.Equal(NumberingMode.Insert, numbering.Mode);
        Assert.Equal("5", numbering.StartText);
        Assert.Equal("3", numbering.StepText);
        Assert.Equal("4", numbering.PadText);
        Assert.Equal("-", numbering.Separator);
        Assert.Equal("2", numbering.InsertPositionText);
        Assert.Equal(NumeralSystem.Hexadecimal, numbering.Numerals);
        Assert.False(numbering.UpperCase);
        Assert.True(numbering.Enabled);
        Assert.False(numbering.RestartPerFolder);
    }

    [Fact]
    public void NumberCase_UpperAndLower_AreEffective()
    {
        NumberingPanelModel upper = Applied(Args(@"apply|D:\P|--number-system|letters|--number-case|upper")).Panels.Numbering!;
        NumberingPanelModel lower = Applied(Args(@"apply|D:\P|--number-system|roman|--number-case|lower")).Panels.Numbering!;
        NumberingPanelModel plain = Applied(Args(@"apply|D:\P|--number-system|letters")).Panels.Numbering!;

        Assert.True(upper.UpperCase);
        Assert.False(lower.UpperCase);
        Assert.False(plain.UpperCase);
    }

    [Theory]
    [InlineData(@"apply|D:\P|--number-case|lower|--number-system|hex", false, 1)]
    [InlineData(@"apply|D:\P|--number-start|V|--number-system|roman", true, 5)]
    [InlineData(@"apply|D:\P|--number-system|hex|--number-start|10", true, 16)]
    [InlineData(@"apply|D:\P|--number-case|lower|--number-system|roman", false, 1)]
    public void Numerals_SetBeforeCaseAndStart(string joined, bool upperCase, int start)
    {
        NumberingPanelModel numbering = Applied(Args(joined)).Panels.Numbering!;

        Assert.Equal(upperCase, numbering.UpperCase);
        Assert.Equal(start, numbering.Start);
    }

    [Theory]
    [InlineData(@"apply|D:\P|--NUMBER-SYSTEM|ROMAN", NumberingMode.Suffix, NumeralSystem.Roman)]
    [InlineData(@"apply|D:\P|--Number-Mode|Prefix", NumberingMode.Prefix, NumeralSystem.Decimal)]
    [InlineData(@"apply|D:\P|--NUMBER|--Number-Case|LOWER|--number-system|Letters", NumberingMode.Suffix, NumeralSystem.Letters)]
    public void Words_IgnoreCase(string joined, NumberingMode mode, NumeralSystem system)
    {
        NumberingPanelModel numbering = Applied(Args(joined)).Panels.Numbering!;

        Assert.Equal(mode, numbering.Mode);
        Assert.Equal(system, numbering.Numerals);
    }

    [Fact]
    public void Words_UnderTurkishCulture()
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo turkish = CultureInfo.GetCultureInfo("tr-TR");
            CultureInfo.CurrentCulture = turkish;
            CultureInfo.CurrentUICulture = turkish;

            NumberingPanelModel numbering = Applied(Args(@"apply|D:\P|--number-system|hex|--number-case|lower")).Panels.Numbering!;
            NumberingPanelModel shout = Applied(Args(@"apply|D:\P|--NUMBER-SYSTEM|HEX|--NUMBER-CASE|LOWER")).Panels.Numbering!;

            Assert.Equal(NumeralSystem.Hexadecimal, numbering.Numerals);
            Assert.False(numbering.UpperCase);
            Assert.Equal(NumeralSystem.Hexadecimal, shout.Numerals);
            Assert.False(shout.UpperCase);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }

    [Fact]
    public void DashValues_Allowed()
    {
        ApplyOptions options = Applied(Args(@"apply|D:\P|--number-separator|-|--prefix|-x"));

        Assert.Equal("-", options.Panels.Numbering!.Separator);
        Assert.Equal("-x", options.Panels.Add!.Prefix);
    }

    [Theory]
    [InlineData("middle", "--number-mode", "NumberModeValues")]
    [InlineData("octal", "--number-system", "NumberSystemValues")]
    [InlineData("hexadecimal", "--number-system", "NumberSystemValues")]
    [InlineData("title", "--number-case", "NumberCaseValues")]
    [InlineData("", "--number-mode", "NumberModeValues")]
    public void BadChoice_NamesTheValueTheOptionAndTheList(string value, string option, string listKey)
    {
        ParsedCommand command = CommandLineParser.Parse(new[] { "apply", @"D:\P", option, value });

        Assert.Equal(CommandKind.Error, command.Kind);
        Assert.Equal(Text("BadChoice", value, option, Text(listKey)) + " " + Strings.Usage + " " + Strings.HelpPointer, command.Error);
    }

    /// <summary>Each row: the options, the option named in the error, how the same model is set by hand, and which of its
    /// error properties is the text. The expected text is the model's own, read in the same culture.</summary>
    public static IEnumerable<object[]> ModelErrorCases() => new[]
    {
        new object[] { "--number-start|x", "--number-start", (Action<NumberingPanelModel>)(m => m.StartText = "x"), (Func<NumberingPanelModel, string>)(m => m.StartError) },
        new object[] { "--number-step|x", "--number-step", (Action<NumberingPanelModel>)(m => m.StepText = "x"), (Func<NumberingPanelModel, string>)(m => m.StepError) },
        new object[] { "--number-pad|256", "--number-pad", (Action<NumberingPanelModel>)(m => m.PadText = "256"), (Func<NumberingPanelModel, string>)(m => m.PadError) },
        new object[]
        {
            "--number-mode|insert|--number-position|0", "--number-position",
            (Action<NumberingPanelModel>)(m => { m.Mode = NumberingMode.Insert; m.InsertPositionText = "0"; }),
            (Func<NumberingPanelModel, string>)(m => m.InsertPositionError),
        },
        new object[]
        {
            "--number-mode|insert", "--number-position",
            (Action<NumberingPanelModel>)(m => m.Mode = NumberingMode.Insert),
            (Func<NumberingPanelModel, string>)(m => m.InsertPositionError),
        },
        new object[]
        {
            "--number-system|roman|--number-start|0", "--number-start",
            (Action<NumberingPanelModel>)(m => { m.Numerals = NumeralSystem.Roman; m.StartText = "0"; }),
            (Func<NumberingPanelModel, string>)(m => m.StartError),
        },
    };

    [Theory]
    [MemberData(nameof(ModelErrorCases))]
    public void ModelErrors_NameTheOption(string options, string option, Action<NumberingPanelModel> setup, Func<NumberingPanelModel, string> error)
    {
        var model = new NumberingPanelModel();
        setup(model);
        string modelText = error(model);
        Assert.NotEmpty(modelText);
        Assert.Contains(Strings.SkipSentenceMarker, modelText, StringComparison.Ordinal);
        string expected = modelText.Substring(0, modelText.LastIndexOf(Strings.SkipSentenceMarker, StringComparison.Ordinal))
            .TrimEnd();

        ParsedCommand command = CommandLineParser.Parse(Args(@"apply|D:\P|" + options));

        Assert.Equal(CommandKind.Error, command.Kind);
        Assert.DoesNotContain("skipped", command.Error, StringComparison.Ordinal);
        Assert.Equal(Text("OptionError", option, expected) + " " + Strings.Usage + " " + Strings.HelpPointer, command.Error);
    }

    [Theory]
    [InlineData(@"apply|D:\P|--number-position|0")]
    [InlineData(@"apply|D:\P|--number-pad|256|--number-system|roman")]
    [InlineData(@"apply|D:\P|--number-pad|256|--number-system|letters")]
    [InlineData(@"apply|D:\P|--number-case|lower")]
    public void DisabledFields_Ignored(string joined)
    {
        NumberingPanelModel numbering = Applied(Args(joined)).Panels.Numbering!;

        Assert.NotNull(numbering.BuildRule());
    }

    [Theory]
    [InlineData(@"apply|D:\P|--prefix|p")]
    [InlineData(@"apply|D:\P|--number")]
    [InlineData(@"apply|D:\P|--word-space")]
    [InlineData(@"apply|D:\P|--number-step|2")]
    public void NewOptionAlone_SatisfiesRuleCheck(string joined)
    {
        ParsedCommand command = CommandLineParser.Parse(Args(joined));

        Assert.Equal(CommandKind.Apply, command.Kind);
    }

    [Fact]
    public void BuildRules_ReadsThePanels()
    {
        ApplyOptions options = Applied(Args(@"apply|D:\P|--replace|x|y|--prefix|p_|--number|--case|upper"));

        IRenameRule[] rules = options.BuildRules().ToArray();

        Assert.Equal(4, rules.Length);
        Assert.Single(rules, rule => rule is ReplaceRule);
        Assert.Single(rules, rule => rule is CaseRule);
        Assert.Single(rules, rule => rule is AddRule);
        Assert.Single(rules, rule => rule is NumberingRule);
    }

    [Fact]
    public void ApplyOptions_NoPanels_IsNone()
    {
        var options = new ApplyOptions(@"D:\P", null, null, CaseMode.Lower, false);

        Assert.Same(ApplyPanels.None, options.Panels);
        Assert.Empty(options.Panels.Given);
    }

    [Fact]
    public void BadCase_ListsTheFourValues()
    {
        string error = CommandLineParser.Parse(Args(@"apply|D:\P|--case|sideways")).Error;

        Assert.Contains("lower, upper, title or sentence", error, StringComparison.Ordinal);
    }

    [Fact]
    public void QuoteInFolder_SkipsTheNoRuleError()
    {
        // cmd turns `apply "D:\Pics\" --case lower` into one argument that swallowed the options: the runner names the quote.
        ApplyOptions options = Applied(new[] { "apply", "D:\\Pics\" --case lower" });

        Assert.Equal("D:\\Pics\" --case lower", options.FolderText);
        Assert.Empty(options.BuildRules());
    }
}
