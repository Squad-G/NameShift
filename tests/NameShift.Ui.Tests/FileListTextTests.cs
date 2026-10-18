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
}