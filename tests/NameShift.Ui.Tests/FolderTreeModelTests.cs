using System;
using System.Collections.Generic;
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

    // NS-56: refresh

    /// <summary>Reveals the path and expands it, so it and every level above are Loaded.</summary>
    private static async Task<FolderNode> Held(FolderTreeModel model, string path)
    {
        FolderNode node = (await model.RevealAsync(path))!;
        await model.ExpandAsync(node);
        return node;
    }

    private async Task<(FolderTreeModel Model, FolderNode Photos)> PhotosAsync()
    {
        _source.SetChildren(@"C:\", "Photos");
        _source.SetChildren(@"C:\Photos", "Old", "Keep");
        var model = new FolderTreeModel(_source);
        FolderNode photos = await Held(model, @"C:\Photos");
        return (model, photos);
    }

    [Fact]
    public async Task RefreshAsync_RenamedChild_ListsNewNames_InSourceOrder()
    {
        (FolderTreeModel model, FolderNode photos) = await PhotosAsync();
        _source.SetChildren(@"C:\Photos", "Keep", "New");

        await model.RefreshAsync(@"C:\Photos");

        Assert.Equal(new[] { "Keep", "New" }, photos.Children.Select(c => c.Name));
        Assert.DoesNotContain(photos.Children, c => c.Name == "Old");
    }

    [Fact]
    public async Task RefreshAsync_SurvivingChild_IsTheSameObject_NewChildNotLoaded()
    {
        (FolderTreeModel model, FolderNode photos) = await PhotosAsync();
        _source.SetChildren(@"C:\Photos\Keep", "Inner");
        FolderNode oldKeep = photos.Children.Single(c => c.Name == "Keep");
        await model.ExpandAsync(oldKeep);
        _source.SetChildren(@"C:\Photos", "Keep", "New");

        await model.RefreshAsync(@"C:\Photos");

        FolderNode newKeep = photos.Children.Single(c => c.Name == "Keep");
        Assert.Same(oldKeep, newKeep);
        Assert.Equal(FolderNodeState.Loaded, newKeep.State);
        Assert.Equal(new[] { "Inner" }, newKeep.Children.Select(c => c.Name));
        Assert.Equal(FolderNodeState.NotLoaded, photos.Children.Single(c => c.Name == "New").State);
    }

    [Fact]
    public async Task RefreshAsync_RenamedLoadedChild_IsFreshNotLoaded()
    {
        (FolderTreeModel model, FolderNode photos) = await PhotosAsync();
        _source.SetChildren(@"C:\Photos\Old", "Deep");
        await model.ExpandAsync(photos.Children.Single(c => c.Name == "Old"));
        _source.SetChildren(@"C:\Photos", "Keep", "New");

        await model.RefreshAsync(@"C:\Photos");

        FolderNode renamed = photos.Children.Single(c => c.Name == "New");
        Assert.Equal(FolderNodeState.NotLoaded, renamed.State);
        Assert.Empty(renamed.Children);
        Assert.Null(model.FindHeld(@"C:\Photos\Old\Deep"));
    }

    [Fact]
    public async Task RefreshAsync_CaseOnlyRename_ReplacesNode_WithDiskSpelling()
    {
        _source.SetChildren(@"C:\", "Photos");
        var model = new FolderTreeModel(_source);
        FolderNode root = await Held(model, @"C:\");
        FolderNode before = root.Children[0];
        _source.SetChildren(@"C:\", "photos");

        await model.RefreshAsync(@"C:\");

        FolderNode after = root.Children[0];
        Assert.NotSame(before, after);
        Assert.Equal("photos", after.Name);
        Assert.Equal(@"C:\photos", after.FullPath);
    }

    [Fact]
    public async Task RefreshAsync_NotLoadedNode_ListsNothing_ReturnsNull()
    {
        _source.SetChildren(@"C:\", "A");
        var model = new FolderTreeModel(_source);
        FolderNode root = await Held(model, @"C:\");
        int calls = _source.ListCalls;

        string? result = await model.RefreshAsync(@"C:\A");

        Assert.Null(result);
        Assert.Equal(calls, _source.ListCalls);
        Assert.Equal(FolderNodeState.NotLoaded, root.Children[0].State);
    }

    [Theory]
    [InlineData(@"\\server\share")]
    [InlineData(@"C:\A\B")]
    [InlineData(@"C:\Missing")]
    public async Task RefreshAsync_PathNotHeld_ChangesNothing_ReturnsNull(string path)
    {
        _source.SetChildren(@"C:\", "A");
        var model = new FolderTreeModel(_source);
        await Held(model, @"C:\");
        int calls = _source.ListCalls;

        Assert.Null(await model.RefreshAsync(path));
        Assert.Equal(calls, _source.ListCalls);
    }

    [Fact]
    public async Task RefreshAsync_ListsFine_ReturnsOwnPath()
    {
        (FolderTreeModel model, _) = await PhotosAsync();

        Assert.Equal(@"C:\Photos", await model.RefreshAsync(@"C:\Photos"));
    }

    [Fact]
    public async Task RefreshAsync_RootWithProblem_MarkedFailed_ReturnsNull()
    {
        _source.SetChildren(@"C:\", "A");
        var model = new FolderTreeModel(_source);
        FolderNode root = await Held(model, @"C:\");
        _source.SetListing(@"C:\", FolderListing.Failed(FolderProblem.NotReady));

        string? result = await model.RefreshAsync(@"C:\");

        Assert.Null(result);
        Assert.Equal(FolderNodeState.Failed, root.State);
        Assert.Equal(FolderProblem.NotReady, root.Problem);
        Assert.Empty(root.Children);
    }

    [Fact]
    public async Task RefreshAsync_FolderGone_RemovedFromParent_ReturnsParent()
    {
        (FolderTreeModel model, _) = await PhotosAsync();
        _source.SetListing(@"C:\Photos", FolderListing.Failed(FolderProblem.NotFound));
        _source.SetChildren(@"C:\", "Other");

        string? result = await model.RefreshAsync(@"C:\Photos");

        Assert.Equal(@"C:\", result);
        Assert.DoesNotContain(model.Roots[1].Children, c => c.Name == "Photos");
    }

    [Fact]
    public async Task RefreshAsync_GoneTwoLevels_ClimbsToNearestExisting()
    {
        _source.SetChildren(@"C:\", "A");
        _source.SetChildren(@"C:\A", "B");
        var model = new FolderTreeModel(_source);
        await Held(model, @"C:\A\B");
        _source.SetListing(@"C:\A", FolderListing.Failed(FolderProblem.NotFound));
        _source.SetListing(@"C:\A\B", FolderListing.Failed(FolderProblem.NotFound));
        _source.SetChildren(@"C:\", "Z");

        string? result = await model.RefreshAsync(@"C:\A\B");

        Assert.Equal(@"C:\", result);
        Assert.DoesNotContain(model.Roots[1].Children, c => c.Name == "A");
    }

    [Fact]
    public async Task RefreshAsync_AccessDeniedButStillListed_KeptFailed_ReturnsOwnPath()
    {
        (FolderTreeModel model, FolderNode photos) = await PhotosAsync();
        _source.SetListing(@"C:\Photos", FolderListing.Failed(FolderProblem.AccessDenied));

        string? result = await model.RefreshAsync(@"C:\Photos");

        Assert.Equal(@"C:\Photos", result);
        Assert.Same(photos, model.Roots[1].Children.Single());
        Assert.Equal(FolderNodeState.Failed, photos.State);
        Assert.Equal(FolderProblem.AccessDenied, photos.Problem);
    }

    [Fact]
    public async Task RefreshAsync_FailedNodeNowLists_BecomesLoaded_ProblemCleared()
    {
        _source.SetChildren(@"C:\", "Photos");
        _source.SetListing(@"C:\Photos", FolderListing.Failed(FolderProblem.AccessDenied), FolderListing.Ok(new[] { new FolderEntry("A", @"C:\Photos\A") }));
        var model = new FolderTreeModel(_source);
        FolderNode photos = await Held(model, @"C:\Photos");
        Assert.Equal(FolderNodeState.Failed, photos.State);

        string? result = await model.RefreshAsync(@"C:\Photos");

        Assert.Equal(@"C:\Photos", result);
        Assert.Equal(FolderNodeState.Loaded, photos.State);
        Assert.Equal(new[] { "A" }, photos.Children.Select(c => c.Name));
        Assert.Null(photos.Problem);
    }

    [Fact]
    public async Task RefreshAsync_NoSubFolderLeft_MayHaveChildrenFalse()
    {
        _source.SetChildren(@"C:\", "A");
        var model = new FolderTreeModel(_source);
        FolderNode root = await Held(model, @"C:\");
        Assert.True(root.MayHaveChildren);
        _source.SetChildren(@"C:\");

        await model.RefreshAsync(@"C:\");

        Assert.False(root.MayHaveChildren);
    }

    [Fact]
    public async Task RefreshAsync_SubFolderAppeared_MayHaveChildrenTrue()
    {
        _source.SetChildren(@"C:\");
        var model = new FolderTreeModel(_source);
        FolderNode root = await Held(model, @"C:\");
        Assert.False(root.MayHaveChildren);
        _source.SetChildren(@"C:\", "A");

        await model.RefreshAsync(@"C:\");

        Assert.True(root.MayHaveChildren);
    }

    [Fact]
    public async Task RefreshAsync_WhileRefreshing_ReturnsSameTask()
    {
        (FolderTreeModel model, _) = await PhotosAsync();
        int calls = _source.ListCalls;
        using var gate = new ManualResetEventSlim(false);
        _source.Gate = gate;

        Task<string?> first = model.RefreshAsync(@"C:\Photos");
        Task<string?> second = model.RefreshAsync(@"C:\Photos");
        gate.Set();
        await first;

        Assert.Same(first, second);
        Assert.Equal(calls + 1, _source.ListCalls);
    }

    [Fact]
    public async Task RefreshAsync_WhileExpanding_WaitsThenLists()
    {
        _source.SetChildren(@"C:\", "A");
        var model = new FolderTreeModel(_source);
        using var gate = new ManualResetEventSlim(false);
        _source.Gate = gate;
        Task expand = model.ExpandAsync(model.Roots[1]);
        Assert.Equal(FolderNodeState.Loading, model.Roots[1].State);

        Task<string?> refresh = model.RefreshAsync(@"C:\");
        await Task.Delay(100);

        // The expand's listing is held at the gate: the refresh must not have listed yet (it waits for the expand).
        Assert.Equal(1, _source.ListCalls);
        Assert.False(refresh.IsCompleted);
        gate.Set();
        await expand;
        await refresh;

        Assert.Equal(FolderNodeState.Loaded, model.Roots[1].State);
        Assert.Equal(2, _source.ListCalls);
    }

    [Fact]
    public async Task RefreshAsync_SourceThrows_NeverEscapes_NodeFailedUnavailable()
    {
        (FolderTreeModel model, FolderNode photos) = await PhotosAsync();
        _source.SetThrows(@"C:\Photos", new InvalidOperationException("boom"));

        string? result = await model.RefreshAsync(@"C:\Photos");

        Assert.Equal(@"C:\Photos", result);
        Assert.Equal(FolderNodeState.Failed, photos.State);
        Assert.Equal(FolderProblem.Unavailable, photos.Problem);
    }

    [Fact]
    public async Task RefreshAsync_DuringRefresh_NodeStaysLoaded()
    {
        (FolderTreeModel model, FolderNode photos) = await PhotosAsync();
        using var gate = new ManualResetEventSlim(false);
        _source.Gate = gate;

        Task<string?> refresh = model.RefreshAsync(@"C:\Photos");

        Assert.Equal(FolderNodeState.Loaded, photos.State);
        Assert.Equal(2, photos.Children.Count);
        gate.Set();
        await refresh;
    }

    [Fact]
    public async Task RefreshAllAsync_TwoFolders_RefreshesBoth_ReturnsBothPaths()
    {
        _source.SetChildren(@"C:\", "A", "B");
        var model = new FolderTreeModel(_source);
        await Held(model, @"C:\A");
        await Held(model, @"C:\B");
        int calls = _source.ListCalls;

        IReadOnlyList<string> result = await model.RefreshAllAsync(new[] { @"C:\A", @"C:\B" });

        Assert.Equal(new[] { @"C:\A", @"C:\B" }, result);
        Assert.Equal(calls + 2, _source.ListCalls);
    }

    [Fact]
    public async Task RefreshAllAsync_OneNotHeld_OtherStillRefreshed()
    {
        _source.SetChildren(@"C:\", "A", "B");
        var model = new FolderTreeModel(_source);
        await Held(model, @"C:\B");
        _source.SetChildren(@"C:\B", "New");

        IReadOnlyList<string> result = await model.RefreshAllAsync(new[] { @"C:\Missing", @"C:\B" });

        Assert.Equal(new[] { @"C:\B" }, result);
        Assert.Equal(new[] { "New" }, model.FindHeld(@"C:\B")!.Children.Select(c => c.Name));
    }

    [Fact]
    public async Task RefreshAllAsync_OneSourceThrows_OtherStillRefreshed()
    {
        _source.SetChildren(@"C:\", "A", "B");
        var model = new FolderTreeModel(_source);
        await Held(model, @"C:\A");
        await Held(model, @"C:\B");
        _source.SetThrows(@"C:\A", new InvalidOperationException("boom"));
        _source.SetChildren(@"C:\B", "New");

        IReadOnlyList<string> result = await model.RefreshAllAsync(new[] { @"C:\A", @"C:\B" });

        Assert.Equal(FolderNodeState.Failed, model.Roots[1].Children.Single(c => c.Name == "A").State);
        Assert.Contains(@"C:\B", result);
        Assert.Equal(new[] { "New" }, model.FindHeld(@"C:\B")!.Children.Select(c => c.Name));
    }

    [Fact]
    public async Task RefreshAllAsync_DuplicatePaths_ReturnedOnce()
    {
        _source.SetChildren(@"C:\", "A");
        var model = new FolderTreeModel(_source);
        await Held(model, @"C:\A");

        IReadOnlyList<string> result = await model.RefreshAllAsync(new[] { @"C:\A", @"c:\a" });

        Assert.Single(result);
    }

    [Fact]
    public async Task FindReplacementFor_ChildRemoved_ReturnsLoadedAncestor()
    {
        (FolderTreeModel model, _) = await PhotosAsync();
        _source.SetChildren(@"C:\Photos", "Keep", "New");
        await model.RefreshAsync(@"C:\Photos");

        Assert.Equal(@"C:\Photos", model.FindReplacementFor(@"C:\Photos\Old\X"));
    }

    [Fact]
    public async Task FindFallbackAsync_HiddenFolderThatExists_ReturnsNull()
    {
        _source.SetChildren(@"C:\", "Users");
        _source.SetChildren(@"C:\Users", "Public");
        var model = new FolderTreeModel(_source);
        await Held(model, @"C:\Users");
        _source.Folders.Add(@"C:\Users\AppData");
        Assert.Equal(@"C:\Users", model.FindReplacementFor(@"C:\Users\AppData"));

        Assert.Null(await model.FindFallbackAsync(@"C:\Users\AppData"));
        Assert.Equal(1, _source.CheckCalls);
    }

    [Fact]
    public async Task FindFallbackAsync_FolderCreatedAfterParentWasListed_ReturnsNull()
    {
        (FolderTreeModel model, _) = await PhotosAsync();
        _source.Folders.Add(@"C:\Photos\Fresh");

        Assert.Null(await model.FindFallbackAsync(@"C:\Photos\Fresh"));
    }

    [Fact]
    public async Task FindFallbackAsync_FolderReallyGone_ReturnsLoadedAncestor()
    {
        (FolderTreeModel model, _) = await PhotosAsync();

        Assert.Equal(@"C:\Photos", await model.FindFallbackAsync(@"C:\Photos\Missing"));
    }

    [Fact]
    public async Task FindFallbackAsync_NoSignItIsGone_DoesNotTouchTheDisk()
    {
        (FolderTreeModel model, _) = await PhotosAsync();

        Assert.Null(await model.FindFallbackAsync(@"C:\Photos\Keep"));
        Assert.Equal(0, _source.CheckCalls);
    }

    [Fact]
    public async Task FindReplacementFor_StillHeldOrUnknown_ReturnsNull()
    {
        (FolderTreeModel model, _) = await PhotosAsync();

        Assert.Null(model.FindReplacementFor(@"C:\Photos\Keep"));
        Assert.Null(model.FindReplacementFor(@"C:\Photos\Keep\Below"));
        Assert.Null(model.FindReplacementFor(@"\\server\share"));
    }
}
