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
    private readonly Dictionary<string, Exception> _throwing = new Dictionary<string, Exception>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ManualResetEventSlim> _gates = new Dictionary<string, ManualResetEventSlim>(StringComparer.OrdinalIgnoreCase);
    private int _calls;

    /// <summary>How many times ListEntries was called.</summary>
    public int Calls => Volatile.Read(ref _calls);

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

    public void SetThrows(string path, Exception exception) => _throwing[path] = exception;

    /// <summary>Makes listings of the folder wait until the returned gate is set.</summary>
    public ManualResetEventSlim Gate(string path)
    {
        var gate = new ManualResetEventSlim(false);
        _gates[path] = gate;
        return gate;
    }

    public FileListing ListEntries(string folderPath)
    {
        Interlocked.Increment(ref _calls);
        if (_gates.TryGetValue(folderPath, out ManualResetEventSlim? gate) && !gate.Wait(GateTimeout))
        {
            throw new TimeoutException("The listing gate of " + folderPath + " was never released.");
        }

        if (_throwing.TryGetValue(folderPath, out Exception? exception))
        {
            throw exception;
        }

        return _listings.TryGetValue(folderPath, out FileListing? listing)
            ? listing
            : FileListing.Ok(Array.Empty<RenameItem>());
    }
}
