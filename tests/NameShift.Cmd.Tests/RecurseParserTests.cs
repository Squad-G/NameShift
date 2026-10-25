using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NameShift.Cmd.Tests;

/// <summary>The <c>--recurse</c> option in the parser and in the help. A tilde in a text stands for a backslash, so
/// that no escape sequence is written in this file.</summary>
public sealed class RecurseParserTests
{
    private static string Real(string text) => text.Replace('~', (char)92);

    private static string Tail => " " + Strings.Usage + " " + Strings.HelpPointer;

    private static ParsedCommand Parse(params string[] options)
    {
        var args = new List<string> { "apply", Real("D:~P"), "--case", "lower" };
        args.AddRange(options);
        return CommandLineParser.Parse(args);
    }

    [Fact]
    public void Recurse_SetsTheOption()
    {
        ParsedCommand command = Parse("--recurse");

        Assert.Equal(CommandKind.Apply, command.Kind);
        Assert.True(command.Options!.Recurse);
    }

    [Fact]
    public void Recurse_AnyCase_SetsTheOption()
    {
        ParsedCommand command = Parse("--RECURSE");

        Assert.Equal(CommandKind.Apply, command.Kind);
        Assert.True(command.Options!.Recurse);
    }

    [Fact]
    public void Recurse_Default_False()
    {
        ParsedCommand command = Parse();

        Assert.Equal(CommandKind.Apply, command.Kind);
        Assert.False(command.Options!.Recurse);
    }

    [Fact]
    public void Recurse_Twice_RepeatedOption()
    {
        ParsedCommand command = Parse("--recurse", "--recurse");

        Assert.Equal(CommandKind.Error, command.Kind);
        Assert.Equal(string.Format(Cmd.EnUs, Strings.RepeatedOption, "--recurse") + Tail, command.Error);
    }

    [Fact]
    public void Recurse_DoesNotMakeTheRunFiltered()
    {
        // The message rule of NS-41 is unchanged: --recurse alone is not a filter.
        Assert.False(Parse("--recurse").Options!.Filtered);
    }

    [Fact]
    public void Help_Lines()
    {
        IReadOnlyList<string> help = HelpText.Apply();

        Assert.Contains("  --recurse                  Also the sub-folders (not hidden ones).", help);
        Assert.Contains("  --include-hidden           Also hidden and system items and folders.", help);
        Assert.DoesNotContain("  --include-hidden           Also hidden and system items.", help);
        Assert.All(help, line => Assert.True(line.Length <= 79, line));
        int include = help.ToList().IndexOf("  --include-folders          Files and folders.");
        Assert.Equal("  --recurse                  Also the sub-folders (not hidden ones).", help[include + 1]);
    }
}
