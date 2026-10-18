using System;
using System.Collections.Generic;
using System.Linq;

namespace NameShift.Core;

/// <summary>Runs conflict checks on the pipeline's results.</summary>
public static class ConflictChecker
{
    /// <summary>Runs each check in order and concatenates, per item, the conflicts of every check.</summary>
    public static ConflictReport Run(IReadOnlyList<RenameResult> results, IEnumerable<IConflictCheck> checks)
    {
        if (results == null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        if (checks == null)
        {
            throw new ArgumentNullException(nameof(checks));
        }

        var list = checks.ToArray();
        var merged = results.Select(_ => new List<Conflict>()).ToArray();
        foreach (var check in list)
        {
            if (check == null)
            {
                throw new ArgumentException("A check is null.", nameof(checks));
            }

            var found = check.Check(results);
            if (found == null || found.Count != results.Count)
            {
                throw new InvalidOperationException(
                    $"{check.GetType().Name} must return one conflict list per result.");
            }

            for (var i = 0; i < merged.Length; i++)
            {
                merged[i].AddRange(found[i]);
            }
        }

        return new ConflictReport(merged);
    }
}
