using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace NameShift.Ui.Tests;

/// <summary>NS-88: several path arguments give one folder and the names to select in it.</summary>
public sealed class OpenTargetManyTests
{
    private const string Work = @"C:\Work";
    private readonly FakeFolderSource _source = new FakeFolderSource();

    private static List<PathArgument> Args(params string[] texts) => texts.Select(t => new PathArgument(t, Work)).ToList();

    [Fact]
    public void PathArguments_SkipsOptionsAndBlanks()
    {
        Assert.Equal(new[] { "a", "b" }, OpenTarget.PathArguments(new[] { "-x", " ", "a", "", "b" }));
        Assert.Equal("a", OpenTarget.FirstPathArgument(new[] { "-x", " ", "a", "", "b" }));
    }

    [Fact]
    public void ResolveMany_TwoFilesSameFolder()
    {
        _source.Files.Add(@"D:\Pics\a.jpg");
        _source.Files.Add(@"D:\Pics\b.jpg");

        MultiOpenRequest request = OpenTarget.ResolveMany(Args(@"D:\Pics\a.jpg", @"D:\Pics\b.jpg"), _source);

        Assert.Equal(@"D:\Pics", request.Folder);
        Assert.Equal(new[] { "a.jpg", "b.jpg" }, request.Names);
        Assert.Equal(0, request.Rejected);
        Assert.Null(request.Message);
        Assert.Equal(1, _source.CheckCalls);
        Assert.Equal(@"D:\Pics", request.Request.Folder);
    }

    [Fact]
    public void ResolveMany_FolderAmongPaths_IsAnItem()
    {
        _source.Folders.Add(@"D:\Pics\Sub");
        _source.Files.Add(@"D:\Pics\a.jpg");

        MultiOpenRequest request = OpenTarget.ResolveMany(Args(@"D:\Pics\Sub", @"D:\Pics\a.jpg"), _source);

        Assert.Equal(@"D:\Pics", request.Folder);
        Assert.Equal(new[] { "Sub", "a.jpg" }, request.Names);
    }

    [Fact]
    public void ResolveMany_FirstMissing_SecondDecides()
    {
        _source.Files.Add(@"D:\Pics\b.jpg");

        MultiOpenRequest request = OpenTarget.ResolveMany(Args(@"D:\Gone\a.jpg", @"D:\Pics\b.jpg"), _source);

        Assert.Equal(@"D:\Pics", request.Folder);
        Assert.Equal(new[] { "b.jpg" }, request.Names);
        Assert.Equal(1, request.Rejected);
        Assert.Equal(2, _source.CheckCalls);
    }

