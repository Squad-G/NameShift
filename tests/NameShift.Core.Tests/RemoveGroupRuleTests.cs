using System;
using System.Collections.Generic;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class RemoveGroupRuleTests
{
    private static readonly ItemContext Ctx = new(Items.File("ab.txt"), 0, 1);

    private static string Run(string name, params IRenameRule[] parts) =>
        new RenamePipeline([new RemoveGroupRule(parts)]).Run(Items.Files(name))[0].NewName;

    [Fact]
    public void Ctor_NullList_ThrowsArgumentNull() =>
        Assert.Throws<ArgumentNullException>(() => new RemoveGroupRule(null!));

    [Fact]
    public void Ctor_NullPart_ThrowsArgument()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => new RemoveGroupRule([new RemoveTidyRule(trim: true), null!]));

        Assert.StartsWith("A part is null.", exception.Message);
    }

    [Fact]
    public void Ctor_PartOfOtherStage_ThrowsArgument()
    {
        Assert.Throws<ArgumentException>(() => new RemoveGroupRule([new ReplaceRule("a", "b")]));
        Assert.Throws<ArgumentException>(
            () => new RemoveGroupRule([new DelegateRule(RuleStage.Add, (n, _) => n)]));
        Assert.Throws<ArgumentException>(
            () => new RemoveGroupRule([new RemoveTidyRule(trim: true), new ReplaceRule("a", "b")]));
    }

    [Fact]
    public void Stage_IsRemove() => Assert.Equal(RuleStage.Remove, new RemoveGroupRule([]).Stage);

    [Fact]
    public void NoParts_DisabledAndUnchanged()
    {
        var group = new RemoveGroupRule([]);
        var name = new NameParts("ab", "txt");

        Assert.False(group.Enabled);
        Assert.Empty(group.Parts);
        Assert.Equal(name, group.Apply(name, Ctx));
    }

    [Fact]
    public void Enabled_WhenAnyPartEnabled()
    {
        Assert.True(new RemoveGroupRule([new RemovePositionRule(), new RemoveTidyRule(trim: true)]).Enabled);
        Assert.True(new RemoveGroupRule([new RemoveTidyRule(trim: true), new RemovePositionRule()]).Enabled);
        Assert.False(new RemoveGroupRule([new RemovePositionRule(), new RemoveTidyRule()]).Enabled);
    }

    [Fact]
    public void Order_TextThenPosition() =>
        Assert.Equal("b.txt", Run("xab.txt", new RemoveTextRule(characters: "x"), new RemovePositionRule(first: 1)));

    [Fact]
    public void Order_PositionThenText() =>
        Assert.Equal("ab.txt", Run("xab.txt", new RemovePositionRule(first: 1), new RemoveTextRule(characters: "x")));

    [Fact]
    public void Parts_KeepTheGivenOrder()
    {
        var text = new RemoveTextRule(characters: "x");
        var position = new RemovePositionRule(first: 1);

        Assert.Equal(new IRenameRule[] { text, position }, new RemoveGroupRule([text, position]).Parts);
    }

    [Fact]
    public void DisabledPositionRule_ChangesNothing() =>
        Assert.Equal("ab.txt", Run("xab.txt", new RemovePositionRule(), new RemoveTextRule(characters: "x")));

    [Fact]
    public void DisabledPart_IsNotApplied()
    {
        var called = false;
        var disabled = new DelegateRule(
            RuleStage.Remove,
            (n, _) =>
            {
                called = true;
                return n.WithBaseName("changed");
            },
            enabled: false);

        Assert.Equal("a.txt", Run(" a.txt", disabled, new RemoveTidyRule(trim: true)));
        Assert.False(called);
    }

    [Fact]
    public void Parts_AreCopied()
    {
        var list = new List<IRenameRule> { new RemoveTextRule(characters: "x") };
        var group = new RemoveGroupRule(list);

        list.Clear();

        Assert.Single(group.Parts);
        Assert.Equal("ab.txt", new RenamePipeline([group]).Run(Items.Files("xab.txt"))[0].NewName);
    }
}
