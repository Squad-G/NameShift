using System;
using NameShift.Ui.Files;
using Xunit;
using static NameShift.Ui.Tests.FakeFileListSource;

namespace NameShift.Ui.Tests.Files;

public sealed class FolderRowTargetTests
{
    [Fact]
    public void FolderInTheRoot_IsRootPlusName()
    {
        Assert.Equal(@"C:\A\Sub", FolderRowTarget.Of(Folder("Sub")));
    }

    [Fact]
    public void FolderInASubLevel_UsesItsOwnFolder()
    {
        Assert.Equal(@"C:\A\Sub\Deep", FolderRowTarget.Of(Folder("Deep", folder: @"C:\A\Sub")));
    }

    [Fact]
    public void File_IsNull()
    {
        Assert.Null(FolderRowTarget.Of(File("a.txt")));
    }

    [Fact]
    public void SpacesAndNonAsciiLetters_StayUnchanged()
    {
        Assert.Equal("C:\\A\\My Caf\u00E9 Pics", FolderRowTarget.Of(Folder("My Caf\u00E9 Pics")));
    }

    [Theory]
    [InlineData("100%")]
    [InlineData("%TEMP%")]
    public void PercentNames_AreNotExpanded(string name)
    {
        Assert.Equal(@"C:\A\" + name, FolderRowTarget.Of(Folder(name)));
    }

    [Fact]
    public void Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => FolderRowTarget.Of(null!));
    }
}
