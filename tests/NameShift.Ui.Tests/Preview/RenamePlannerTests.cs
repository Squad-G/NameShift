using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NameShift.Core;
using NameShift.Ui.Preview;
using Xunit;

namespace NameShift.Ui.Tests.Preview;

public sealed class RenamePlannerTests
{
    private const string F = @"C:\F";

    /// <summary>Renames the items named in the map to the mapped full name; a null value is a rule error.</summary>
    private sealed class MapRule : IRenameRule
    {
        private readonly Dictionary<string, string?> _map;

        public MapRule(Dictionary<string, string?> map)
        {
            _map = map;
        }

        public RuleStage Stage => RuleStage.Replace;

        public bool Enabled => true;

        public NameParts Apply(NameParts name, ItemContext context)
        {
            if (!_map.TryGetValue(context.Item.Name, out string? newName))
            {
                return name;
            }

            if (newName == null)
            {
                throw new InvalidOperationException("boom");
            }

            return NameParts.Split(newName, context.Item.Kind);
        }
    }

    private sealed class Case
    {
        public IReadOnlyList<IRenameRule> Rules { get; set; } = Array.Empty<IRenameRule>();

        public IReadOnlyList<RenameItem> Items { get; set; } = Array.Empty<RenameItem>();

        public IReadOnlyDictionary<string, IReadOnlyCollection<string>> Map { get; set; } =
            new Dictionary<string, IReadOnlyCollection<string>>();

        public List<ConflictCode[]> ExpectedCodes { get; } = new List<ConflictCode[]>();

        public int Changed { get; set; }

        public int Errors { get; set; }

        public int ConflictRows { get; set; }
    }

