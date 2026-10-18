using System;

namespace NameShift.Disk;

/// <summary>The outcome of a journaled batch: the executor's result, and any problem writing the journal.</summary>
public sealed class RenameRunResult
{
    internal RenameRunResult(ExecutionResult? execution, JournalWriteFailure? journalIssue)
    {
        if ((execution == null) != (journalIssue?.Phase == JournalWritePhase.BeforeStart))
        {
            throw new ArgumentException(
                "The batch is not started exactly when the journal could not be written before it.", nameof(execution));
        }

        Execution = execution;
        JournalIssue = journalIssue;
    }

    /// <summary>The executor's result; null only when the batch was not started because its journal could not be
    /// written (see <see cref="JournalIssue"/>).</summary>
    public ExecutionResult? Execution { get; }

    /// <summary>Null when the journal was written as planned. BeforeStart: the batch was not started. AfterFinish:
    /// the batch ran, but Revert is not available for it.</summary>
    public JournalWriteFailure? JournalIssue { get; }
}
