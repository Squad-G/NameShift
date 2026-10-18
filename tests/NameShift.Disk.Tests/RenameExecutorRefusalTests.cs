using System;
using System.Collections.Generic;
using System.IO;
using NameShift.Core;
using Xunit;

namespace NameShift.Disk.Tests;

public sealed class RenameExecutorRefusalTests : ExecutorTestBase
{
    private static readonly IReadOnlyList<Conflict> NoConflict = Array.Empty<Conflict>();

    private RenameResult Changed(int index, string name, string newName) =>
        new(new RenameItem(name, Temp.Path), index, newName, RenameStatus.Changed, null);

    [Fact]
    public void Execute_PlanWithAConflict_RefusesAndChangesNothing()
    {
        Temp.File("a");
        Temp.File("b");
        var report = new ConflictReport([NoConflict, [new Conflict(ConflictCode.DuplicateTarget)]]);

        var result = Executor.Execute([Changed(0, "a", "x"), Changed(1, "b", "y")], report);

        Assert.Equal(ExecutionOutcome.Refused, result.Outcome);
        Assert.Equal(FailureReason.HasConflicts, result.Failure!.Reason);
        Assert.Null(result.Failure.ItemPath);
        Assert.Empty(Disk.Moves);
        Assert.Equal(0, Disk.Reads);
        Assert.Equal(["a", "b"], Temp.Listing());
    }

    [Fact]
    public void Execute_PlanWithARuleError_Refuses()
    {
        Temp.File("a");
        Temp.File("b");
        var error = new RenameResult(
            new RenameItem("b", Temp.Path), 1, "b", RenameStatus.Error, new RenameError(RuleStage.Numbering, "NumberingRule", "boom"));

        var result = Executor.Execute([Changed(0, "a", "x"), error], new ConflictReport([NoConflict, NoConflict]));

        Assert.Equal(ExecutionOutcome.Refused, result.Outcome);
        Assert.Equal(FailureReason.HasRuleErrors, result.Failure!.Reason);
        Assert.Empty(Disk.Moves);
        Assert.Equal(0, Disk.Reads);
        Assert.Equal(["a", "b"], Temp.Listing());
    }