    private static Case Fixture(string keys)
    {
        var names = new List<string> { "keep.txt" };
        var map = new Dictionary<string, string?>(StringComparer.Ordinal);
        var c = new Case();
        c.ExpectedCodes.Add(Array.Empty<ConflictCode>());
        foreach (string key in keys.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            switch (key)
            {
                case "change":
                    Add(names, map, c, "change.txt", "changed.txt", Array.Empty<ConflictCode>());
                    break;
                case "error":
                    Add(names, map, c, "error.txt", null, Array.Empty<ConflictCode>());
                    break;
                case "invalid":
                    Add(names, map, c, "invalid.txt", "a?b.txt", new[] { ConflictCode.InvalidCharacter });
                    break;
                case "duplicate":
                    Add(names, map, c, "dup1.txt", "same.txt", new[] { ConflictCode.DuplicateTarget });
                    Add(names, map, c, "dup2.txt", "same.txt", new[] { ConflictCode.DuplicateTarget });
                    break;
                case "exists":
                    Add(names, map, c, "exists.txt", "taken.txt", new[] { ConflictCode.TargetExists });
                    break;
                case "long":
                    Add(names, map, c, "long.txt", new string('n', 251) + ".txt", new[] { ConflictCode.PathTooLong });
                    break;
                default:
                    throw new ArgumentException("Unknown key " + key, nameof(keys));
            }
        }

        c.Rules = new IRenameRule[] { new MapRule(map) };
        c.Items = names.Select(n => new RenameItem(n, F)).ToArray();
        var existing = new List<string>(names) { "taken.txt" };
        c.Map = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [F] = existing.ToArray(),
        };
        return c;
    }

    private static void Add(List<string> names, Dictionary<string, string?> map, Case c, string name, string? newName, ConflictCode[] codes)
    {
        names.Add(name);
        map[name] = newName;
        c.ExpectedCodes.Add(codes);
        if (newName == null)
        {
            c.Errors++;
        }
        else
        {
            c.Changed++;
        }

        if (codes.Length > 0)
        {
            c.ConflictRows++;
        }
    }

    [Theory]
    [InlineData("", RenameBlocker.NothingToRename)]
    [InlineData("change", RenameBlocker.None)]
    [InlineData("error", RenameBlocker.RuleErrors)]
    [InlineData("change,error", RenameBlocker.RuleErrors)]
    [InlineData("invalid", RenameBlocker.Conflicts)]
    [InlineData("change,invalid", RenameBlocker.Conflicts)]
    [InlineData("duplicate", RenameBlocker.Conflicts)]
    [InlineData("exists", RenameBlocker.Conflicts)]
    [InlineData("long", RenameBlocker.Conflicts)]
    [InlineData("error,invalid", RenameBlocker.ConflictsAndRuleErrors)]
    [InlineData("change,error,duplicate,exists,long", RenameBlocker.ConflictsAndRuleErrors)]
    [InlineData("change,invalid,duplicate,exists,long", RenameBlocker.Conflicts)]
    public void Plan_GivesTheBlockerAndTheChecksCodes(string keys, RenameBlocker expected)
    {
        Case c = Fixture(keys);

        RenamePlan plan = RenamePlanner.Plan(c.Rules, c.Items, c.Map, CancellationToken.None);

        IReadOnlyList<RenameResult> byHand = new RenamePipeline(c.Rules).Run(c.Items);
        ConflictReport byHandConflicts = ConflictChecker.Run(
            byHand,
            new IConflictCheck[] { new InvalidNameCheck(), new DuplicateTargetCheck(c.Map), new PathLengthCheck() });
        Assert.Equal(expected, plan.Blocker);
        Assert.Equal(c.Items.Count, plan.Results.Count);
        Assert.Equal(c.Items.Count, plan.Conflicts.Count);
        for (int i = 0; i < c.Items.Count; i++)
        {
            ConflictCode[] codes = plan.Conflicts.ForItem(i).Select(x => x.Code).ToArray();
            Assert.Equal(c.ExpectedCodes[i], codes);
            Assert.Equal(byHandConflicts.ForItem(i).Select(x => x.Code).ToArray(), codes);
            Assert.Equal(byHand[i].NewName, plan.Results[i].NewName);
            Assert.Equal(byHand[i].Status, plan.Results[i].Status);
        }

        Assert.Equal(c.Changed, plan.ChangedCount);
        Assert.Equal(c.Errors, plan.ErrorCount);
        Assert.Equal(c.ConflictRows, plan.ConflictCount);
    }

    [Fact]
    public void Plan_RunsTheChecksInOrder()
    {
        string tooLong = "a?" + new string('n', 249) + ".txt";
        RenameItem[] items = { new RenameItem("o1.txt", F), new RenameItem("o2.txt", F) };
        var map = new Dictionary<string, string?>(StringComparer.Ordinal) { ["o1.txt"] = tooLong, ["o2.txt"] = tooLong };
        var existing = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [F] = new[] { "o1.txt", "o2.txt" },
        };

        RenamePlan plan = RenamePlanner.Plan(new IRenameRule[] { new MapRule(map) }, items, existing, CancellationToken.None);

        var expected = new[] { ConflictCode.InvalidCharacter, ConflictCode.DuplicateTarget, ConflictCode.PathTooLong };
        Assert.Equal(expected, plan.Conflicts.ForItem(0).Select(x => x.Code).ToArray());
        Assert.Equal(expected, plan.Conflicts.ForItem(1).Select(x => x.Code).ToArray());
        Assert.Equal(2, plan.ConflictCount);
    }

    [Fact]
    public void Plan_CancelledToken_Throws()
    {
        Case c = Fixture("change");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => RenamePlanner.Plan(c.Rules, c.Items, c.Map, cts.Token));
    }

    [Fact]
    public void Plan_CancelledToken_EmptyList_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var empty = new Dictionary<string, IReadOnlyCollection<string>>();

        Assert.ThrowsAny<OperationCanceledException>(
            () => RenamePlanner.Plan(Array.Empty<IRenameRule>(), Array.Empty<RenameItem>(), empty, cts.Token));
    }

    [Fact]
    public void Plan_EmptyList_IsNothingToRename()
    {
        var empty = new Dictionary<string, IReadOnlyCollection<string>>();

        RenamePlan plan = RenamePlanner.Plan(Array.Empty<IRenameRule>(), Array.Empty<RenameItem>(), empty, CancellationToken.None);

        Assert.Equal(RenameBlocker.NothingToRename, plan.Blocker);
        Assert.Empty(plan.Results);
        Assert.Equal(0, plan.Conflicts.Count);
        Assert.Equal(0, plan.ChangedCount);
        Assert.Equal(0, plan.ErrorCount);
        Assert.Equal(0, plan.ConflictCount);
    }

    [Fact]
    public void Plan_NullArguments_Throw()
    {
        Case c = Fixture("change");

        Assert.Equal("rules", Assert.Throws<ArgumentNullException>(
            () => RenamePlanner.Plan(null!, c.Items, c.Map, CancellationToken.None)).ParamName);
        Assert.Equal("items", Assert.Throws<ArgumentNullException>(
            () => RenamePlanner.Plan(c.Rules, null!, c.Map, CancellationToken.None)).ParamName);
        Assert.Equal("existingNamesByFolder", Assert.Throws<ArgumentNullException>(
            () => RenamePlanner.Plan(c.Rules, c.Items, null!, CancellationToken.None)).ParamName);
    }
}
