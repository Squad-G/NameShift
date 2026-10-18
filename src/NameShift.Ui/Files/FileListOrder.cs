using System.Collections.Generic;
using System.Linq;
using NameShift.Core;

namespace NameShift.Ui.Files;

/// <summary>The list's order: folders first, then files, each in natural name order (Explorer's).</summary>
public static class FileListOrder
{
    public static IReadOnlyList<RenameItem> Sort(IEnumerable<RenameItem> items) =>
        items
            .OrderBy(item => item.Kind == ItemKind.Folder ? 0 : 1)
            .ThenBy(item => item.Name, NaturalNameComparer.Instance)
            .ThenBy(item => item.Name, System.StringComparer.Ordinal)
            .ToList();
}