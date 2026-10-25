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

/// <summary>NS-70 AC 5: the filter row's model drives the file list, with a fake source (no disk).</summary>
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
