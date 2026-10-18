using System;
using System.Collections.Generic;
using NameShift.Core;
using NameShift.Disk;

namespace NameShift.Ui.Actions;

/// <summary>The app's <see cref="IBatchRenamer"/>: forwards to <see cref="JournaledRenamer"/>, the only code that renames
/// files. No logic of its own.</summary>
public sealed class JournaledBatchRenamer : IBatchRenamer
{
    private readonly JournaledRenamer _renamer;

    public JournaledBatchRenamer(JournaledRenamer renamer)
    {
        _renamer = renamer ?? throw new ArgumentNullException(nameof(renamer));
    }

    public LastBatch LoadLastBatch() => _renamer.LoadLastBatch();

    public RenameRunResult Rename(IReadOnlyList<RenameResult> results, ConflictReport conflicts) => _renamer.Rename(results, conflicts);

    public RevertResult Revert() => _renamer.Revert();
}
