using System;

namespace NameShift.Disk;

/// <summary>Why a batch did not succeed, with raw data for the UI to format.</summary>
public sealed class ExecutionFailure
{
    internal ExecutionFailure(
        FailureReason reason,
        string? itemPath = null,
        string? blockingPath = null,
        string? systemMessage = null,
        int? hResult = null)
    {
        if (!Enum.IsDefined(typeof(FailureReason), reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        var isPlanLevel = reason == FailureReason.HasConflicts || reason == FailureReason.HasRuleErrors;
        if (isPlanLevel != (itemPath == null))
        {
            throw new ArgumentException("An item path is required exactly when the failure is about one item.", nameof(itemPath));
        }

        if ((reason == FailureReason.TargetInTheWay) != (blockingPath != null))
        {
            throw new ArgumentException("A blocking path is required exactly when a target is in the way.", nameof(blockingPath));
        }

        if ((systemMessage == null) != (hResult == null))
        {
            throw new ArgumentException("An exception gives both a message and an HResult.", nameof(hResult));
        }

        Reason = reason;
        ItemPath = itemPath;
        BlockingPath = blockingPath;
        SystemMessage = systemMessage;
        HResult = hResult;
    }

    /// <summary>Why the batch was refused or stopped.</summary>
    public FailureReason Reason { get; }

    /// <summary>The original full path of the item being renamed; null for HasConflicts and HasRuleErrors.</summary>
    public string? ItemPath { get; }

    /// <summary>The full path of the entry in the way; TargetInTheWay only.</summary>
    public string? BlockingPath { get; }

    /// <summary>The exception's message when an exception occurred, else null.</summary>
    public string? SystemMessage { get; }

    /// <summary>The exception's HResult when an exception occurred, else null (NS-48 uses it to recognise "in use").</summary>
    public int? HResult { get; }
}
