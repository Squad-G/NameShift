using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Threading;
using NameShift.Core;
using NameShift.Ui.Files;
using NameShift.Ui.Folders;
using Xunit;
using Xunit.Abstractions;

namespace NameShift.Ui.Tests.Files;

/// <summary>NS-74: the sub-folder walk of <see cref="DiskFileListSource"/> over an in-memory tree (no disk).</summary>
public sealed class SubfolderListingTests
{
    private const string Root = @"C:\R";

    private readonly FakeDirectoryEnumerator _tree = new FakeDirectoryEnumerator();
    private readonly DiskFileListSource _source;
    private readonly ITestOutputHelper _output;

    public SubfolderListingTests(ITestOutputHelper output)
    {
        _output = output;
        _source = new DiskFileListSource(_tree);
        _tree.Declare(Root);
    }

    private FileListing List(bool includeSubfolders = true) => _source.ListEntries(Root, includeSubfolders, CancellationToken.None);

    private static string[] PathsOf(FileListing listing) =>
        listing.Items.Select(item => item.FolderPath + "\\" + item.Name).ToArray();

    // AC 2

    [Fact]
    public void CancelledToken_Throws_NotFailed()
    {
        string folder1 = _tree.Folder(Root, "folder1");
        _tree.File(folder1, "a.txt");
        _tree.File(folder1, "b.txt");
        _tree.Folder(Root, "folder2");
        using var cts = new CancellationTokenSource();
        _tree.OnEntry = (path, _) =>
        {
            if (path == folder1)
            {
                cts.Cancel();
            }
        };

        Assert.ThrowsAny<OperationCanceledException>(() => _source.ListEntries(Root, true, cts.Token));
    }

    [Fact]
    public void OperationCanceled_TokenNotCancelled_IsFailed()
    {
        _tree.ThrowAtCall(Root, new OperationCanceledException());

        FileListing listing = List();

        Assert.Equal(FolderProblem.Unavailable, listing.Problem);
        Assert.Empty(listing.Items);
    }

    // AC 6

    [Fact]
    public void NoSubfolders_RootOnly()
    {
        _tree.File(Root, "a.txt");
        string x = _tree.Folder(Root, "x");
        _tree.File(x, "b.txt");

        FileListing listing = List(includeSubfolders: false);

        Assert.Null(listing.Problem);
        Assert.Equal(new[] { @"C:\R\a.txt", @"C:\R\x" }, PathsOf(listing));
        Assert.Equal(new[] { Root }, _tree.Enumerated);
        Assert.Equal(0, listing.SkippedFolders);
    }

    // AC 7

    [Fact]
    public void Tree_EachItemItsOwnFolder()
    {
        _tree.File(Root, "a.txt");
        _tree.File(Root, "hidden.txt", ItemAttributes.Hidden);
        string x = _tree.Folder(Root, "x");
        _tree.File(x, "b.txt");
        _tree.File(x, "system.sys", ItemAttributes.System);
        string y = _tree.Folder(x, "y");
        _tree.File(y, "c.txt");
        _tree.Folder(Root, "e");

        FileListing listing = List();

        Assert.Null(listing.Problem);
        Assert.Equal(
            new[] { @"C:\R\a.txt", @"C:\R\hidden.txt", @"C:\R\x", @"C:\R\e", @"C:\R\x\b.txt", @"C:\R\x\system.sys", @"C:\R\x\y", @"C:\R\x\y\c.txt" },
            PathsOf(listing));
        Assert.Equal(new[] { Root, x, y }, listing.ExistingNamesByFolder.Keys.OrderBy(k => k.Length));
        Assert.Equal(new[] { "a.txt", "hidden.txt", "x", "e" }, listing.ExistingNamesByFolder[Root]);
        Assert.Equal(new[] { "b.txt", "system.sys", "y" }, listing.ExistingNamesByFolder[x]);
        Assert.Equal(new[] { "c.txt" }, listing.ExistingNamesByFolder[y]);
        Assert.False(listing.ExistingNamesByFolder.ContainsKey(@"C:\R\e"));
        Assert.Contains(@"C:\R\e", _tree.Enumerated);
    }

    // AC 8

