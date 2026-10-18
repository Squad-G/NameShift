using System;
using System.Collections.Generic;

namespace NameShift.Disk;

/// <summary>A validated last-batch.json.</summary>
internal sealed class JournalRecord
{
    public JournalRecord(
        string id, bool isReverted, DateTimeOffset time, IReadOnlyList<string> folders, IReadOnlyList<CompletedRename> renames)
    {
        Id = id;
        IsReverted = isReverted;
        Time = time;
        Folders = folders;
        Renames = renames;
    }

    /// <summary>The id of the operation that wrote the file.</summary>
    public string Id { get; }

    /// <summary>True for state "reverted" (no paths), false for "done".</summary>
    public bool IsReverted { get; }

    /// <summary>When the batch finished.</summary>
    public DateTimeOffset Time { get; }

    /// <summary>The folders, as stored.</summary>
    public IReadOnlyList<string> Folders { get; }

    /// <summary>The revertable entries.</summary>
    public IReadOnlyList<CompletedRename> Renames { get; }
}
