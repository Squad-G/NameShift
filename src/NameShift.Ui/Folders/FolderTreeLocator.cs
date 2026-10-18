using System;
using System.Collections.Generic;

namespace NameShift.Ui.Folders;

/// <summary>Finds the tree root that holds a path: the deepest one wins.</summary>
public static class FolderTreeLocator
{
    /// <summary>Null when no root contains the path (a network path, for example).</summary>
    public static FolderTreeLocation? Locate(IReadOnlyList<FolderRoot> roots, string path)
    {
        int best = -1;
        int bestLength = -1;
        for (int i = 0; i < roots.Count; i++)
        {
            string root = roots[i].FullPath.TrimEnd('\\');
            if (root.Length > bestLength && Contains(root, path))
            {
                best = i;
                bestLength = root.Length;
            }
        }

        if (best < 0)
        {
            return null;
        }

        string rest = path.Substring(bestLength).Trim('\\');
        string[] segments = rest.Length == 0 ? Array.Empty<string>() : rest.Split('\\');
        return new FolderTreeLocation(best, segments);
    }

    private static bool Contains(string trimmedRoot, string path)
    {
        if (!path.StartsWith(trimmedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return path.Length == trimmedRoot.Length || path[trimmedRoot.Length] == '\\';
    }
}