    [Fact]
    public void ReparseFolder_ListedNotEntered()
    {
        string target = _tree.Folder(Root, "target");
        _tree.File(target, "inside.txt");
        _tree.Folder(Root, "link", reparse: true);
        _tree.Link(@"C:\R\link", target);

        FileListing listing = List();

        Assert.Null(listing.Problem);
        RenameItem link = listing.Items.Single(item => item.Name == "link");
        Assert.Equal(ItemKind.Folder, link.Kind);
        Assert.Equal(Root, link.FolderPath);
        Assert.Single(listing.Items, item => item.Name == "inside.txt");
        Assert.DoesNotContain(@"C:\R\link", _tree.Enumerated, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(0, listing.SkippedFolders);
    }

    [Fact]
    public void Loop_Ends()
    {
        // Many entries per level: a followed loop reaches the fake's cap after a few levels, never hangs.
        for (int i = 0; i < 1000; i++)
        {
            _tree.File(Root, "f" + i.ToString(CultureInfo.InvariantCulture) + ".txt");
        }

        _tree.Folder(Root, "loop", reparse: true);
        _tree.Link(@"C:\R\loop", Root);
        string x = _tree.Folder(Root, "x");
        _tree.File(x, "b.txt");

        FileListing listing = List();

        Assert.Null(listing.Problem);
        Assert.Equal(1003, listing.Items.Count);
        Assert.Equal(PathsOf(listing).Length, PathsOf(listing).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(new[] { Root, x }, _tree.Enumerated);
        Assert.Equal(0, listing.SkippedFolders);
    }

    [Fact]
    public void ReparseFile_ListedAsAFile()
    {
        _tree.File(Root, "link.txt", reparse: true);

        FileListing listing = List();

        RenameItem item = Assert.Single(listing.Items);
        Assert.Equal("link.txt", item.Name);
        Assert.Equal(ItemKind.File, item.Kind);
        Assert.Equal(Root, item.FolderPath);
    }

    // AC 9

    private static Exception Make(string kind)
    {
        switch (kind)
        {
            case "access":
                return new UnauthorizedAccessException("denied");
            case "toolong":
                return new PathTooLongException("too long");
            case "notfound":
                return new DirectoryNotFoundException("removed");
            case "io":
                return new IOException("device error");
            case "security":
                return new SecurityException("policy");
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown exception kind.");
        }
    }

    [Theory]
    [InlineData("access", false)]
    [InlineData("access", true)]
    [InlineData("toolong", false)]
    [InlineData("toolong", true)]
    [InlineData("notfound", false)]
    [InlineData("notfound", true)]
    [InlineData("io", false)]
    [InlineData("io", true)]
    [InlineData("security", false)]
    [InlineData("security", true)]
    public void UnreadableSubfolder_Skipped(string kind, bool midway)
    {
        _tree.File(Root, "a.txt");
        string bad = _tree.Folder(Root, "bad");
        for (int i = 0; i < 4; i++)
        {
            _tree.File(bad, "b" + i.ToString(CultureInfo.InvariantCulture) + ".txt");
        }

        string good = _tree.Folder(Root, "good");
        _tree.File(good, "g.txt");
        if (midway)
        {
            _tree.ThrowAfter(bad, 2, Make(kind));
        }
        else
        {
            _tree.ThrowAtCall(bad, Make(kind));
        }

        FileListing listing = List();

        Assert.Null(listing.Problem);
        string[] expectedBad = midway ? new[] { @"C:\R\bad\b0.txt", @"C:\R\bad\b1.txt" } : Array.Empty<string>();
        Assert.Equal(
            new[] { @"C:\R\a.txt", @"C:\R\bad", @"C:\R\good" }.Concat(expectedBad).Concat(new[] { @"C:\R\good\g.txt" }),
            PathsOf(listing));
        Assert.Equal(1, listing.SkippedFolders);
    }

    [Fact]
    public void MidwayFailure_DiscoveredChildrenStillEntered()
    {
        string x = _tree.Folder(Root, "x");
        string y = _tree.Folder(x, "y");
        _tree.File(x, "lost.txt");
        _tree.File(y, "c.txt");
        _tree.ThrowAfter(x, 1, new IOException("device error"));

        FileListing listing = List();

        Assert.Null(listing.Problem);
        Assert.Equal(new[] { @"C:\R\x", @"C:\R\x\y", @"C:\R\x\y\c.txt" }, PathsOf(listing));
        Assert.Equal(1, listing.SkippedFolders);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RootFails_IsFailed(bool midway)
    {
        _tree.File(Root, "a.txt");
        _tree.File(Root, "b.txt");
        var denied = new UnauthorizedAccessException("denied");
        if (midway)
        {
            _tree.ThrowAfter(Root, 1, denied);
        }
        else
        {
            _tree.ThrowAtCall(Root, denied);
        }

        FileListing listing = List();

        Assert.Equal(DiskFolderSource.Classify(denied), listing.Problem);
        Assert.Equal(FolderProblem.AccessDenied, listing.Problem);
        Assert.Empty(listing.Items);
        Assert.Equal(0, listing.SkippedFolders);
    }

    // AC 10

    [Fact]
    public void FiveThousandDeep_NoStackOverflow_Fast()
    {
        const int depth = 5000;
        _tree.File(Root, "f.txt");
        string folder = Root;
        for (int i = 0; i < depth; i++)
        {
            folder = _tree.Folder(folder, "d");
            _tree.File(folder, "f.txt");
        }

        // A quadratic walk is cancelled here instead of running for minutes: the test then fails.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var watch = Stopwatch.StartNew();
        FileListing listing = _source.ListEntries(Root, true, cts.Token);
        watch.Stop();

        _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "5,000 folders deep ({0:N0} characters at the deepest): {1:N1} ms.", folder.Length, watch.Elapsed.TotalMilliseconds));
        Assert.Null(listing.Problem);
        Assert.Equal((2 * depth) + 1, listing.Items.Count);
        Assert.Equal(folder, listing.Items[listing.Items.Count - 1].FolderPath);
        Assert.True(folder.Length > 10000);
        Assert.True(
            watch.Elapsed < TimeSpan.FromSeconds(3),
            string.Format(CultureInfo.InvariantCulture, "The walk took {0:N0} ms; budget 3,000 ms.", watch.Elapsed.TotalMilliseconds));
    }

    // AC 11

    [Fact]
    public void Order_TreeOrder_NaturalSiblings()
    {
        AssertTreeOrder();
    }

    [Fact]
    public void Order_TreeOrder_NaturalSiblings_TurkishCulture()
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            CultureInfo.CurrentUICulture = new CultureInfo("tr-TR");
            AssertTreeOrder();
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }

    private void AssertTreeOrder()
    {
        // Declared out of order: the walk sorts the siblings.
        string x10 = _tree.Folder(Root, "X10");
        string x2 = _tree.Folder(Root, "x2");
        string ib = _tree.Folder(Root, "Ib");
        string x = _tree.Folder(Root, "x");
        string ia = _tree.Folder(Root, "ia");
        _tree.File(Root, "r.txt");
        string y = _tree.Folder(x, "y");
        foreach (string folder in new[] { x10, x2, ib, x, ia, y })
        {
            _tree.File(folder, "f.txt");
        }

        FileListing listing = List();

        string[] groups = listing.Items.Select(item => item.FolderPath).Distinct().ToArray();
        Assert.Equal(new[] { Root, ia, ib, x, y, x2, x10 }, groups);
    }

    // AC 13

    [Fact]
    public void Cancel_AfterKEntries_AtMostKPlus1000More()
    {
        const int k = 10;
        for (int i = 0; i < 5000; i++)
        {
            _tree.File(Root, "f" + i.ToString(CultureInfo.InvariantCulture) + ".txt");
        }

        using var cts = new CancellationTokenSource();
        int seen = 0;
        _tree.OnEntry = (_, _) =>
        {
            if (++seen == k)
            {
                cts.Cancel();
            }
        };

        Assert.ThrowsAny<OperationCanceledException>(() => _source.ListEntries(Root, true, cts.Token));
        _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "Entries asked for: {0:N0}.", _tree.TotalYielded));
        Assert.True(_tree.TotalYielded <= k + 1000, "Entries asked for: " + _tree.TotalYielded.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Cancel_InFolder1_Folder2NeverEnumerated()
    {
        string folder1 = _tree.Folder(Root, "folder1");
        _tree.File(folder1, "a.txt");
        string folder2 = _tree.Folder(Root, "folder2");
        _tree.File(folder2, "b.txt");
        using var cts = new CancellationTokenSource();
        _tree.OnEntry = (path, _) =>
        {
            if (path == folder1)
            {
                cts.Cancel();
            }
        };

        Assert.ThrowsAny<OperationCanceledException>(() => _source.ListEntries(Root, true, cts.Token));
        Assert.Equal(new[] { Root, folder1 }, _tree.Enumerated);
        Assert.Equal(0, _tree.YieldedIn(folder2));
    }

    [Fact]
    public void ListEntries_NoSkips_SkippedFoldersZero()
    {
        string x = _tree.Folder(Root, "x");
        _tree.File(x, "a.txt");

        Assert.Equal(0, List().SkippedFolders);
    }
}
