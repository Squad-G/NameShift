using System.Collections.Generic;
using NameShift.Core;
using NameShift.Disk;

namespace NameShift.Ui.Actions;

/// <summary>Runs and reverts batches with their undo journal. The app's implementation is JournaledBatchRenamer;
/// every call does disk work, so the window calls it off the UI thread.</summary>
public interface IBatchRenamer
{
    /// <summary>Reads the journal: what Revert would do, or why it is disabled. Never writes and never throws.</summary>
    LastBatch LoadLastBatch();

    /// <summary>Runs the checked plan and records it for Revert.</summary>
    RenameRunResult Rename(IReadOnlyList<RenameResult> results, ConflictReport conflicts);

    /// <summary>What a default Revert would skip now (changed files included), read from the journal and the disk.
    /// Never writes and never throws; empty when the journal is not revertable.</summary>
    RevertCheck CheckRevert();

    /// <summary>Reverts the journal's last batch. A file changed after the batch is skipped unless
    /// <paramref name="includeModified"/>.</summary>
    RevertResult Revert(bool includeModified = false);
}
