using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NameShift.Core;
using NameShift.Ui.Files;
using Xunit;
using static NameShift.Ui.Tests.FakeFileListSource;

namespace NameShift.Ui.Tests.Files;

/// <summary>NS-70 AC 5 and NS-78 AC 2: the filter row's model drives the file list (its filter, and the Sub-folders
/// reload), with a fake source (no disk). The fake returns the listing set for a path whatever the Sub-folders flag, so
/// a test sets the listing it wants before each load.</summary>
public sealed class FilterBindingTests
{
    private const string FolderA = @"C:\A";
    private const string FolderB = @"C:\B";

    private readonly FakeFileListSource _source = new FakeFileListSource();
    private readonly FilterPanelModel _filters = new FilterPanelModel();
    private readonly FileListModel _files;
    private readonly FilterBinding _binding;
    private int _orderChanges;

    public FilterBindingTests()
    {
        _source.SetEntries(FolderA, File("a.jpg"), File("b.txt"), File("c.jpg"));
        _source.SetEntries(FolderB, File("x.jpg", folder: FolderB), File("y.txt", folder: FolderB));
        _files = new FileListModel(_source);
        _files.OrderChanged += (_, _) => Interlocked.Increment(ref _orderChanges);
        _binding = new FilterBinding(_filters, _files);
    }

    private static string[] Names(IEnumerable<RenameItem> items) => items.Select(i => i.Name).ToArray();

    /// <summary>The listing of A with its sub-folder, as a walk with Sub-folders on gives it.</summary>
    private void SetTreeListing() =>
        _source.SetEntries(FolderA, File("a.jpg"), File("b.txt"), File("c.jpg"), File("s.txt", folder: @"C:\A\Sub"));

    private void SetFolderOnlyListing() =>
        _source.SetEntries(FolderA, File("a.jpg"), File("b.txt"), File("c.jpg"));

    [Fact]
    public async Task Tick_ReloadsWithSubfolders()
    {
        await _files.LoadAsync(FolderA);
        int calls = _source.Calls;
        SetTreeListing();

        _filters.IncludeSubfolders = true;

        Assert.True(await TestWait.Within(_binding.LastReload));
        Assert.False(await TestWait.Within(_binding.LastRun));
        Assert.Equal(calls + 1, _source.Calls);
        Assert.True(_source.LastIncludeSubfolders);
        Assert.True(_source.LastToken.CanBeCanceled);
        Assert.True(_files.IncludeSubfolders);
        Assert.Equal(new[] { "a.jpg", "b.txt", "c.jpg", "s.txt" }, Names(_files.Items));
    }

    [Fact]
    public async Task Untick_ReloadsWithout()
    {
        await _files.LoadAsync(FolderA);
        SetTreeListing();
        _filters.IncludeSubfolders = true;
        Assert.True(await TestWait.Within(_binding.LastReload));
        int calls = _source.Calls;
        SetFolderOnlyListing();

        _filters.IncludeSubfolders = false;

        Assert.True(await TestWait.Within(_binding.LastReload));
        Assert.Equal(calls + 1, _source.Calls);
        Assert.False(_source.LastIncludeSubfolders);
        Assert.False(_files.IncludeSubfolders);
        Assert.Equal(new[] { "a.jpg", "b.txt", "c.jpg" }, Names(_files.Items));
    }

    [Fact]
    public async Task Reload_ClearsTheSelection()
    {
        await _files.LoadAsync(FolderA);
        _files.SetSelection(new[] { 1 });
        Assert.Equal(new[] { 1 }, _files.SelectedRows);

        _filters.IncludeSubfolders = true;

        Assert.True(await TestWait.Within(_binding.LastReload));
        Assert.Empty(_files.SelectedRows);
    }

