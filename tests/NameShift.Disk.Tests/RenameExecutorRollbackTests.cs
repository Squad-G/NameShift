using System;
using System.IO;
using System.Linq;
using NameShift.Core;
using Xunit;

namespace NameShift.Disk.Tests;

public sealed class RenameExecutorRollbackTests : ExecutorTestBase
{
    private static readonly string[] Originals = ["f1", "f2", "f3", "f4", "f5"];

    private PlannedRename[] FiveFiles()
    {
        foreach (var name in Originals)
        {
            Temp.File(name);
        }

        return Originals.Select(name => Rename(name, "g" + name.Substring(1))).ToArray();
    }

    private void AssertFiveBack()
    {
        Assert.Equal(Originals, Temp.Listing());
        Assert.Equal(Originals, Originals.Select(Temp.Content));
    }

    [Fact]
    public void Execute_ThirdOfFiveLocked_RollsBackAll()
    {
        var renames = FiveFiles();
        ExecutionResult result;
        using (new FileStream(Temp.Full("f3"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            result = Executor.Execute(renames);
        }

        Assert.Equal(ExecutionOutcome.RolledBack, result.Outcome);
        Assert.Equal(FailureReason.RenameFailed, result.Failure!.Reason);
        Assert.Equal(Temp.Full("f3"), result.Failure.ItemPath);
        Assert.NotNull(result.Failure.SystemMessage);
        Assert.NotNull(result.Failure.HResult);
        Assert.Empty(result.Renamed);
        Assert.Empty(result.NeedsAttention);
        AssertFiveBack();
    }

    [Fact]
    public void Execute_ThirdOfFiveRemovedMidBatch_RollsBackTheOthers()
    {
        var renames = FiveFiles();
        Disk.BeforeMove = (call, _, _) =>
        {
            if (call == 2)
            {
                File.Delete(Temp.Full("f3"));
            }
        };

        var result = Executor.Execute(renames);

        Assert.Equal(ExecutionOutcome.RolledBack, result.Outcome);
        Assert.Equal(FailureReason.SourceMissing, result.Failure!.Reason);
        Assert.Equal(Temp.Full("f3"), result.Failure.ItemPath);
        Assert.Equal(["f1", "f2", "f4", "f5"], Temp.Listing());
        Assert.DoesNotContain(Disk.Moves, move => move.From == Temp.Full("f3"));
    }

    [Fact]
    public void Execute_FolderWithALockedFileInside_RollsBack()
    {
        foreach (var name in new[] { "d1", "d2", "d3" })
        {
            Temp.Folder(name);
            Temp.File(Path.Combine(name, "inside.txt"));
        }

        ExecutionResult result;
        using (new FileStream(Temp.Full(@"d2\inside.txt"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            result = Executor.Execute(
                [Rename("d1", "e1", ItemKind.Folder), Rename("d2", "e2", ItemKind.Folder), Rename("d3", "e3", ItemKind.Folder)]);
        }

        Assert.Equal(ExecutionOutcome.RolledBack, result.Outcome);
        Assert.Equal(FailureReason.RenameFailed, result.Failure!.Reason);
        Assert.Equal(Temp.Full("d2"), result.Failure.ItemPath);
        Assert.Equal(["d1", "d2", "d3"], Temp.Listing());
        Assert.Equal(["inside.txt"], Temp.Listing("d1"));
    }

    [Fact]
    public void Execute_RollbackStepFails_ListsNeedsAttentionWithBothNames()
    {
        var renames = FiveFiles();
        FileStream? lockOnG2 = null;
        Disk.BeforeMove = (call, _, _) =>
        {
            if (call == 3)
            {
                lockOnG2 = new FileStream(Temp.Full("g2"), FileMode.Open, FileAccess.Read, FileShare.None);
            }
        };
        Disk.FailMove = (from, _) => from == Temp.Full("f3") ? new IOException("f3 failed") : null;

        ExecutionResult result;
        try
        {
            result = Executor.Execute(renames);
        }
        finally
        {
            lockOnG2?.Dispose();
        }

        Assert.Equal(ExecutionOutcome.NeedsAttention, result.Outcome);
        Assert.Equal(FailureReason.RenameFailed, result.Failure!.Reason);
        Assert.Equal("f3 failed", result.Failure.SystemMessage);
        var stranded = Assert.Single(result.NeedsAttention);
        Assert.Equal(Temp.Full("g2"), stranded.CurrentPath);
        Assert.Equal(Temp.Full("f2"), stranded.OriginalPath);
        Assert.Equal(ItemKind.File, stranded.Kind);
        Assert.NotNull(stranded.SystemMessage);
        Assert.Equal(["f1", "f3", "f4", "f5", "g2"], Temp.Listing());
        Assert.Equal("f1", Temp.Content("f1"));
        Assert.Equal("f2", Temp.Content("g2"));
    }

    [Fact]
    public void Execute_RollbackFailsOnATempName_ReportsTheTempPath()
    {
        Temp.File("a");
        Temp.File("b");
        Disk.FailMove = (from, to) =>
            (from == Temp.Full("b") && to == Temp.Full("a")) || (from == TempName(1) && to == Temp.Full("a"))
                ? new IOException("refused")
                : null;

        var result = Executor.Execute([Rename("a", "b"), Rename("b", "a")]);

        Assert.Equal(ExecutionOutcome.NeedsAttention, result.Outcome);
        var stranded = Assert.Single(result.NeedsAttention);
        Assert.Equal(TempName(1), stranded.CurrentPath);
        Assert.Equal(Temp.Full("a"), stranded.OriginalPath);
        Assert.Equal("refused", stranded.SystemMessage);
        Assert.Equal("b", Temp.Content("b"));
        Assert.Equal("a", Temp.Content($"~ns-{Token}-1"));
    }

    [Fact]
    public void Execute_TargetCreatedMidBatch_StopsAndRollsBack()
    {
        var renames = FiveFiles();
        Disk.BeforeMove = (call, _, _) =>
        {
            if (call == 2)
            {
                Temp.File("g3", "intruder");
            }
        };

        var result = Executor.Execute(renames);

        Assert.Equal(ExecutionOutcome.RolledBack, result.Outcome);
        Assert.Equal(FailureReason.TargetInTheWay, result.Failure!.Reason);
        Assert.Equal(Temp.Full("f3"), result.Failure.ItemPath);
        Assert.Equal(Temp.Full("g3"), result.Failure.BlockingPath);
        Assert.DoesNotContain(Disk.Moves, move => move.To == Temp.Full("g3"));
        Assert.Equal("intruder", Temp.Content("g3"));
        Assert.Equal(["f1", "f2", "f3", "f4", "f5", "g3"], Temp.Listing());
        Assert.Equal(Originals, Originals.Select(Temp.Content));
    }

    [Theory]
    [InlineData(ItemKind.File)]
    [InlineData(ItemKind.Folder)]
    public void Execute_TargetHiddenFromTheCheck_TheRenameCallRefusesToReplace(ItemKind kind)
    {
        if (kind == ItemKind.File)
        {
            Temp.File("a");
        }
        else
        {
            Temp.Folder("a");
            Temp.File(@"a\inside.txt");
        }

        Temp.File("x", "intruder");
        Disk.HideFromExists = path => path == Temp.Full("x");

        var result = Executor.Execute([Rename("a", "x", kind)]);

        Assert.Equal(ExecutionOutcome.RolledBack, result.Outcome);
        Assert.Equal(FailureReason.RenameFailed, result.Failure!.Reason);
        Assert.Single(Disk.Moves);
        Assert.Equal("intruder", Temp.Content("x"));
        Assert.Equal(["a", "x"], Temp.Listing());
    }

    [Fact]
    public void Execute_ExceptionOfAnyType_RollsBack()
    {
        var renames = FiveFiles();
        Disk.FailMove = (from, _) => from == Temp.Full("f2") ? new InvalidOperationException("odd") : null;

        var result = Executor.Execute(renames);

        Assert.Equal(ExecutionOutcome.RolledBack, result.Outcome);
        Assert.Equal(FailureReason.RenameFailed, result.Failure!.Reason);
        Assert.Equal("odd", result.Failure.SystemMessage);
        AssertFiveBack();
    }
}
