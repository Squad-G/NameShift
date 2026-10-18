using System;

namespace NameShift.Disk;

/// <summary>A journal entry Revert did not revert; it stays in the journal, so a later Revert can try again.</summary>
public sealed class SkippedRevert
{
    internal SkippedRevert(
        CompletedRename entry,
        RevertSkipReason reason,
        string? blockingPath = null,
        string? systemMessage = null,
        int? hResult = null)
    {
        if (!Enum.IsDefined(typeof(RevertSkipReason), reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        var blocked = reason == RevertSkipReason.NameTaken || reason == RevertSkipReason.WaitsForSkippedItem;
        if (blocked != (blockingPath != null))
        {
            throw new ArgumentException("A blocking path is required exactly when a name is held.", nameof(blockingPath));
        }

        if ((systemMessage == null) != (hResult == null) || (reason == RevertSkipReason.RenameFailed && systemMessage == null))
        {
            throw new ArgumentException("An exception gives both a message and an HResult.", nameof(hResult));
        }

        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        Reason = reason;
        BlockingPath = blockingPath;
        SystemMessage = systemMessage;
        HResult = hResult;
    }

    /// <summary>The journal entry, as it was before this revert.</summary>
    public CompletedRename Entry { get; }

    /// <summary>Why it was not reverted.</summary>
    public RevertSkipReason Reason { get; }

    /// <summary>The full path of the entry holding the old name; NameTaken and WaitsForSkippedItem only.</summary>
    public string? BlockingPath { get; }

    /// <summary>The exception's message when an exception occurred, else null.</summary>
    public string? SystemMessage { get; }

    /// <summary>The exception's HResult when an exception occurred, else null.</summary>
    public int? HResult { get; }
}
