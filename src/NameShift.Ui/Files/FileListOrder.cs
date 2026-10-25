using System;
using System.Collections.Generic;
using System.Linq;
using NameShift.Core;

namespace NameShift.Ui.Files;

/// <summary>The list's order: folders first, then files, each ordered by a column (natural name order by default,
/// Explorer's). Ties are always in natural name order, ascending. Items of several folders (sub-folders listed) stay
/// grouped by folder, in the order the folders first appear.</summary>
public static class FileListOrder
{
    /// <summary>Folders first, then files, each in natural name order, ascending (<see cref="FileListSort.Default"/>).</summary>
    /// <exception cref="ArgumentNullException">When items is null.</exception>
    public static IReadOnlyList<RenameItem> Sort(IEnumerable<RenameItem> items) => Sort(items, FileListSort.Default);

    /// <summary>A new list of the same items: folders first in every order, then the column's order, then natural name
    /// order ascending for ties. The sort is stable. With items of several folders, the items are first grouped by
    /// <see cref="RenameItem.FolderPath"/> (ignoring case) in the order each folder first appears in the input (the
    /// source's tree order), always ascending, and each group is ordered as one folder: a descending column reverses
    /// the rows of a group, never the groups.</summary>
    /// <exception cref="ArgumentNullException">When items or sort is null.</exception>
    public static IReadOnlyList<RenameItem> Sort(IEnumerable<RenameItem> items, FileListSort sort)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        if (sort == null)
        {
            throw new ArgumentNullException(nameof(sort));
        }

        var list = items as IReadOnlyCollection<RenameItem> ?? items.ToList();
        var folderOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (RenameItem item in list)
        {
            if (!folderOrder.ContainsKey(item.FolderPath))
            {
                folderOrder.Add(item.FolderPath, folderOrder.Count);
            }
        }

        IOrderedEnumerable<RenameItem> grouped = folderOrder.Count > 1
            ? list.OrderBy(item => folderOrder[item.FolderPath]).ThenBy(item => item.Kind == ItemKind.Folder ? 0 : 1)
            : list.OrderBy(item => item.Kind == ItemKind.Folder ? 0 : 1);
        switch (sort.Column)
        {
            case FileListColumn.Size:
                return Tied(sort.Descending ? grouped.ThenByDescending(item => item.Size) : grouped.ThenBy(item => item.Size));
            case FileListColumn.Modified:
                return Tied(sort.Descending ? grouped.ThenByDescending(item => item.Modified) : grouped.ThenBy(item => item.Modified));
            default:
                return sort.Descending
                    ? grouped
                        .ThenByDescending(item => item.Name, NaturalNameComparer.Instance)
                        .ThenByDescending(item => item.Name, StringComparer.Ordinal)
                        .ToList()
                    : Tied(grouped);
        }
    }

    private static IReadOnlyList<RenameItem> Tied(IOrderedEnumerable<RenameItem> ordered) =>
        ordered
            .ThenBy(item => item.Name, NaturalNameComparer.Instance)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .ToList();
}
