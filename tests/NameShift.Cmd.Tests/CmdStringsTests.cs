using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using Xunit;

namespace NameShift.Cmd.Tests;

public sealed class CmdStringsTests
{
    [Fact]
    public void EveryKeyResolves()
    {
        List<PropertyInfo> properties = typeof(Strings)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string))
            .ToList();

        foreach (PropertyInfo property in properties)
        {
            Assert.False(string.IsNullOrEmpty((string?)property.GetValue(null)), property.Name);
        }

        var manager = new ResourceManager("NameShift.Cmd.Strings", typeof(Strings).Assembly);
        ResourceSet set = manager.GetResourceSet(CultureInfo.InvariantCulture, true, true)!;
        var keys = set.Cast<System.Collections.DictionaryEntry>().Select(e => (string)e.Key).OrderBy(k => k).ToList();

        Assert.Equal(keys, properties.Select(p => p.Name).OrderBy(n => n).ToList());
    }

    [Fact]
    public void VerbosityTexts()
    {
        Assert.Equal("--quiet and --verbose cannot be used together.", Strings.QuietWithVerbose);
        Assert.Equal("--quiet and --dry-run cannot be used together.", Strings.QuietWithDryRun);
        Assert.Equal("{0}: unchanged", Strings.UnchangedLine);
    }

    [Fact]
    public void RuleOptionTexts()
    {
        Assert.Equal("The text of --prefix cannot be empty.", Strings.EmptyPrefix);
        Assert.Equal("The text of --suffix cannot be empty.", Strings.EmptySuffix);
        Assert.Equal("The text of --insert cannot be empty.", Strings.EmptyInsert);
        Assert.Equal("--insert needs two values: the text and the position.", Strings.InsertNeedsValues);
        Assert.Equal("\"{0}\" is not a position of --insert: use a whole number from 1.", Strings.BadInsertPosition);
        Assert.Equal("--from-end needs --insert.", Strings.FromEndNeedsInsert);
        Assert.Equal("{0} needs a value.", Strings.OptionNeedsValue);
        Assert.Equal("{0}: {1}", Strings.OptionError);
        Assert.Equal("\"{0}\" is not a value of {1}: use {2}.", Strings.BadChoice);
        Assert.Equal("prefix, suffix or insert", Strings.NumberModeValues);
        Assert.Equal("decimal, hex, letters or roman", Strings.NumberSystemValues);
        Assert.Equal("upper or lower", Strings.NumberCaseValues);
        Assert.Equal("Until then numbering is skipped.", Strings.NumberingSkippedSentence);
        Assert.Equal("Give at least one rule option, such as --replace or --case.", Strings.NoRule);
    }

    [Fact]
    public void QuoteInFolder_ShowsTheBackslashes()
    {
        string text = string.Format(CultureInfo.InvariantCulture, Strings.QuoteInFolder, "D:\\Pics\"");

        Assert.Equal(
            "The folder D:\\Pics\" contains a quote: remove the backslash before the closing quote, as in \"D:\\Pics\" instead of \"D:\\Pics\\\".",
            text);
    }

    [Fact]
    public void Usage_NamesTheExeAndBothVerbs()
    {
        Assert.Equal("Usage: NameShiftCmd apply <folder> <options> | NameShiftCmd undo [--dry-run]", Strings.Usage);
    }

    [Fact]
    public void NoCommand_NamesBothVerbs()
    {
        Assert.Equal("Give a command: apply or undo.", Strings.NoCommand);
    }

    [Fact]
    public void ExitCodeTexts()
    {
        Assert.Equal("Done, or nothing to rename or undo.", Strings.ExitCode0);
        Assert.Equal("Nothing changed (refused, rolled back, no history, or a crash before it).", Strings.ExitCode3);
        Assert.Equal("Look at the disk: items left renamed or not restored, no history, a crash.", Strings.ExitCode4);
    }

    [Fact]
    public void UndoTexts()
    {
        Assert.Equal(
            "The undo history cannot be used: a rename was cut short or the history file is unreadable. No file was changed.",
            Strings.UndoHistoryUnusable);
        Assert.Equal("undo takes no folder or rule options, only --dry-run.", Strings.UndoOnlyDryRun);
        Assert.Equal("NameShift hit an unexpected problem. No file was changed.", Strings.UnexpectedBeforeUndo);
    }
}
