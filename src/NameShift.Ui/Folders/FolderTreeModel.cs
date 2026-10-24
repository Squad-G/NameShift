using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NameShift.Ui.Folders;

/// <summary>
/// The folder tree's data: roots from the source, children listed lazily and off the calling thread.
/// All state is touched on the calling (UI) thread; only the disk call runs on the pool.
/// </summary>
public sealed class FolderTreeModel
{
    private readonly IFolderSource _source;
    private readonly List<FolderRoot> _roots;
    private readonly Dictionary<FolderNode, Task> _loading = new Dictionary<FolderNode, Task>();
    private readonly Dictionary<FolderNode, Task<string?>> _refreshing = new Dictionary<FolderNode, Task<string?>>();

    public FolderTreeModel(IFolderSource source)
    {
        _source = source;
        _roots = source.GetRoots().ToList();
        Roots = _roots.Select(r => new FolderNode(r.DisplayName, r.FullPath, null, r.Kind)).ToList();
    }

    public IReadOnlyList<FolderNode> Roots { get; }

    /// <summary>Lists the node's children once; a node being loaded returns the same task. Never throws.</summary>
    public Task ExpandAsync(FolderNode node)
    {
        if (node.State == FolderNodeState.Loaded)
        {
            return Task.CompletedTask;
        }

        if (node.State == FolderNodeState.Loading)
        {
            return _loading[node];
        }

        node.State = FolderNodeState.Loading;
        node.Problem = null;
        Task task = LoadAsync(node);
        _loading[node] = task;
        return task;
    }

    /// <summary>Expands each level down to <paramref name="path"/>; null when the path is not reachable in the tree.</summary>
    public async Task<FolderNode?> RevealAsync(string path)
    {
        FolderTreeLocation? location = FolderTreeLocator.Locate(_roots, path);
        if (location is null)
        {
            return null;
        }

        FolderNode node = Roots[location.RootIndex];
        foreach (string segment in location.Segments)
        {
            await ExpandAsync(node);
            FolderNode? child = node.Children.FirstOrDefault(c => string.Equals(c.Name, segment, StringComparison.OrdinalIgnoreCase));
            if (child is null)
            {
                return null;
            }

            node = child;
        }

        return node;
    }

    /// <summary>The node at <paramref name="path"/> when every level down to it is Loaded and lists it; no disk access.
    /// A root path returns the root whatever its state. Null otherwise.</summary>
    public FolderNode? FindHeld(string path)
    {
        FolderTreeLocation? location = FolderTreeLocator.Locate(_roots, path);
        if (location is null)
        {
            return null;
        }

        FolderNode node = Roots[location.RootIndex];
        foreach (string segment in location.Segments)
        {
            FolderNode? child = node.State == FolderNodeState.Loaded ? FindChild(node, segment) : null;
            if (child is null)
            {
                return null;
            }

            node = child;
        }

        return node;
    }

    /// <summary>Null when the tree has no sign that <paramref name="path"/> is gone. Otherwise the path of the deepest
    /// held folder above it: a Loaded ancestor no longer lists the next segment. No disk access.</summary>
    public string? FindReplacementFor(string path)
    {
        FolderTreeLocation? location = FolderTreeLocator.Locate(_roots, path);
        if (location is null)
        {
            return null;
        }

        FolderNode node = Roots[location.RootIndex];
        foreach (string segment in location.Segments)
        {
            if (node.State != FolderNodeState.Loaded)
            {
                return null;
            }

            FolderNode? child = FindChild(node, segment);
            if (child is null)
            {
                return node.FullPath;
            }

            node = child;
        }

        return null;
    }

    /// <summary>Re-lists the held node at <paramref name="path"/>. Returns the path of the node that was refreshed and
    /// still exists (its own path, or the nearest refreshed ancestor when it is gone); null when nothing was refreshed.
    /// Never throws.</summary>
    public Task<string?> RefreshAsync(string path)
    {
        FolderNode? node = FindHeld(path);
        if (node is null || node.State == FolderNodeState.NotLoaded)
        {
            return Task.FromResult<string?>(null);
        }

        return RefreshNodeAsync(node);
    }

