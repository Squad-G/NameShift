using System;
using System.Collections.Generic;
using System.IO;

namespace NameShift.Disk;

/// <summary>The undo journal's store: last-batch.json (the committed last batch) and pending.json (the write-ahead
/// marker of a batch or revert in progress) in the history root. It deletes only its own pending.json.</summary>
internal sealed class UndoJournal
{
    /// <summary>The committed journal's file name.</summary>
    public const string LastBatchFileName = "last-batch.json";

    /// <summary>The write-ahead marker's file name.</summary>
    public const string PendingFileName = "pending.json";

    private readonly IJournalFiles files;
    private readonly string lastBatchPath;
    private readonly string pendingPath;

    public UndoJournal(string root, IJournalFiles files)
    {
        this.files = files;
        lastBatchPath = Path.Combine(root, LastBatchFileName);
        pendingPath = Path.Combine(root, PendingFileName);
    }

    /// <summary>Reads the journal. Never writes, moves or deletes anything, and never throws.</summary>
    public LastBatch Load()
    {
        try
        {
            return LoadOrThrow();
        }
        catch (Exception)
        {
            // Any exception at all (I/O, access, an odd file system): no journal state may stop the app, and an
            // unreadable journal only disables Revert (AC5).
            return new LastBatch(LastBatchState.Unreadable);
        }
    }

    /// <summary>Writes pending.json before an operation moves anything.</summary>
    public void WritePending(string id, string operation, DateTimeOffset time, IEnumerable<PlannedRename> renames) =>
        files.WriteAtomic(pendingPath, JournalJson.SerializePending(id, operation, time, renames));

    /// <summary>Replaces last-batch.json.</summary>
    public void WriteLastBatch(string id, bool isReverted, DateTimeOffset time, IReadOnlyList<CompletedRename> renames) =>
        files.WriteAtomic(lastBatchPath, JournalJson.SerializeLastBatch(id, isReverted, time, renames));

    /// <summary>Removes pending.json once its operation is committed or abandoned.</summary>
    public void DeletePending() => files.Delete(pendingPath);

    private LastBatch LoadOrThrow()
    {
        var lastExists = files.Exists(lastBatchPath);
        var record = lastExists ? TryRead(() => JournalJson.ParseLastBatch(files.ReadAll(lastBatchPath))) : null;
        if (files.Exists(pendingPath))
        {
            // A pending.json with the id of last-batch.json is a stale marker whose delete failed after the commit; any
            // other pending.json means a batch or revert was cut short, or its journal could not be written.
            var pendingId = TryRead(() => JournalJson.ParsePendingId(files.ReadAll(pendingPath)));
            if (pendingId == null || record == null || !string.Equals(pendingId, record.Id, StringComparison.Ordinal))
            {
                return new LastBatch(LastBatchState.Interrupted);
            }
        }

        if (!lastExists)
        {
            return new LastBatch(LastBatchState.None);
        }

        if (record == null)
        {
            return new LastBatch(LastBatchState.Unreadable);
        }

        return record.IsReverted
            ? new LastBatch(LastBatchState.Reverted, record.Time)
            : new LastBatch(LastBatchState.Revertable, record.Time, record.Folders, record.Renames);
    }

    private static T? TryRead<T>(Func<T> read)
        where T : class
    {
        try
        {
            return read();
        }
        catch (Exception)
        {
            // Every exception (locked file, broken JSON, wrong types, invalid entries) means "unreadable": the journal
            // content must never stop the app or make Revert misbehave (AC5). The file is left untouched.
            return null;
        }
    }
}
