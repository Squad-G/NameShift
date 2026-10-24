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
