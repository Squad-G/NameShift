using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NameShift.Core;
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
        new object?[] { @"apply|D:\P|--case|lower|-q|-v|--dry-run", "QuietWithVerbose", null, null },
        new object?[] { @"apply|-q|-v", "QuietWithVerbose", null, null },
        new object?[] { @"apply|D:\P|--replace|IMG|-v", "ReplaceNeedsValues", null, null },
        new object?[] { @"apply|D:\P|--replace|-q|x", "ReplaceNeedsValues", null, null },
        new object?[] { @"apply|D:\P|--case|-q", "BadCase", "-q", null },
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
                "--quiet", "-q", "--verbose", "-v",
            },
            CommandLineParser.Words);
        Assert.Equal(CommandLineParser.Words, CommandLineParser.GlobalWords.Concat(CommandLineParser.ApplyOptionWords));
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
