using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using NameShift.Disk;
using NameShift.Ui.Actions;
using Xunit;
using UiStrings = NameShift.Ui.Strings;

namespace NameShift.Cmd.Tests;

/// <summary>What undo does with each journal state and each revert outcome, with a fake renamer (no file moves, no
/// journal).</summary>
public sealed class CommandRunnerUndoTests
{
    private static readonly string[] Undo = { "undo" };

    private static FakeBatchRenamer Revertable(RevertResult result, int renames = 3) => new FakeBatchRenamer
    {
        LastBatch = CmdResults.Batch(LastBatchState.Revertable, CmdResults.Renames(renames)),
        RevertResult = result,
    };

    [Fact]
    public void Reverted_WindowWords_Exit0()
    {
        var renamer = Revertable(CmdResults.Reverted(3));

        var run = Cmd.Run(renamer, @"C:\nowhere", Undo);

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written(ActionText.ForRevert(renamer.RevertResult!, Cmd.EnUs).Status), run.Output);
        Assert.Equal(Cmd.Written("3 names restored."), run.Output);
        Assert.Equal("", run.Error);
        Assert.Equal(1, renamer.LoadCalls);
        Assert.Equal(1, renamer.RevertCalls);
        Assert.Equal(0, renamer.RenameCalls);

        var one = Cmd.Run(Revertable(CmdResults.Reverted(1), 1), @"C:\nowhere", Undo);

