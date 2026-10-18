using System;
using System.Collections.Generic;
using NameShift.Core;

namespace NameShift.Ui.Files;

/// <summary>The rows of the file list that go into the pipeline, in list order. Immutable.</summary>
public sealed class FileBatch
{
    /// <summary>No rows: the batch of a list that is not loaded.</summary>
    public static readonly FileBatch Empty = WholeList(Array.Empty<RenameItem>());

    // Null for the whole list (row i is index i); otherwise one entry per list row, -1 when the row is not in the batch.
    private readonly int[]? _indexOfRow;

    private FileBatch(IReadOnlyList<RenameItem> listItems, IReadOnlyList<RenameItem> items, IReadOnlyList<int> rows, int[]? indexOfRow)
    {
        ListItems = listItems;
        Items = items;
        Rows = rows;
        _indexOfRow = indexOfRow;
    }

    /// <summary>The file list's items this batch was taken from (the reference is kept).</summary>
    public IReadOnlyList<RenameItem> ListItems { get; }

    /// <summary>What the pipeline runs on: Items[i] is ListItems[Rows[i]].</summary>
    public IReadOnlyList<RenameItem> Items { get; }

    /// <summary>True when every listed row is in the batch (no selection, or every row selected).</summary>
    public bool IsWholeList => _indexOfRow == null;

    /// <summary>The number of items in the batch.</summary>
    public int Count => Items.Count;

    /// <summary>The list row of each item, ascending.</summary>
    public IReadOnlyList<int> Rows { get; }

    /// <summary>The batch of every listed row; Items is the same reference as listItems.</summary>
    public static FileBatch WholeList(IReadOnlyList<RenameItem> listItems)
    {
        if (listItems == null)
        {
            throw new ArgumentNullException(nameof(listItems));
        }

        var rows = new int[listItems.Count];
        for (int i = 0; i < rows.Length; i++)
        {
            rows[i] = i;
        }

        return new FileBatch(listItems, listItems, rows, null);
    }

    /// <summary>Built by FileListModel: indexOfRow has one entry per list row (-1 when not selected), rows are the selected rows ascending.</summary>
    internal static FileBatch Of(IReadOnlyList<RenameItem> listItems, int[] indexOfRow, int[] rows)
    {
        var items = new RenameItem[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            items[i] = listItems[rows[i]];
        }

        return new FileBatch(listItems, items, rows, indexOfRow);
    }

    /// <summary>The pipeline index of a list row; false when the row is out of range or not in the batch. O(1).</summary>
    public bool TryGetIndex(int row, out int index)
    {
        index = row;
        if (row < 0 || row >= ListItems.Count)
        {
            index = -1;
            return false;
        }

        if (_indexOfRow == null)
        {
            return true;
        }

        index = _indexOfRow[row];
        return index >= 0;
    }
}
