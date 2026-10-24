using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NameShift.Core;

namespace NameShift.Disk;

/// <summary>Turns a checked plan into real renames: never overwrites anything, orders chains, swaps and case-only
/// changes through temporary names in the same folder, and rolls the whole batch back on any failure. It never
/// deletes, copies or changes attributes.</summary>
public sealed class RenameExecutor
{
    private const int ErrorSharingViolation = 32;
    private const int ErrorLockViolation = 33;

    private readonly IFileSystem fileSystem;
    private readonly Func<string> newToken;

    /// <summary>Creates an executor on the real disk, with a new random temporary-name token per Execute call.</summary>
    public RenameExecutor()
        : this(new DiskFileSystem(), () => Guid.NewGuid().ToString("N").Substring(0, 8))
    {
    }

    internal RenameExecutor(IFileSystem fileSystem, Func<string> newToken)
    {
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.newToken = newToken ?? throw new ArgumentNullException(nameof(newToken));
    }

    /// <summary>Runs the Changed results of a checked plan. Refuses (touching nothing, not even reading the disk) when
    /// the report has any conflict or any result has Status Error. Unchanged results are skipped.</summary>
    public ExecutionResult Execute(IReadOnlyList<RenameResult> results, ConflictReport conflicts)
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
            throw new ArgumentException("The report must have one entry per result.", nameof(conflicts));
        }

        if (results.Any(result => result == null))
        {
            throw new ArgumentException("A result is null.", nameof(results));
        }

        if (conflicts.HasConflicts)
        {
            return Refused(new ExecutionFailure(FailureReason.HasConflicts));
        }

        if (results.Any(result => result.Status == RenameStatus.Error))
        {
            return Refused(new ExecutionFailure(FailureReason.HasRuleErrors));
        }

        return Execute(ToPlannedRenames(results));
    }

    /// <summary>The Changed results as renames, in result Index order: what Execute(results, conflicts) runs (the
    /// journal's pending.json records the same list).</summary>
    internal static List<PlannedRename> ToPlannedRenames(IReadOnlyList<RenameResult> results) =>
        results
            .Where(result => result.Status == RenameStatus.Changed)
            .OrderBy(result => result.Index)
            .Select(result => new PlannedRename(result.Item.FolderPath, result.Item.Name, result.NewName, result.Item.Kind))
            .ToList();

    /// <summary>Runs the given renames (NS-9's Revert calls this with CompletedRename.Inverse()). Throws when a rename is
    /// null or two renames in one folder share an old name or a new name (ordinal ignore-case).</summary>
    public ExecutionResult Execute(IReadOnlyList<PlannedRename> renames)
    {
        if (renames == null)
        {
            throw new ArgumentNullException(nameof(renames));
        }

        if (renames.Any(rename => rename == null))
        {
            throw new ArgumentException("A rename is null.", nameof(renames));
        }

        if (renames.Count == 0)
        {
            return new ExecutionResult(ExecutionOutcome.Succeeded);
        }

        return new Batch(fileSystem, newToken(), renames).Run();
    }

    private static ExecutionResult Refused(ExecutionFailure failure) =>
        new(ExecutionOutcome.Refused, failure: failure);

    /// <summary>True for a Windows sharing or lock violation: the low word of the HResult is the Win32 error code.</summary>
    private static bool IsInUse(int hResult)
    {
        int code = hResult & 0xFFFF;
        return code == ErrorSharingViolation || code == ErrorLockViolation;
    }

    /// <summary>One Execute call: its renames, their folder keys and the state of each item.</summary>
    private sealed class Batch
    {
        private readonly IFileSystem fileSystem;
        private readonly string token;
        private readonly IReadOnlyList<PlannedRename> renames;
        private readonly string[] folderKeys;
        private readonly string[] oldPaths;
        private readonly string[] currentNames;
        private readonly HashSet<string> oldPathSet = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> batchPaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> folderRenameByOldPath = new(StringComparer.OrdinalIgnoreCase);
        private int tempCount;

        public Batch(IFileSystem fileSystem, string token, IReadOnlyList<PlannedRename> renames)
        {
            this.fileSystem = fileSystem;
            this.token = token;
            this.renames = renames;
            folderKeys = renames.Select(rename => FolderKey.Of(rename.FolderPath)).ToArray();
            oldPaths = new string[renames.Count];
            currentNames = renames.Select(rename => rename.OldName).ToArray();

            var newPathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < renames.Count; i++)
            {
                oldPaths[i] = Path.Combine(folderKeys[i], renames[i].OldName);
                if (!oldPathSet.Add(oldPaths[i]))
                {
                    throw new ArgumentException($"Two renames start from the same item: {oldPaths[i]}", nameof(renames));
                }

                var newPath = Path.Combine(folderKeys[i], renames[i].NewName);
                if (!newPathSet.Add(newPath))
                {
                    throw new ArgumentException($"Two renames go to the same name: {newPath}", nameof(renames));
                }

                if (renames[i].Kind == ItemKind.Folder)
                {
                    folderRenameByOldPath[oldPaths[i]] = i;
                }
            }

            batchPaths.UnionWith(oldPathSet);
            batchPaths.UnionWith(newPathSet);
        }

        public ExecutionResult Run()
        {
            var refusal = PreFlight();
            if (refusal != null)
            {
                return Refused(refusal);
            }

            var steps = RenameOrder.Plan(renames, folderKeys, NewTempPath);
            var done = new List<RenameStep>(steps.Count);
            foreach (var step in steps)
            {
                var failure = Perform(step);
                if (failure != null)
                {
                    return RollBack(done, failure);
                }

                done.Add(step);
            }

            var finalNames = renames.Select(rename => rename.NewName).ToArray();
            var memo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var renamed = steps
                .Where(step => step.IsFinal)
                .Select(step => new CompletedRename(
                    oldPaths[step.RenameIndex],
                    Path.Combine(MapFolder(folderKeys[step.RenameIndex], finalNames, memo), finalNames[step.RenameIndex]),
                    step.Kind))
                .ToList();
            return new ExecutionResult(ExecutionOutcome.Succeeded, renamed);
        }

        /// <summary>Reads the disk only: every source is there with its kind, and no final target is taken by an entry
        /// outside the batch.</summary>
        private ExecutionFailure? PreFlight()
        {
            for (var i = 0; i < renames.Count; i++)
            {
                if (!fileSystem.Exists(oldPaths[i], renames[i].Kind))
                {
                    return new ExecutionFailure(FailureReason.SourceMissing, oldPaths[i]);
                }
            }

            for (var i = 0; i < renames.Count; i++)
            {
                // A target named like an item of the batch (the item's own name too, for a case-only change) is freed
                // by the batch itself; the ordering takes care of it.
                var target = Path.Combine(folderKeys[i], renames[i].NewName);
                if (!oldPathSet.Contains(target) && fileSystem.ExistsAny(target))
                {
                    return new ExecutionFailure(FailureReason.TargetInTheWay, oldPaths[i], target);
                }
            }

            return null;
        }

        /// <summary>A free temporary path in the folder: "~ns-token-n", skipping names on disk or used by the batch.</summary>
        private string NewTempPath(string folderKey)
        {
            while (true)
            {
                tempCount++;
                var path = Path.Combine(folderKey, $"~ns-{token}-{tempCount}");
                if (!batchPaths.Contains(path) && !fileSystem.ExistsAny(path))
                {
                    return path;
                }
            }
        }

        /// <summary>Does one move after checking the disk right before it; returns the failure, or null when done.</summary>
        private ExecutionFailure? Perform(RenameStep step)
        {
            var itemPath = oldPaths[step.RenameIndex];
            if (fileSystem.ExistsAny(step.ToPath))
            {
                return new ExecutionFailure(FailureReason.TargetInTheWay, itemPath, step.ToPath);
            }

            if (!fileSystem.Exists(step.FromPath, step.Kind))
            {
                return new ExecutionFailure(FailureReason.SourceMissing, itemPath);
            }

            try
            {
                fileSystem.Move(step.FromPath, step.ToPath, step.Kind);
            }
            catch (Exception ex)
            {
                // Every exception, whatever its type, stops the batch: a half-renamed batch is the worst outcome, so
                // nothing may escape before the disk is restored by the rollback.
                return fileSystem.ExistsAny(step.ToPath)
                    ? new ExecutionFailure(FailureReason.TargetInTheWay, itemPath, step.ToPath, ex.Message, ex.HResult)
                    : new ExecutionFailure(IsInUse(ex.HResult) ? FailureReason.ItemInUse : FailureReason.RenameFailed,
                        itemPath, null, ex.Message, ex.HResult);
            }

            currentNames[step.RenameIndex] = Path.GetFileName(step.ToPath);
            return null;
        }

        /// <summary>Undoes the done steps in reverse order, never overwriting; continues after a failed undo, since each
        /// undo checks the disk on its own.</summary>
        private ExecutionResult RollBack(List<RenameStep> done, ExecutionFailure failure)
        {
            var undoMessages = new Dictionary<int, string>();
            for (var d = done.Count - 1; d >= 0; d--)
            {
                var step = done[d];

                // Skipped when its old name is taken (the undo would replace something) or the item is not where the
                // undo expects it (an earlier undo it depends on failed).
                if (fileSystem.ExistsAny(step.FromPath) || !fileSystem.Exists(step.ToPath, step.Kind))
                {
                    continue;
                }

                try
                {
                    fileSystem.Move(step.ToPath, step.FromPath, step.Kind);
                    currentNames[step.RenameIndex] = Path.GetFileName(step.FromPath);
                }
                catch (Exception ex)
                {
                    // Any exception: keep undoing the others, and report this item below.
                    if (!undoMessages.ContainsKey(step.RenameIndex))
                    {
                        undoMessages[step.RenameIndex] = ex.Message;
                    }
                }
            }

            var memo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var stranded = Enumerable.Range(0, renames.Count)
                .Where(i => !string.Equals(currentNames[i], renames[i].OldName, StringComparison.Ordinal))
                .Select(i => new StrandedItem(
                    Path.Combine(MapFolder(folderKeys[i], currentNames, memo), currentNames[i]),
                    oldPaths[i],
                    renames[i].Kind,
                    undoMessages.TryGetValue(i, out var message) ? message : null))
                .ToList();

            return stranded.Count == 0
                ? new ExecutionResult(ExecutionOutcome.RolledBack, failure: failure)
                : new ExecutionResult(ExecutionOutcome.NeedsAttention, failure: failure, needsAttention: stranded);
        }

        /// <summary>Where a folder of the batch is, given each item's name (final names after success, current names
        /// after a rollback): renamed parent folders applied. Memoized per key.</summary>
        private string MapFolder(string key, string[] names, Dictionary<string, string> memo)
        {
            if (memo.TryGetValue(key, out var mapped))
            {
                return mapped;
            }

            var parent = FolderKey.Parent(key);
            if (folderRenameByOldPath.TryGetValue(key, out var folder))
            {
                mapped = Path.Combine(MapFolder(folderKeys[folder], names, memo), names[folder]);
            }
            else if (parent == null)
            {
                mapped = key;
            }
            else
            {
                mapped = Path.Combine(MapFolder(parent, names, memo), Path.GetFileName(key));
            }

            memo[key] = mapped;
            return mapped;
        }
    }
}
