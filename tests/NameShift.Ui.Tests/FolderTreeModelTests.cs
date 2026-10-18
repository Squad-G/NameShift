using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NameShift.Ui.Folders;
using Xunit;

namespace NameShift.Ui.Tests;

public sealed class FolderTreeModelTests
{
    private readonly FakeFolderSource _source = new FakeFolderSource();

    public FolderTreeModelTests()
    {
        _source.Roots.Add(new FolderRoot("Documents", @"C:\Users\D\Documents", FolderRootKind.SpecialFolder));
        _source.Roots.Add(new FolderRoot("C:", @"C:\", FolderRootKind.Drive));
    }

    [Fact]
    public void Roots_FollowTheSourceOrder_AndStartNotLoaded()
    {
        var model = new FolderTreeModel(_source);

        Assert.Equal(new[] { "Documents", "C:" }, model.Roots.Select(r => r.Name));
        Assert.All(model.Roots, r =>
        {
            Assert.Equal(FolderNodeState.NotLoaded, r.State);
            Assert.True(r.MayHaveChildren);
            Assert.Null(r.Parent);
            Assert.NotNull(r.RootKind);
        });
        Assert.Equal(0, _source.ListCalls);
    }

    [Fact]
    public async Task ExpandAsync_ListsChildren_AsNotLoadedNodesWithTheirParent()
    {
        _source.SetChildren(@"C:\", "A", "B");
        var model = new FolderTreeModel(_source);

        await model.ExpandAsync(model.Roots[1]);

        FolderNode root = model.Roots[1];
        Assert.Equal(FolderNodeState.Loaded, root.State);
        Assert.Equal(new[] { "A", "B" }, root.Children.Select(c => c.Name));
        Assert.All(root.Children, c =>
        {
            Assert.Same(root, c.Parent);
            Assert.Null(c.RootKind);
            Assert.Equal(FolderNodeState.NotLoaded, c.State);
        });
    }

    [Fact]
    public async Task RevealAsync_LoadsEachLevel_AndReturnsTheTarget()
    {
        _source.SetChildren(@"C:\", "A");
        _source.SetChildren(@"C:\A", "B");
        var model = new FolderTreeModel(_source);

        FolderNode? node = await model.RevealAsync(@"C:\A\B");

        Assert.NotNull(node);
        Assert.Equal(@"C:\A\B", node!.FullPath);
        Assert.Equal(FolderNodeState.Loaded, model.Roots[1].State);
        Assert.Equal(FolderNodeState.Loaded, model.Roots[1].Children[0].State);
        Assert.Equal(FolderNodeState.NotLoaded, node.State);
    }

    [Fact]
    public async Task RevealAsync_SegmentNotListed_ReturnsNull()
    {
        _source.SetChildren(@"C:\", "Visible");
        var model = new FolderTreeModel(_source);

        Assert.Null(await model.RevealAsync(@"C:\Hidden\X"));
    }

    [Fact]
    public async Task RevealAsync_PathOutsideEveryRoot_ReturnsNull()
    {
        var model = new FolderTreeModel(_source);

        Assert.Null(await model.RevealAsync(@"\\server\share"));
    }

    [Fact]
    public async Task RevealAsync_Root_ReturnsTheRootWithoutListing()
    {
        var model = new FolderTreeModel(_source);

        FolderNode? node = await model.RevealAsync(@"C:\");

        Assert.Same(model.Roots[1], node);
        Assert.Equal(0, _source.ListCalls);
    }

    [Fact]
    public async Task ExpandAsync_Problem_MarksNodeFailed_WithNoChildren()
    {
        _source.SetListing(@"C:\", FolderListing.Failed(FolderProblem.NotReady));
        var model = new FolderTreeModel(_source);

        await model.ExpandAsync(model.Roots[1]);

        Assert.Equal(FolderNodeState.Failed, model.Roots[1].State);
        Assert.Equal(FolderProblem.NotReady, model.Roots[1].Problem);
        Assert.Empty(model.Roots[1].Children);
    }

    [Fact]
    public async Task ExpandAsync_SourceThrows_IsUnavailable_NotAnException()
    {
        _source.SetThrows(@"C:\", new InvalidOperationException("boom"));
        var model = new FolderTreeModel(_source);

        await model.ExpandAsync(model.Roots[1]);

        Assert.Equal(FolderNodeState.Failed, model.Roots[1].State);
        Assert.Equal(FolderProblem.Unavailable, model.Roots[1].Problem);
    }

    [Fact]
    public async Task ExpandAsync_AfterFailure_RetriesOnNextExpand()
    {
        _source.SetListing(
            @"C:\",
            FolderListing.Failed(FolderProblem.NotReady),
            FolderListing.Ok(new[] { new FolderEntry("A", @"C:\A") }));
        var model = new FolderTreeModel(_source);

        await model.ExpandAsync(model.Roots[1]);
        await model.ExpandAsync(model.Roots[1]);

        Assert.Equal(FolderNodeState.Loaded, model.Roots[1].State);
        Assert.Null(model.Roots[1].Problem);
        Assert.Single(model.Roots[1].Children);
        Assert.Equal(2, _source.ListCalls);
    }

    [Fact]
    public async Task ExpandAsync_WhileLoading_ListsOnlyOnce()
    {
        _source.SetChildren(@"C:\", "A");
        using var gate = new ManualResetEventSlim(false);
        _source.Gate = gate;
        var model = new FolderTreeModel(_source);

        Task first = model.ExpandAsync(model.Roots[1]);
        Task second = model.ExpandAsync(model.Roots[1]);
        Assert.Same(first, second);
        Assert.Equal(FolderNodeState.Loading, model.Roots[1].State);
        gate.Set();
        await first;

        Assert.Equal(1, _source.ListCalls);
        Assert.Equal(FolderNodeState.Loaded, model.Roots[1].State);
    }

    [Fact]
    public async Task ExpandAsync_EmptyFolder_HasNoChildrenGlyph()
    {
        _source.SetChildren(@"C:\");
        var model = new FolderTreeModel(_source);

        await model.ExpandAsync(model.Roots[1]);

        Assert.False(model.Roots[1].MayHaveChildren);
    }

    [Fact]
    public async Task ExpandAsync_Loaded_DoesNotListAgain()
    {
        _source.SetChildren(@"C:\", "A");
        var model = new FolderTreeModel(_source);

        await model.ExpandAsync(model.Roots[1]);
        await model.ExpandAsync(model.Roots[1]);

        Assert.Equal(1, _source.ListCalls);
    }
}
