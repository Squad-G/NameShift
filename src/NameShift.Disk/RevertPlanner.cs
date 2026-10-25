using System.Collections.Generic;
using System.Linq;
using NameShift.Core;

namespace NameShift.Disk;

/// <summary>Decides, by reading the disk only, which journal entries Revert can try: never one whose old name is taken
/// by something else, nor one waiting for an entry that stays. Linear in the number of entries.</summary>
internal static class RevertPlanner
{
    /// <summary>Plans a revert of the entries (validated journal entries: unique sources and unique targets). A folder
    /// skipped with NameTaken does not block its children (they are reverted inside it); the children of a missing
    /// folder are missing too. Unless <paramref name="includeModified"/>, a file whose size or last write time differs
    /// from its fingerprint is skipped as Modified. One reason per entry, in this precedence: ItemMissing, NameTaken,
    /// Modified, WaitsForSkippedItem.</summary>
    public static RevertPlan Plan(IReadOnlyList<CompletedRename> entries, IFileSystem fileSystem, bool includeModified = false)
    {
        var plan = new RevertPlan(entries);

        // 1. The item must still be where the batch left it, with its kind.
        for (var i = 0; i < plan.Count; i++)
        {
            if (!fileSystem.Exists(plan.Source(i), plan.Entry(i).Kind))
            {
                plan.Mark(i, RevertSkipReason.ItemMissing);
            }
        }

        // 2. Its old name must be free, or held by another entry that will move away. An entry whose item is missing
        // frees nothing: if something sits at its path, it is someone else's.
        for (var i = 0; i < plan.Count; i++)
        {
            if (plan.SkipOf(i) != null)
            {
                continue;
            }

            var target = plan.Target(i);
            var holder = plan.EntryWithSource(target);
            var freedByTheBatch = holder >= 0 && plan.SkipOf(holder)?.Reason != RevertSkipReason.ItemMissing;
            if (!freedByTheBatch && fileSystem.ExistsAny(target))
            {
                plan.Mark(i, RevertSkipReason.NameTaken, target);
            }
        }

        // 2b. A file with a fingerprint must still be as the batch left it. A file that exists but cannot be read counts
        // as changed: "unchanged" cannot be confirmed. Entries without a fingerprint (folders, old journals) are not
        // checked.
        if (!includeModified)
        {
            for (var i = 0; i < plan.Count; i++)
            {
                var entry = plan.Entry(i);
                if (plan.SkipOf(i) != null || entry.Kind != ItemKind.File || entry.Fingerprint == null)
                {
                    continue;
                }

                var now = fileSystem.TryGetFingerprint(plan.Source(i));
                if (now == null || !now.Equals(entry.Fingerprint))
                {
                    plan.Mark(i, RevertSkipReason.Modified);
                }
            }
        }

        // 3. Without further disk reads: an entry whose target is held by a skipped entry waits for it.
        plan.Propagate(Enumerable.Range(0, plan.Count).Where(i => plan.SkipOf(i) != null).ToList());
        return plan;
    }
}
