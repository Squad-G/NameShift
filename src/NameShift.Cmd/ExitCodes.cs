using System;
using NameShift.Disk;

namespace NameShift.Cmd;

/// <summary>The exit code of a batch the renamer ran or reverted. The members keep their <c>apply</c> names:
/// <see cref="ExitCode.NotRenamed"/> reads "nothing changed" for <c>undo</c>.</summary>
internal static class ExitCodes
{
    /// <summary>The exit code of a revert: nothing to revert 0; reverted 0 (4 when the history could not be saved
    /// afterwards); partly reverted 4; nothing reverted 3; not started 3; needs attention 4.</summary>
    public static ExitCode For(RevertResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        switch (result.Outcome)
        {
            case RevertOutcome.NothingToRevert:
                return ExitCode.Success;
            case RevertOutcome.Reverted:
                return result.JournalIssue?.Phase == JournalWritePhase.AfterFinish ? ExitCode.NeedsAttention : ExitCode.Success;
            case RevertOutcome.PartlyReverted:
                return ExitCode.NeedsAttention;
            case RevertOutcome.NothingReverted:
            case RevertOutcome.NotStarted:
                return ExitCode.NotRenamed;
            case RevertOutcome.NeedsAttention:
                return ExitCode.NeedsAttention;
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "Unknown revert outcome.");
        }
    }

    /// <summary>The exit code of a rename batch.</summary>
    public static ExitCode For(RenameRunResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        ExecutionResult? execution = result.Execution;
        if (execution == null)
        {
            // The journal could not be written before the batch: it never started.
            return ExitCode.NotRenamed;
        }

        switch (execution.Outcome)
        {
            case ExecutionOutcome.Succeeded:
                return result.JournalIssue?.Phase == JournalWritePhase.AfterFinish ? ExitCode.NeedsAttention : ExitCode.Success;
            case ExecutionOutcome.Refused:
            case ExecutionOutcome.RolledBack:
                return ExitCode.NotRenamed;
            case ExecutionOutcome.NeedsAttention:
                return ExitCode.NeedsAttention;
            default:
                throw new ArgumentOutOfRangeException(nameof(result), execution.Outcome, "Unknown execution outcome.");
        }
    }
}
