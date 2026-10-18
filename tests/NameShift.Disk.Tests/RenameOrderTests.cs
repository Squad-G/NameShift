using System;
using System.Collections.Generic;
using System.Linq;
using NameShift.Core;
using Xunit;

namespace NameShift.Disk.Tests;

/// <summary>The pure planner: no disk, a fake temporary-name factory (~t1, ~t2, ...).</summary>
public sealed class RenameOrderTests
{
    private const string Folder = @"C:\F";

    private static IReadOnlyList<RenameStep> Plan(params PlannedRename[] renames)
    {
        var count = 0;
        var keys = renames.Select(rename => FolderKey.Of(rename.FolderPath)).ToArray();
        return RenameOrder.Plan(renames, keys, folder => $@"{folder}\~t{++count}");
    }

    private static PlannedRename R(string oldName, string newName, string folder = Folder, ItemKind kind = ItemKind.File) =>
        new(folder, oldName, newName, kind);

    private static IEnumerable<string> Moves(IEnumerable<RenameStep> steps) =>
        steps.Select(step => $"{Name(step.FromPath)}>{Name(step.ToPath)}");

    private static string Name(string path) => path.Substring(path.LastIndexOf('\\') + 1);

    [Fact]
    public void Plan_Chain_FreeTargetFirst() =>
        Assert.Equal(["b>c", "a>b"], Moves(Plan(R("a", "b"), R("b", "c"))));

    [Fact]
    public void Plan_IndependentRenames_KeepPlanOrder() =>
        Assert.Equal(["c>z", "a>x", "b>y"], Moves(Plan(R("c", "z"), R("a", "x"), R("b", "y"))));

    [Fact]
    public void Plan_Swap_OneTempStep()
    {
        var steps = Plan(R("a", "b"), R("b", "a"));

        Assert.Equal(["a>~t1", "b>a", "~t1>b"], Moves(steps));
        Assert.Equal([false, true, true], steps.Select(step => step.IsFinal));
        Assert.Equal([0, 1, 0], steps.Select(step => step.RenameIndex));
    }

    [Fact]
    public void Plan_CaseOnly_TwoSteps() =>
        Assert.Equal(["photo>~t1", "~t1>Photo"], Moves(Plan(R("photo", "Photo"))));

    [Fact]
    public void Plan_ChainAndASwapTogether()
    {
        var steps = Plan(R("a", "d"), R("b", "c"), R("c", "b"), R("d", "e"));

        Assert.Equal(["d>e", "a>d", "b>~t1", "c>b", "~t1>c"], Moves(steps));
        var names = new HashSet<string>(["a", "b", "c", "d"], StringComparer.OrdinalIgnoreCase);
        foreach (var step in steps)
        {
            Assert.True(names.Add(Name(step.ToPath)), $"{step.ToPath} was taken");
            Assert.True(names.Remove(Name(step.FromPath)));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Plan_DeeperFolderFirst(bool parentFirst)
    {
        var parent = R("X", "Y", Folder, ItemKind.Folder);
        var child = R("a", "b", @"C:\F\X\");
        var deeper = R("c", "d", @"C:\F\X\Z");

        var steps = parentFirst ? Plan(parent, child, deeper) : Plan(deeper, child, parent);

        Assert.Equal(["c>d", "a>b", "X>Y"], Moves(steps));
        Assert.Equal(@"C:\F\X\a", steps[1].FromPath);
    }

    [Fact]
    public void Plan_TenThousandReversedChain_IsLinear()
    {
        const int Count = 10_000;
        var renames = Enumerable.Range(0, Count).Select(i => R($"f{i:D5}", $"f{i + 1:D5}")).ToArray();

        var steps = Plan(renames);

        Assert.Equal(Count, steps.Count);
        Assert.All(steps, step => Assert.True(step.IsFinal));
        Assert.Equal(Count - 1, steps[0].RenameIndex);
        Assert.Equal(0, steps[Count - 1].RenameIndex);
    }
}
