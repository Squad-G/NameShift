using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NameShift.Ui.Tests;

public sealed class FolderNavigatorExactTests
{
    private readonly FakeFolderSource _source = new FakeFolderSource { StartFolder = @"C:\Start" };
    private readonly List<string> _events = new List<string>();
    private readonly FolderNavigator _navigator;

    public FolderNavigatorExactTests()
    {
        _navigator = new FolderNavigator(_source);
        _navigator.FolderChanged += (_, e) => _events.Add(e.Path);
    }

    [Fact]
    public async Task Exact_100Percent_Opens()
    {
        _source.Folders.Add(@"C:\A\100%");

        PathBarResult result = await _navigator.NavigateToFolderAsync(@"C:\A\100%");

        Assert.Equal(PathBarResult.Navigated, result);
        Assert.Equal(new[] { @"C:\A\100%" }, _events);
        Assert.Equal(@"C:\A\100%", _source.LastChecked);
    }

    [Fact]
    public async Task Exact_PercentTemp_NotExpanded()
    {
        const string path = @"C:\A\%TEMP%";
        Assert.NotEqual(path, Environment.ExpandEnvironmentVariables(path));
        _source.Folders.Add(path);

        PathBarResult result = await _navigator.NavigateToFolderAsync(path);

        Assert.Equal(PathBarResult.Navigated, result);
        Assert.Equal(path, _source.LastChecked);
        Assert.Equal(new[] { path }, _events);
    }

    [Fact]
    public async Task Exact_LeadingSpace_Unchanged()
    {
        _source.Folders.Add(@"C:\A\ x");

        PathBarResult result = await _navigator.NavigateToFolderAsync(@"C:\A\ x");

        Assert.Equal(PathBarResult.Navigated, result);
        Assert.Equal(@"C:\A\ x", _source.LastChecked);
    }

    [Fact]
    public async Task Exact_Quotes_NotStripped()
    {
        _source.Folders.Add(@"C:\A");

        PathBarResult result = await _navigator.NavigateToFolderAsync("\"C:\\A\"");

        Assert.Equal(PathBarResult.Invalid, result);
        Assert.Equal(0, _source.CheckCalls);
        Assert.Empty(_events);
    }

    [Fact]
    public async Task Exact_SameFolder_Unchanged()
    {
        _source.Folders.Add(@"C:\A");
        await _navigator.NavigateToFolderAsync(@"C:\A");
        _events.Clear();

        PathBarResult result = await _navigator.NavigateToFolderAsync(@"C:\A");

        Assert.Equal(PathBarResult.Unchanged, result);
        Assert.Empty(_events);
    }

    [Fact]
    public async Task Exact_SupersededByTree()
    {
        _source.Folders.Add(@"D:\Row");
        using var gate = new ManualResetEventSlim(false);
        _source.Gate = gate;

        Task<PathBarResult> open = _navigator.NavigateToFolderAsync(@"D:\Row");
        _navigator.SelectFromTree(@"D:\B");
        gate.Set();
        PathBarResult result = await open;

        Assert.Equal(PathBarResult.Superseded, result);
        Assert.Equal(new[] { @"D:\B" }, _events);
        Assert.Equal(@"D:\B", _navigator.CurrentFolder);
    }

    [Fact]
    public async Task Exact_TooLong_NeverThrows()
    {
        string path = @"C:\A\" + new string('x', 300);

        PathBarResult result = await _navigator.NavigateToFolderAsync(path);

#if NETFRAMEWORK
        Assert.Equal(PathBarResult.Invalid, result);
        Assert.Equal(0, _source.CheckCalls);
#else
        Assert.Equal(PathBarResult.NotFound, result);
#endif
        Assert.Empty(_events);
    }

    [Fact]
    public async Task Exact_NotFound_RaisesNothing()
    {
        PathBarResult result = await _navigator.NavigateToFolderAsync(@"C:\A\Gone");

        Assert.Equal(PathBarResult.NotFound, result);
        Assert.Empty(_events);
    }

    [Fact]
    public async Task Exact_File_NotAFolder()
    {
        _source.Files.Add(@"C:\A\f.txt");

        PathBarResult result = await _navigator.NavigateToFolderAsync(@"C:\A\f.txt");

        Assert.Equal(PathBarResult.NotAFolder, result);
        Assert.Empty(_events);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Exact_EmptyOrNull_Empty(string? path)
    {
        PathBarResult result = await _navigator.NavigateToFolderAsync(path!);

        Assert.Equal(PathBarResult.Empty, result);
        Assert.Equal(0, _source.CheckCalls);
    }
}
