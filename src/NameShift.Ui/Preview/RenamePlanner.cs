using System;
using System.Collections.Generic;
using System.Threading;
using NameShift.Core;

namespace NameShift.Ui.Preview;

/// <summary>Plans a rename: the one place the pipeline and the conflict checks run, for the window and the command line.</summary>
public static class RenamePlanner
{
    /// <summary>Runs the rules over the items, then InvalidNameCheck, DuplicateTargetCheck (given every entry name of
    /// each folder of the batch, as listed on disk) and PathLengthCheck, in this order. Throws
    /// OperationCanceledException when the token is cancelled.</summary>
    public static RenamePlan Plan(
        IReadOnlyList<IRenameRule> rules,
        IReadOnlyList<RenameItem> items,
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> existingNamesByFolder,
        CancellationToken cancellationToken)
    {
        if (rules == null)
        {
            throw new ArgumentNullException(nameof(rules));
        }

        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        if (existingNamesByFolder == null)
        {
            throw new ArgumentNullException(nameof(existingNamesByFolder));
        }

        IReadOnlyList<RenameResult> results = new RenamePipeline(rules).Run(items, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        IConflictCheck[] checks =
        {
            new InvalidNameCheck(),
            new DuplicateTargetCheck(existingNamesByFolder),
            new PathLengthCheck(),
        };
        return new RenamePlan(results, ConflictChecker.Run(results, checks));
    }
}
