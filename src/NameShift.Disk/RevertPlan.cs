using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NameShift.Disk;

/// <summary>The journal entries of one Revert and which of them are skipped. Each entry's source is its normalized
/// NewPath (where the batch left it) and its target the normalized path its inverse goes to.</summary>
internal sealed class RevertPlan
{
    private readonly IReadOnlyList<CompletedRename> entries;
    private readonly string[] sources;
    private readonly string[] targets;
    private readonly Dictionary<string, int> entryBySource = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> entryByTarget = new(StringComparer.OrdinalIgnoreCase);
    private readonly SkippedRevert?[] skips;

    public RevertPlan(IReadOnlyList<CompletedRename> entries)
    {
        this.entries = entries;
        sources = new string[entries.Count];
        targets = new string[entries.Count];
        skips = new SkippedRevert?[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            var inverse = entries[i].Inverse();
            sources[i] = FolderKey.NormalizePath(entries[i].NewPath);
            targets[i] = FolderKey.NormalizePath(Path.Combine(inverse.FolderPath, inverse.NewName));
            entryBySource[sources[i]] = i;
            entryByTarget[targets[i]] = i;
        }
    }

    /// <summary>The number of entries.</summary>
    public int Count => entries.Count;

    /// <summary>The entries not skipped, in journal order.</summary>
    public IReadOnlyList<CompletedRename> Included =>
        Enumerable.Range(0, entries.Count).Where(i => skips[i] == null).Select(i => entries[i]).ToList();

    /// <summary>The skipped entries, in journal order.</summary>
    public IReadOnlyList<SkippedRevert> Skipped => skips.Where(skip => skip != null).Select(skip => skip!).ToList();

    /// <summary>The entry's journal record.</summary>
    public CompletedRename Entry(int index) => entries[index];

    /// <summary>The entry's normalized source path.</summary>
    public string Source(int index) => sources[index];

    /// <summary>The entry's normalized target path.</summary>
    public string Target(int index) => targets[index];

    /// <summary>The skip of an entry, or null when it is included.</summary>
    public SkippedRevert? SkipOf(int index) => skips[index];

    /// <summary>The entry whose source is the path (normalized here), or -1.</summary>
    public int EntryWithSource(string path) =>
        entryBySource.TryGetValue(FolderKey.NormalizePath(path), out var index) ? index : -1;

    /// <summary>Skips an entry without propagating (the planner propagates once, after all its disk checks).</summary>
    public void Mark(int index, RevertSkipReason reason, string? blockingPath = null, string? systemMessage = null, int? hResult = null) =>
        skips[index] = new SkippedRevert(entries[index], reason, blockingPath, systemMessage, hResult);

    /// <summary>Skips an entry and every entry that, as a result, waits for its name.</summary>
    public void Skip(int index, RevertSkipReason reason, string? blockingPath = null, string? systemMessage = null, int? hResult = null)
    {
        Mark(index, reason, blockingPath, systemMessage, hResult);
        Propagate([index]);
    }

    /// <summary>Every skipped entry that is still where the batch left it (not ItemMissing) keeps holding its source, so
    /// the entry whose target that is is skipped too, and so on. Linear: each entry is queued at most once.</summary>
    public void Propagate(IEnumerable<int> skipped)
    {
        var queue = new Queue<int>(skipped.Where(j => skips[j]!.Reason != RevertSkipReason.ItemMissing));
        while (queue.Count > 0)
        {
            var j = queue.Dequeue();

            // k == j is a case-only change: an entry never waits for itself.
            if (entryByTarget.TryGetValue(sources[j], out var k) && k != j && skips[k] == null)
            {
                Mark(k, RevertSkipReason.WaitsForSkippedItem, sources[j]);
                queue.Enqueue(k);
            }
        }
    }
}
