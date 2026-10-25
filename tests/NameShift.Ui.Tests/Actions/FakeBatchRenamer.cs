using System;
using System.Collections.Generic;
using System.Threading;
using NameShift.Core;
using NameShift.Disk;
using NameShift.Ui.Actions;
using NameShift.Ui.Tests.Files;

namespace NameShift.Ui.Tests.Actions;

/// <summary>An in-memory renamer: returns the configured results, records its calls, and can block, throw, or run an
/// action inside Rename and Revert (to change the fake folder, as the disk would). Touches no disk.</summary>
internal sealed class FakeBatchRenamer : IBatchRenamer
{
    private int _loadCalls;
    private int _renameCalls;
    private int _revertCalls;

    /// <summary>What each journal read returns, unless Load is set.</summary>
    public LastBatch Last { get; set; } = DiskResults.Last(LastBatchState.None);

    /// <summary>Replaces Last for each journal read (e.g. to block one read).</summary>
    public Func<LastBatch>? Load { get; set; }

    public RenameRunResult RenameResult { get; set; } = DiskResults.Succeeded(3);

    public RevertResult RevertResult { get; set; } = DiskResults.Revert(RevertOutcome.Reverted, DiskResults.Renames(3));

    /// <summary>What CheckRevert returns.</summary>
    public RevertCheck CheckResult { get; set; } = RevertCheck.Empty;

    /// <summary>The includeModified of the last Revert call, or null before any.</summary>
    public bool? LastIncludeModified { get; private set; }

    public int CheckCalls { get; private set; }

    /// <summary>Runs inside CheckRevert.</summary>
    public Action? CheckDuring { get; set; }

    /// <summary>When true, CheckRevert throws after CheckDuring.</summary>
    public bool CheckThrows { get; set; }

    /// <summary>Runs inside Rename and Revert, after the gate.</summary>
    public Action? During { get; set; }

    /// <summary>When set, Rename and Revert wait for it.</summary>
    public ManualResetEventSlim? Gate { get; set; }

    /// <summary>When true, Rename and Revert throw after During.</summary>
    public bool Throws { get; set; }

    public int LoadCalls => Volatile.Read(ref _loadCalls);

    public int RenameCalls => Volatile.Read(ref _renameCalls);

    public int RevertCalls => Volatile.Read(ref _revertCalls);

    public IReadOnlyList<RenameResult>? Results { get; private set; }

    public ConflictReport? Conflicts { get; private set; }

    public LastBatch LoadLastBatch()
    {
        Interlocked.Increment(ref _loadCalls);
        return Load != null ? Load() : Last;
    }

    public RenameRunResult Rename(IReadOnlyList<RenameResult> results, ConflictReport conflicts)
    {
        Interlocked.Increment(ref _renameCalls);
        Results = results;
        Conflicts = conflicts;
        Act();
        return RenameResult;
    }

    public RevertCheck CheckRevert()
    {
        CheckCalls++;
        CheckDuring?.Invoke();
        if (CheckThrows)
        {
            throw new InvalidOperationException("A bug in the check.");
        }

        return CheckResult;
    }

    public RevertResult Revert(bool includeModified = false)
    {
        Interlocked.Increment(ref _revertCalls);
        LastIncludeModified = includeModified;
        Act();
        return RevertResult;
    }

    private void Act()
    {
        if (Gate != null)
        {
            TestWait.Block(Gate);
        }
        During?.Invoke();
        if (Throws)
        {
            throw new InvalidOperationException("A bug in the renamer.");
        }
    }
}
