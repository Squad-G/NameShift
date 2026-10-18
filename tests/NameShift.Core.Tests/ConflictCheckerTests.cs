using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class ConflictCheckerTests
{
    private static readonly Conflict A = new(ConflictCode.DuplicateTarget);
    private static readonly Conflict B = new(ConflictCode.TargetExists);
    private static readonly Conflict C = new(ConflictCode.PathTooLong);

    private static IReadOnlyList<RenameResult> TwoResults() =>
        new RenamePipeline([]).Run(Items.Files("a.txt", "b.txt"));

    private static IReadOnlyList<Conflict> List(params Conflict[] conflicts) => conflicts;

    [Fact]
    public void Run_WithNoChecks_ReportsNoConflicts()
    {
        var report = ConflictChecker.Run(TwoResults(), []);

        Assert.Equal(2, report.Count);
        Assert.Empty(report.ForItem(0));
        Assert.Empty(report.ForItem(1));
        Assert.False(report.HasConflicts);
    }

    [Fact]
    public void Run_ConcatenatesEachItemsConflictsInCheckOrder()
    {
        var report = ConflictChecker.Run(
            TwoResults(),
            [new FixedCheck(List(A), List()), new FixedCheck(List(B), List(C))]);

        Assert.Equal([A, B], report.ForItem(0));
        Assert.Equal([C], report.ForItem(1));
        Assert.True(report.HasConflicts);
    }

    [Fact]
    public void Run_CheckReturningTheWrongCount_Throws() =>
        Assert.Throws<InvalidOperationException>(
            () => ConflictChecker.Run(TwoResults(), [new FixedCheck(List())]));

    [Fact]
    public void Run_WithInvalidNameCheck_FlagsOnlyTheBadItem()
    {
        var rule = new DelegateRule(
            RuleStage.Name,
            (name, context) => context.Index == 1 ? name.WithBaseName("CON") : name);
        var results = new RenamePipeline([rule]).Run(Items.Files("a.txt", "b.txt"));

        var report = ConflictChecker.Run(results, [new InvalidNameCheck()]);

        Assert.Empty(report.ForItem(0));
        Assert.Equal([new Conflict(ConflictCode.ReservedName, "CON")], report.ForItem(1));
    }

    [Fact]
    public void Run_RejectsNulls()
    {
        Assert.Throws<ArgumentNullException>(() => ConflictChecker.Run(null!, []));
        Assert.Throws<ArgumentNullException>(() => ConflictChecker.Run(TwoResults(), null!));
        Assert.Throws<ArgumentException>(() => ConflictChecker.Run(TwoResults(), [null!]));
    }

    [Fact]
    public void ConflictReport_RejectsNulls()
    {
        Assert.Throws<ArgumentNullException>(() => new ConflictReport(null!));
        Assert.Throws<ArgumentException>(() => new ConflictReport([null!]));
    }

    [Fact]
    public void ForItem_OutsideTheList_Throws()
    {
        var report = ConflictChecker.Run(TwoResults(), []);

        Assert.Throws<ArgumentOutOfRangeException>(() => report.ForItem(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => report.ForItem(2));
    }

    [Fact]
    public void Conflict_RejectsAnUnknownCode() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Conflict((ConflictCode)99));

    [Fact]
    public void Conflict_EqualityAndToString()
    {
        var one = new Conflict(ConflictCode.InvalidCharacter, "?");

        Assert.Equal(one, new Conflict(ConflictCode.InvalidCharacter, "?"));
        Assert.Equal(one.GetHashCode(), new Conflict(ConflictCode.InvalidCharacter, "?").GetHashCode());
        Assert.NotEqual(one, new Conflict(ConflictCode.InvalidCharacter, "*"));
        Assert.NotEqual(one, new Conflict(ConflictCode.ReservedName, "?"));
        Assert.False(one.Equals((object?)null));
        Assert.Equal("InvalidCharacter: ?", one.ToString());
        Assert.Equal("EmptyName", new Conflict(ConflictCode.EmptyName).ToString());
    }

    private sealed class FixedCheck(params IReadOnlyList<Conflict>[] perItem) : IConflictCheck
    {
        public IReadOnlyList<IReadOnlyList<Conflict>> Check(IReadOnlyList<RenameResult> results) => perItem;
    }

    [Fact]
    public void Run_TenThousandChangedItems_IsLinearAndFast()
    {
        const int count = 10000;
        var results = new RenameResult[count];
        for (var i = 0; i < count; i++)
        {
            var item = new RenameItem($"f{i}.txt", @"C:\Test");
            results[i] = new RenameResult(item, i, i % 2 == 0 ? "same.txt" : $"n{i}.txt", RenameStatus.Changed, null);
        }

        var existing = new Dictionary<string, IReadOnlyCollection<string>>
        {
            [@"C:\Test"] = Enumerable.Range(0, count).Select(i => $"e{i}.txt").ToArray(),
        };
        IConflictCheck[] checks = [new InvalidNameCheck(), new DuplicateTargetCheck(existing), new PathLengthCheck()];

        var watch = Stopwatch.StartNew();
        var report = ConflictChecker.Run(results, checks);
        watch.Stop();

        Assert.Equal(count, report.Count);
        Assert.Equal([new Conflict(ConflictCode.DuplicateTarget)], report.ForItem(0));
        Assert.Empty(report.ForItem(1));
        Assert.Equal(count / 2, Enumerable.Range(0, count).Count(i => report.ForItem(i).Count > 0));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), watch.Elapsed.ToString());
    }
}
