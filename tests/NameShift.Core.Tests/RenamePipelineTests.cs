using System;
using System.Linq;
using System.Threading;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class RenamePipelineTests
{
    private static DelegateRule Append(RuleStage stage, string text, bool enabled = true) =>
        new(stage, (name, _) => name.WithBaseName(name.BaseName + text), enabled);

    [Fact]
    public void Run_WithNoRules_ReturnsEveryNameUnchanged()
    {
        var results = new RenamePipeline([]).Run(Items.Files("a.txt", "b.txt"));

        Assert.Equal(["a.txt", "b.txt"], results.Select(r => r.NewName));
        Assert.All(results, r => Assert.Equal(RenameStatus.Unchanged, r.Status));
        Assert.All(results, r => Assert.Null(r.Error));
    }

    [Theory]
    [InlineData(".gitignore", ItemKind.File)]
    [InlineData("a.", ItemKind.File)]
    [InlineData("Makefile", ItemKind.File)]
    [InlineData("a.tar.gz", ItemKind.File)]
    [InlineData("v1.2", ItemKind.Folder)]
    public void Run_WithNoRules_KeepsOddNamesIdentical(string name, ItemKind kind)
    {
        var results = new RenamePipeline([]).Run([new RenameItem(name, @"C:\Test", kind)]);

        Assert.Equal(name, results[0].NewName);
        Assert.Equal(RenameStatus.Unchanged, results[0].Status);
    }

    [Fact]
    public void Run_GivesRulesTheSplitName()
    {
        var rule = new RecordingRule();
        var items = new[]
        {
            Items.File("a.tar.gz"),
            Items.File(".gitignore"),
            new RenameItem("v1.2", @"C:\Test", ItemKind.Folder),
        };

        new RenamePipeline([rule]).Run(items);

        Assert.Equal(
            [new NameParts("a.tar", "gz"), new NameParts(".gitignore", ""), new NameParts("v1.2", "")],
            rule.Calls.Select(c => c.Name));
    }

    [Fact]
    public void Run_GivesRulesTheItemFactsIndexAndCount()
    {
        var rule = new RecordingRule();
        var item = new RenameItem(
            "x.jpg",
            @"C:\Photos\Paris",
            ItemKind.File,
            ItemAttributes.Hidden | ItemAttributes.ReadOnly,
            1234,
            new DateTime(2020, 1, 2, 3, 4, 5),
            new DateTime(2021, 6, 7, 8, 9, 10));

        new RenamePipeline([rule]).Run([Items.File("a.txt"), item, Items.File("c.txt")]);

        var context = rule.Calls[1].Context;
        Assert.Same(item, context.Item);
        Assert.Equal("x.jpg", context.Item.Name);
        Assert.Equal(@"C:\Photos\Paris", context.Item.FolderPath);
        Assert.Equal(ItemKind.File, context.Item.Kind);
        Assert.Equal(ItemAttributes.Hidden | ItemAttributes.ReadOnly, context.Item.Attributes);
        Assert.Equal(1234, context.Item.Size);
        Assert.Equal(new DateTime(2020, 1, 2, 3, 4, 5), context.Item.Created);
        Assert.Equal(new DateTime(2021, 6, 7, 8, 9, 10), context.Item.Modified);
        Assert.Equal(1, context.Index);
        Assert.Equal(3, context.Count);
    }

    [Fact]
    public void Run_DoesNotNeedTheDisk()
    {
        var item = new RenameItem("a.txt", @"Q:\does\not\exist");

        var results = new RenamePipeline([Append(RuleStage.Add, "_x")]).Run([item]);

        Assert.Equal("a_x.txt", results[0].NewName);
        Assert.Equal(RenameStatus.Changed, results[0].Status);
    }

    [Fact]
    public void Constructor_SortsRulesByStage_StableForEqualStages()
    {
        var numbering = Append(RuleStage.Numbering, "N");
        var replace1 = Append(RuleStage.Replace, "R1");
        var regEx = Append(RuleStage.RegEx, "X");
        var replace2 = Append(RuleStage.Replace, "R2");

        var pipeline = new RenamePipeline([numbering, replace1, regEx, replace2]);

        Assert.Equal([regEx, replace1, replace2, numbering], pipeline.Rules);
        Assert.Equal("abXR1R2N.txt", pipeline.Run(Items.Files("ab.txt"))[0].NewName);
    }

    [Fact]
    public void Run_EachRuleGetsThePreviousRulesResult()
    {
        var regEx = new DelegateRule(RuleStage.RegEx, (name, _) => name.WithExtension("md"));
        var add = new DelegateRule(RuleStage.Add, (name, _) => name.WithBaseName(name.BaseName + "_" + name.Extension));

        var results = new RenamePipeline([add, regEx]).Run(Items.Files("a.txt"));

        Assert.Equal("a_md.md", results[0].NewName);
    }

    [Fact]
    public void Run_ARuleCanChangeTheExtension()
    {
        var rule = new DelegateRule(RuleStage.Extension, (name, _) => name.WithExtension(name.Extension.ToUpperInvariant()));

        var results = new RenamePipeline([rule]).Run(Items.Files("a.txt"));

        Assert.Equal("a.TXT", results[0].NewName);
        Assert.Equal(RenameStatus.Changed, results[0].Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    public void Constructor_RejectsAStageOutsideOneToEleven(int stage)
    {
        var rule = new DelegateRule((RuleStage)stage, (name, _) => name);

        Assert.Throws<ArgumentOutOfRangeException>(() => new RenamePipeline([rule]));
    }

    [Fact]
    public void Constructor_RejectsNullRulesAndNullElements()
    {
        Assert.Throws<ArgumentNullException>(() => new RenamePipeline(null!));
        Assert.Throws<ArgumentException>(() => new RenamePipeline([null!]));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Run_StageOrderWinsOverRegistrationOrder(bool addFirst)
    {
        var add = Append(RuleStage.Add, "_x");
        var upper = new DelegateRule(RuleStage.Case, (name, _) => name.WithBaseName(name.BaseName.ToUpperInvariant()));
        var rules = addFirst ? new IRenameRule[] { add, upper } : new IRenameRule[] { upper, add };

        var results = new RenamePipeline(rules).Run(Items.Files("ab.txt"));

        Assert.Equal("AB_x.txt", results[0].NewName);
    }

    [Fact]
    public void Run_DisabledRuleHasNoEffect()
    {
        var results = new RenamePipeline([Append(RuleStage.Add, "_x", enabled: false)]).Run(Items.Files("a.txt"));

        Assert.Equal("a.txt", results[0].NewName);
        Assert.Equal(RenameStatus.Unchanged, results[0].Status);
    }

    [Fact]
    public void Run_KeepsInputOrderAndPassesEachItemsIndex()
    {
        var rule = new DelegateRule(RuleStage.Add, (name, context) => name.WithBaseName(name.BaseName + "_" + context.Index));

        var results = new RenamePipeline([rule]).Run(Items.Files("b.txt", "a.txt"));

        Assert.Equal("b.txt", results[0].Item.Name);
        Assert.Equal(0, results[0].Index);
        Assert.Equal("b_0.txt", results[0].NewName);
        Assert.Equal("a.txt", results[1].Item.Name);
        Assert.Equal(1, results[1].Index);
        Assert.Equal("a_1.txt", results[1].NewName);
    }

    [Fact]
    public void Run_WhenARuleThrows_MarksOnlyThatItemAndNamesTheRule()
    {
        var pipeline = new RenamePipeline(
            [new ThrowingRule(RuleStage.Numbering, 1, "number below zero"), Append(RuleStage.Add, "_x")]);

        var results = pipeline.Run(Items.Files("a.txt", "b.txt", "c.txt"));

        Assert.Equal("a_x.txt", results[0].NewName);
        Assert.Equal(RenameStatus.Changed, results[0].Status);
        Assert.Equal(RenameStatus.Error, results[1].Status);
        Assert.Equal("b.txt", results[1].NewName);
        Assert.NotNull(results[1].Error);
        Assert.Equal(RuleStage.Numbering, results[1].Error!.Stage);
        Assert.Equal("ThrowingRule", results[1].Error!.RuleName);
        Assert.Equal("number below zero", results[1].Error!.Message);
        Assert.Equal("Numbering: number below zero", results[1].Error!.ToString());
        Assert.Equal("c_x.txt", results[2].NewName);
        Assert.Equal(RenameStatus.Changed, results[2].Status);
    }

    [Fact]
    public void Run_AfterAnError_LaterRulesAreSkippedForThatItem()
    {
        var recorder = new RecordingRule(RuleStage.Extension);

        new RenamePipeline([new ThrowingRule(RuleStage.RegEx, 0, "boom"), recorder]).Run(Items.Files("a.txt"));

        Assert.Empty(recorder.Calls);
    }

    [Fact]
    public void Run_RuleReturningNull_IsAnError()
    {
        var rule = new DelegateRule(RuleStage.Name, (_, _) => null!);

        var results = new RenamePipeline([rule]).Run(Items.Files("a.txt"));

        Assert.Equal(RenameStatus.Error, results[0].Status);
        Assert.Equal("a.txt", results[0].NewName);
    }

    [Fact]
    public void Run_CaseOnlyChange_IsChanged()
    {
        var rule = new DelegateRule(
            RuleStage.Case,
            (name, _) => name.WithBaseName(char.ToUpperInvariant(name.BaseName[0]) + name.BaseName.Substring(1)));

        var results = new RenamePipeline([rule]).Run(Items.Files("photo.jpg"));

        Assert.Equal("Photo.jpg", results[0].NewName);
        Assert.Equal(RenameStatus.Changed, results[0].Status);
    }

    [Fact]
    public void Run_EmptyList_ReturnsNoResults()
    {
        Assert.Empty(new RenamePipeline([]).Run([]));
    }

    [Fact]
    public void Run_RejectsNullItemsAndNullElements()
    {
        var pipeline = new RenamePipeline([]);

        Assert.Throws<ArgumentNullException>(() => pipeline.Run(null!));
        Assert.Throws<ArgumentException>(() => pipeline.Run([null!]));
    }

    [Fact]
    public void Run_CancelledToken_Throws()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => new RenamePipeline([]).Run(Items.Files("a.txt"), source.Token));
    }

    [Fact]
    public void Run_ARuleThrowingCancellation_Propagates()
    {
        var rule = new DelegateRule(RuleStage.Name, (_, _) => throw new OperationCanceledException());

        Assert.Throws<OperationCanceledException>(() => new RenamePipeline([rule]).Run(Items.Files("a.txt")));
    }
}
