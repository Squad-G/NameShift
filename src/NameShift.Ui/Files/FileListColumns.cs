using System;
using System.Collections.Generic;

namespace NameShift.Ui.Files;

/// <summary>
/// The file list's columns in shown order, with and without the Folder column (NS-78): every index the list uses
/// (the row's cells, the sort click, the sort arrow) comes from here, so nothing shifts by one when Folder appears.
/// </summary>
public sealed class FileListColumns
{
    private static readonly FileListColumns WithoutFolder = new FileListColumns(false);
    private static readonly FileListColumns WithFolder = new FileListColumns(true);

    private readonly ShownColumn[] _order;

    private FileListColumns(bool showFolder)
    {
        ShowFolder = showFolder;
        _order = showFolder
            ? new[] { ShownColumn.Name, ShownColumn.Folder, ShownColumn.NewName, ShownColumn.Status, ShownColumn.Size, ShownColumn.Modified }
            : new[] { ShownColumn.Name, ShownColumn.NewName, ShownColumn.Status, ShownColumn.Size, ShownColumn.Modified };
        Order = Array.AsReadOnly(_order);
    }

    /// <summary>Whether the Folder column is shown.</summary>
    public bool ShowFolder { get; }

    /// <summary>The shown columns, left to right: Name, [Folder], New name, Status, Size, Modified.</summary>
    public IReadOnlyList<ShownColumn> Order { get; }

    /// <summary>The map for this flag; one shared instance per flag.</summary>
    public static FileListColumns For(bool showFolder) => showFolder ? WithFolder : WithoutFolder;

    /// <summary>The column's index in the shown list; -1 for a column that is not shown (Folder when hidden).</summary>
    public int IndexOf(ShownColumn column) => Array.IndexOf(_order, column);

    /// <summary>The column at this index.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside <see cref="Order"/>.</exception>
    public ShownColumn ColumnAt(int index)
    {
        if (index < 0 || index >= _order.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _order[index];
    }

    /// <summary>The sort a click on this header asks for; null for Folder, New name and Status (not sortable).</summary>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside <see cref="Order"/>.</exception>
    public FileListColumn? SortColumnAt(int index) => ColumnAt(index) switch
    {
        ShownColumn.Name => FileListColumn.Name,
        ShownColumn.Size => FileListColumn.Size,
        ShownColumn.Modified => FileListColumn.Modified,
        _ => null,
    };

    /// <summary>The index of the header that carries the arrow of this sort.</summary>
    public int SortArrowIndex(FileListColumn sort) => sort switch
    {
        FileListColumn.Name => IndexOf(ShownColumn.Name),
        FileListColumn.Size => IndexOf(ShownColumn.Size),
        FileListColumn.Modified => IndexOf(ShownColumn.Modified),
        _ => -1,
    };
}
