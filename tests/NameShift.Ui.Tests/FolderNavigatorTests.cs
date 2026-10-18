using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NameShift.Ui.Tests;

public sealed class FolderNavigatorTests
{
    private readonly FakeFolderSource _source = new FakeFolderSource { StartFolder = @"C:\Start" };
    private readonly List<string> _events = new List<string>();
    private readonly FolderNavigator _navigator;

    public FolderNavigatorTests()
    {
        _navigator = new FolderNavigator(_source);
        _navigator.FolderChanged += (_, e) => _events.Add(e.Path);
    }

    [Fact]
    public void Start_GoesToTheStartFolder_AndRaises()
    {
        _source.StartFolder = @"D:\Docs";

        _navigator.Start();

        Assert.Equal(@"D:\Docs", _navigator.CurrentFolder);
        Assert.Equal(new[] { @"D:\Docs" }, _events);
    }

    [Fact]
    public void SelectFromTree_ChangesFolder_AndRaisesOnceWithThePath()
    {
        _navigator.Start();
        _events.Clear();

        _navigator.SelectFromTree(@"D:\B");

        Assert.Equal(@"D:\B", _navigator.CurrentFolder);
        Assert.Equal(new[] { @"D:\B" }, _events);
    }

    [Fact]
    public void SelectFromTree_SameFolderDifferentCase_DoesNotRaise()
    {
        _navigator.SelectFromTree(@"C:\Data");
        _events.Clear();

        _navigator.SelectFromTree(@"c:\data\");

        Assert.Empty(_events);
    }

    [Fact]
    public async Task TypedPath_ExistingFolder_NavigatesAndRaises()
    {
        _source.Folders.Add(@"D:\Pics");

        PathBarResult result = await _navigator.NavigateToTypedPathAsync(@"D:\Pics");

        Assert.Equal(PathBarResult.Navigated, result);
        Assert.Equal(new[] { @"D:\Pics" }, _events);
    }

    [Fact]
    public async Task TypedPath_Missing_KeepsPreviousFolder_NoEvent()
    {
        _navigator.Start();
        _events.Clear();

        PathBarResult result = await _navigator.NavigateToTypedPathAsync(@"C:\nope");

        Assert.Equal(PathBarResult.NotFound, result);
        Assert.Equal(@"C:\Start", _navigator.CurrentFolder);
        Assert.Empty(_events);
    }

    [Fact]
    public async Task TypedPath_File_IsNotAFolder_NoEvent()
    {
        _source.Files.Add(@"C:\a.txt");

        PathBarResult result = await _navigator.NavigateToTypedPathAsync(@"C:\a.txt");

        Assert.Equal(PathBarResult.NotAFolder, result);
        Assert.Empty(_events);
    }

    [Fact]
    public async Task TypedPath_Invalid_DoesNotTouchTheDisk()
    {
        PathBarResult result = await _navigator.NavigateToTypedPathAsync("C:foo");

        Assert.Equal(PathBarResult.Invalid, result);
        Assert.Equal(0, _source.CheckCalls);
    }

    [Fact]
    public async Task TypedPath_Blank_IsEmpty_DoesNotTouchTheDisk()
    {
        PathBarResult result = await _navigator.NavigateToTypedPathAsync("  ");

        Assert.Equal(PathBarResult.Empty, result);
        Assert.Equal(0, _source.CheckCalls);
    }

    [Fact]
    public async Task TypedPath_CurrentFolder_IsUnchanged_NoEvent()
    {
        _source.Folders.Add(@"C:\Start");
        _navigator.Start();
        _events.Clear();

        PathBarResult result = await _navigator.NavigateToTypedPathAsync(@"c:\start\");

        Assert.Equal(PathBarResult.Unchanged, result);
        Assert.Empty(_events);
    }

    [Fact]
    public async Task TypedPath_NotSupersededByTreeEchoOfTheCurrentFolder()
    {
        _source.Folders.Add(@"D:\Typed");
        _navigator.Start();
        _events.Clear();
        using var gate = new ManualResetEventSlim(false);
        _source.Gate = gate;

        Task<PathBarResult> typed = _navigator.NavigateToTypedPathAsync(@"D:\Typed");
        _navigator.SelectFromTree(@"c:\start");
        gate.Set();
        PathBarResult result = await typed;

        Assert.Equal(PathBarResult.Navigated, result);
        Assert.Equal(new[] { @"D:\Typed" }, _events);
    }

    [Fact]
    public async Task TypedPath_SupersededByTreeSelection_DoesNotRaiseForTheTypedPath()
    {
        _source.Folders.Add(@"D:\Typed");
        using var gate = new ManualResetEventSlim(false);
        _source.Gate = gate;

        Task<PathBarResult> typed = _navigator.NavigateToTypedPathAsync(@"D:\Typed");
        _navigator.SelectFromTree(@"D:\B");
        gate.Set();
        PathBarResult result = await typed;

        Assert.Equal(PathBarResult.Superseded, result);
        Assert.Equal(new[] { @"D:\B" }, _events);
        Assert.Equal(@"D:\B", _navigator.CurrentFolder);
    }
}
