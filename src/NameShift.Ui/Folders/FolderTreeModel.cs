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
