using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NameShift.Core;
using NameShift.Ui.Folders;

namespace NameShift.Ui.Files;

/// <summary>
/// The file list's data for the current folder. Loads off the calling thread; a newer load supersedes an older one.
/// All state is touched on the calling (UI) thread; only the disk call and the sort run on the pool.
/// </summary>
public sealed class FileListModel
{
    private readonly IFileListSource _source;
    private int _version;

    public FileListModel(IFileListSource source)
    {
        _source = source;
    }

    public string? FolderPath { get; private set; }

    public FileListState State { get; private set; }

    /// <summary>The rows in display order; row i is pipeline index i.</summary>
    public IReadOnlyList<RenameItem> Items { get; private set; } = Array.Empty<RenameItem>();

    /// <summary>Set only when the state is Failed.</summary>
    public FolderProblem? Problem { get; private set; }

    /// <summary>Loading, the problem text, "This folder is empty", or null when rows are shown.</summary>
    public string? Message
    {
        get
        {
            switch (State)
            {
                case FileListState.Loading:
                    return Strings.Loading;
                case FileListState.Failed:
                    return FolderProblemText.For(Problem!.Value);
                case FileListState.Loaded when Items.Count == 0:
                    return Strings.FolderEmpty;
                default:
                    return null;
            }
        }
    }

    /// <summary>Raised on the calling thread when the state or the items change (load started, load applied).</summary>
    public event EventHandler? Changed;

    /// <summary>Lists the folder; false when a newer load superseded this one. Never throws.</summary>
    public async Task<bool> LoadAsync(string folderPath)
    {
        int version = ++_version;
        FolderPath = folderPath;
        State = FileListState.Loading;
        Items = Array.Empty<RenameItem>();
        Problem = null;
        Changed?.Invoke(this, EventArgs.Empty);

        FileListing listing;
        try
        {
            listing = await Task.Run(() =>
            {
                FileListing found = _source.ListEntries(folderPath);
                return found.Problem is null ? FileListing.Ok(FileListOrder.Sort(found.Items)) : found;
            });
        }
        catch (Exception)
        {
            // A source must not throw, but a failed listing must never take the window down.
            listing = FileListing.Failed(FolderProblem.Unavailable);
        }

        // The version check and the state change stay together: nothing awaits between them.
        if (version != _version)
        {
            return false;
        }

        Items = listing.Items;
        Problem = listing.Problem;
        State = listing.Problem is null ? FileListState.Loaded : FileListState.Failed;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }
}