using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>The rows in display order. What the pipeline runs on is <see cref="Batch"/>: row i is pipeline index i only when the batch is the whole list.</summary>
    public IReadOnlyList<RenameItem> Items { get; private set; } = Array.Empty<RenameItem>();

    /// <summary>Every entry name of the folder, hidden and system included, for the duplicate check (NS-6:3). Not the
    /// visible list. A snapshot of the folder at load time: a file created later is not seen until the folder reloads;
    /// NS-8 re-checks every target on disk before renaming and never replaces, so this is safe.</summary>
    public IReadOnlyList<string> ExistingNames { get; private set; } = Array.Empty<string>();

    /// <summary>The rows that go into the pipeline: the selected rows in list order, or the whole list with no selection.
    /// Rebuilt on every selection change and every load (a load resets the selection to none).</summary>
    public FileBatch Batch { get; private set; } = FileBatch.Empty;

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

    /// <summary>Raised on the calling thread when the state or the items change (load started, load applied). A load
    /// also resets the batch, and raises this event only, not <see cref="SelectionChanged"/>.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised on the calling thread when <see cref="Batch"/> changes through <see cref="SetSelection"/>.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Selects the rows (any order, duplicates allowed). No rows, or every row, is the whole list.
    /// Raises <see cref="SelectionChanged"/> only when the batch changes.</summary>
    /// <exception cref="ArgumentNullException">When rows is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">When a row is outside the listed items.</exception>
    public void SetSelection(IReadOnlyCollection<int> rows)
    {
        if (rows == null)
        {
            throw new ArgumentNullException(nameof(rows));
        }

        IReadOnlyList<RenameItem> listItems = Items;
        var selected = new bool[listItems.Count];
        int count = 0;
        foreach (int row in rows)
        {
            if (row < 0 || row >= selected.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(rows), row, "A selected row is not in the list.");
            }

            if (!selected[row])
            {
                selected[row] = true;
                count++;
            }
        }

        FileBatch batch = count == 0 || count == selected.Length ? FileBatch.WholeList(listItems) : BatchOf(listItems, selected, count);
        if (SameRows(Batch, batch))
        {
            return;
        }

        Batch = batch;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private static FileBatch BatchOf(IReadOnlyList<RenameItem> listItems, bool[] selected, int count)
    {
        var indexOfRow = new int[selected.Length];
        var rows = new int[count];
        int next = 0;
        for (int row = 0; row < selected.Length; row++)
        {
            if (selected[row])
            {
                indexOfRow[row] = next;
                rows[next++] = row;
            }
            else
            {
                indexOfRow[row] = -1;
            }
        }

        return FileBatch.Of(listItems, indexOfRow, rows);
    }

    private static bool SameRows(FileBatch current, FileBatch next)
    {
        if (current.IsWholeList || next.IsWholeList)
        {
            return current.IsWholeList && next.IsWholeList;
        }

        return current.Rows.SequenceEqual(next.Rows);
    }
    /// <summary>Lists the folder; false when a newer load superseded this one. Never throws.</summary>
    public async Task<bool> LoadAsync(string folderPath)
    {
        int version = ++_version;
        FolderPath = folderPath;
        State = FileListState.Loading;
        Items = Array.Empty<RenameItem>();
        Batch = FileBatch.Empty;
        ExistingNames = Array.Empty<string>();
        Problem = null;
        Changed?.Invoke(this, EventArgs.Empty);

        FileListing listing;
        try
        {
            listing = await Task.Run(() =>
            {
                FileListing found = _source.ListEntries(folderPath);
                return found.Problem is null ? FileListing.Ok(FileListOrder.Sort(found.Items), found.ExistingNames) : found;
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
        Batch = FileBatch.WholeList(Items);
        ExistingNames = listing.ExistingNames;
        Problem = listing.Problem;
        State = listing.Problem is null ? FileListState.Loaded : FileListState.Failed;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }
}