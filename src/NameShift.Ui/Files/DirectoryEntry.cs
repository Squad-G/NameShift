using System;
using NameShift.Core;

namespace NameShift.Ui.Files;

/// <summary>One entry of a folder as the disk reports it. A reparse point (a junction, a symbolic link) is a property of
/// the entry, not of the item: the walk never enters a folder that is one.</summary>
internal sealed class DirectoryEntry
{
    public DirectoryEntry(string name, ItemKind kind, bool isReparsePoint, ItemAttributes attributes, long size, DateTime created, DateTime modified)
    {
        Name = name;
        Kind = kind;
        IsReparsePoint = isReparsePoint;
        Attributes = attributes;
        Size = size;
        Created = created;
        Modified = modified;
    }

    /// <summary>The full name, without folder.</summary>
    public string Name { get; }

    public ItemKind Kind { get; }

    /// <summary>True for a junction, a symbolic link or any other reparse point.</summary>
    public bool IsReparsePoint { get; }

    public ItemAttributes Attributes { get; }

    /// <summary>The size in bytes; 0 for a folder.</summary>
    public long Size { get; }

    public DateTime Created { get; }

    public DateTime Modified { get; }
}
