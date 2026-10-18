using System;

namespace NameShift.Disk;

/// <summary>Writing the undo journal failed, with raw data for the UI to format.</summary>
public sealed class JournalWriteFailure
{
    internal JournalWriteFailure(JournalWritePhase phase, Exception exception)
    {
        if (!Enum.IsDefined(typeof(JournalWritePhase), phase))
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }

        if (exception == null)
        {
            throw new ArgumentNullException(nameof(exception));
        }

        Phase = phase;
        SystemMessage = exception.Message;
        HResult = exception.HResult;
    }

    /// <summary>Whether the failure stopped the operation before it started, or came after its moves.</summary>
    public JournalWritePhase Phase { get; }

    /// <summary>The exception's message.</summary>
    public string SystemMessage { get; }

    /// <summary>The exception's HResult.</summary>
    public int HResult { get; }
}
