using System;
using System.IO;
using NameShift.Core;

namespace NameShift.Disk;

/// <summary>One rename the batch did, with full paths before and after the whole batch.</summary>
public sealed class CompletedRename
{
    internal CompletedRename(string oldPath, string newPath, ItemKind kind, FileFingerprint? fingerprint = null)
    {
        OldPath = oldPath ?? throw new ArgumentNullException(nameof(oldPath));
        NewPath = newPath ?? throw new ArgumentNullException(nameof(newPath));
        if (fingerprint != null && kind != ItemKind.File)
        {
            throw new ArgumentException("Only a file has a fingerprint.", nameof(fingerprint));
        }

        Kind = kind;
        Fingerprint = fingerprint;
    }

    /// <summary>The full path before the batch.</summary>
    public string OldPath { get; }

    /// <summary>The full path after the whole batch (renamed parent folders applied).</summary>
    public string NewPath { get; }

    /// <summary>Whether the item is a file or a folder.</summary>
    public ItemKind Kind { get; }

    /// <summary>The file's size and last write time right after the batch; null for a folder, for a file that could
    /// not be read then, and for a journal written before fingerprints (never checked).</summary>
    public FileFingerprint? Fingerprint { get; }

    /// <summary>The rename that undoes this one, in the post-batch state: folder = parent of NewPath, old name = file
    /// name of NewPath, new name = file name of OldPath.</summary>
    public PlannedRename Inverse() =>
        new(Path.GetDirectoryName(NewPath)!, Path.GetFileName(NewPath), Path.GetFileName(OldPath), Kind);

    /// <summary>The same entry at another new path (a reverted parent folder), with its fingerprint.</summary>
    internal CompletedRename WithNewPath(string newPath) => new(OldPath, newPath, Kind, Fingerprint);
}
