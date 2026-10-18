using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NameShift.Ui.Folders;
using Xunit;

namespace NameShift.Ui.Tests;

public sealed class DiskFolderSourceTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "NameShift.Ui.Tests", Guid.NewGuid().ToString("N"));

    public DiskFolderSourceTests()
    {
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        foreach (string path in Directory.GetFileSystemEntries(_temp, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        Directory.Delete(_temp, true);
    }

    private string Folder(string name)
    {
        string path = Path.Combine(_temp, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static DiskFolderSource Source(Func<KnownFolder, string?> resolve, params string[] drives) =>
        new DiskFolderSource(resolve, () => drives, Directory.Exists, @"Z:\");

    [Fact]
    public void GetRoots_ListsSpecialFoldersInOrderThenDrives()
    {
        string desktop = Folder("desk");
        string documents = Folder("docs");
        string downloads = Folder("down");
        string pictures = Folder("pics");
        var paths = new Dictionary<KnownFolder, string>
        {
            [KnownFolder.Desktop] = desktop,
            [KnownFolder.Documents] = documents,
            [KnownFolder.Downloads] = downloads,
            [KnownFolder.Pictures] = pictures,
        };

        IReadOnlyList<FolderRoot> roots = Source(k => paths[k], @"C:\", @"D:\").GetRoots();

        Assert.Equal(new[] { Strings.Desktop, Strings.Documents, Strings.Downloads, Strings.Pictures, "C:", "D:" }, roots.Select(r => r.DisplayName));
        Assert.Equal(new[] { desktop, documents, downloads, pictures, @"C:\", @"D:\" }, roots.Select(r => r.FullPath));
        Assert.All(roots.Take(4), r => Assert.Equal(FolderRootKind.SpecialFolder, r.Kind));
        Assert.All(roots.Skip(4), r => Assert.Equal(FolderRootKind.Drive, r.Kind));
    }

    [Fact]
    public void GetRoots_SkipsSpecialFolderThatIsMissing()
    {
        string desktop = Folder("desk");
        string documents = Folder("docs");
        string Resolve(KnownFolder k) => k switch
        {
            KnownFolder.Desktop => desktop,
            KnownFolder.Documents => documents,
            KnownFolder.Downloads => "",
            _ => Path.Combine(_temp, "does-not-exist"),
        };

        IReadOnlyList<FolderRoot> roots = Source(k => Resolve(k), @"C:\").GetRoots();

        Assert.Equal(new[] { Strings.Desktop, Strings.Documents, "C:" }, roots.Select(r => r.DisplayName));
    }

    [Fact]
    public void RealKnownFolders_ResolveToWindowsPaths()
    {
        var source = new DiskFolderSource();

        IReadOnlyList<FolderRoot> roots = source.GetRoots();

        FolderRoot? documents = roots.FirstOrDefault(r => r.DisplayName == Strings.Documents);
        Assert.NotNull(documents);
        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), documents!.FullPath);
        Assert.Contains(roots, r => r.DisplayName == Strings.Downloads);
        Assert.Contains(roots, r => r.Kind == FolderRootKind.Drive);
    }

    [Fact]
    public void StartFolder_IsDocuments()
    {
        string documents = Folder("docs");

        Assert.Equal(documents, Source(_ => documents).StartFolder);
    }

    [Fact]
    public void StartFolder_DocumentsMissing_IsSystemDriveRoot()
    {
        Assert.Equal(@"Z:\", Source(_ => Path.Combine(_temp, "nope")).StartFolder);
        Assert.Equal(@"Z:\", Source(_ => null).StartFolder);
    }

    [Fact]
    public void ListChildren_ReturnsVisibleSubFoldersOnly_InNaturalOrder()
    {
        string parent = Folder("parent");
        foreach (string name in new[] { "f10", "f2", "F1", "sys" })
        {
            Directory.CreateDirectory(Path.Combine(parent, name));
        }

        string hidden = Path.Combine(parent, "hid");
        Directory.CreateDirectory(hidden);
        File.SetAttributes(hidden, FileAttributes.Hidden | FileAttributes.Directory);
        File.SetAttributes(Path.Combine(parent, "sys"), FileAttributes.System | FileAttributes.Directory);
        File.WriteAllText(Path.Combine(parent, "a.txt"), "x");

        FolderListing listing = Source(_ => null).ListChildren(parent);

        Assert.Null(listing.Problem);
        Assert.Equal(new[] { "F1", "f2", "f10", "sys" }, listing.Folders.Select(f => f.Name));
        Assert.Equal(Path.Combine(parent, "f2"), listing.Folders[1].FullPath);
    }

    [Fact]
    public void ListChildren_MissingFolder_IsNotFound()
    {
        FolderListing listing = Source(_ => null).ListChildren(Path.Combine(_temp, "missing", "deeper"));

        Assert.Equal(FolderProblem.NotFound, listing.Problem);
        Assert.Empty(listing.Folders);
    }

    [Fact]
    public void ListChildren_OnAFile_IsAProblem_NotACrash()
    {
        string file = Path.Combine(_temp, "f.txt");
        File.WriteAllText(file, "x");

        FolderListing listing = Source(_ => null).ListChildren(file);

        Assert.NotNull(listing.Problem);
        Assert.Empty(listing.Folders);
    }

    [Fact]
    public void Classify_MapsExceptions()
    {
        Assert.Equal(FolderProblem.AccessDenied, DiskFolderSource.Classify(new UnauthorizedAccessException()));
        Assert.Equal(FolderProblem.NotFound, DiskFolderSource.Classify(new DirectoryNotFoundException()));
        Assert.Equal(FolderProblem.NotFound, DiskFolderSource.Classify(new DriveNotFoundException()));
        Assert.Equal(FolderProblem.NotReady, DiskFolderSource.Classify(new IOException("not ready", unchecked((int)0x80070015))));
        Assert.Equal(FolderProblem.Unavailable, DiskFolderSource.Classify(new IOException("other")));
        Assert.Equal(FolderProblem.Unavailable, DiskFolderSource.Classify(new PathTooLongException()));
        Assert.Equal(FolderProblem.Unavailable, DiskFolderSource.Classify(new InvalidOperationException()));
    }

    [Fact]
    public void CheckFolder_FolderFileMissing()
    {
        string folder = Folder("check");
        string file = Path.Combine(folder, "a.txt");
        File.WriteAllText(file, "x");
        DiskFolderSource source = Source(_ => null);

        Assert.Equal(FolderCheck.Folder, source.CheckFolder(folder));
        Assert.Equal(FolderCheck.File, source.CheckFolder(file));
        Assert.Equal(FolderCheck.NotFound, source.CheckFolder(Path.Combine(folder, "missing")));
        Assert.Equal(FolderCheck.NotFound, source.CheckFolder(@"C:\a<b"));
    }
}
