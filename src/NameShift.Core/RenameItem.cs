using System;

namespace NameShift.Core;

/// <summary>The read-only facts of one file or folder, built by the caller.</summary>
public sealed class RenameItem
{
    /// <summary>Creates an item; <paramref name="name"/> is the full name without folder.</summary>
    public RenameItem(
        string name,
        string folderPath,
        ItemKind kind = ItemKind.File,
        ItemAttributes attributes = ItemAttributes.None,
        long size = 0,
        DateTime created = default,
        DateTime modified = default,
        DateTime? taken = null)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("The name is required.", nameof(name));
        }

        Name = name;
        FolderPath = folderPath ?? throw new ArgumentNullException(nameof(folderPath));
        Kind = kind;
        Attributes = attributes;
        Size = size;
        Created = created;
        Modified = modified;
        Taken = taken;
    }

    /// <summary>The original full name, e.g. "a.tar.gz" (no folder).</summary>
    public string Name { get; }

    /// <summary>The containing folder.</summary>
    public string FolderPath { get; }

    /// <summary>Whether the item is a file or a folder.</summary>
    public ItemKind Kind { get; }

    /// <summary>The item's attributes.</summary>
    public ItemAttributes Attributes { get; }

    /// <summary>The size in bytes; 0 for folders.</summary>
    public long Size { get; }

    /// <summary>The creation time, as read by the app (local time).</summary>
    public DateTime Created { get; }

    /// <summary>The last modification time, as read by the app (local time).</summary>
    public DateTime Modified { get; }

    /// <summary>The date the photo was taken, as <c>PhotoDate.Read</c> returns it (no conversion); null when unknown.
    /// Set by the caller; the engine never reads files.</summary>
    public DateTime? Taken { get; }

    /// <summary>A new item equal to this one in every other fact, with this photo date; this item is unchanged.</summary>
    public RenameItem WithTaken(DateTime? taken) =>
        new RenameItem(Name, FolderPath, Kind, Attributes, Size, Created, Modified, taken);
}