    [Fact]
    public void ResolveMany_DuplicatesOnce()
    {
        _source.Files.Add(@"D:\Pics\a.jpg");

        MultiOpenRequest request = OpenTarget.ResolveMany(
            Args(@"D:\Pics\a.jpg", @"d:\pics\A.JPG", @"D:\Pics\a.jpg\", @"D:/Pics/a.jpg"), _source);

        Assert.Equal(new[] { "a.jpg" }, request.Names);
        Assert.Equal(0, request.Rejected);
    }

    [Fact]
    public void ResolveMany_RelativePerPath()
    {
        _source.Files.Add(@"D:\Pics\a.jpg");
        var paths = new List<PathArgument>
        {
            new PathArgument("a.jpg", @"D:\Pics"),
            new PathArgument(@"..\Pics\b.jpg", @"D:\Other"),
            new PathArgument("c.jpg", @"D:\Pics\"),
        };

        MultiOpenRequest request = OpenTarget.ResolveMany(paths, _source);

        Assert.Equal(@"D:\Pics", request.Folder);
        Assert.Equal(new[] { "a.jpg", "b.jpg", "c.jpg" }, request.Names);
        Assert.Equal(0, request.Rejected);
    }

    [Fact]
    public void ResolveMany_OtherFolderAndInvalid_Rejected()
    {
        _source.Files.Add(@"D:\Pics\a.jpg");

        MultiOpenRequest request = OpenTarget.ResolveMany(
            Args(@"D:\Pics\a.jpg", @"D:\Other\x.jpg", @"C:\a<b"), _source);

        Assert.Equal(@"D:\Pics", request.Folder);
        Assert.Equal(new[] { "a.jpg" }, request.Names);
        Assert.Equal(2, request.Rejected);
        Assert.Equal(1, _source.CheckCalls);
    }

    [Fact]
    public void ResolveMany_NoneExists_FirstMessage()
    {
        MultiOpenRequest request = OpenTarget.ResolveMany(Args(@"D:\Gone\a.jpg", @"D:\Gone\b.jpg"), _source);

        Assert.Null(request.Folder);
        Assert.Empty(request.Names);
        Assert.Equal(string.Format(System.Globalization.CultureInfo.CurrentCulture, Strings.PathNotFound, @"D:\Gone\a.jpg"), request.Message);
        Assert.Equal(request.Message, request.Request.Message);
        Assert.Equal(2, _source.CheckCalls);
    }

    [Fact]
    public void ResolveMany_FirstInvalid_MessageIsPathInvalid()
    {
        MultiOpenRequest request = OpenTarget.ResolveMany(Args(@"C:\a<b", @"D:\Gone\b.jpg"), _source);

        Assert.Null(request.Folder);
        Assert.Equal(Strings.PathInvalid, request.Message);
    }

    [Fact]
    public void ResolveMany_SpacesNonAsciiRootUnc()
    {
        _source.Files.Add("D:\\My Pics\\caf\u00E9 1.jpg");
        _source.Files.Add("D:\\My Pics\\\u00E9t\u00E9.jpg");
        _source.Files.Add(@"D:\a.jpg");
        _source.Files.Add(@"\\srv\share\p\a.jpg");
        _source.Files.Add(@"\\srv\share\p\b.jpg");

        MultiOpenRequest spaces = OpenTarget.ResolveMany(Args("D:\\My Pics\\caf\u00E9 1.jpg", "\"D:\\My Pics\\\u00E9t\u00E9.jpg\""), _source);
        MultiOpenRequest root = OpenTarget.ResolveMany(Args(@"D:\a.jpg"), _source);
        MultiOpenRequest unc = OpenTarget.ResolveMany(Args(@"\\srv\share\p\a.jpg", @"\\srv\share\p\b.jpg"), _source);

        Assert.Equal(@"D:\My Pics", spaces.Folder);
        Assert.Equal(new[] { "caf\u00E9 1.jpg", "\u00E9t\u00E9.jpg" }, spaces.Names);
        Assert.Equal(@"D:\", root.Folder);
        Assert.Equal(new[] { "a.jpg" }, root.Names);
        Assert.Equal(@"\\srv\share\p", unc.Folder);
        Assert.Equal(new[] { "a.jpg", "b.jpg" }, unc.Names);
    }

    [Fact]
    public void ResolveMany_DriveRootAmongPaths_NotSelected()
    {
        _source.Folders.Add(@"D:\");
        _source.Files.Add(@"D:\a.jpg");

        MultiOpenRequest request = OpenTarget.ResolveMany(Args(@"D:\", @"D:\a.jpg"), _source);

        Assert.Equal(@"D:\", request.Folder);
        Assert.Equal(new[] { "a.jpg" }, request.Names);
        Assert.Equal(1, request.Rejected);
    }

    [Fact]
    public void ResolveMany_Hundreds()
    {
        var paths = new List<PathArgument>();
        for (int i = 0; i < 600; i++)
        {
            string path = @"D:\Pics\photo number " + i + ".jpg";
            _source.Files.Add(path);
            paths.Add(new PathArgument(path, Work));
        }

        MultiOpenRequest request = OpenTarget.ResolveMany(paths, _source);

        Assert.Equal(@"D:\Pics", request.Folder);
        Assert.Equal(600, request.Names.Count);
        Assert.Equal(0, request.Rejected);
        Assert.Equal(1, _source.CheckCalls);
    }

    [Fact]
    public void Normalize_AndParentOf()
    {
        Assert.True(OpenTarget.Normalize("  ", Work).IsBlank);
        Assert.Equal(Strings.PathInvalid, OpenTarget.Normalize(@"C:\a<b", Work).Message);
        Assert.Equal(@"C:\Work\x.txt", OpenTarget.Normalize("x.txt", Work).FullPath);
        Assert.Equal(@"D:\", OpenTarget.ParentOf(@"D:\a.jpg"));
        Assert.Equal(@"D:\Pics", OpenTarget.ParentOf(@"D:\Pics\a.jpg"));
        Assert.Null(OpenTarget.ParentOf(@"D:\"));
        Assert.Null(OpenTarget.ParentOf(@"\\srv\share"));
        Assert.Equal(@"\\srv\share", OpenTarget.ParentOf(@"\\srv\share\a.jpg"));
    }
}
