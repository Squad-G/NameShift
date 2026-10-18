using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using NameShift.Ui.Folders;
using Xunit;

namespace NameShift.Ui.Tests;

public sealed class FolderProblemTextTests
{
    [Fact]
    public void EveryProblemHasText()
    {
        foreach (FolderProblem problem in Enum.GetValues(typeof(FolderProblem)))
        {
            Assert.False(string.IsNullOrWhiteSpace(FolderProblemText.For(problem)));
        }
    }

    [Fact]
    public void UnknownProblem_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FolderProblemText.For((FolderProblem)99));
    }
}

public sealed class StringsTests
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

        var manager = new ResourceManager("NameShift.Ui.Strings", typeof(Strings).Assembly);
        ResourceSet set = manager.GetResourceSet(CultureInfo.InvariantCulture, true, true)!;
        var keys = set.Cast<System.Collections.DictionaryEntry>().Select(e => (string)e.Key).OrderBy(k => k).ToList();

        Assert.Equal(keys, properties.Select(p => p.Name).OrderBy(n => n).ToList());
    }
}

public sealed class NaturalNameComparerTests
{
    [Fact]
    public void SortsLikeExplorer()
    {
        var names = new List<string> { "file10", "file2", "File1" };

        names.Sort(NaturalNameComparer.Instance);

        Assert.Equal(new[] { "File1", "file2", "file10" }, names);
    }

    [Fact]
    public void NullsComeFirst()
    {
        Assert.Equal(0, NaturalNameComparer.Instance.Compare(null, null));
        Assert.True(NaturalNameComparer.Instance.Compare(null, "a") < 0);
        Assert.True(NaturalNameComparer.Instance.Compare("a", null) > 0);
    }
}
