using System;
using System.Collections.Generic;
using System.Linq;

namespace NameShift.Disk;

/// <summary>The outcome of a Revert: what was reverted, what was not and why, and any journal problem.</summary>
public sealed class RevertResult
{
    internal RevertResult(
        RevertOutcome outcome,
        IReadOnlyList<CompletedRename>? reverted = null,
        IReadOnlyList<SkippedRevert>? notReverted = null,
        ExecutionFailure? failure = null,
        IReadOnlyList<StrandedItem>? needsAttention = null,
        JournalWriteFailure? journalIssue = null)
    {
        if (!Enum.IsDefined(typeof(RevertOutcome), outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        var revertedList = (reverted ?? Array.Empty<CompletedRename>()).ToArray();
        var skippedList = (notReverted ?? Array.Empty<SkippedRevert>()).ToArray();
        var strandedList = (needsAttention ?? Array.Empty<StrandedItem>()).ToArray();
        var attention = outcome == RevertOutcome.NeedsAttention;
        if ((outcome == RevertOutcome.Reverted || outcome == RevertOutcome.PartlyReverted) != (revertedList.Length > 0))
        {
            throw new ArgumentException("Reverted renames are required exactly when something was reverted.", nameof(reverted));
        }

        var mustSkip = outcome == RevertOutcome.PartlyReverted || outcome == RevertOutcome.NothingReverted;
        if ((mustSkip && skippedList.Length == 0) || (!mustSkip && !attention && skippedList.Length > 0))
        {
            throw new ArgumentException("Skipped entries do not match the outcome.", nameof(notReverted));
        }

        if (attention != (failure != null) || attention != (strandedList.Length > 0))
        {
            throw new ArgumentException("A failure and stranded items are required exactly for NeedsAttention.", nameof(failure));
        }

        if ((outcome == RevertOutcome.NotStarted) != (journalIssue?.Phase == JournalWritePhase.BeforeStart)
            || (outcome == RevertOutcome.NothingToRevert && journalIssue != null))
        {
            throw new ArgumentException("The journal issue does not match the outcome.", nameof(journalIssue));
        }

        Outcome = outcome;
        Reverted = Array.AsReadOnly(revertedList);
        NotReverted = Array.AsReadOnly(skippedList);
        Failure = failure;
        NeedsAttention = Array.AsReadOnly(strandedList);
        JournalIssue = journalIssue;
    }

    /// <summary>How the revert ended.</summary>
    public RevertOutcome Outcome { get; }

    /// <summary>The renames done (OldPath = where the item was before the revert, NewPath = where it is now); empty
    /// unless Reverted or PartlyReverted.</summary>
    public IReadOnlyList<CompletedRename> Reverted { get; }

    /// <summary>The entries not reverted, with why; non-empty for PartlyReverted and NothingReverted, possibly for
    /// NeedsAttention, else empty.</summary>
    public IReadOnlyList<SkippedRevert> NotReverted { get; }

    /// <summary>The failed step; NeedsAttention only.</summary>
    public ExecutionFailure? Failure { get; }

    /// <summary>The items the failed revert left away from where they were; NeedsAttention only.</summary>
    public IReadOnlyList<StrandedItem> NeedsAttention { get; }

    /// <summary>Null when the journal was written as planned. BeforeStart (NotStarted): nothing was moved.
    /// AfterFinish: the moves are done, but Revert is disabled until a new batch.</summary>
    public JournalWriteFailure? JournalIssue { get; }
}
