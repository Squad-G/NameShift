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

    /// <summary>Reverts the journal's last batch.</summary>
    RevertResult Revert();
}
