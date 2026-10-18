using System;
using System.IO;
using System.Linq;
using System.Text;
using NameShift.Core;
using Xunit;

namespace NameShift.Disk.Tests;

public sealed class JournaledRenameTests : JournalTestBase
{
    [Fact]
    public void Rename_ThreeFiles_WritesTheJournal()
    {
        RenameFiles(("a", "x"), ("b", "y"), ("c", "z"));

        Assert.True(File.Exists(LastBatchPath));
        var last = Renamer.LoadLastBatch();
        Assert.Equal(LastBatchState.Revertable, last.State);
        Assert.True(last.CanRevert);
        Assert.Equal(Now, last.Time);
        Assert.Equal([Temp.Path], last.Folders);
        Assert.Equal(
            [(Temp.Full("a"), Temp.Full("x")), (Temp.Full("b"), Temp.Full("y")), (Temp.Full("c"), Temp.Full("z"))],
            Pairs(last.Renames));
        Assert.All(last.Renames, rename => Assert.Equal(ItemKind.File, rename.Kind));
        var text = Encoding.UTF8.GetString(JournalBytes());
        Assert.Contains("\"version\": 1", text);
        Assert.Contains("\"state\": \"done\"", text);
        Assert.Contains("\"time\": \"2026-10-18T14:03:07.1234567+00:00\"", text);
    }

    [Fact]
    public void DefaultHistoryRoot_IsUnderLocalAppData() =>
        Assert.Equal(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NameShift", "history"),
            JournaledRenamer.DefaultHistoryRoot);

    [Fact]
    public void Rename_FolderAndItemInside_JournalsBothWithFinalPaths()
    {
        Temp.Folder("X");
        Temp.File(@"X\a.txt");

        Run(Changed(0, "X", "Y", ItemKind.Folder), Changed(1, "a.txt", "b.txt", sub: "X"));

        var last = Renamer.LoadLastBatch();
        Assert.Equal(
            [(Temp.Full(@"X\a.txt"), Temp.Full(@"Y\b.txt")), (Temp.Full("X"), Temp.Full("Y"))],
            Pairs(last.Renames));
        Assert.Equal([ItemKind.File, ItemKind.Folder], last.Renames.Select(rename => rename.Kind));
        Assert.Equal([Temp.Full("X"), Temp.Path], last.Folders);
    }

    [Fact]
    public void Rename_RolledBack_WritesNoEntryAndKeepsThePrevious()
    {
        RenameFiles(("a", "b"));
        var before = JournalBytes();
        Temp.File("c");
        Temp.File("d");

        RenameRunResult result;
        using (new FileStream(Temp.Full("d"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            result = Run(Changed(0, "c", "e"), Changed(1, "d", "f"));
        }

        Assert.Equal(ExecutionOutcome.RolledBack, result.Execution!.Outcome);
        Assert.Null(result.JournalIssue);
        Assert.Equal(before, JournalBytes());
        Assert.False(File.Exists(PendingPath));
        var last = Renamer.LoadLastBatch();
        Assert.True(last.CanRevert);
        Assert.Equal([(Temp.Full("a"), Temp.Full("b"))], Pairs(last.Renames));
    }

    [Fact]
    public void Rename_RolledBackWithNoPrevious_CreatesNoJournal()
    {
        Temp.File("a");

        RenameRunResult result;
        using (new FileStream(Temp.Full("a"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            result = Run(Changed(0, "a", "b"));
        }

        Assert.Equal(ExecutionOutcome.RolledBack, result.Execution!.Outcome);
        Assert.False(File.Exists(LastBatchPath));
        Assert.False(File.Exists(PendingPath));
        Assert.Equal(LastBatchState.None, Renamer.LoadLastBatch().State);
    }

    [Fact]
    public void Rename_RefusedOrNothingToDo_NeverTouchesTheJournal()
    {
        Temp.File("a");
        Temp.File("b");
        var conflict = new ConflictReport([Array.Empty<Conflict>(), [new Conflict(ConflictCode.DuplicateTarget)]]);
        var error = new RenameResult(
            new RenameItem("b", Temp.Path), 1, "b", RenameStatus.Error, new RenameError(RuleStage.Numbering, "NumberingRule", "boom"));
        var unchanged = new RenameResult(new RenameItem("a", Temp.Path), 0, "a", RenameStatus.Unchanged, null);

        var refused = Renamer.Rename([Changed(0, "a", "x"), Changed(1, "b", "y")], conflict);
        var ruleError = Renamer.Rename([Changed(0, "a", "x"), error], NoConflicts(2));
        var nothing = Renamer.Rename([unchanged], NoConflicts(1));

        Assert.Equal(FailureReason.HasConflicts, refused.Execution!.Failure!.Reason);
        Assert.Equal(FailureReason.HasRuleErrors, ruleError.Execution!.Failure!.Reason);
        Assert.Equal(ExecutionOutcome.Succeeded, nothing.Execution!.Outcome);
        Assert.All(new[] { refused, ruleError, nothing }, result => Assert.Null(result.JournalIssue));
        Assert.Equal(0, Files.Reads);
        Assert.Empty(Files.Writes);
        Assert.Empty(Files.Deletes);
        Assert.False(Directory.Exists(Root));
        Assert.Equal(["a", "b"], Temp.Listing());
    }

    [Fact]
    public void Rename_InvalidArguments_ThrowWithoutTouchingTheJournal()
    {
        Assert.Throws<ArgumentNullException>(() => Renamer.Rename(null!, NoConflicts(0)));
        Assert.Throws<ArgumentNullException>(() => Renamer.Rename([], null!));
        Assert.Throws<ArgumentException>(() => Renamer.Rename([Changed(0, "a", "x")], NoConflicts(2)));
        Assert.Empty(Files.Writes);
    }

    [Fact]
    public void Rename_Twice_KeepsOnlyTheLastBatch()
    {
        RenameFiles(("a", "b"));
        RenameFiles(("b", "c"), ("d", "e"));

        var last = Renamer.LoadLastBatch();
        Assert.Equal([(Temp.Full("b"), Temp.Full("c")), (Temp.Full("d"), Temp.Full("e"))], Pairs(last.Renames));
        Assert.Equal(["last-batch.json"], HistoryListing());
    }

    [Fact]
    public void Rename_AfterACorruptJournal_ReplacesIt()
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(LastBatchPath, "{ not json");
        Assert.Equal(LastBatchState.Unreadable, Renamer.LoadLastBatch().State);

        RenameFiles(("a", "b"));

        var last = Renamer.LoadLastBatch();
        Assert.Equal(LastBatchState.Revertable, last.State);
        Assert.Equal([(Temp.Full("a"), Temp.Full("b"))], Pairs(last.Renames));
        Assert.Equal(["last-batch.json"], HistoryListing());
    }

    [Fact]
    public void Constructor_RelativeRoot_Throws() =>
        Assert.Throws<ArgumentException>(() => new JournaledRenamer(@"relative\history"));
}
