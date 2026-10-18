using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NameShift.Core;

namespace NameShift.Disk;

/// <summary>The app's only way to run a batch: every successful batch is recorded in the undo journal (only the last
/// one), and a batch never starts unless its journal can be written. Revert replays the inverse renames through the
/// executor, skips (never overwrites) what cannot be reverted and keeps those entries revertable.
/// Not thread-safe: one operation at a time. There is no lock across processes.</summary>
public sealed class JournaledRenamer
{
    private readonly IFileSystem fileSystem;
    private readonly RenameExecutor executor;
    private readonly UndoJournal journal;
    private readonly Func<DateTimeOffset> clock;
    private readonly Func<string> newId;

    /// <summary>Creates a renamer on the real disk, with its journal in the given folder (created on the first write).</summary>
    public JournaledRenamer(string historyRoot)
        : this(
            historyRoot,
            new DiskFileSystem(),
            () => Guid.NewGuid().ToString("N").Substring(0, 8),
            new DiskJournalFiles(),
            () => DateTimeOffset.UtcNow,
            () => Guid.NewGuid().ToString("N"))
    {
    }

    internal JournaledRenamer(
        string historyRoot,
        IFileSystem fileSystem,
        Func<string> newToken,
        IJournalFiles journalFiles,
        Func<DateTimeOffset> clock,
        Func<string> newId)
    {
        if (historyRoot == null)
        {
            throw new ArgumentNullException(nameof(historyRoot));
        }

        if (historyRoot.Length == 0 || !Path.IsPathRooted(historyRoot))
        {
            throw new ArgumentException("The history folder must be a full path.", nameof(historyRoot));
        }

        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        executor = new RenameExecutor(fileSystem, newToken ?? throw new ArgumentNullException(nameof(newToken)));
        journal = new UndoJournal(historyRoot, journalFiles ?? throw new ArgumentNullException(nameof(journalFiles)));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.newId = newId ?? throw new ArgumentNullException(nameof(newId));
    }

    /// <summary>%LOCALAPPDATA%\NameShift\history. Only computed: reading it creates nothing.</summary>
    public static string DefaultHistoryRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NameShift", "history");

    /// <summary>Reads the journal: what Revert would do, or why it is disabled. Never writes, moves or deletes anything,
    /// and never throws.</summary>
    public LastBatch LoadLastBatch() => journal.Load();

