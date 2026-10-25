using System;
using System.Collections.Generic;
using System.IO;
using NameShift.Core;
using NameShift.Ui.Files;

namespace NameShift.Cmd;

/// <summary>The rule of <c>--recurse</c> without <c>--include-hidden</c>: an item whose folder is, or is under, a
/// hidden or system folder of the walk is left out. Text only; the disk is never read.</summary>
internal static class HiddenFolders
{
    /// <summary>The items of <paramref name="kept"/> that are not inside a hidden or system folder found in
    /// <paramref name="listed"/> (the whole listing, which carries the folder items and their attributes), in the same
    /// order. Folders are compared by whole path segments, ignoring case. Items of the root are never left out, and
    /// the root itself is never tested (it is not an item of the listing). With no hidden folder, <paramref name="kept"/>
    /// itself is returned.</summary>
    public static IReadOnlyList<RenameItem> LeaveOut(string root, IReadOnlyList<RenameItem> listed, IReadOnlyList<RenameItem> kept)
    {
        if (root == null)
        {
            throw new ArgumentNullException(nameof(root));
        }

        if (listed == null)
        {
            throw new ArgumentNullException(nameof(listed));
        }

        if (kept == null)
        {
            throw new ArgumentNullException(nameof(kept));
        }

        var hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (RenameItem item in listed)
        {
            if (item.Kind == ItemKind.Folder && (item.Attributes & (ItemAttributes.Hidden | ItemAttributes.System)) != 0)
            {
                hidden.Add(Join(FileListText.RelativeFolder(root, item), item.Name));
            }
        }

        if (hidden.Count == 0)
        {
            return kept;
        }

        var result = new List<RenameItem>(kept.Count);
        foreach (RenameItem item in kept)
        {
            if (!InsideHidden(FileListText.RelativeFolder(root, item), hidden))
            {
                result.Add(item);
            }
        }

        return result;
    }

    /// <summary>True when the folder, or a prefix of it made of whole segments, is in the set.</summary>
    private static bool InsideHidden(string relativeFolder, HashSet<string> hidden)
    {
        int end = 0;
        while (end < relativeFolder.Length)
        {
            int next = relativeFolder.IndexOf(Path.DirectorySeparatorChar, end);
            end = next < 0 ? relativeFolder.Length : next;
            if (hidden.Contains(relativeFolder.Substring(0, end)))
            {
                return true;
            }

            end++;
        }

        return false;
    }

    private static string Join(string folder, string name) =>
        folder.Length == 0 ? name : folder + Path.DirectorySeparatorChar + name;
}
