using System;
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
        Assert.True(CommandLineParser.TryParse(Args(joined), out ApplyOptions? options, out string error), error);

        Assert.Equal(@"D:\P", options!.FolderText);
        Assert.Equal(find, options.Find);
        Assert.Equal(with, options.With);
        Assert.Equal(caseMode, options.Case);
        Assert.Equal(dryRun, options.DryRun);
    }

    [Fact]
    public void ReplaceValues_MayStartWithADash()
    {
        Assert.True(CommandLineParser.TryParse(Args(@"apply|D:\P|--replace|-|_"), out ApplyOptions? options, out string error), error);

        Assert.Equal("-", options!.Find);
        Assert.Equal("_", options.With);
        Assert.Null(options.Case);
    }

    [Fact]
    public void ReplaceWith_MayBeEmpty()
    {
        Assert.True(CommandLineParser.TryParse(Args(@"apply|D:\P|--replace|IMG_|"), out ApplyOptions? options, out string error), error);

        Assert.Equal("IMG_", options!.Find);
        Assert.Equal("", options.With);
    }

    [Fact]
    public void BuildRules_UsesThePanelDefaults()
    {
        Assert.True(CommandLineParser.TryParse(Args(@"apply|D:\P|--replace|IMG|Trip|--case|title"), out ApplyOptions? options, out _));

        IRenameRule[] rules = options!.BuildRules().ToArray();

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
        Assert.True(CommandLineParser.TryParse(Args(@"apply|D:\P|--case|lower"), out ApplyOptions? options, out _));

        Assert.IsType<CaseRule>(Assert.Single(options!.BuildRules()));
    }

    [Fact]
    public void UsageErrors_NoArguments()
    {
        Assert.False(CommandLineParser.TryParse(Array.Empty<string>(), out ApplyOptions? options, out string error));

        Assert.Null(options);
        Assert.Equal(Strings.NoCommand + " " + Strings.Usage, error);
    }

    [Theory]
    [InlineData(@"rename|D:\P", "UnknownCommand", "rename", null)]
    [InlineData(@"apply|D:\P|--case|lower|--x", "UnknownOption", "--x", null)]
    [InlineData(@"apply|D:\P|--replace|a", "ReplaceNeedsValues", null, null)]
    [InlineData(@"apply|D:\P|--case", "CaseNeedsValue", null, null)]
    [InlineData(@"apply|D:\P|--case|sideways", "BadCase", "sideways", null)]
    [InlineData(@"apply|D:\P|--case|lower|--case|upper", "RepeatedOption", "--case", null)]
    [InlineData(@"apply|D:\P|--replace|a|b|--replace|c|d", "RepeatedOption", "--replace", null)]
    [InlineData(@"apply|D:\P|--replace||y", "EmptyFind", null, null)]
    [InlineData(@"apply|D:\P|D:\Q|--case|lower", "TwoFolders", @"D:\P", @"D:\Q")]
    [InlineData(@"apply|--case|lower", "NoFolder", null, null)]
    [InlineData(@"apply|D:\P", "NoRule", null, null)]
    [InlineData(@"apply|D:\P|--dry-run", "NoRule", null, null)]
    [InlineData(@"apply|D:\P|-", "UnknownOption", "-", null)]
    [InlineData(@"apply|D:\P|--x|--case", "UnknownOption", "--x", null)]
    public void UsageErrors(string joined, string key, string? value0, string? value1)
    {
        object[] values = new[] { value0, value1 }.Where(v => v != null).Cast<object>().ToArray();

        Assert.False(CommandLineParser.TryParse(Args(joined), out ApplyOptions? options, out string error));

        Assert.Null(options);
        Assert.Equal(Text(key, values) + " " + Strings.Usage, error);
        Assert.StartsWith(Text(key, values), error, StringComparison.Ordinal);
        Assert.EndsWith(Strings.Usage, error, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", error, StringComparison.Ordinal);
        Assert.DoesNotContain("\r", error, StringComparison.Ordinal);
    }

    [Fact]
    public void BadCase_ListsTheFourValues()
    {
        Assert.False(CommandLineParser.TryParse(Args(@"apply|D:\P|--case|sideways"), out _, out string error));

        Assert.Contains("lower, upper, title or sentence", error, StringComparison.Ordinal);
    }

    [Fact]
    public void QuoteInFolder_SkipsTheNoRuleError()
    {
        // cmd turns `apply "D:\Pics\" --case lower` into one argument that swallowed the options: the runner names the quote.
        Assert.True(CommandLineParser.TryParse(new[] { "apply", "D:\\Pics\" --case lower" }, out ApplyOptions? options, out string error), error);

        Assert.Equal("D:\\Pics\" --case lower", options!.FolderText);
        Assert.Empty(options.BuildRules());
    }
}
