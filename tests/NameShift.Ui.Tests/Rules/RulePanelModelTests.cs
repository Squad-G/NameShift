using System;
using NameShift.Core;
using Xunit;

namespace NameShift.Ui.Tests.Rules;

public sealed class RulePanelModelTests
{
    [Fact]
    public void Enabled_Change_RaisesChangedOnce()
    {
        var panel = FakeRulePanelModel.Replace();
        int count = 0;
        panel.Changed += (_, _) => count++;

        panel.Enabled = true;
        Assert.Equal(1, count);

        panel.Enabled = true;
        Assert.Equal(1, count);
    }

    [Fact]
    public void SetField_SameValue_RaisesNothing()
    {
        var panel = FakeRulePanelModel.Replace();
        int count = 0;
        panel.Changed += (_, _) => count++;

        panel.Text = "a";
        panel.Text = "a";

        Assert.Equal(1, count);
    }

    [Fact]
    public void Reset_RestoresDefaultsAndUnchecks()
    {
        var panel = FakeRulePanelModel.Replace();
        panel.Text = "a";
        panel.Enabled = true;

        panel.Reset();

        Assert.Equal("", panel.Text);
        Assert.False(panel.Enabled);
        Assert.True(panel.IsDefault);
    }

    [Fact]
    public void Reset_RaisesChangedExactlyOnce()
    {
        var panel = FakeRulePanelModel.Replace();
        panel.Text = "a";
        panel.Enabled = true;
        int count = 0;
        panel.Changed += (_, _) => count++;

        panel.Reset();
        Assert.Equal(1, count);

        panel.Reset();
        Assert.Equal(2, count);
    }

    [Fact]
    public void Ctor_EmptyTitle_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new FakeRulePanelModel(RuleStage.Replace, _ => null, ""));
    }
}