using System;
using System.Collections.Generic;
using NameShift.Core;
using NameShift.Ui.Files;
using NameShift.Ui.Preview;
using Xunit;

namespace NameShift.Ui.Tests.Preview;

public sealed class RenamePlanTests
{
    private static readonly RenameItem[] Items =
    {
        FakeFileListSource.File("a.txt"),
        FakeFileListSource.File("b.txt"),
    };

    private static RenameResult[] ResultsOf(bool changed, bool error)
    {
        return new[]
        {
            changed
                ? new RenameResult(Items[0], 0, "x.txt", RenameStatus.Changed, null)
                : new RenameResult(Items[0], 0, "a.txt", RenameStatus.Unchanged, null),
            error
                ? new RenameResult(Items[1], 1, "b.txt", RenameStatus.Error, new RenameError(RuleStage.Replace, "Replace", "boom"))
                : new RenameResult(Items[1], 1, "b.txt", RenameStatus.Unchanged, null),
        };
    }

    private static ConflictReport ConflictsOf(bool conflict)
    {
        IReadOnlyList<Conflict> none = Array.Empty<Conflict>();
        IReadOnlyList<Conflict> first = conflict ? new[] { new Conflict(ConflictCode.TargetExists) } : none;
        return new ConflictReport(new[] { first, none });
    }

    [Theory]
    [InlineData(false, false, false, RenameBlocker.NothingToRename)]
    [InlineData(true, false, false, RenameBlocker.None)]
    [InlineData(false, true, false, RenameBlocker.RuleErrors)]
    [InlineData(true, true, false, RenameBlocker.RuleErrors)]
    [InlineData(false, false, true, RenameBlocker.Conflicts)]
    [InlineData(true, false, true, RenameBlocker.Conflicts)]
    [InlineData(false, true, true, RenameBlocker.ConflictsAndRuleErrors)]
    [InlineData(true, true, true, RenameBlocker.ConflictsAndRuleErrors)]
    public void Blocker_FollowsChangesConflictsAndErrors(bool changed, bool error, bool conflict, RenameBlocker expected)
    {
        var plan = new RenamePlan(ResultsOf(changed, error), ConflictsOf(conflict));

        Assert.Equal(expected, plan.Blocker);
    }

    [Fact]
    public void Counts_ErrorsAreNotChanged_ConflictRowsCountOnce()
    {
        var results = new[]
        {
            new RenameResult(Items[0], 0, "x.txt", RenameStatus.Changed, null),
            new RenameResult(Items[1], 1, "b.txt", RenameStatus.Error, new RenameError(RuleStage.Replace, "Replace", "boom")),
            new RenameResult(FakeFileListSource.File("c.txt"), 2, "c.txt", RenameStatus.Unchanged, null),
        };
        IReadOnlyList<Conflict> none = Array.Empty<Conflict>();
        IReadOnlyList<Conflict> two = new[] { new Conflict(ConflictCode.InvalidCharacter), new Conflict(ConflictCode.PathTooLong) };

        var plan = new RenamePlan(results, new ConflictReport(new[] { two, none, none }));

        Assert.Equal(1, plan.ChangedCount);
        Assert.Equal(1, plan.ErrorCount);
        Assert.Equal(1, plan.ConflictCount);
        Assert.True(plan.HasConflicts);
    }

    [Fact]
    public void Ctor_Validates()
    {
        Assert.Equal("results", Assert.Throws<ArgumentNullException>(() => new RenamePlan(null!, ConflictsOf(false))).ParamName);
        Assert.Equal("conflicts", Assert.Throws<ArgumentNullException>(() => new RenamePlan(ResultsOf(false, false), null!)).ParamName);
        var oneList = new ConflictReport(new IReadOnlyList<Conflict>[] { Array.Empty<Conflict>() });
        Assert.Equal("conflicts", Assert.Throws<ArgumentException>(() => new RenamePlan(ResultsOf(false, false), oneList)).ParamName);
    }

    [Theory]
    [InlineData(false, false, false, RenameBlocker.NothingToRename)]
    [InlineData(true, false, false, RenameBlocker.None)]
    [InlineData(false, true, false, RenameBlocker.RuleErrors)]
    [InlineData(true, true, false, RenameBlocker.RuleErrors)]
    [InlineData(false, false, true, RenameBlocker.Conflicts)]
    [InlineData(true, false, true, RenameBlocker.Conflicts)]
    [InlineData(false, true, true, RenameBlocker.ConflictsAndRuleErrors)]
    [InlineData(true, true, true, RenameBlocker.ConflictsAndRuleErrors)]
    public void Snapshot_Blocker_FollowsItsArguments(bool changed, bool error, bool conflict, RenameBlocker expected)
    {
        RenameResult[] results = ResultsOf(changed, error);
        ConflictReport conflicts = ConflictsOf(conflict);

        var fromItems = new PreviewSnapshot(Items, results, conflicts);
        var fromBatch = new PreviewSnapshot(FileBatch.WholeList(Items), results, conflicts);

        Assert.Equal(expected, fromItems.Blocker);
        Assert.Equal(expected, fromBatch.Blocker);
    }

    [Fact]
    public void Snapshot_Empty_IsNothingToRename()
    {
        Assert.Equal(RenameBlocker.NothingToRename, PreviewSnapshot.Empty.Blocker);
    }

    [Fact]
    public void Snapshot_FromPlan_ExposesThePlan()
    {
        var plan = new RenamePlan(ResultsOf(true, true), ConflictsOf(true));

        var snapshot = new PreviewSnapshot(FileBatch.WholeList(Items), plan);

        Assert.Same(plan, snapshot.Plan);
        Assert.Same(plan.Results, snapshot.Results);
        Assert.Same(plan.Conflicts, snapshot.Conflicts);
        Assert.Equal(plan.ChangedCount, snapshot.ChangedCount);
        Assert.Equal(plan.ErrorCount, snapshot.ErrorCount);
        Assert.Equal(plan.ConflictCount, snapshot.ConflictCount);
        Assert.Equal(plan.HasConflicts, snapshot.HasConflicts);
        Assert.Equal(plan.Blocker, snapshot.Blocker);
        FileBatch oneItem = FileBatch.WholeList(new[] { Items[0] });
        Assert.Equal("plan", Assert.Throws<ArgumentException>(() => new PreviewSnapshot(oneItem, plan)).ParamName);
    }
}
