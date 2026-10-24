using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;

namespace NameShift.Ui.Tests;

public sealed class OpenTargetTests
{
    private const string Work = @"C:\Work";
    private const string Variable = "NAMESHIFT_TEST_NS47_OPEN";

    private readonly FakeFolderSource _source = new FakeFolderSource();

    public static IEnumerable<object?[]> Arguments() => new[]
    {
        new object?[] { new string[0], null },
        new object?[] { new[] { @"D:\Pics" }, @"D:\Pics" },
        new object?[] { new[] { @"D:\Pics", @"E:\Other" }, @"D:\Pics" },
        new object?[] { new[] { "--verbose", @"D:\Pics" }, @"D:\Pics" },
        new object?[] { new[] { " --verbose", @"D:\Pics" }, @"D:\Pics" },
        new object?[] { new[] { "" }, null },
        new object?[] { new[] { "   " }, null },
        new object?[] { new[] { "", @"D:\Pics" }, @"D:\Pics" },
    };

    [Theory]
    [MemberData(nameof(Arguments))]
    public void FirstPathArgument_SkipsOptionsAndBlanks(string[] args, string? expected)
    {
        Assert.Equal(expected, OpenTarget.FirstPathArgument(args));
    }

    [Fact]
    public void FirstPathArgument_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => OpenTarget.FirstPathArgument(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    [InlineData("\"  \"")]
    public void Nothing(string? text)
    {
        OpenRequest result = Resolve(text);

        AssertNothing(result);
        Assert.Equal(0, _source.CheckCalls);
    }

    [Theory]
    [InlineData(@"D:\Pics")]
    [InlineData(@"D:\Pics\")]
    [InlineData("D:/Pics")]
    [InlineData(@"""D:\Pics""")]
    [InlineData(@"""  D:\Pics  """)]
    [InlineData(@"  D:\Pics  ")]
    public void Folder_ReadLikeThePathBar(string text)
    {
        _source.Folders.Add(@"D:\Pics");

        AssertFolder(@"D:\Pics", Resolve(text));
    }

    [Fact]
    public void Folder_WithSpaces()
    {
        _source.Folders.Add(@"D:\My Pics");

        AssertFolder(@"D:\My Pics", Resolve(@"D:\My Pics"));
    }

    [Theory]
    [InlineData("D:")]
    [InlineData(@"D:\")]
    public void DriveRoot(string text)
    {
        _source.Folders.Add(@"D:\");

        AssertFolder(@"D:\", Resolve(text));
    }

    [Fact]
    public void PercentNotExpanded()
    {
        _source.Folders.Add(@"C:\%TEMP%");

        // TEMP is set on every Windows machine: expanding it would look for another folder.
        AssertFolder(@"C:\%TEMP%", Resolve(@"C:\%TEMP%"));
    }

    [Theory]
    [InlineData(@"C:\%" + Variable + "%", @"C:\%" + Variable + "%")]
    [InlineData("%" + Variable + "%", @"C:\Work\%" + Variable + "%")]
    public void PercentNotExpanded_OwnVariable(string text, string expected)
    {
        // A variable this class owns (set and restored here): expanding it would give C:\Expanded or C:\Work\Expanded.
        _source.Folders.Add(expected);
        _source.Folders.Add(@"C:\Expanded");
        _source.Folders.Add(@"C:\Work\Expanded");
        string? before = Environment.GetEnvironmentVariable(Variable);
        Environment.SetEnvironmentVariable(Variable, "Expanded");
        try
        {
            AssertFolder(expected, Resolve(text));
        }
        finally
        {
            Environment.SetEnvironmentVariable(Variable, before);
        }
    }

    [Theory]
    [InlineData(@"D:\My Pics""")]
    [InlineData(@"D:\My Pics"" --verbose")]
    public void ClosingQuoteTrap(string text)
    {
        _source.Folders.Add(@"D:\My Pics");

        AssertFolder(@"D:\My Pics", Resolve(text));
    }

    [Fact]
    public void LeadingQuoteOnly_Nothing()
    {
        _source.Folders.Add(@"D:\My Pics");

        AssertNothing(Resolve(@"""D:\My Pics"" --verbose"));
    }

    [Theory]
    [InlineData(@"D:\Pics\a.jpg", @"D:\Pics", "a.jpg")]
    [InlineData(@"D:\a.jpg", @"D:\", "a.jpg")]
    [InlineData(@"\\server\share\a.jpg", @"\\server\share", "a.jpg")]
    public void File_OpensItsFolder(string text, string folder, string fileName)
    {
        _source.Files.Add(text);

        OpenRequest result = Resolve(text);

        Assert.Equal(folder, result.Folder);
        Assert.Equal(fileName, result.FileName);
        Assert.Null(result.Message);
    }

    [Fact]
    public void NotFound_MessageWithNormalisedPath()
    {
        OpenRequest result = Resolve("d:/pics/");

        Assert.Equal(string.Format(CultureInfo.CurrentCulture, Strings.PathNotFound, @"d:\pics"), result.Message);
        Assert.Null(result.Folder);
        Assert.Null(result.FileName);
    }

    [Theory]
    [InlineData(@"D:\Pics?")]
    [InlineData("a|b")]
    [InlineData("a<b")]
    [InlineData("a\u0001b")]
    [InlineData(@"C:\a:b")]
    public void Forbidden_Invalid(string text)
    {
        AssertInvalid(Resolve(text));
        Assert.Equal(0, _source.CheckCalls);
    }

    [Fact]
    public void QuoteInside_CutToRelative()
    {
        OpenRequest result = Resolve("a\"b");

        Assert.Equal(string.Format(CultureInfo.CurrentCulture, Strings.PathNotFound, @"C:\Work\a"), result.Message);
        Assert.Null(result.Folder);
    }

    [Fact]
    public void LongPath_DoesNotThrow()
    {
        OpenRequest result = Resolve(@"D:\" + new string('a', 300));

        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("Pics", @"C:\Work\Pics")]
    [InlineData(".", @"C:\Work")]
    [InlineData(@"..\Pics", @"C:\Pics")]
    public void Relative_JoinedToCurrentDirectory(string text, string expected)
    {
        _source.Folders.Add(expected);

        AssertFolder(expected, Resolve(text));
    }

    [Fact]
    public void Relative_DriveRootCurrentDirectory()
    {
        _source.Folders.Add(@"C:\Pics");

        AssertFolder(@"C:\Pics", OpenTarget.Resolve("Pics", @"C:\", _source));
    }

    [Theory]
    [InlineData(@"\Pics")]
    [InlineData("D:Pics")]
    public void RootOrDriveRelative_Invalid(string text)
    {
        _source.Folders.Add(@"C:\Pics");
        _source.Folders.Add(@"C:\Work\Pics");
        _source.Folders.Add(@"D:\Pics");

        AssertInvalid(Resolve(text));
        Assert.Equal(0, _source.CheckCalls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Work")]
    [InlineData(@"\\server")]
    public void BadCurrentDirectory_Invalid(string currentDirectory)
    {
        AssertInvalid(OpenTarget.Resolve("Pics", currentDirectory, _source));
        Assert.Equal(0, _source.CheckCalls);
    }

    [Theory]
    [InlineData(@"\\?\C:\x")]
    [InlineData(@"\\.\C:\x")]
    [InlineData("//./C/x")]
    [InlineData("//?/C/x")]
    public void DevicePrefixes_Invalid(string text)
    {
        AssertInvalid(Resolve(text));
        Assert.Equal(0, _source.CheckCalls);
    }

    [Fact]
    public void NetworkFolder_Opens()
    {
        _source.Folders.Add(@"\\server\share\Docs");

        AssertFolder(@"\\server\share\Docs", Resolve(@"\\server\share\Docs"));
    }

    [Fact]
    public void Resolve_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => OpenTarget.Resolve(@"D:\Pics", null!, _source));
        Assert.Throws<ArgumentNullException>(() => OpenTarget.Resolve(@"D:\Pics", Work, null!));
    }

    [Fact]
    public void Resolve_OneDiskCheck()
    {
        _source.Folders.Add(@"D:\Pics");

        Resolve(@"D:\Pics");

        Assert.Equal(1, _source.CheckCalls);
    }

    [Fact]
    public void Resolve_NeverThrows()
    {
        var texts = new[]
        {
            "\"",
            "\"\"\"",
            ":",
            @"\\",
            @"\\server",
            @"C:\a\..\..\..",
            "\0",
            "\u0663",
            "\"\u0663\"",
            new string('a', 40000),
        };

        foreach (string text in texts)
        {
            Assert.NotNull(Resolve(text));
        }
    }

    private static void AssertNothing(OpenRequest result)
    {
        Assert.Null(result.Folder);
        Assert.Null(result.FileName);
        Assert.Null(result.Message);
    }

    private static void AssertFolder(string expected, OpenRequest result)
    {
        Assert.Equal(expected, result.Folder);
        Assert.Null(result.FileName);
        Assert.Null(result.Message);
    }

    private static void AssertInvalid(OpenRequest result)
    {
        Assert.Equal(Strings.PathInvalid, result.Message);
        Assert.Null(result.Folder);
        Assert.Null(result.FileName);
    }

    private OpenRequest Resolve(string? text) => OpenTarget.Resolve(text, Work, _source);
}
