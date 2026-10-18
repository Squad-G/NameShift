using System;
using System.IO;
using System.Linq;
using NameShift.Core;
using NameShift.Disk;

namespace NameShift.Ui.Tests.Actions;

/// <summary>Real Disk result objects, built through their validating internal constructors: an impossible combination
/// throws here, so a test can never feed the window a result the renamer could not return.</summary>
internal static class DiskResults
{
    public const int SharingViolation = unchecked((int)0x80070020);

    public static CompletedRename Done(string oldPath, string newPath, ItemKind kind = ItemKind.File) =>
        new CompletedRename(oldPath, newPath, kind);

    /// <summary>n renames in C:\A: f0.txt to x_f0.txt, and so on.</summary>
    public static CompletedRename[] Renames(int count, string folder = @"C:\A") =>
        Enumerable.Range(0, count)
            .Select(i => Done(Path.Combine(folder, $"f{i}.txt"), Path.Combine(folder, $"x_f{i}.txt")))
            .ToArray();

    public static ExecutionFailure Failure(FailureReason reason, string? itemPath = null, string? blockingPath = null, string? systemMessage = null) =>
        new ExecutionFailure(reason, itemPath, blockingPath, systemMessage, systemMessage == null ? null : SharingViolation);

    public static StrandedItem Stranded(string currentPath, string originalPath, string? systemMessage = "Access is denied.") =>
        new StrandedItem(currentPath, originalPath, ItemKind.File, systemMessage);

    public static JournalWriteFailure Journal(JournalWritePhase phase, string message = "The disk is full.") =>
        new JournalWriteFailure(phase, new IOException(message));

    public static RenameRunResult Succeeded(int count, JournalWriteFailure? journalIssue = null) =>
        new RenameRunResult(new ExecutionResult(ExecutionOutcome.Succeeded, Renames(count)), journalIssue);

    public static RenameRunResult Failed(ExecutionOutcome outcome, ExecutionFailure failure, JournalWriteFailure? journalIssue = null) =>
        new RenameRunResult(new ExecutionResult(outcome, failure: failure), journalIssue);

    public static RenameRunResult NeedsAttention(ExecutionFailure failure, params StrandedItem[] stranded) =>
        new RenameRunResult(new ExecutionResult(ExecutionOutcome.NeedsAttention, failure: failure, needsAttention: stranded), null);

    public static RenameRunResult NotStarted(string message) =>
        new RenameRunResult(null, Journal(JournalWritePhase.BeforeStart, message));

    public static SkippedRevert Skip(CompletedRename entry, RevertSkipReason reason, string? blockingPath = null, string? systemMessage = null) =>
        new SkippedRevert(entry, reason, blockingPath, systemMessage, systemMessage == null ? null : SharingViolation);

    public static RevertResult Revert(
        RevertOutcome outcome,
        CompletedRename[]? reverted = null,
        SkippedRevert[]? notReverted = null,
        ExecutionFailure? failure = null,
        StrandedItem[]? needsAttention = null,
        JournalWriteFailure? journalIssue = null) =>
        new RevertResult(outcome, reverted, notReverted, failure, needsAttention, journalIssue);

    /// <summary>A journal read: Revertable has count renames in the folder, Reverted has a time, the others nothing.</summary>
    public static LastBatch Last(LastBatchState state, int count = 3, string folder = @"C:\A")
    {
        DateTimeOffset time = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        switch (state)
        {
            case LastBatchState.Revertable:
                return new LastBatch(state, time, new[] { folder }, Renames(count, folder));
            case LastBatchState.Reverted:
                return new LastBatch(state, time);
            default:
                return new LastBatch(state);
        }
    }
}
