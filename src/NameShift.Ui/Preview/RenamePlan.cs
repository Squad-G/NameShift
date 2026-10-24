using System;
using System.Collections.Generic;
using NameShift.Core;

namespace NameShift.Ui.Preview;

/// <summary>The new names of a batch and their conflicts, and whether Rename may run them. Immutable.</summary>
public sealed class RenamePlan
{
    /// <summary>Creates a plan; there must be exactly one conflict list per result.</summary>
    public RenamePlan(IReadOnlyList<RenameResult> results, ConflictReport conflicts)
    {
        if (results == null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        if (conflicts == null)
        {
            throw new ArgumentNullException(nameof(conflicts));
        }

        if (conflicts.Count != results.Count)
        {
            throw new ArgumentException("There must be one conflict list per result.", nameof(conflicts));
        }

        Results = results;
        Conflicts = conflicts;
        for (int i = 0; i < results.Count; i++)
        {
            if (conflicts.ForItem(i).Count > 0)
            {
                ConflictCount++;
            }

            if (results[i].Status == RenameStatus.Error)
            {
                ErrorCount++;
            }
            else if (results[i].Status == RenameStatus.Changed)
            {
                ChangedCount++;
            }
        }

        Blocker = BlockerFor(ChangedCount, ConflictCount, ErrorCount);
    }

    /// <summary>One result per item, Results[i].Index == i.</summary>
    public IReadOnlyList<RenameResult> Results { get; }

    /// <summary>One conflict list per result, by position.</summary>
    public ConflictReport Conflicts { get; }

    /// <summary>Results whose status is Changed: what a Rename would rename.</summary>
    public int ChangedCount { get; }

    /// <summary>Results whose status is Error (a rule failed).</summary>
    public int ErrorCount { get; }

    /// <summary>Items with at least one conflict (a row with two codes counts once).</summary>
    public int ConflictCount { get; }

    /// <summary>ConflictCount > 0.</summary>
    public bool HasConflicts => ConflictCount > 0;

    /// <summary>Why Rename cannot run this plan; None when it can. The one place in the code base where "Rename needs a
    /// change, no conflict and no rule error" is written.</summary>
    public RenameBlocker Blocker { get; }

    private static RenameBlocker BlockerFor(int changed, int conflicts, int errors)
    {
        if (conflicts > 0 && errors > 0)
        {
            return RenameBlocker.ConflictsAndRuleErrors;
        }

        if (conflicts > 0)
        {
            return RenameBlocker.Conflicts;
        }

        if (errors > 0)
        {
            return RenameBlocker.RuleErrors;
        }

        return changed > 0 ? RenameBlocker.None : RenameBlocker.NothingToRename;
    }
}