    [Fact]
    public void Execute_ReportOfAnotherSize_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            Executor.Execute([Changed(0, "a", "x"), Changed(1, "b", "y")], new ConflictReport([NoConflict])));

    [Fact]
    public void Execute_CheckedPlan_RenamesTheChangedResultsOnly()
    {
        Temp.File("a");
        Temp.File("b");
        var unchanged = new RenameResult(new RenameItem("b", Temp.Path), 1, "b", RenameStatus.Unchanged, null);

        var result = Executor.Execute([Changed(0, "a", "x"), unchanged], new ConflictReport([NoConflict, NoConflict]));

        Assert.Equal(ExecutionOutcome.Succeeded, result.Outcome);
        Assert.Equal(["b", "x"], Temp.Listing());
        var renamed = Assert.Single(result.Renamed);
        Assert.Equal(Temp.Full("a"), renamed.OldPath);
        Assert.Equal(Temp.Full("x"), renamed.NewPath);
    }

    [Fact]
    public void Execute_SourceRemovedSincePreview_RefusesBeforeAnyRename()
    {
        Temp.File("a");
        Temp.File("c");

        var result = Executor.Execute([Rename("a", "x"), Rename("b", "y"), Rename("c", "z")]);

        Assert.Equal(ExecutionOutcome.Refused, result.Outcome);
        Assert.Equal(FailureReason.SourceMissing, result.Failure!.Reason);
        Assert.Equal(Temp.Full("b"), result.Failure.ItemPath);
        Assert.Empty(Disk.Moves);
        Assert.Equal(["a", "c"], Temp.Listing());
    }

    [Fact]
    public void Execute_FolderPlannedButAFileThere_RefusesSourceMissing()
    {
        Temp.File("a");

        var result = Executor.Execute([Rename("a", "x", ItemKind.Folder)]);

        Assert.Equal(ExecutionOutcome.Refused, result.Outcome);
        Assert.Equal(FailureReason.SourceMissing, result.Failure!.Reason);
        Assert.Empty(Disk.Moves);
    }

    [Fact]
    public void Execute_TargetCreatedAfterPreview_RefusesAndKeepsIt()
    {
        Temp.File("a");
        Temp.File("x", "intruder");

        var result = Executor.Execute([Rename("a", "x")]);

        AssertTargetInTheWayRefusal(result);
    }

    [Fact]
    public void Execute_TargetIsAHiddenSystemFile_RefusesAndKeepsIt()
    {
        Temp.File("a");
        var x = Temp.File("x", "intruder");
        File.SetAttributes(x, FileAttributes.Hidden | FileAttributes.System);

        var result = Executor.Execute([Rename("a", "x")]);

        AssertTargetInTheWayRefusal(result);
    }

    [Fact]
    public void Execute_TargetIsAFolder_Refuses()
    {
        Temp.File("a");
        Temp.Folder("x");

        var result = Executor.Execute([Rename("a", "x")]);

        Assert.Equal(ExecutionOutcome.Refused, result.Outcome);
        Assert.Equal(FailureReason.TargetInTheWay, result.Failure!.Reason);
        Assert.Equal(Temp.Full("x"), result.Failure.BlockingPath);
        Assert.Empty(Disk.Moves);
        Assert.Equal("a", Temp.Content("a"));
    }

    [Fact]
    public void Execute_EmptyPlan_SucceedsWithNothing()
    {
        var result = Executor.Execute(Array.Empty<PlannedRename>());

        Assert.Equal(ExecutionOutcome.Succeeded, result.Outcome);
        Assert.Null(result.Failure);
        Assert.Empty(result.Renamed);
        Assert.Empty(result.NeedsAttention);
        Assert.Empty(Disk.Moves);
        Assert.Equal(0, Disk.Reads);
    }

    [Theory]
    [InlineData("relative", "a", "b")]
    [InlineData("", "a", "b")]
    [InlineData(@"C:\Test", @"a\b", "c")]
    [InlineData(@"C:\Test", "a", "a/b")]
    [InlineData(@"C:\Test", "a", "a:b")]
    [InlineData(@"C:\Test", "a", "..")]
    [InlineData(@"C:\Test", ".", "a")]
    [InlineData(@"C:\Test", "", "a")]
    [InlineData(@"C:\Test", "a", "a")]
    public void PlannedRename_InvalidArguments_Throw(string folder, string oldName, string newName) =>
        Assert.ThrowsAny<ArgumentException>(() => new PlannedRename(folder, oldName, newName, ItemKind.File));

    [Fact]
    public void PlannedRename_CaseOnlyChange_IsAllowed() =>
        Assert.Equal("Photo.jpg", new PlannedRename(@"C:\Test", "photo.jpg", "Photo.jpg", ItemKind.File).NewName);

    [Fact]
    public void PlannedRename_UndefinedKind_Throws() =>
        Assert.ThrowsAny<ArgumentException>(() => new PlannedRename(@"C:\Test", "a", "b", (ItemKind)7));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Execute_TwoRenamesFromOrToTheSameName_Throws(bool sameOldName)
    {
        Temp.File("a");
        Temp.File("b");
        PlannedRename[] renames = sameOldName
            ? [Rename("a", "x"), Rename("A", "y")]
            : [Rename("a", "x"), Rename("b", "X")];

        Assert.Throws<ArgumentException>(() => Executor.Execute(renames));
        Assert.Empty(Disk.Moves);
        Assert.Equal(["a", "b"], Temp.Listing());
    }

    private void AssertTargetInTheWayRefusal(ExecutionResult result)
    {
        Assert.Equal(ExecutionOutcome.Refused, result.Outcome);
        Assert.Equal(FailureReason.TargetInTheWay, result.Failure!.Reason);
        Assert.Equal(Temp.Full("a"), result.Failure.ItemPath);
        Assert.Equal(Temp.Full("x"), result.Failure.BlockingPath);
        Assert.Empty(Disk.Moves);
        Assert.Equal("intruder", Temp.Content("x"));
        Assert.Equal("a", Temp.Content("a"));
    }
}
