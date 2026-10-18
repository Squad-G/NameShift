using System;

namespace NameShift.Core;

/// <summary>The computed new name of one item.</summary>
public sealed class RenameResult
{
    /// <summary>Creates a result; an error is given if and only if the status is Error.</summary>
    public RenameResult(RenameItem item, int index, string newName, RenameStatus status, RenameError? error)
    {
        if ((status == RenameStatus.Error) != (error != null))
        {
            throw new ArgumentException("An error is required exactly when the status is Error.", nameof(error));
        }

        Item = item ?? throw new ArgumentNullException(nameof(item));
        Index = index;
        NewName = newName ?? throw new ArgumentNullException(nameof(newName));
        Status = status;
        Error = error;
    }

    /// <summary>The item the result is about.</summary>
    public RenameItem Item { get; }

    /// <summary>The item's position in the input list.</summary>
    public int Index { get; }

    /// <summary>The full new name; on Error it is the original name.</summary>
    public string NewName { get; }

    /// <summary>Unchanged, changed or error.</summary>
    public RenameStatus Status { get; }

    /// <summary>The error, when the status is Error.</summary>
    public RenameError? Error { get; }
}
