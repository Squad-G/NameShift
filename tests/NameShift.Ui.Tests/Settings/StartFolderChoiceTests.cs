using System;
using System.Collections.Generic;
using System.IO;
using NameShift.Ui.Folders;
using NameShift.Ui.Settings;
using Xunit;

namespace NameShift.Ui.Tests.Settings;

/// <summary>NS-37 AC 8: the folder the window opens on.</summary>
public sealed class StartFolderChoiceTests
{
    [Fact]
    public void Pick_ArgumentWins()
    {
        Assert.Equal(StartFolderSource.Argument, StartFolderChoice.Pick(@"D:\X", @"D:\Y"));
        Assert.Equal(StartFolderSource.Argument, StartFolderChoice.Pick(@"D:\X", null));
    }

    [Fact]
    public void Pick_Remembered()
    {
        Assert.Equal(StartFolderSource.Remembered, StartFolderChoice.Pick(null, @"D:\Y"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Pick_NoneOrBlank_StartFolder(string? remembered)
    {
        Assert.Equal(StartFolderSource.StartFolder, StartFolderChoice.Pick(null, remembered));
    }

    [Fact]
    public void Resolve_Exists_Folder()
    {
        var source = new FakeFolderSource();
        source.Folders.Add(@"D:\Pics");

        OpenRequest request = StartFolderChoice.Resolve(@"D:\Pics", source);

        Assert.Equal(@"D:\Pics", request.Folder);
        Assert.Null(request.FileName);
        Assert.Null(request.Message);
        Assert.Equal(1, source.CheckCalls);
    }

    [Fact]
    public void Resolve_KeepsThePathExactly()
    {
        string folder = @"D:\" + "\u00c9t\u00e9" + @"\a  b";
        var source = new FakeFolderSource();
        source.Folders.Add(folder);

        Assert.Equal(folder, StartFolderChoice.Resolve(folder, source).Folder);
        Assert.Equal(folder, source.LastChecked);
    }

    [Fact]
    public void Resolve_Gone_Nothing()
    {
        var source = new FakeFolderSource();

        OpenRequest request = StartFolderChoice.Resolve(@"D:\Pics", source);

        Assert.Same(OpenRequest.Nothing, request);
        Assert.Null(request.Message);
        Assert.Equal(1, source.CheckCalls);
    }

    [Fact]
    public void Resolve_IsAFile_Nothing()
    {
        var source = new FakeFolderSource();
        source.Files.Add(@"D:\Pics");

        Assert.Same(OpenRequest.Nothing, StartFolderChoice.Resolve(@"D:\Pics", source));
    }

    [Theory]
    [InlineData(@"C:\a<b")]
    [InlineData(@"relative\path")]
    [InlineData(@"""D:\Pics""")]
    [InlineData(@"%USERPROFILE%\Pics")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_Invalid_Nothing_NoCheck(string? remembered)
    {
        var source = new FakeFolderSource();
        source.Folders.Add(@"D:\Pics");

        Assert.Same(OpenRequest.Nothing, StartFolderChoice.Resolve(remembered, source));
        Assert.Equal(0, source.CheckCalls);
    }

    [Fact]
    public void Resolve_Throws_Nothing()
    {
        Assert.Same(OpenRequest.Nothing, StartFolderChoice.Resolve(@"D:\Pics", new ThrowingFolderSource()));
    }

    /// <summary>A folder source whose check throws (the interface promises it never does; the start must survive it).</summary>
    private sealed class ThrowingFolderSource : IFolderSource
    {
        public string StartFolder => @"C:\";

        public IReadOnlyList<FolderRoot> GetRoots() => Array.Empty<FolderRoot>();

        public FolderListing ListChildren(string folderPath) => throw new IOException("unreachable");

        public FolderCheck CheckFolder(string path) => throw new IOException("unreachable");
    }
}
