using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NameShift.Core;
using NameShift.Ui.Files;

namespace NameShift.Ui.Tests;

/// <summary>An in-memory file list source: no disk, and per-folder gates that hold a listing until released.</summary>
internal sealed class FakeFileListSource : IFileListSource
{
    /// <summary>How long a gated listing waits before it gives up (a test that forgets to release fails, never hangs).</summary>
    public static readonly TimeSpan GateTimeout = TimeSpan.FromSeconds(10);

    private readonly Dictionary<string, FileListing> _listings = new Dictionary<string, FileListing>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Path, bool IncludeSubfolders), FileListing> _flagListings =
        new Dictionary<(string Path, bool IncludeSubfolders), FileListing>(new FlagKeyComparer());
    private readonly Dictionary<string, Exception> _throwing = new Dictionary<string, Exception>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ManualResetEventSlim> _gates = new Dictionary<string, ManualResetEventSlim>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _blockUntilCancelled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new object();
    private int _calls;
    private bool _lastIncludeSubfolders;
    private CancellationToken _lastToken;

    /// <summary>How many times ListEntries was called.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <summary>The includeSubfolders of the last ListEntries call.</summary>
    public bool LastIncludeSubfolders
    {
        get
        {
            lock (_lock)
            {
                return _lastIncludeSubfolders;
            }
        }
    }

    /// <summary>The token of the last ListEntries call.</summary>
    public CancellationToken LastToken
    {
        get
        {
            lock (_lock)
            {
                return _lastToken;
            }
        }
    }

    public static RenameItem File(string name, long size = 0, string folder = @"C:\A") =>
        new RenameItem(name, folder, ItemKind.File, ItemAttributes.None, size);

    public static RenameItem Folder(string name, string folder = @"C:\A") =>
        new RenameItem(name, folder, ItemKind.Folder);

    public static RenameItem Hidden(string name, string folder = @"C:\A") =>
        new RenameItem(name, folder, ItemKind.File, ItemAttributes.Hidden);

    public void SetEntries(string path, params RenameItem[] items) => SetListing(path, FileListing.Ok(items));

    /// <summary>Declares a folder with hidden entries: they are listed after the items, as hidden files of the folder,
    /// so the default filter leaves them out but their names still count.</summary>
    public void SetEntries(string path, string[] hiddenNames, params RenameItem[] items) =>
        SetListing(path, FileListing.Ok(items.Concat(hiddenNames.Select(name => Hidden(name, path))).ToArray()));

    public void SetListing(string path, FileListing listing) => _listings[path] = listing;

    /// <summary>Declares the listing for one value of includeSubfolders; a call with the other value (or a path with no
    /// entry for its flag) falls back to the listing set with <see cref="SetListing(string, FileListing)"/>.</summary>
    public void SetListing(string path, bool includeSubfolders, FileListing listing) =>
        _flagListings[(path, includeSubfolders)] = listing;

    public void SetThrows(string path, Exception exception) => _throwing[path] = exception;

    /// <summary>Makes the next listing of the folder (one call only) wait until its token is cancelled (at most
    /// <see cref="GateTimeout"/>), then throw <see cref="OperationCanceledException"/> with that token, as a walk
    /// cancelled midway does.</summary>
    public void BlockUntilCancelled(string path)
    {
        lock (_lock)
        {
            _blockUntilCancelled.Add(path);
        }
    }

    /// <summary>Makes listings of the folder wait until the returned gate is set.</summary>
    public ManualResetEventSlim Gate(string path)
    {
        var gate = new ManualResetEventSlim(false);
        _gates[path] = gate;
        return gate;
    }

    public FileListing ListEntries(string folderPath, bool includeSubfolders, CancellationToken cancellationToken)
    {
        bool block;
        lock (_lock)
        {
            _lastIncludeSubfolders = includeSubfolders;
            _lastToken = cancellationToken;
            block = _blockUntilCancelled.Remove(folderPath);
        }

        Interlocked.Increment(ref _calls);
        if (block)
        {
            if (!cancellationToken.WaitHandle.WaitOne(GateTimeout))
            {
                throw new TimeoutException("The listing of " + folderPath + " was never cancelled.");
            }

            throw new OperationCanceledException(cancellationToken);
        }

        if (_gates.TryGetValue(folderPath, out ManualResetEventSlim? gate) && !gate.Wait(GateTimeout))
        {
            throw new TimeoutException("The listing gate of " + folderPath + " was never released.");
        }

        if (_throwing.TryGetValue(folderPath, out Exception? exception))
        {
            throw exception;
        }

        if (_flagListings.TryGetValue((folderPath, includeSubfolders), out FileListing? flagListing))
        {
            return flagListing;
        }

        return _listings.TryGetValue(folderPath, out FileListing? listing)
            ? listing
            : FileListing.Ok(Array.Empty<RenameItem>());
    }

    private sealed class FlagKeyComparer : IEqualityComparer<(string Path, bool IncludeSubfolders)>
    {
        public bool Equals((string Path, bool IncludeSubfolders) x, (string Path, bool IncludeSubfolders) y) =>
            x.IncludeSubfolders == y.IncludeSubfolders && StringComparer.OrdinalIgnoreCase.Equals(x.Path, y.Path);

        public int GetHashCode((string Path, bool IncludeSubfolders) key) =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(key.Path) ^ (key.IncludeSubfolders ? 1 : 0);
    }
}
