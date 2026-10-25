using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using NameShift.Core;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Cmd.Tests;

/// <summary>NS-79: the apply parser is split into readers; these tests pin what the split must keep (words, error
/// precedence) and the seam NS-75 and NS-76 extend.</summary>
public sealed class ApplyReadersTests
{
    private static string[] Args(string joined) => joined.Split('|');

    private static string Text(string key, params object[] values)
    {
        PropertyInfo property = typeof(Strings).GetProperty(key, BindingFlags.Public | BindingFlags.Static)
            ?? throw new ArgumentException("No string " + key, nameof(key));
        return string.Format(CultureInfo.InvariantCulture, (string)property.GetValue(null)!, values);
    }

    [Fact]
    public void RepeatedDryRun_IsAccepted()
    {
        ParsedCommand command = CommandLineParser.Parse(Args(@"apply|D:\P|--case|lower|--dry-run|--dry-run"));

        Assert.Equal(CommandKind.Apply, command.Kind);
        Assert.True(command.Options!.DryRun);
    }

    [Fact]
    public void CaseValueThatIsAnOption_IsBadCase()
    {
        ParsedCommand command = CommandLineParser.Parse(Args(@"apply|D:\P|--case|--zzz"));

        Assert.Equal(CommandKind.Error, command.Kind);
        Assert.StartsWith(Text("BadCase", "--zzz"), command.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownWordAsPrefixValue_IsTheValue()
    {
        ParsedCommand command = CommandLineParser.Parse(Args(@"apply|D:\P|--prefix|--zzz"));

        Assert.Equal(CommandKind.Apply, command.Kind);
        Assert.Equal("--zzz", command.Options!.Panels.Add!.Prefix);
    }

    [Theory]
    [InlineData("-q|--quiet", "--quiet")]
    [InlineData("-v|--verbose", "--verbose")]
    public void ShortAliases_RepeatName_TheLongOption(string joined, string option)
    {
        ParsedCommand command = CommandLineParser.Parse(Args(@"apply|D:\P|--case|lower|" + joined));

        Assert.Equal(CommandKind.Error, command.Kind);
        Assert.StartsWith(Text("RepeatedOption", option), command.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"apply|D:\P|--zzz|--case", "UnknownOption", "--zzz", null)]
    [InlineData(@"apply|D:\P|D:\Q|-q|-v", "TwoFolders", @"D:\P", @"D:\Q")]
    [InlineData(@"apply||-q|-v|--case|lower", "NoFolder", null, null)]
    [InlineData(@"apply|D:\P|-q|-v|--dry-run|--case|lower", "QuietWithVerbose", null, null)]
    [InlineData(@"apply|D:\P|-q|--dry-run|--overwrite|--case|lower", "QuietWithDryRun", null, null)]
    [InlineData(@"apply|D:\P|--overwrite|--from-end|--case|lower", "OverwriteNeedsSave", null, null)]
    [InlineData(@"apply|D:\P|--from-end|--number-start|x", "FromEndNeedsInsert", null, null)]
    [InlineData(@"apply|--dry-run", "NoFolder", null, null)]
    public void ErrorPrecedence_FirstMistakeWins(string joined, string key, string? value0, string? value1)
    {
        object[] values = new[] { value0, value1 }.Where(v => v != null).Cast<object>().ToArray();

        ParsedCommand command = CommandLineParser.Parse(Args(joined));

        Assert.Equal(CommandKind.Error, command.Kind);
        Assert.Equal(Text(key, values) + " " + Strings.Usage + " " + Strings.HelpPointer, command.Error);
    }

    [Fact]
    public void ErrorPrecedence_NumberingBeatsTheMissingFolder()
    {
        ParsedCommand command = CommandLineParser.Parse(Args("apply|--number-start|x"));

        Assert.Equal(CommandKind.Error, command.Kind);
        Assert.StartsWith("--number-start: ", command.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(Strings.NoFolder, command.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorPrecedence_StartErrorBeatsStepError()
    {
        ParsedCommand command = CommandLineParser.Parse(Args(@"apply|D:\P|--number-start|x|--number-step|0"));

        Assert.Equal(CommandKind.Error, command.Kind);
        Assert.StartsWith("--number-start: ", command.Error, StringComparison.Ordinal);
    }
}