        Assert.Equal(0, one.Code);
        Assert.Equal(Cmd.Written("1 name restored."), one.Output);
    }

    [Theory]
    [InlineData(LastBatchState.None)]
    [InlineData(LastBatchState.Reverted)]
    public void NoneOrReverted_NothingToRevert_NoRevertCall(LastBatchState state)
    {
        var renamer = new FakeBatchRenamer { LastBatch = CmdResults.Batch(state) };

        var run = Cmd.Run(renamer, @"C:\nowhere", Undo);

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written(UiStrings.NothingToRevert), run.Output);
        Assert.Equal(Cmd.Written("There is nothing to revert."), run.Output);
        Assert.Equal("", run.Error);
        Assert.Equal(1, renamer.LoadCalls);
        Assert.Equal(0, renamer.RevertCalls);
    }

    [Theory]
    [InlineData(LastBatchState.Interrupted)]
    [InlineData(LastBatchState.Unreadable)]
    public void InterruptedOrUnreadable_Exit3_NoRevertCall(LastBatchState state)
    {
        // Revert() would answer NothingToRevert here: the scripted result proves it is never asked.
        var renamer = new FakeBatchRenamer { LastBatch = CmdResults.Batch(state), RevertResult = CmdResults.NothingToRevert() };

        var run = Cmd.Run(renamer, @"C:\nowhere", Undo);

        Assert.Equal(3, run.Code);
        Assert.Equal(Cmd.Written(Strings.UndoHistoryUnusable), run.Error);
        Assert.Equal("", run.Output);
        Assert.Equal(0, renamer.RevertCalls);
    }

    [Fact]
    public void Partly_Exit4_DetailsOnError()
    {
        var renamer = Revertable(CmdResults.PartlyReverted(2, 1));
        ActionReport report = ActionText.ForRevert(renamer.RevertResult!, Cmd.EnUs);

        var run = Cmd.Run(renamer, @"C:\nowhere", Undo);

        Assert.Equal(4, run.Code);
        Assert.NotNull(report.Details);
        Assert.Equal(Cmd.Written(report.Status, report.Details!), run.Error);
        Assert.Equal("", run.Output);
        Assert.Equal(1, renamer.RevertCalls);
    }

    [Fact]
    public void NothingReverted_Exit3_DetailsOnError()
    {
        var renamer = Revertable(CmdResults.NothingReverted(2));
        ActionReport report = ActionText.ForRevert(renamer.RevertResult!, Cmd.EnUs);

        var run = Cmd.Run(renamer, @"C:\nowhere", Undo);

        Assert.Equal(3, run.Code);
        Assert.NotNull(report.Details);
        Assert.Equal(Cmd.Written(report.Status, report.Details!), run.Error);
        Assert.Equal("", run.Output);
    }

    [Fact]
    public void NothingRevertedAfterFinishIssue_Exit3_SaysHistoryNotCleaned()
    {
        var renamer = Revertable(CmdResults.NothingReverted(2, afterFinishIssue: true));
        ActionReport report = ActionText.ForRevert(renamer.RevertResult!, Cmd.EnUs);

        var run = Cmd.Run(renamer, @"C:
owhere", Undo);

        Assert.Equal(3, run.Code);
        Assert.Equal("", run.Output);
        Assert.NotNull(report.Details);
        Assert.Equal(Cmd.Written(report.Status, report.Details!), run.Error);
        Assert.StartsWith(UiStrings.RevertNone, report.Status, StringComparison.Ordinal);
        Assert.Contains(UiStrings.HistoryNotSaved.Substring(0, UiStrings.HistoryNotSaved.IndexOf("{0}", StringComparison.Ordinal)), report.Status, StringComparison.Ordinal);
        Assert.Contains("The disk is full", run.Error, StringComparison.Ordinal);
        Assert.Equal(1, renamer.RevertCalls);
    }

    [Fact]
    public void Partly_25Skipped_DetailsCappedAt20()
    {
        var renamer = Revertable(CmdResults.PartlyReverted(1, 25), 26);
        ActionReport report = ActionText.ForRevert(renamer.RevertResult!, Cmd.EnUs);

        var run = Cmd.Run(renamer, @"C:\nowhere", Undo);

        Assert.Equal(4, run.Code);
        string[] lines = Cmd.Lines(run.Error);
        // The status, the skipped header, 20 skipped lines and the "more" line.
        Assert.Equal(1 + 1 + ActionText.DetailLines + 1, lines.Length);
        Assert.Equal(Cmd.Written(report.Status, report.Details!), run.Error);
        Assert.Equal(report.Status, lines[0]);
        Assert.Equal("...and 5 more.", lines[lines.Length - 1]);
    }

    [Fact]
    public void NotStarted_Exit3()
    {
        var renamer = Revertable(CmdResults.RevertNotStarted("The disk is full."));

        var run = Cmd.Run(renamer, @"C:\nowhere", Undo);

        Assert.Equal(3, run.Code);
        Assert.Equal(Cmd.Written(ActionText.ForRevert(renamer.RevertResult!, Cmd.EnUs).Status), run.Error);
        Assert.Equal("", run.Output);
    }

    [Fact]
    public void NeedsAttention_Exit4()
    {
        var renamer = Revertable(CmdResults.RevertNeedsAttention());
        ActionReport report = ActionText.ForRevert(renamer.RevertResult!, Cmd.EnUs);

        var run = Cmd.Run(renamer, @"C:\nowhere", Undo);

        Assert.Equal(4, run.Code);
        Assert.NotNull(report.Details);
        Assert.Equal(Cmd.Written(report.Status, report.Details!), run.Error);
        Assert.Equal("", run.Output);
    }

    [Fact]
    public void RevertedAfterFinishIssue_Exit4_OnError()
    {
        var renamer = Revertable(CmdResults.Reverted(3, afterFinishIssue: true));
        ActionReport report = ActionText.ForRevert(renamer.RevertResult!, Cmd.EnUs);

        var run = Cmd.Run(renamer, @"C:\nowhere", Undo);

        Assert.Equal(4, run.Code);
        Assert.StartsWith("3 names restored.", report.Status, StringComparison.Ordinal);
        Assert.Equal(report.Details == null ? Cmd.Written(report.Status) : Cmd.Written(report.Status, report.Details), run.Error);
        Assert.Equal("", run.Output);
    }

    [Fact]
    public void NothingToRevertAnswer_AfterTheJournalSaidRevertable_Exit0OnOutput()
    {
        // Another process reverted between the read and the Revert call.
        var renamer = Revertable(CmdResults.NothingToRevert());

        var run = Cmd.Run(renamer, @"C:\nowhere", Undo);

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written(UiStrings.NothingToRevert), run.Output);
        Assert.Equal("", run.Error);
    }

    [Fact]
    public void RevertThrows_Exit4_ActionUnexpected()
    {
        var renamer = Revertable(CmdResults.Reverted(3));
        renamer.RevertThrows = new InvalidOperationException("boom");

        var run = Cmd.Run(renamer, @"C:\nowhere", Undo);

        Assert.Equal(4, run.Code);
        Assert.Equal(Cmd.Written(UiStrings.ActionUnexpected), run.Error);
        Assert.Equal("", run.Output);
        Assert.Equal(1, renamer.RevertCalls);
    }

    [Fact]
    public void LoadThrows_Exit3_UnexpectedBeforeUndo()
    {
        // LastBatch is null: the fake throws NotSupportedException from LoadLastBatch.
        var renamer = new FakeBatchRenamer { RevertResult = CmdResults.Reverted(3) };

        var run = Cmd.Run(renamer, @"C:\nowhere", Undo);

        Assert.Equal(3, run.Code);
        Assert.Equal(Cmd.Written(Strings.UnexpectedBeforeUndo), run.Error);
        Assert.Equal("", run.Output);
        Assert.Equal(0, renamer.RevertCalls);
    }

    [Fact]
    public void ApplyCatchAll_Unchanged()
    {
        var renamer = new FakeBatchRenamer();
        string missing = Path.Combine(Path.GetTempPath(), "NameShift.Cmd.Tests", Guid.NewGuid().ToString("N"));
        var files = new ThrowingFileListSource();

        var run = Cmd.Run(renamer, missing, new[] { "apply", Path.GetTempPath(), "--case", "lower" }, files);

        Assert.Equal(3, run.Code);
        Assert.Equal(Cmd.Written(Strings.UnexpectedBeforeRename), run.Error);
    }

    [Fact]
    public void UndoThenApply_OnOneRunner_UsesTheApplyCatchAll()
    {
        // The undo flag is per run: a later apply on the same runner still says "renamed".
        var output = new StringWriter(CultureInfo.InvariantCulture);
        var error = new StringWriter(CultureInfo.InvariantCulture);
        var renamer = new FakeBatchRenamer { LastBatch = CmdResults.Batch(LastBatchState.None) };
        var runner = new CommandRunner(output, error, Path.GetTempPath(), renamer, new ThrowingFileListSource(), Cmd.EnUs, "1.2.3", Cmd.UnusedPresets());

        Assert.Equal(0, runner.Run(Undo));
        int code = runner.Run(new[] { "apply", Path.GetTempPath(), "--case", "lower" });

        Assert.Equal(3, code);
        Assert.EndsWith(Cmd.Written(Strings.UnexpectedBeforeRename), error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void DryRun_ListsJournal_NoRevert()
    {
        var renamer = new FakeBatchRenamer
        {
            LastBatch = CmdResults.Batch(
                LastBatchState.Revertable,
                CmdResults.Done(@"C:\A\a.txt", @"C:\A\x_a.txt"),
                CmdResults.Done(@"C:\B\b.txt", @"C:\B\x_b.txt")),
        };

        var run = Cmd.Run(renamer, @"C:\nowhere", new[] { "undo", "--dry-run" });

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written("x_a.txt -> a.txt", "x_b.txt -> b.txt"), run.Output);
        Assert.Equal("", run.Error);
        Assert.Equal(1, renamer.LoadCalls);
        Assert.Equal(0, renamer.RevertCalls);
    }

    [Fact]
    public void DryRun_NoBatch_AsCriteria3And4()
    {
        var none = new FakeBatchRenamer { LastBatch = CmdResults.Batch(LastBatchState.None) };
        var noneRun = Cmd.Run(none, @"C:\nowhere", new[] { "undo", "--dry-run" });

        Assert.Equal(0, noneRun.Code);
        Assert.Equal(Cmd.Written(UiStrings.NothingToRevert), noneRun.Output);
        Assert.Equal("", noneRun.Error);

        var unreadable = new FakeBatchRenamer { LastBatch = CmdResults.Batch(LastBatchState.Unreadable) };
        var unreadableRun = Cmd.Run(unreadable, @"C:\nowhere", new[] { "undo", "--dry-run" });

        Assert.Equal(3, unreadableRun.Code);
        Assert.Equal(Cmd.Written(Strings.UndoHistoryUnusable), unreadableRun.Error);
        Assert.Equal("", unreadableRun.Output);
        Assert.Equal(0, none.RevertCalls + unreadable.RevertCalls);
    }

    [Fact]
    public void LargeBatch_NoPrompt()
    {
        var renamer = Revertable(CmdResults.Reverted(600), 600);

        var run = Cmd.Run(renamer, @"C:\nowhere", Undo);

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written("600 names restored."), run.Output);
        Assert.Equal(1, renamer.RevertCalls);
    }

    [Fact]
    public void AnyCurrentDirectory()
    {
        string missing = Path.Combine(Path.GetTempPath(), "NameShift.Cmd.Tests", Guid.NewGuid().ToString("N"), "gone");
        var reference = Cmd.Run(Revertable(CmdResults.Reverted(3)), @"C:\nowhere", Undo);

        var run = Cmd.Run(Revertable(CmdResults.Reverted(3)), missing, Undo);

        Assert.Equal(reference, run);
        Assert.Equal(0, run.Code);
    }

    [Fact]
    public void Apply_NeverReadsTheJournal()
    {
        string folder = Path.Combine(Path.GetTempPath(), "NameShift.Cmd.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllText(Path.Combine(folder, "A.TXT"), "x");
            var renamer = new FakeBatchRenamer { Result = CmdResults.Succeeded(1) };

            var dry = Cmd.Run(renamer, folder, new[] { "apply", folder, "--case", "lower", "--dry-run" });
            var real = Cmd.Run(renamer, folder, new[] { "apply", folder, "--case", "lower" });

            Assert.Equal(0, dry.Code);
            Assert.Equal(0, real.Code);
            Assert.Equal(1, renamer.RenameCalls);
            Assert.Equal(0, renamer.LoadCalls);
            Assert.Equal(0, renamer.RevertCalls);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    private sealed class ThrowingFileListSource : NameShift.Ui.Files.IFileListSource
    {
        public NameShift.Ui.Files.FileListing ListEntries(string folderPath, bool includeSubfolders, CancellationToken cancellationToken) => throw new InvalidOperationException("listing failed");
    }
}
