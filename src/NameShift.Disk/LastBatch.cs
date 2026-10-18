using System;
using System.Collections.Generic;
using System.Linq;

namespace NameShift.Disk;

/// <summary>What the undo journal holds about the last batch, as read at one moment.</summary>
public sealed class LastBatch
{
    internal LastBatch(
        LastBatchState state,
        DateTimeOffset? time = null,
        IReadOnlyList<string>? folders = null,
        IReadOnlyList<CompletedRename>? renames = null)
    {
        if (!Enum.IsDefined(typeof(LastBatchState), state))
        {
            throw new ArgumentOutOfRangeException(nameof(state));
        }

        var folderList = (folders ?? Array.Empty<string>()).ToArray();
        var renameList = (renames ?? Array.Empty<CompletedRename>()).ToArray();
        var revertable = state == LastBatchState.Revertable;
        if ((revertable || state == LastBatchState.Reverted) != (time != null))
        {
            throw new ArgumentException("A time is required exactly for a revertable or reverted batch.", nameof(time));
        }

        if (revertable != (renameList.Length > 0) || (!revertable && folderList.Length > 0))
        {
            throw new ArgumentException("Only a revertable batch has folders and renames, and it has renames.", nameof(renames));
        }

        State = state;
        Time = time;
        Folders = Array.AsReadOnly(folderList);
        Renames = Array.AsReadOnly(renameList);
    }

    /// <summary>The journal's state.</summary>
    public LastBatchState State { get; }

    /// <summary>When the batch finished (UTC); set for Revertable and Reverted, else null.</summary>
    public DateTimeOffset? Time { get; }

    /// <summary>The distinct folders the batch renamed in (parents of the old paths); empty unless Revertable.</summary>
    public IReadOnlyList<string> Folders { get; }

    /// <summary>The renames Revert would undo; empty unless Revertable.</summary>
    public IReadOnlyList<CompletedRename> Renames { get; }

    /// <summary>True when Revert can run.</summary>
    public bool CanRevert => State == LastBatchState.Revertable;
}
