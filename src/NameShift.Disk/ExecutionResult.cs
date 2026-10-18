using System;
using System.Collections.Generic;
using System.Linq;

namespace NameShift.Disk;

/// <summary>The outcome of a batch: the done renames on success, else the failure and any stranded items.</summary>
public sealed class ExecutionResult
{
    internal ExecutionResult(
        ExecutionOutcome outcome,
        IReadOnlyList<CompletedRename>? renamed = null,
        ExecutionFailure? failure = null,
        IReadOnlyList<StrandedItem>? needsAttention = null)
    {
        if (!Enum.IsDefined(typeof(ExecutionOutcome), outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        var renamedList = (renamed ?? Array.Empty<CompletedRename>()).ToArray();
        var strandedList = (needsAttention ?? Array.Empty<StrandedItem>()).ToArray();
        var succeeded = outcome == ExecutionOutcome.Succeeded;
        if (succeeded == (failure != null))
        {
            throw new ArgumentException("A failure is required exactly when the batch did not succeed.", nameof(failure));
        }

        if (!succeeded && renamedList.Length > 0)
        {
            throw new ArgumentException("Only a successful batch has renames.", nameof(renamed));
        }

        if ((outcome == ExecutionOutcome.NeedsAttention) != (strandedList.Length > 0))
        {
            throw new ArgumentException(
                "Stranded items are required exactly when the outcome is NeedsAttention.", nameof(needsAttention));
        }

        Outcome = outcome;
        Renamed = Array.AsReadOnly(renamedList);
        Failure = failure;
        NeedsAttention = Array.AsReadOnly(strandedList);
    }

    /// <summary>How the batch ended.</summary>
    public ExecutionOutcome Outcome { get; }

    /// <summary>On success, every rename in the order each item reached its final name; else empty.</summary>
    public IReadOnlyList<CompletedRename> Renamed { get; }

    /// <summary>Why the batch did not succeed; null exactly when it succeeded.</summary>
    public ExecutionFailure? Failure { get; }

    /// <summary>The items a failed rollback left away from their original name; non-empty exactly when the outcome is
    /// NeedsAttention.</summary>
    public IReadOnlyList<StrandedItem> NeedsAttention { get; }
}
