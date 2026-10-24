using System;
using System.Collections.Generic;
using NameShift.Core;
using NameShift.Ui.Files;

namespace NameShift.Ui.Preview;

/// <summary>The input a preview run used and its output. Immutable.</summary>
public sealed class PreviewSnapshot
{
    /// <summary>No preview: no items and no results.</summary>
    public static readonly PreviewSnapshot Empty = new PreviewSnapshot(
        FileBatch.Empty,
        Array.Empty<RenameResult>(),
        new ConflictReport(Array.Empty<IReadOnlyList<Conflict>>()));

    /// <summary>Creates a snapshot of a run over every item of the list.</summary>
    public PreviewSnapshot(IReadOnlyList<RenameItem> items, IReadOnlyList<RenameResult> results,
        ConflictReport conflicts)
        : this(FileBatch.WholeList(items), results, conflicts)
    {
    }

    /// <summary>Creates a snapshot; there must be exactly one result per item of the batch.</summary>
    public PreviewSnapshot(FileBatch batch, IReadOnlyList<RenameResult> results, ConflictReport conflicts)
    {
        if (batch == null)
        {
            throw new ArgumentNullException(nameof(batch));
        }

        if (results == null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        if (conflicts == null)
        {
            throw new ArgumentNullException(nameof(conflicts));
        }

        if (batch.Count != results.Count)
        {
            throw new ArgumentException("There must be one result per item.", nameof(results));
        }

        Plan = new RenamePlan(results, conflicts);
        Batch = batch;
    }

    /// <summary>Creates a snapshot of a plan; there must be exactly one result per item of the batch.</summary>
    internal PreviewSnapshot(FileBatch batch, RenamePlan plan)
    {
        if (batch == null)
        {
            throw new ArgumentNullException(nameof(batch));
        }

        if (plan == null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        if (batch.Count != plan.Results.Count)
        {
            throw new ArgumentException("There must be one result per item.", nameof(plan));
        }

        Batch = batch;
        Plan = plan;
    }

    /// <summary>The batch the pipeline ran on, with the list it was taken from (compared with the file list's items).</summary>
    public FileBatch Batch { get; }

    /// <summary>The exact list the pipeline ran on.</summary>
    public IReadOnlyList<RenameItem> Items => Batch.Items;

    /// <summary>The plan of the batch: its results, conflicts, counts and Blocker.</summary>
    public RenamePlan Plan { get; }

    /// <summary>One result per item, Results[i].Index == i.</summary>
    public IReadOnlyList<RenameResult> Results => Plan.Results;

    /// <summary>One conflict list per result, by position.</summary>
    public ConflictReport Conflicts => Plan.Conflicts;

    /// <summary>Items with at least one conflict (a row with two codes counts once).</summary>
    public int ConflictCount => Plan.ConflictCount;

    /// <summary>Results whose status is Error (a rule failed).</summary>
    public int ErrorCount => Plan.ErrorCount;

    /// <summary>Results whose status is Changed: what a Rename would rename.</summary>
    public int ChangedCount => Plan.ChangedCount;

    /// <summary>ConflictCount > 0.</summary>
    public bool HasConflicts => Plan.HasConflicts;

    /// <summary>Why Rename cannot run the plan; None when it can.</summary>
    public RenameBlocker Blocker => Plan.Blocker;
}
