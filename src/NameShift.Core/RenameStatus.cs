namespace NameShift.Core;

/// <summary>The outcome of computing one item's new name.</summary>
public enum RenameStatus
{
    /// <summary>The new name equals the original name.</summary>
    Unchanged,

    /// <summary>The new name differs from the original name.</summary>
    Changed,

    /// <summary>A rule failed, so no new name could be computed.</summary>
    Error,
}
