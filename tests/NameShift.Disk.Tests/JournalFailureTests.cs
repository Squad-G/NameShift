using System.IO;
using Xunit;

namespace NameShift.Disk.Tests;

public sealed class JournalFailureTests : JournalTestBase
{
    private const string LastBatchFile = "last-batch.json";
    private const string PendingFile = "pending.json";

    private static System.Func<string, System.Exception?> Fail(string fileName) =>
        name => name == fileName ? new IOException($"{fileName} refused") : null;

    [Fact]
    public void Rename_PendingWriteFails_DoesNotStart()
    {
        RenameFiles(("a", "b"));
        var journal = JournalBytes();
        var moves = Disk.Moves.Count;
        Temp.File("c");
        Files.FailWrite = Fail(PendingFile);

        var result = Run(Changed(0, "c", "d"));

        Assert.Null(result.Execution);
        Assert.Equal(JournalWritePhase.BeforeStart, result.JournalIssue!.Phase);
        Assert.Equal("pending.json refused", result.JournalIssue.SystemMessage);
        Assert.NotEqual(0, result.JournalIssue.HResult);
        Assert.Equal(moves, Disk.Moves.Count);
        Assert.Equal(["b", "c"], Temp.Listing());
        Assert.Equal(journal, JournalBytes());
        Assert.True(Renamer.LoadLastBatch().CanRevert);
    }

    [Fact]
    public void Rename_CommitFails_RenamedButRevertDisabled()
    {
        RenameFiles(("a", "b"));
        Temp.File("c");
        Files.FailWrite = Fail(LastBatchFile);

        var result = Run(Changed(0, "c", "d"));

        Assert.Equal(ExecutionOutcome.Succeeded, result.Execution!.Outcome);
        Assert.Equal(JournalWritePhase.AfterFinish, result.JournalIssue!.Phase);
        Assert.Equal(["b", "d"], Temp.Listing());
        Assert.Equal(LastBatchState.Interrupted, Renamer.LoadLastBatch().State);
        var restarted = NewRenamer();
        Assert.Equal(LastBatchState.Interrupted, restarted.LoadLastBatch().State);
        Assert.Equal(RevertOutcome.NothingToRevert, restarted.Revert().Outcome);
        Assert.Equal(["b", "d"], Temp.Listing());
    }

    [Fact]
    public void Rename_PendingDeleteFailsAfterSuccess_IsHarmless()
    {
        Files.FailDelete = Fail(PendingFile);

        var result = RenameFiles(("a", "b"));

        Assert.Null(result.JournalIssue);
        Assert.True(File.Exists(PendingPath));
        Assert.Equal(LastBatchState.Revertable, Renamer.LoadLastBatch().State);
    }

    [Fact]
    public void Rename_PendingDeleteFailsAfterRollback_ReportsAndDisablesRevert()
    {
        RenameFiles(("a", "b"));
        Temp.File("c");
        Files.FailDelete = Fail(PendingFile);

        RenameRunResult result;
        using (new FileStream(Temp.Full("c"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            result = Run(Changed(0, "c", "d"));
        }

        Assert.Equal(ExecutionOutcome.RolledBack, result.Execution!.Outcome);
        Assert.Equal(JournalWritePhase.AfterFinish, result.JournalIssue!.Phase);
        Assert.Equal(LastBatchState.Interrupted, Renamer.LoadLastBatch().State);
    }

    [Fact]
    public void Revert_PendingWriteFails_NotStarted()
    {
        RenameFiles(("a", "b"));
        var moves = Disk.Moves.Count;
        Files.FailWrite = Fail(PendingFile);

        var result = Renamer.Revert();

        Assert.Equal(RevertOutcome.NotStarted, result.Outcome);
        Assert.Equal(JournalWritePhase.BeforeStart, result.JournalIssue!.Phase);
        Assert.Equal(moves, Disk.Moves.Count);
        Assert.Equal(["b"], Temp.Listing());
        Assert.True(Renamer.LoadLastBatch().CanRevert);
    }

    [Fact]
    public void Revert_CommitFails_RevertedButDisabled()
    {
        RenameFiles(("a", "b"));
        Files.FailWrite = Fail(LastBatchFile);

        var result = Renamer.Revert();

        Assert.Equal(RevertOutcome.Reverted, result.Outcome);
        Assert.Equal(JournalWritePhase.AfterFinish, result.JournalIssue!.Phase);
        Assert.Equal(["a"], Temp.Listing());
        Assert.Equal(LastBatchState.Interrupted, Renamer.LoadLastBatch().State);
        Assert.Equal(RevertOutcome.NothingToRevert, Renamer.Revert().Outcome);
    }

    [Theory]
    [InlineData("{\"version\": 1, \"id\": \"crash\"}")]
    [InlineData("garbage")]
    public void Load_LeftoverPendingFromACrash_IsInterrupted(string pending)
    {
        RenameFiles(("a", "b"));
        File.WriteAllText(PendingPath, pending);

        Assert.Equal(LastBatchState.Interrupted, Renamer.LoadLastBatch().State);
        Assert.Equal(RevertOutcome.NothingToRevert, Renamer.Revert().Outcome);
        Assert.Equal(["b"], Temp.Listing());
        Assert.Equal(pending, File.ReadAllText(PendingPath));
    }

    [Fact]
    public void Load_PendingWithNoJournal_IsInterrupted()
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(PendingPath, "{\"version\": 1, \"id\": \"crash\"}");

        Assert.Equal(LastBatchState.Interrupted, Renamer.LoadLastBatch().State);
    }

    [Fact]
    public void Rename_AfterAnInterruption_MakesRevertAvailableAgain()
    {
        RenameFiles(("a", "b"));
        File.WriteAllText(PendingPath, "garbage");

        RenameFiles(("b", "c"));

        Assert.Equal(LastBatchState.Revertable, Renamer.LoadLastBatch().State);
        Assert.Equal(["last-batch.json"], HistoryListing());
    }
}
