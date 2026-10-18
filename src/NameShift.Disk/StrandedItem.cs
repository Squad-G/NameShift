using System;
using NameShift.Core;

namespace NameShift.Disk;

/// <summary>An item a failed rollback left away from its original name; the user must look at it.</summary>
public sealed class StrandedItem
{
    internal StrandedItem(string currentPath, string originalPath, ItemKind kind, string? systemMessage)
    {
        CurrentPath = currentPath ?? throw new ArgumentNullException(nameof(currentPath));
        OriginalPath = originalPath ?? throw new ArgumentNullException(nameof(originalPath));
        Kind = kind;
        SystemMessage = systemMessage;
    }

    /// <summary>Where the item is now (may be a ~ns- temporary name; renamed parent folders applied).</summary>
    public string CurrentPath { get; }

    /// <summary>Where the item was before the batch.</summary>
    public string OriginalPath { get; }

    /// <summary>Whether the item is a file or a folder.</summary>
    public ItemKind Kind { get; }

    /// <summary>Why its undo failed: the exception's message, or null when the undo was skipped because it could not
    /// be done safely (its old name was taken, or the item was not where the undo expected it).</summary>
    public string? SystemMessage { get; }
}
