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
    private readonly Dictionary<string, FileListing> _listings = new Dictionary<string, FileListing>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Exception> _throwing = new Dictionary<string, Exception>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ManualResetEventSlim> _gates = new Dictionary<string, ManualResetEventSlim>(StringComparer.OrdinalIgnoreCase);

    public static RenameItem File(string name, long size = 0, string folder = @"C:\A") =>
        new RenameItem(name, folder, ItemKind.File, ItemAttributes.None, size);

    public static RenameItem Folder(string name, string folder = @"C:\A") =>
        new RenameItem(name, folder, ItemKind.Folder);

    public void SetEntries(string path, params RenameItem[] items) => SetListing(path, FileListing.Ok(items));

    /// <summary>Declares a folder with hidden or system entries: their names are listed but they are not items.</summary>
    public void SetEntries(string path, string[] hiddenNames, params RenameItem[] items) =>
        SetListing(path, FileListing.Ok(items, items.Select(i => i.Name).Concat(hiddenNames).ToArray()));

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
        if (_gates.TryGetValue(folderPath, out ManualResetEventSlim? gate))
        {
            gate.Wait();
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