    /// <summary>Refreshes each path in turn (sequentially, in the given order); returns the non-null results, without
    /// duplicates (OrdinalIgnoreCase), in order. Never throws.</summary>
    public async Task<IReadOnlyList<string>> RefreshAllAsync(IEnumerable<string> paths)
    {
        var result = new List<string>();
        foreach (string path in paths)
        {
            string? refreshed = await RefreshAsync(path);
            if (refreshed != null && !result.Contains(refreshed, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(refreshed);
            }
        }

        return result;
    }

    private static FolderNode? FindChild(FolderNode node, string name) =>
        node.Children.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    private Task<string?> RefreshNodeAsync(FolderNode node)
    {
        if (_refreshing.TryGetValue(node, out Task<string?>? running))
        {
            return running;
        }

        Task<string?> task = RefreshCoreAsync(node);
        if (!task.IsCompleted)
        {
            _refreshing[node] = task;
        }

        return task;
    }

    private async Task<string?> RefreshCoreAsync(FolderNode node)
    {
        try
        {
            if (node.State == FolderNodeState.Loading && _loading.TryGetValue(node, out Task? loading))
            {
                await loading;
            }

            FolderListing listing;
            try
            {
                listing = await Task.Run(() => _source.ListChildren(node.FullPath));
            }
            catch (Exception)
            {
                listing = FolderListing.Failed(FolderProblem.Unavailable);
            }

            // The entry is removed on the calling thread before the task returns, and before any further await.
            _refreshing.Remove(node);
            if (listing.Problem is FolderProblem problem)
            {
                return await ApplyProblemAsync(node, problem);
            }

            node.Children = Reconcile(node, listing.Folders);
            node.Problem = null;
            node.State = FolderNodeState.Loaded;
            node.MayHaveChildren = node.Children.Count > 0;
            return node.FullPath;
        }
        finally
        {
            _refreshing.Remove(node);
        }
    }

    private async Task<string?> ApplyProblemAsync(FolderNode node, FolderProblem problem)
    {
        if (node.Parent is null)
        {
            Fail(node, problem);
            return null;
        }

        string? up = await RefreshNodeAsync(node.Parent);

        // Still listed all the way up: keep it, marked Failed. An ancestor that went too leaves it detached.
        if (IsAttached(node))
        {
            Fail(node, problem);
            return node.FullPath;
        }

        return up;
    }

    private static void Fail(FolderNode node, FolderProblem problem)
    {
        node.Children = Array.Empty<FolderNode>();
        node.Problem = problem;
        node.State = FolderNodeState.Failed;
    }

    private static bool IsAttached(FolderNode node)
    {
        for (FolderNode? n = node; n?.Parent != null; n = n.Parent)
        {
            if (!n.Parent.Children.Any(c => ReferenceEquals(c, n)))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<FolderNode> Reconcile(FolderNode node, IReadOnlyList<FolderEntry> entries)
    {
        var old = new Dictionary<string, FolderNode>(StringComparer.Ordinal);
        foreach (FolderNode child in node.Children)
        {
            if (!old.ContainsKey(child.Name))
            {
                old.Add(child.Name, child);
            }
        }

        var children = new List<FolderNode>(entries.Count);
        foreach (FolderEntry entry in entries)
        {
            if (old.TryGetValue(entry.Name, out FolderNode? existing) && string.Equals(existing.FullPath, entry.FullPath, StringComparison.Ordinal))
            {
                children.Add(existing);
            }
            else
            {
                children.Add(new FolderNode(entry.Name, entry.FullPath, node, null));
            }
        }

        return children;
    }
    private async Task LoadAsync(FolderNode node)
    {
        FolderListing listing;
        try
        {
            listing = await Task.Run(() => _source.ListChildren(node.FullPath));
        }
        catch (Exception)
        {
            // A source must not throw, but a failed listing must never take the window down.
            listing = FolderListing.Failed(FolderProblem.Unavailable);
        }

        _loading.Remove(node);
        if (listing.Problem is FolderProblem problem)
        {
            node.Children = Array.Empty<FolderNode>();
            node.Problem = problem;
            node.State = FolderNodeState.Failed;
            return;
        }

        node.Children = listing.Folders.Select(f => new FolderNode(f.Name, f.FullPath, node, null)).ToList();
        node.MayHaveChildren = node.Children.Count > 0;
        node.State = FolderNodeState.Loaded;
    }
}