    /// <summary>Runs a checked plan like <see cref="RenameExecutor.Execute(IReadOnlyList{RenameResult}, ConflictReport)"/>,
    /// recording it for Revert. A plan the executor refuses outright, or with nothing to rename, runs without touching
    /// the journal (the previous batch stays revertable).</summary>
    public RenameRunResult Rename(IReadOnlyList<RenameResult> results, ConflictReport conflicts)
    {
        if (results == null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        if (conflicts == null)
        {
            throw new ArgumentNullException(nameof(conflicts));
        }

        // Invalid arguments (the executor throws), a refused plan and an empty one never reach the journal.
        if (conflicts.Count != results.Count
            || results.Any(result => result == null)
            || conflicts.HasConflicts
            || results.Any(result => result.Status == RenameStatus.Error)
            || !results.Any(result => result.Status == RenameStatus.Changed))
        {
            return new RenameRunResult(executor.Execute(results, conflicts), null);
        }

        var planned = results
            .Where(result => result.Status == RenameStatus.Changed)
            .OrderBy(result => result.Index)
            .Select(result => new PlannedRename(result.Item.FolderPath, result.Item.Name, result.NewName, result.Item.Kind))
            .ToList();
        var id = newId();
        try
        {
            journal.WritePending(id, JournalJson.RenameOperation, clock(), planned);
        }
        catch (Exception ex)
        {
            // Any failure: never rename without a way to undo, so the batch is not started.
            return new RenameRunResult(null, new JournalWriteFailure(JournalWritePhase.BeforeStart, ex));
        }

        // An exception escaping the executor leaves pending.json in place on purpose: the journal then reads
        // Interrupted and Revert stays disabled, the safe state when the disk is unknown.
        var execution = executor.Execute(results, conflicts);
        if (execution.Outcome != ExecutionOutcome.Succeeded)
        {
            // Nothing changed on disk (or the executor lists what needs attention); last-batch.json is untouched (AC4).
            return new RenameRunResult(execution, DeletePending());
        }

        return new RenameRunResult(execution, Commit(id, isReverted: false, clock(), execution.Renamed));
    }

    /// <summary>Reverts the last batch: every entry that can safely go back gets its old name; the others are listed,
    /// never overwritten, and stay revertable.</summary>
    public RevertResult Revert()
    {
        var last = LoadLastBatch();
        if (!last.CanRevert)
        {
            return new RevertResult(RevertOutcome.NothingToRevert);
        }

        var plan = RevertPlanner.Plan(last.Renames, fileSystem);
        var included = plan.Included;
        if (included.Count == 0)
        {
            return new RevertResult(RevertOutcome.NothingReverted, notReverted: plan.Skipped);
        }

        var id = newId();
        try
        {
            journal.WritePending(id, JournalJson.RevertOperation, clock(), included.Select(entry => entry.Inverse()));
        }
        catch (Exception ex)
        {
            // Any failure: nothing is moved without a journal that can say so.
            return new RevertResult(
                RevertOutcome.NotStarted, journalIssue: new JournalWriteFailure(JournalWritePhase.BeforeStart, ex));
        }

        while (true)
        {
            var execution = executor.Execute(included.Select(entry => entry.Inverse()).ToList());
            if (execution.Outcome == ExecutionOutcome.Succeeded)
            {
                return CommitRevert(id, last.Time!.Value, plan, execution.Renamed);
            }

            if (execution.Outcome == ExecutionOutcome.NeedsAttention)
            {
                // The journal is left as it was: every entry stays, and a later Revert skips the stranded ones as
                // missing, never overwriting.
                return new RevertResult(
                    RevertOutcome.NeedsAttention,
                    notReverted: plan.Skipped,
                    failure: execution.Failure,
                    needsAttention: execution.NeedsAttention,
                    journalIssue: DeletePending());
            }

            // Refused or RolledBack: the disk is as before this attempt. Skip the failed entry and try the rest; each
            // round removes at least one entry, so the loop ends.
            SkipFailedEntry(plan, execution.Failure!);
            included = plan.Included;
            if (included.Count == 0)
            {
                return new RevertResult(RevertOutcome.NothingReverted, notReverted: plan.Skipped, journalIssue: DeletePending());
            }
        }
    }

    private static void SkipFailedEntry(RevertPlan plan, ExecutionFailure failure)
    {
        var index = plan.EntryWithSource(failure.ItemPath!);
        if (index < 0 || plan.SkipOf(index) != null)
        {
            throw new InvalidOperationException($"The executor failed on an item the revert did not plan: {failure.ItemPath}");
        }

        switch (failure.Reason)
        {
            case FailureReason.SourceMissing:
                plan.Skip(index, RevertSkipReason.ItemMissing);
                break;
            case FailureReason.TargetInTheWay:
                plan.Skip(index, RevertSkipReason.NameTaken, failure.BlockingPath, failure.SystemMessage, failure.HResult);
                break;
            case FailureReason.RenameFailed:
                plan.Skip(index, RevertSkipReason.RenameFailed, null, failure.SystemMessage, failure.HResult);
                break;
            default:
                throw new InvalidOperationException($"Unexpected executor failure for a revert: {failure.Reason}");
        }
    }

    /// <summary>After a successful run: "reverted" when every entry went back, else "done" with the skipped entries
    /// (rebased onto the reverted folders) so that Revert stays available for them.</summary>
    private RevertResult CommitRevert(string id, DateTimeOffset batchTime, RevertPlan plan, IReadOnlyList<CompletedRename> reverted)
    {
        var skipped = plan.Skipped;
        if (skipped.Count == 0)
        {
            return new RevertResult(
                RevertOutcome.Reverted, reverted, journalIssue: Commit(id, isReverted: true, batchTime, Array.Empty<CompletedRename>()));
        }

        var remaining = Rebase(skipped.Select(skip => skip.Entry), reverted);
        return new RevertResult(
            RevertOutcome.PartlyReverted, reverted, skipped, journalIssue: Commit(id, isReverted: false, batchTime, remaining));
    }

    /// <summary>Moves each skipped entry's NewPath into the folder's new place when a containing folder was reverted:
    /// the deepest reverted folder's prefix is replaced, once (replacing again would break a folder swap).</summary>
    private static IReadOnlyList<CompletedRename> Rebase(IEnumerable<CompletedRename> entries, IReadOnlyList<CompletedRename> reverted)
    {
        var folderMoves = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rename in reverted.Where(rename => rename.Kind == ItemKind.Folder))
        {
            folderMoves[FolderKey.NormalizePath(rename.OldPath)] = rename.NewPath;
        }

        return entries
            .Select(entry =>
            {
                var path = FolderKey.NormalizePath(entry.NewPath);
                for (var folder = FolderKey.Parent(path); folder != null; folder = FolderKey.Parent(folder))
                {
                    if (folderMoves.TryGetValue(folder, out var movedTo))
                    {
                        return new CompletedRename(entry.OldPath, movedTo + path.Substring(folder.Length), entry.Kind);
                    }
                }

                return entry;
            })
            .ToList();
    }

    /// <summary>Writes last-batch.json, then removes pending.json. A failed write leaves pending.json, so the journal
    /// reads Interrupted and Revert is disabled; the moves stay done.</summary>
    private JournalWriteFailure? Commit(string id, bool isReverted, DateTimeOffset time, IReadOnlyList<CompletedRename> renames)
    {
        try
        {
            journal.WriteLastBatch(id, isReverted, time, renames);
        }
        catch (Exception ex)
        {
            // Any failure: the files are already renamed (correctly); only Revert is lost, and the caller says so.
            return new JournalWriteFailure(JournalWritePhase.AfterFinish, ex);
        }

        try
        {
            journal.DeletePending();
        }
        catch (Exception)
        {
            // Harmless: the leftover pending.json has the id of last-batch.json, so it is recognised as stale.
        }

        return null;
    }

    /// <summary>Removes pending.json after an operation that changed nothing in the journal. A failed delete leaves it,
    /// so Revert of the batch before is disabled (safe), and the caller says so.</summary>
    private JournalWriteFailure? DeletePending()
    {
        try
        {
            journal.DeletePending();
            return null;
        }
        catch (Exception ex)
        {
            // Any failure: reported, never thrown, since the operation itself is over.
            return new JournalWriteFailure(JournalWritePhase.AfterFinish, ex);
        }
    }
}
