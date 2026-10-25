using System;
using System.Collections.Generic;
using Xunit;

namespace NameShift.Cmd.Tests;

/// <summary>The readers' shared helpers: the order of the choice checks, the once-only switch, the read outcome.</summary>
public sealed class OptionHelpersTests
{
    private static string Tail => " " + Strings.Usage + " " + Strings.HelpPointer;

    private static ParsedCommand Parse(string joined)
    {
        var args = new List<string> { "apply", @"D:\P" };
        args.AddRange(joined.Split('|'));
        return CommandLineParser.Parse(args);
    }

    private static void AssertUsageError(string options, string key, params object[] values)
    {
        ParsedCommand command = Parse(options);

        Assert.Equal(CommandKind.Error, command.Kind);
        Assert.Equal(OptionArgs.Format(key, values) + Tail, command.Error);
    }

    [Theory]
    [InlineData("--number-mode")]
    [InlineData("--number-mode|--dry-run")]
    [InlineData("--number-system")]
    [InlineData("--number-system|--dry-run")]
    [InlineData("--number-case")]
    [InlineData("--number-case|--dry-run")]
    public void NumberingChoice_MissingValue(string options)
    {
        AssertUsageError(options, Strings.OptionNeedsValue, options.Split('|')[0]);
    }

    [Theory]
    [InlineData("--number-mode|prefix|--number-mode")]
    [InlineData("--number-system|hex|--number-system")]
    [InlineData("--number-case|upper|--number-case")]
    public void NumberingChoice_MissingBeforeRepeat(string options)
    {
        AssertUsageError(options, Strings.OptionNeedsValue, options.Split('|')[0]);
    }

    [Theory]
    [InlineData("--number-mode|prefix|--number-mode|middle")]
    [InlineData("--number-system|hex|--number-system|octal")]
    [InlineData("--number-case|upper|--number-case|title")]
    public void NumberingChoice_RepeatBeforeBadChoice(string options)
    {
        AssertUsageError(options, Strings.RepeatedOption, options.Split('|')[0]);
    }

    [Fact]
    public void TakeSwitch_FirstSets_SecondFails()
    {
        bool b = false;

        Assert.True(OptionArgs.TakeSwitch(ref b, "--x", out ParsedCommand? first));
        Assert.True(b);
        Assert.Null(first);

        Assert.False(OptionArgs.TakeSwitch(ref b, "--x", out ParsedCommand? second));
        Assert.True(b);
        Assert.Equal(OptionArgs.Format(Strings.RepeatedOption, "--x") + Tail, second!.Error);
    }

    [Fact]
    public void From_OkIsTaken()
    {
        Assert.Same(OptionRead.Taken, OptionRead.From(true, null));
    }

    [Fact]
    public void From_FailedCarriesTheError()
    {
        ParsedCommand e = ParsedCommand.Failed("m");

        OptionRead read = OptionRead.From(false, e);

        Assert.True(read.IsMine);
        Assert.Same(e, read.Error);
    }

    [Fact]
    public void From_FailedWithoutError_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => OptionRead.From(false, null));
    }
}