    [Fact]
    public async Task Reset_WithBoxAndMask_ConsistentEnd()
    {
        await _files.LoadAsync(FolderA);
        SetTreeListing();
        _filters.Mask = "*.txt";
        _filters.IncludeSubfolders = true;
        await TestWait.Within(_binding.LastRun);
        Assert.True(await TestWait.Within(_binding.LastReload));
        await TestWait.Within(_files.PendingFilter);
        Assert.Equal(new[] { "b.txt", "s.txt" }, Names(_files.Items));
        int calls = _source.Calls;
        SetFolderOnlyListing();

        _filters.Reset();

        await TestWait.Within(_binding.LastRun);
        await TestWait.Within(_binding.LastReload);
        await TestWait.Within(_files.PendingFilter);
        Assert.False(_files.IncludeSubfolders);
        Assert.False(_source.LastIncludeSubfolders);
        Assert.Equal(calls + 1, _source.Calls);
        Assert.Equal(new[] { "a.jpg", "b.txt", "c.jpg" }, Names(_files.Items));
        Assert.Same(_filters.Filter, _files.Filter);
    }

    [Fact]
    public async Task TickBeforeAnyFolder_FirstLoadUsesIt()
    {
        _filters.IncludeSubfolders = true;

        Assert.False(await TestWait.Within(_binding.LastReload));
        Assert.Equal(0, _source.Calls);
        Assert.True(_files.IncludeSubfolders);

        Assert.True(await TestWait.Within(_files.LoadAsync(FolderA)));
        Assert.Equal(1, _source.Calls);
        Assert.True(_source.LastIncludeSubfolders);
    }

    [Fact]
    public async Task Disposed_NoReload()
    {
        await _files.LoadAsync(FolderA);
        int calls = _source.Calls;

        _binding.Dispose();
        _filters.IncludeSubfolders = true;
        await TestWait.Within(_binding.LastReload);

        Assert.Equal(calls, _source.Calls);
        Assert.False(_files.IncludeSubfolders);
    }

    [Fact]
    public async Task MaskChange_FiltersItems()
    {
        await _files.LoadAsync(FolderA);

        _filters.Mask = "*.jpg";

        Assert.True(await TestWait.Within(_binding.LastRun));
        Assert.Equal(new[] { "a.jpg", "c.jpg" }, Names(_files.Items));
    }

    [Fact]
    public async Task Reset_OneChanged_OneRun()
    {
        await _files.LoadAsync(FolderA);
        IReadOnlyList<RenameItem> unfiltered = _files.Items;
        _filters.Mask = "*.jpg";
        Assert.True(await TestWait.Within(_binding.LastRun));
        int changed = 0;
        _filters.Changed += (_, _) => changed++;
        int before = Volatile.Read(ref _orderChanges);

        _filters.Reset();

        Assert.True(await TestWait.Within(_binding.LastRun));
        Assert.Equal(1, changed);
        Assert.Equal(before + 1, Volatile.Read(ref _orderChanges));
        Assert.Equal(unfiltered, _files.Items);
    }

    [Fact]
    public async Task CheckBoxAndBack_SameAsUnfiltered()
    {
        await _files.LoadAsync(FolderA);
        RenameItem[] before = _files.Items.ToArray();

        _filters.IncludeFiles = false;
        _filters.IncludeFiles = true;
        await TestWait.Within(_binding.LastRun);
        await TestWait.Within(_files.PendingFilter);

        Assert.Equal(before.Length, _files.Items.Count);
        for (int i = 0; i < before.Length; i++)
        {
            Assert.Same(before[i], _files.Items[i]);
        }
    }

    [Fact]
    public async Task FiltersSurviveAFolderChange()
    {
        await _files.LoadAsync(FolderA);
        _filters.Mask = "*.jpg";
        await TestWait.Within(_binding.LastRun);

        await TestWait.Within(_files.LoadAsync(FolderB));

        Assert.Equal(new[] { "x.jpg" }, Names(_files.Items));
    }

    [Fact]
    public async Task Dispose_StopsForwarding()
    {
        await _files.LoadAsync(FolderA);
        ItemFilter filter = _files.Filter;

        _binding.Dispose();
        _filters.Mask = "*.jpg";
        await TestWait.Within(_binding.LastRun);
        await TestWait.Within(_files.PendingFilter);

        Assert.Same(filter, _files.Filter);
        Assert.Equal(3, _files.Items.Count);
        _binding.Dispose();
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new FilterBinding(null!, _files));
        Assert.Throws<ArgumentNullException>(() => new FilterBinding(_filters, null!));
    }
}
