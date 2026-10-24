using System;
using System.Reflection;
using Xunit;

namespace NameShift.Cmd.Tests;

public sealed class ProductVersionTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("1.0.0+abc", "1.0.0")]
    [InlineData("0.2.0-beta+x", "0.2.0-beta")]
    public void ProductVersion_Strip(string version, string expected)
    {
        Assert.Equal(expected, ProductVersion.Strip(version));
    }

    [Fact]
    public void ProductVersion_Of_CmdAssembly()
    {
        Assembly assembly = typeof(Strings).Assembly;
        var attribute = (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
            assembly, typeof(AssemblyInformationalVersionAttribute))!;
        string expected = attribute.InformationalVersion;
        int plus = expected.IndexOf('+');
        if (plus >= 0)
        {
            expected = expected.Substring(0, plus);
        }

        Assert.Equal(expected, ProductVersion.Of(assembly));
        Assert.DoesNotContain("+", ProductVersion.Of(assembly), StringComparison.Ordinal);
    }

    [Fact]
    public void ProductVersion_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ProductVersion.Of(null!));
        Assert.Throws<ArgumentNullException>(() => ProductVersion.Strip(null!));
    }
}
