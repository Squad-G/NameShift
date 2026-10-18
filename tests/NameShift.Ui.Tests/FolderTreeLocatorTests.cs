using System.Collections.Generic;
using NameShift.Ui.Folders;
using Xunit;

namespace NameShift.Ui.Tests;

public sealed class FolderTreeLocatorTests
{
    private static FolderRoot Special(string path) => new FolderRoot("x", path, FolderRootKind.SpecialFolder);

    private static FolderRoot Drive(string path) => new FolderRoot("x", path, FolderRootKind.Drive);

    [Fact]
    public void Locate_PicksTheDeepestRoot()
    {
        var roots = new List<FolderRoot> { Drive(@"C:\"), Special(@"C:\Users\D\Documents") };

        FolderTreeLocation? location = FolderTreeLocator.Locate(roots, @"C:\Users\D\Documents\Work\2026");

        Assert.NotNull(location);
        Assert.Equal(1, location!.RootIndex);
        Assert.Equal(new[] { "Work", "2026" }, location.Segments);
    }

    [Fact]
    public void Locate_RespectsSegmentBoundaries_AndCase()
    {
        var roots = new List<FolderRoot> { Drive(@"C:\"), Special(@"C:\Users\David") };

        FolderTreeLocation? other = FolderTreeLocator.Locate(roots, @"C:\Users\DavidX");
        FolderTreeLocation? same = FolderTreeLocator.Locate(roots, @"c:\users\david");

        Assert.Equal(0, other!.RootIndex);
        Assert.Equal(new[] { "Users", "DavidX" }, other.Segments);
        Assert.Equal(1, same!.RootIndex);
        Assert.Empty(same.Segments);
    }

    [Fact]
    public void Locate_DriveRoot_HasNoSegments()
    {
        FolderTreeLocation? location = FolderTreeLocator.Locate(new List<FolderRoot> { Drive(@"C:\") }, @"C:\");

        Assert.Equal(0, location!.RootIndex);
        Assert.Empty(location.Segments);
    }

    [Fact]
    public void Locate_UncPath_ReturnsNull()
    {
        var roots = new List<FolderRoot> { Drive(@"C:\") };

        Assert.Null(FolderTreeLocator.Locate(roots, @"\\server\share"));
    }
}
