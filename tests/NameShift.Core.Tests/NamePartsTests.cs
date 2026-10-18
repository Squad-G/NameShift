using Xunit;

namespace NameShift.Core.Tests;

public sealed class NamePartsTests
{
    [Theory]
    [InlineData("a.tar.gz", "a.tar", "gz")]
    [InlineData(".gitignore", ".gitignore", "")]
    [InlineData("Makefile", "Makefile", "")]
    [InlineData("photo.JPG", "photo", "JPG")]
    [InlineData("a.", "a.", "")]
    [InlineData(".config.json", ".config", "json")]
    public void Split_File_SplitsAtTheLastDot(string fullName, string baseName, string extension)
    {
        var parts = NameParts.Split(fullName, ItemKind.File);

        Assert.Equal(baseName, parts.BaseName);
        Assert.Equal(extension, parts.Extension);
    }

    [Theory]
    [InlineData("v1.2")]
    [InlineData(".git")]
    [InlineData("Photos")]
    public void Split_Folder_HasNoExtension(string fullName)
    {
        var parts = NameParts.Split(fullName, ItemKind.Folder);

        Assert.Equal(fullName, parts.BaseName);
        Assert.Equal("", parts.Extension);
    }

    [Theory]
    [InlineData("a.tar.gz", ItemKind.File)]
    [InlineData(".gitignore", ItemKind.File)]
    [InlineData("Makefile", ItemKind.File)]
    [InlineData("photo.JPG", ItemKind.File)]
    [InlineData("a.", ItemKind.File)]
    [InlineData(".config.json", ItemKind.File)]
    [InlineData("v1.2", ItemKind.Folder)]
    [InlineData(".git", ItemKind.Folder)]
    [InlineData("Photos", ItemKind.Folder)]
    public void ToFullName_RoundTripsEveryName(string fullName, ItemKind kind)
    {
        Assert.Equal(fullName, NameParts.Split(fullName, kind).ToFullName());
    }

    [Theory]
    [InlineData("a", "txt", "a.txt")]
    [InlineData("a", "", "a")]
    [InlineData("", "txt", ".txt")]
    public void ToFullName_JoinsWithADotOnlyWhenThereIsAnExtension(string baseName, string extension, string expected)
    {
        Assert.Equal(expected, new NameParts(baseName, extension).ToFullName());
        Assert.Equal(expected, new NameParts(baseName, extension).ToString());
    }

    [Fact]
    public void With_ReplacesOnePart()
    {
        var parts = new NameParts("a", "txt");

        Assert.Equal(new NameParts("b", "txt"), parts.WithBaseName("b"));
        Assert.Equal(new NameParts("a", "md"), parts.WithExtension("md"));
    }

    [Fact]
    public void Equals_IsOrdinal()
    {
        var parts = new NameParts("a", "txt");

        Assert.True(parts.Equals((object)new NameParts("a", "txt")));
        Assert.Equal(parts.GetHashCode(), new NameParts("a", "txt").GetHashCode());
        Assert.NotEqual(parts, new NameParts("A", "txt"));
        Assert.False(parts.Equals(null));
        Assert.False(parts.Equals("a.txt"));
    }
}
