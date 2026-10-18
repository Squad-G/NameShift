using System;
using System.IO;
using NameShift.Core;

namespace NameShift.Disk;

/// <summary>One rename the batch did, with full paths before and after the whole batch.</summary>
public sealed class CompletedRename
{
    internal CompletedRename(string oldPath, string newPath, ItemKind kind)
    {
        OldPath = oldPath ?? throw new ArgumentNullException(nameof(oldPath));
        NewPath = newPath ?? throw new ArgumentNullException(nameof(newPath));
        Kind = kind;
    }

    /// <summary>The full path before the batch.</summary>
    public string OldPath { get; }

    /// <summary>The full path after the whole batch (renamed parent folders applied).</summary>
    public string NewPath { get; }

    /// <summary>Whether the item is a file or a folder.</summary>
    public ItemKind Kind { get; }

    /// <summary>The rename that undoes this one, in the post-batch state: folder = parent of NewPath, old name = file
    /// name of NewPath, new name = file name of OldPath.</summary>
    public PlannedRename Inverse() =>
        new(Path.GetDirectoryName(NewPath)!, Path.GetFileName(NewPath), Path.GetFileName(OldPath), Kind);
}
