using System.Linq;
using NameShift.Core;
using Xunit;

namespace NameShift.Disk.Tests;

public sealed class RevertPlannerTests
{
    private const string Dir = @"C:\T";

    private static string P(string relative) => $@"{Dir}\{relative}";

    /// <summary>A journal entry: the item was at old, the batch left it at new.</summary>
    private static CompletedRename E(string oldName, string newName, ItemKind kind = ItemKind.File) =>
        new(P(oldName), P(newName), kind);

    private static (string? Old, RevertSkipReason? Reason, string? Blocking)[] Skips(RevertPlan plan) =>
        plan.Skipped.Select(skip => ((string?)skip.Entry.OldPath, (RevertSkipReason?)skip.Reason, skip.BlockingPath)).ToArray();

    [Fact]
    public void Plan_ItemRemoved_IsMissing()
    {
        var plan = RevertPlanner.Plan([E("a", "b")], new FakeFileSystem());

        Assert.Empty(plan.Included);
        Assert.Equal([(P("a"), RevertSkipReason.ItemMissing, null)], Skips(plan));
    }

    [Fact]
    public void Plan_FolderWhereTheFileWas_IsMissing()
    {
        var plan = RevertPlanner.Plan([E("a", "b")], new FakeFileSystem().With(P("b"), ItemKind.Folder));

        Assert.Equal([(P("a"), RevertSkipReason.ItemMissing, null)], Skips(plan));
    }

    [Fact]
    public void Plan_OldNameTaken_IsSkipped()
    {
        var plan = RevertPlanner.Plan([E("a", "b"), E("c", "d")], new FakeFileSystem().With(P("b")).With(P("d")).With(P("a")));

        Assert.Equal([P("c")], plan.Included.Select(entry => entry.OldPath));
        Assert.Equal([(P("a"), RevertSkipReason.NameTaken, P("a"))], Skips(plan));
    }

    [Fact]
    public void Plan_CaseOnlyChange_TargetsItselfAndIsIncluded()
    {
        var plan = RevertPlanner.Plan([E("photo.jpg", "Photo.jpg")], new FakeFileSystem().With(P("Photo.jpg")));

        Assert.Single(plan.Included);
        Assert.Empty(plan.Skipped);
    }

    [Fact]
    public void Plan_SwapWithBothPresent_IsIncluded()
    {
        var plan = RevertPlanner.Plan([E("a", "b"), E("b", "a")], new FakeFileSystem().With(P("a")).With(P("b")));

        Assert.Equal(2, plan.Included.Count);
    }

    [Fact]
    public void Plan_ChainBlocked_PropagatesToTheWaitingEntry()
    {
        var plan = RevertPlanner.Plan(
            [E("b", "c"), E("a", "b")], new FakeFileSystem().With(P("c")).With(P("b")).With(P("a")));

        Assert.Empty(plan.Included);
        Assert.Equal(
            [(P("b"), RevertSkipReason.WaitsForSkippedItem, P("b")), (P("a"), RevertSkipReason.NameTaken, P("a"))],
            Skips(plan));
    }

    [Fact]
    public void Plan_MissingEntryWhoseNameIsNowTakenBySomethingElse_BlocksTheEntryGoingThere()
    {
        // The folder X was renamed to Y, and c went to X; now a file sits at Y, so the folder is missing and the file
        // at Y is someone else's.
        var plan = RevertPlanner.Plan(
            [E("X", "Y", ItemKind.Folder), E("Y", "c")], new FakeFileSystem().With(P("Y")).With(P("c")));

        Assert.Equal(
            [(P("X"), RevertSkipReason.ItemMissing, null), (P("Y"), RevertSkipReason.NameTaken, P("Y"))],
            Skips(plan));
    }

    [Fact]
    public void Plan_MissingEntryWithAFreeName_DoesNotBlock()
    {
        var plan = RevertPlanner.Plan([E("a", "b"), E("b", "c")], new FakeFileSystem().With(P("c")));

        Assert.Equal([P("b")], plan.Included.Select(entry => entry.OldPath));
        Assert.Equal([(P("a"), RevertSkipReason.ItemMissing, null)], Skips(plan));
    }

    [Fact]
    public void Plan_FolderSkipped_ChildrenArePlannedInsideIt()
    {
        var plan = RevertPlanner.Plan(
            [E(@"X\a", @"Y\b"), E("X", "Y", ItemKind.Folder)],
            new FakeFileSystem().With(P("Y"), ItemKind.Folder).With(P(@"Y\b")).With(P("X")));

        Assert.Equal([P(@"X\a")], plan.Included.Select(entry => entry.OldPath));
        Assert.Equal([(P("X"), RevertSkipReason.NameTaken, P("X"))], Skips(plan));
    }

    [Fact]
    public void Plan_ChildrenOfAMissingFolder_AreMissing()
    {
        var plan = RevertPlanner.Plan([E(@"X\a", @"Y\b"), E("X", "Y", ItemKind.Folder)], new FakeFileSystem());

        Assert.Equal(
            [(P(@"X\a"), RevertSkipReason.ItemMissing, null), (P("X"), RevertSkipReason.ItemMissing, null)],
            Skips(plan));
    }

    [Fact]
    public void Plan_TenThousandRotationBlockedAtTheEnd_IsLinear()
    {
        const int Count = 10_000;
        var entries = Enumerable.Range(0, Count).Select(i => E($"f{i}", $"f{i + 1}")).ToList();
        var disk = new FakeFileSystem().With(P("f0"));
        for (var i = 1; i <= Count; i++)
        {
            disk.With(P($"f{i}"));
        }

        var plan = RevertPlanner.Plan(entries, disk);

        Assert.Empty(plan.Included);
        Assert.Equal(RevertSkipReason.NameTaken, plan.Skipped[0].Reason);
        Assert.All(plan.Skipped.Skip(1), skip => Assert.Equal(RevertSkipReason.WaitsForSkippedItem, skip.Reason));
        Assert.True(disk.Reads <= (2 * Count) + 2, $"{disk.Reads} disk reads");
    }
}
