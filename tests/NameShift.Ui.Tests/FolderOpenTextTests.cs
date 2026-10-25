using System;
using System.Globalization;
using Xunit;

namespace NameShift.Ui.Tests;

public sealed class FolderOpenTextTests
{
    private static readonly CultureInfo En = new CultureInfo("en-US");

    [Theory]
    [InlineData(PathBarResult.Navigated, null)]
    [InlineData(PathBarResult.Unchanged, null)]
    [InlineData(PathBarResult.Superseded, null)]
    [InlineData(PathBarResult.Empty, "That is not a valid folder path.")]
    [InlineData(PathBarResult.Invalid, "That is not a valid folder path.")]
    [InlineData(PathBarResult.NotFound, @"Folder not found: C:\A\Gone")]
    [InlineData(PathBarResult.NotAFolder, @"That is a file, not a folder: C:\A\Gone")]
    public void EachResult_GivesItsTextOrNull(PathBarResult result, string? expected)
    {
        Assert.Equal(expected, FolderOpenText.For(result, @"C:\A\Gone", En));
    }

    [Fact]
    public void AnUndefinedResult_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FolderOpenText.For((PathBarResult)99, @"C:\A", En));
    }
}
