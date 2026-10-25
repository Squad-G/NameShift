using System;
using System.IO;
using NameShift.Core;

namespace NameShift.Ui.Files;

/// <summary>Where a double-click or Enter on a list row goes.</summary>
public static class FolderRowTarget
{
    /// <summary>The row's own full path when it is a folder (also one of a sub-level), else null. Used as it is:
    /// no trimming, no expansion.</summary>
    public static string? Of(RenameItem item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        return item.Kind == ItemKind.Folder ? Path.Combine(item.FolderPath, item.Name) : null;
    }
}
