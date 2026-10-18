namespace NameShift.Disk;

/// <summary>What the undo journal holds about the last batch.</summary>
public enum LastBatchState
{
    /// <summary>No batch was journaled yet.</summary>
    None,

    /// <summary>The last batch (or what a partial revert left of it) can be reverted.</summary>
    Revertable,

    /// <summary>The last batch was fully reverted; Revert stays disabled until a new batch.</summary>
    Reverted,

    /// <summary>A batch or a revert was cut short, or its journal could not be written; Revert is disabled.</summary>
    Interrupted,

    /// <summary>The journal is corrupt or cannot be read; it is kept untouched and Revert is disabled.</summary>
    Unreadable,
}
