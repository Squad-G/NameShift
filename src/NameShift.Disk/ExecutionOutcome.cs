namespace NameShift.Disk;

/// <summary>How a batch ended.</summary>
public enum ExecutionOutcome
{
    /// <summary>Every rename was done (also an empty plan, with nothing to do).</summary>
    Succeeded,

    /// <summary>Refused by the checks before any move was attempted; the disk is untouched.</summary>
    Refused,

    /// <summary>A rename failed; every done rename was undone, so the disk is as before. This also covers a failure on
    /// the first move (zero renames to undo).</summary>
    RolledBack,

    /// <summary>A rename failed and some undo steps failed too; see <see cref="ExecutionResult.NeedsAttention"/>.</summary>
    NeedsAttention,
}
