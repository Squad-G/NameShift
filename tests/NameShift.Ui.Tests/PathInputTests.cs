using System;
using Xunit;

namespace NameShift.Ui.Tests;

public sealed class PathInputTests
{
    [Theory]
    [InlineData("  \"C:\\Temp\\\"  ", "C:\\Temp")]
    [InlineData("C:", "C:\\")]
    [InlineData("C:\\", "C:\\")]
    [InlineData("c:/a//b/../c", "c:\\a\\c")]
    [InlineData("\\\\server\\share\\x\\", "\\\\server\\share\\x")]
    public void TryNormalize_AcceptsAndNormalizes(string text, string expected)
    {
        Assert.Equal(PathInputResult.Ok, PathInput.TryNormalize(text, out string path));
        Assert.Equal(expected, path);
    }

    [Fact]
    public void TryNormalize_ExpandsEnvironmentVariables()
    {
        string expected = Environment.ExpandEnvironmentVariables("%TEMP%").TrimEnd('\\');

        Assert.Equal(PathInputResult.Ok, PathInput.TryNormalize("%TEMP%", out string path));
        Assert.Equal(expected, path);
    }

    [Fact]
    public void TryNormalize_NoExpansion_KeepsPercent()
    {
        // A variable this test owns: no real one is read or changed.
        const string Name = "NAMESHIFT_TEST_NS47";
        string? before = Environment.GetEnvironmentVariable(Name);
        Environment.SetEnvironmentVariable(Name, "Expanded");
        try
        {
            Assert.Equal(PathInputResult.Ok, PathInput.TryNormalize(@"C:\%NAMESHIFT_TEST_NS47%", out string kept, expandVariables: false));
            Assert.Equal(@"C:\%NAMESHIFT_TEST_NS47%", kept);
            Assert.Equal(PathInputResult.Ok, PathInput.TryNormalize(@"C:\%NAMESHIFT_TEST_NS47%", out string expanded));
            Assert.Equal(@"C:\Expanded", expanded);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Name, before);
        }
    }

    [Theory]
    [InlineData("C:foo")]
    [InlineData("\\foo")]
    [InlineData("foo")]
    [InlineData("..\\x")]
    [InlineData("C:\\a<b")]
    [InlineData("C:\\a?")]
    [InlineData("C:\\a:b")]
    [InlineData("\\\\?\\C:\\x")]
    [InlineData("\\\\.\\C:\\x")]
    [InlineData("\\\\server")]
    [InlineData("\\\\\\share")]
    [InlineData("1:\\x")]
    [InlineData("C:\\a\tb")]
    public void TryNormalize_RejectsInvalid(string text)
    {
        Assert.Equal(PathInputResult.Invalid, PathInput.TryNormalize(text, out string path));
        Assert.Equal("", path);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    public void TryNormalize_Blank_IsEmpty(string? text)
    {
        Assert.Equal(PathInputResult.Empty, PathInput.TryNormalize(text, out string path));
        Assert.Equal("", path);
    }

    [Theory]
    [InlineData("C:\\Data", "c:\\data\\", true)]
    [InlineData("C:\\", "c:", true)]
    [InlineData("C:\\Data", "C:\\Data2", false)]
    public void SameFolder_IgnoresCaseAndTrailingBackslash(string a, string b, bool expected)
    {
        Assert.Equal(expected, PathInput.SameFolder(a, b));
    }
}
