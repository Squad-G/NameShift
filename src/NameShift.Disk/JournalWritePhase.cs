namespace NameShift.Disk;

/// <summary>When writing the undo journal failed.</summary>
public enum JournalWritePhase
{
    /// <summary>Before anything moved: the batch or revert was not started.</summary>
    BeforeStart,

    /// <summary>After the moves: they are done, but the journal could not record it, so Revert is disabled.</summary>
    AfterFinish,
}
