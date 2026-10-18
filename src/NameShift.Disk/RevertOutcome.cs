namespace NameShift.Disk;

/// <summary>How a Revert ended.</summary>
public enum RevertOutcome
{
    /// <summary>The journal holds no revertable batch; nothing was read beyond the journal, nothing was written.</summary>
    NothingToRevert,

    /// <summary>Every entry got its old name back; Revert is now disabled.</summary>
    Reverted,

    /// <summary>Some entries were reverted; the others are listed and stay revertable.</summary>
    PartlyReverted,

    /// <summary>No entry could be reverted; they are listed, and the journal is unchanged.</summary>
    NothingReverted,

    /// <summary>The journal could not be written before the revert, so nothing was moved.</summary>
    NotStarted,

    /// <summary>A revert step failed and undoing the attempt failed too; some items need the user to look at them.
    /// The journal is unchanged.</summary>
    NeedsAttention,
}
