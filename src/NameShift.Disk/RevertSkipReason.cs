namespace NameShift.Disk;

/// <summary>Why an entry was not reverted.</summary>
public enum RevertSkipReason
{
    /// <summary>The item is no longer where the batch left it (removed, moved, or replaced by another kind).</summary>
    ItemMissing,

    /// <summary>Something else now has the item's old name; it is never overwritten.</summary>
    NameTaken,

    /// <summary>The item's old name is held by another entry that could not be reverted.</summary>
    WaitsForSkippedItem,

    /// <summary>The rename call itself failed (in use, access denied, ...).</summary>
    RenameFailed,
}
