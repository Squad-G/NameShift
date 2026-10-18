namespace NameShift.Disk;

/// <summary>Why a batch was refused or stopped.</summary>
public enum FailureReason
{
    /// <summary>The conflict report had a conflict (NS-5, NS-6, NS-7).</summary>
    HasConflicts,

    /// <summary>A rule failed for an item (NS-1).</summary>
    HasRuleErrors,

    /// <summary>The item is no longer on disk as a file or folder of its kind.</summary>
    SourceMissing,

    /// <summary>Something that is not part of the batch occupies a name the batch needs.</summary>
    TargetInTheWay,

    /// <summary>The rename call itself failed (in use, access denied, path too long, ...).</summary>
    RenameFailed,
}
