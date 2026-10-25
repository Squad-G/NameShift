using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NameShift.Core;
using NameShift.Ui.Files;
using Xunit;
using static NameShift.Ui.Tests.FakeFileListSource;

namespace NameShift.Ui.Tests.Files;

/// <summary>NS-94 AC 3 to 5: the filter and the Sub-folders box set one after the other, in either order, end in the same
/// list. Every call is awaited (no sleep, no timer): the order is the test's own, so there is no race to pin.</summary>
public sealed class ListOrderTests
{
    private const string FolderA = @"C:\A";
    private const string SubFolder = @"C:\A\Sub";

    private readonly FakeFileListSource _source = new FakeFileListSource();
    private readonly FileListModel _files;
    private readonly RenameItem[] _rootItems;
    private readonly RenameItem[] _boxItems;
    private readonly ItemFilter _jpgOnly = new ItemFilter(mask: "*.jpg");

    public ListOrderTests()
    {
        _rootItems = new[] { File("a.jpg"), File("b.txt"), Hidden("h.jpg") };
        _boxItems = _rootItems.Concat(new[] { File("c.jpg", folder: SubFolder) }).ToArray();
        _source.SetListing(FolderA, FileListing.Ok(_rootItems));
        _source.SetListing(FolderA, true, FileListing.Ok(_boxItems));
        _files = new FileListModel(_source);
    }

    private static string[] Names(IEnumerable<RenameItem> items) => items.Select(i => i.Name).ToArray();

    [Fact]
    public async Task FilterThenBox_ThenUndo()
    {
        Assert.True(await TestWait.Within(_files.LoadAsync(FolderA)));

        Assert.True(await TestWait.Within(_files.SetFilterAsync(_jpgOnly)));
        Assert.True(await TestWait.Within(_files.SetIncludeSubfoldersAsync(true)));
        AssertEnds(_jpgOnly, true, new[] { "a.jpg", "c.jpg" });

        Assert.True(await TestWait.Within(_files.SetFilterAsync(new ItemFilter())));
        Assert.True(await TestWait.Within(_files.SetIncludeSubfoldersAsync(false)));
        AssertEnds(_files.Filter, false, new[] { "a.jpg", "b.txt" });
        Assert.True(_files.IsFilterDefault);
    }

    [Fact]
    public async Task BoxThenFilter_ThenUndo()
    {
        Assert.True(await TestWait.Within(_files.LoadAsync(FolderA)));

        Assert.True(await TestWait.Within(_files.SetIncludeSubfoldersAsync(true)));
        Assert.True(await TestWait.Within(_files.SetFilterAsync(_jpgOnly)));
        AssertEnds(_jpgOnly, true, new[] { "a.jpg", "c.jpg" });

        Assert.True(await TestWait.Within(_files.SetIncludeSubfoldersAsync(false)));
        Assert.True(await TestWait.Within(_files.SetFilterAsync(new ItemFilter())));
        AssertEnds(_files.Filter, false, new[] { "a.jpg", "b.txt" });
        Assert.True(_files.IsFilterDefault);
    }

    private void AssertEnds(ItemFilter filter, bool includeSubfolders, string[] names)
    {
        Assert.Equal(names, Names(_files.Items));
        Assert.Same(filter, _files.Filter);
        Assert.Equal(includeSubfolders, _files.IncludeSubfolders);
        Assert.Equal(includeSubfolders, _source.LastIncludeSubfolders);

        // The same list the filter makes of the listing for these settings (default sort: by name).
        IEnumerable<RenameItem> listed = includeSubfolders ? _boxItems : _rootItems;
        Assert.Equal(
            Names(listed.Where(filter.Includes)).OrderBy(n => n, System.StringComparer.OrdinalIgnoreCase),
            Names(_files.Items));
    }
}
