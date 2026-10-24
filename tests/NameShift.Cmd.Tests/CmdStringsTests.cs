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
    public void Usage_NamesTheExeAndEveryOption()
    {
        Assert.Equal(
            "Usage: NameShiftCmd apply <folder> [--replace <find> <with>] [--case lower|upper|title|sentence] [--dry-run]",
            Strings.Usage);
    }
}
