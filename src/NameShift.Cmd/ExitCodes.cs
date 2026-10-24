using System;
using NameShift.Disk;

namespace NameShift.Cmd;

/// <summary>The exit code of a batch the renamer ran.</summary>
internal static class ExitCodes
{
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
