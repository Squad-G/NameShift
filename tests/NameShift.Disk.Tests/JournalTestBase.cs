using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NameShift.Core;
using Xunit;

namespace NameShift.Disk.Tests;

/// <summary>Items in one temp folder, the journal in a second one (never inside the folder being renamed), a hooked
/// disk and journal, a fixed clock, ids "id1", "id2"... and predictable temp names (~ns-00000000-n).</summary>
public abstract class JournalTestBase : IDisposable
{
    internal const string Token = "00000000";

    internal static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 18, 14, 3, 7, TimeSpan.Zero).AddTicks(1234567);

    private int idCount;

    internal JournalTestBase()
    {
        Temp = new TempFolder();
        History = new TempFolder();
        Root = History.Full("history");
        Disk = new HookedFileSystem();
        Files = new HookedJournalFiles();
        Renamer = NewRenamer();
    }

    internal TempFolder Temp { get; }

    internal TempFolder History { get; }

    /// <summary>The history root; not created up front, so the first write creates it.</summary>
    internal string Root { get; }

    internal HookedFileSystem Disk { get; }

    internal HookedJournalFiles Files { get; }

    internal JournaledRenamer Renamer { get; }

    internal string LastBatchPath => Path.Combine(Root, "last-batch.json");

    internal string PendingPath => Path.Combine(Root, "pending.json");

    /// <summary>A new renamer on the same root, disk and journal files: a restart of the app.</summary>
    internal JournaledRenamer NewRenamer() =>
        new(Root, Disk, () => Token, Files, () => Now, () => "id" + ++idCount);

    internal static ConflictReport NoConflicts(int count) =>
        new(Enumerable.Range(0, count).Select(_ => (IReadOnlyList<Conflict>)Array.Empty<Conflict>()).ToList());

    /// <summary>A Changed result for an item in the temp folder (or one of its sub-folders).</summary>
    internal RenameResult Changed(int index, string name, string newName, ItemKind kind = ItemKind.File, string? sub = null) =>
        new(new RenameItem(name, sub == null ? Temp.Path : Temp.Full(sub), kind), index, newName, RenameStatus.Changed, null);

    /// <summary>Runs the results as one batch with no conflicts.</summary>
    internal RenameRunResult Run(params RenameResult[] results) => Renamer.Rename(results, NoConflicts(results.Length));

    /// <summary>Creates the files that do not exist yet, renames them in the temp folder as one batch, and checks that
    /// the batch and its journal succeeded.</summary>
    internal RenameRunResult RenameFiles(params (string Old, string New)[] renames)
    {
        foreach (var rename in renames.Where(rename => !File.Exists(Temp.Full(rename.Old))))
        {
            Temp.File(rename.Old);
        }

        var result = Run(renames.Select((rename, index) => Changed(index, rename.Old, rename.New)).ToArray());
        Assert.Equal(ExecutionOutcome.Succeeded, result.Execution!.Outcome);
        Assert.Null(result.JournalIssue);
        return result;
    }

    /// <summary>The bytes of last-batch.json.</summary>
    internal byte[] JournalBytes() => File.ReadAllBytes(LastBatchPath);

    /// <summary>The file names in the history root, sorted ordinal.</summary>
    internal IReadOnlyList<string> HistoryListing() =>
        Directory.GetFiles(Root).Select(path => Path.GetFileName(path)).OrderBy(name => name, StringComparer.Ordinal).ToList();

    /// <summary>The journal's entries as (old, new) full paths.</summary>
    internal static IEnumerable<(string Old, string New)> Pairs(IEnumerable<CompletedRename> renames) =>
        renames.Select(rename => (rename.OldPath, rename.NewPath));

    public void Dispose()
    {
        Temp.Dispose();
        History.Dispose();
        GC.SuppressFinalize(this);
    }
}
