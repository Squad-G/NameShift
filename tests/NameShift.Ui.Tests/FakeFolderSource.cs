using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NameShift.Ui.Folders;

namespace NameShift.Ui.Tests;

/// <summary>An in-memory folder source: no disk, call counters, and an optional gate that holds calls until released.</summary>
internal sealed class FakeFolderSource : IFolderSource
{
    private readonly Dictionary<string, Queue<FolderListing>> _listings = new Dictionary<string, Queue<FolderListing>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Exception> _throwing = new Dictionary<string, Exception>(StringComparer.OrdinalIgnoreCase);
    private int _listCalls;
    private int _checkCalls;

    public List<FolderRoot> Roots { get; } = new List<FolderRoot>();

    public HashSet<string> Folders { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Files { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public string StartFolder { get; set; } = @"C:\";

    /// <summary>When set, ListChildren and CheckFolder wait for it before answering.</summary>
    public ManualResetEventSlim? Gate { get; set; }

    public int ListCalls => _listCalls;

    public int CheckCalls => _checkCalls;

    /// <summary>Queues the answers for a folder: each call takes the next one, the last one repeats.</summary>
    public void SetListing(string path, params FolderListing[] answers) =>
        _listings[path] = new Queue<FolderListing>(answers);

    /// <summary>Lists the given child names under a folder.</summary>
    public void SetChildren(string path, params string[] names) =>
        SetListing(
            path,
            FolderListing.Ok(names.Select(n => new FolderEntry(n, path.TrimEnd('\\') + "\\" + n)).ToList()));

    public void SetThrows(string path, Exception exception) => _throwing[path] = exception;

    public IReadOnlyList<FolderRoot> GetRoots() => Roots;

    public FolderListing ListChildren(string folderPath)
    {
        Interlocked.Increment(ref _listCalls);
        Gate?.Wait();
        if (_throwing.TryGetValue(folderPath, out Exception? exception))
        {
            throw exception;
        }

        if (!_listings.TryGetValue(folderPath, out Queue<FolderListing>? queue))
        {
            return FolderListing.Ok(Array.Empty<FolderEntry>());
        }

        return queue.Count > 1 ? queue.Dequeue() : queue.Peek();
    }

    public FolderCheck CheckFolder(string path)
    {
        Interlocked.Increment(ref _checkCalls);
        Gate?.Wait();
        if (Folders.Contains(path))
        {
            return FolderCheck.Folder;
        }

        return Files.Contains(path) ? FolderCheck.File : FolderCheck.NotFound;
    }
}
