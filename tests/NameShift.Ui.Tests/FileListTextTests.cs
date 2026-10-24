using System;
using System.Globalization;
using NameShift.Core;
using NameShift.Ui.Files;
using Xunit;

namespace NameShift.Ui.Tests;

public sealed class FileListTextTests
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    private static RenameItem FileOf(long size, DateTime modified = default) =>
        new RenameItem("a.txt", @"C:\A", ItemKind.File, ItemAttributes.None, size, default, modified);

    [Theory]
    [InlineData(2, 3, "2 of 3 files")]
    [InlineData(3, 3, "3 of 3 files")]
    [InlineData(1, 1, "1 of 1 file")]
    [InlineData(0, 0, "")]
    [InlineData(1234, 10000, "1,234 of 10,000 files")]
    public void SelectionCount_Text(int batchCount, int total, string expected)
    {
        Assert.Equal(expected, FileListText.SelectionCount(batchCount, total, CultureInfo.GetCultureInfo("en-US")));
    }

    [Theory]
    [InlineData(0L, "0 KB")]
    [InlineData(1L, "1 KB")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1025L, "2 KB")]
    public void Size_File_IsKilobytesRoundedUp(long size, string expected)
    {
        Assert.Equal(expected, FileListText.Size(FileOf(size), CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Size_File_GroupsDigitsInTheCulture()
    {
        Assert.Equal("1.206 KB", FileListText.Size(FileOf(1234567), German));
    }

    [Fact]
    public void Size_Folder_IsEmpty()
    {
        var folder = new RenameItem("d", @"C:\A", ItemKind.Folder);

        Assert.Equal("", FileListText.Size(folder, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Modified_IsShortDateAndTime_InTheCulture()
    {
        var modified = new DateTime(2026, 10, 18, 14, 5, 0);

        string invariant = FileListText.Modified(FileOf(0, modified), CultureInfo.InvariantCulture);
        string german = FileListText.Modified(FileOf(0, modified), German);

        Assert.Equal("10/18/2026 14:05", invariant);
        Assert.Equal(modified.ToString("g", German), german);
        Assert.NotEqual(invariant, german);
    }

    [Fact]
    public void Modified_Unknown_IsEmpty()
    {
        Assert.Equal("", FileListText.Modified(FileOf(0), CultureInfo.InvariantCulture));
    }

    // NS-74 AC 14

    [Theory]
    [InlineData(@"C:\Pics", @"C:\Pics", "")]
    [InlineData(@"C:\Pics", @"c:\pics\a\b", @"a\b")]
    [InlineData(@"C:\Pics", @"C:\Pics\a\", "a")]
    [InlineData(@"C:\Pics", @"C:\Pics\", "")]
    [InlineData(@"C:\Pics\", @"C:\Pics", "")]
    [InlineData(@"C:\Pics\", @"c:\pics\a\b", @"a\b")]
    [InlineData(@"C:\Pics\", @"C:\Pics\a\", "a")]
    [InlineData(@"C:\", @"C:\x", "x")]
    [InlineData(@"C:\", @"C:\", "")]
    [InlineData(@"C:\", @"c:\x\y", @"x\y")]
    [InlineData(@"C:\Pics", @"C:\Pics2\x", @"C:\Pics2\x")]
    [InlineData(@"C:\Pics", @"C:\Pics2", @"C:\Pics2")]
    [InlineData(@"C:\Pics", @"D:\y", @"D:\y")]
    [InlineData(@"C:\Pics\a", @"C:\Pics", @"C:\Pics")]
    public void RelativeFolder(string root, string folder, string expected)
    {
        var item = new RenameItem("a.txt", folder);

        Assert.Equal(expected, FileListText.RelativeFolder(root, item));
    }

    [Fact]
    public void RelativeFolder_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => FileListText.RelativeFolder(null!, FileOf(0)));
        Assert.Throws<ArgumentNullException>(() => FileListText.RelativeFolder(@"C:\A", null!));
    }
}
