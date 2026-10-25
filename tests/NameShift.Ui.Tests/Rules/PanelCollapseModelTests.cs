using System;
using System.Collections.Generic;
using NameShift.Core;
using NameShift.Ui.Presets;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Rules;

public sealed class PanelCollapseModelTests
{
    private readonly FakeRulePanelModel _replace = FakeRulePanelModel.Replace();
    private readonly FakeRulePanelModel _add = FakeRulePanelModel.Add();
    private readonly FakeRulePanelModel _case = FakeRulePanelModel.Case();

    private RulePanelHostModel Host() => new RulePanelHostModel(new IRulePanelModel[] { _replace, _add, _case });

    private static List<IRulePanelModel> Record(PanelCollapseModel model)
    {
        var events = new List<IRulePanelModel>();
        model.Changed += (_, panel) => events.Add(panel);
        return events;
    }

    [Fact]
    public void AllOff_AllCollapsed()
    {
        var model = new PanelCollapseModel(Host());

        Assert.True(model.IsCollapsed(_replace));
        Assert.True(model.IsCollapsed(_add));
        Assert.True(model.IsCollapsed(_case));
    }

    [Fact]
    public void OneOn_ThatOneOpen()
    {
        _add.Enabled = true;

        var model = new PanelCollapseModel(Host());

        Assert.True(model.IsCollapsed(_replace));
        Assert.False(model.IsCollapsed(_add));
        Assert.True(model.IsCollapsed(_case));
    }

    [Fact]
    public void Saved_Overrides()
    {
        _replace.Enabled = true;
        var saved = new Dictionary<RuleStage, bool> { [RuleStage.Replace] = true, [RuleStage.Add] = false };

        var model = new PanelCollapseModel(Host(), saved);

        Assert.True(model.IsCollapsed(_replace)); // on, but saved collapsed
        Assert.False(model.IsCollapsed(_add)); // off, but saved open
        Assert.True(model.IsCollapsed(_case)); // not saved: off gives collapsed
    }

    [Fact]
    public void Saved_UnknownStage_Ignored()
    {
        _add.Enabled = true;
        var saved = new Dictionary<RuleStage, bool> { [RuleStage.Numbering] = true };

        var model = new PanelCollapseModel(Host(), saved);

        Assert.True(model.IsCollapsed(_replace));
        Assert.False(model.IsCollapsed(_add));
        Assert.True(model.IsCollapsed(_case));
        Assert.False(model.States.ContainsKey(RuleStage.Numbering));
    }

    [Fact]
    public void Toggle_TwiceReturns()
    {
        var model = new PanelCollapseModel(Host());
        List<IRulePanelModel> events = Record(model);

        model.Toggle(_add);
        Assert.False(model.IsCollapsed(_add));
        model.Toggle(_add);

        Assert.True(model.IsCollapsed(_add));
        Assert.Equal(new IRulePanelModel[] { _add, _add }, events);
    }

    [Fact]
    public void SetCollapsed_Changes_RaisesOnceWithThePanel()
    {
        var model = new PanelCollapseModel(Host());
        List<IRulePanelModel> events = Record(model);

        model.SetCollapsed(_case, false);

        Assert.False(model.IsCollapsed(_case));
        Assert.Equal(new IRulePanelModel[] { _case }, events);
    }

    [Fact]
    public void SetSame_RaisesNothing()
    {
        var model = new PanelCollapseModel(Host());
        List<IRulePanelModel> events = Record(model);

        model.SetCollapsed(_replace, true);

        Assert.True(model.IsCollapsed(_replace));
        Assert.Empty(events);
    }

    [Fact]
    public void UnknownPanel_Throws()
    {
        var model = new PanelCollapseModel(Host());
        var stranger = new FakeRulePanelModel(RuleStage.Numbering, _ => null, "&Numbering");

        Assert.Throws<ArgumentException>(() => model.IsCollapsed(stranger));
        Assert.Throws<ArgumentException>(() => model.Toggle(stranger));
        Assert.Throws<ArgumentException>(() => model.SetCollapsed(stranger, false));
        Assert.Throws<ArgumentException>(() => model.IsCollapsed(null!));
    }

    [Fact]
    public void NullHost_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new PanelCollapseModel(null!));
    }

    [Fact]
    public void Tick_OpensCollapsed()
    {
        var model = new PanelCollapseModel(Host());
        List<IRulePanelModel> events = Record(model);

        _replace.Enabled = true;

        Assert.False(model.IsCollapsed(_replace));
        Assert.Equal(new IRulePanelModel[] { _replace }, events);
    }

    [Fact]
    public void Tick_AlreadyOpen_NoEvent()
    {
        var model = new PanelCollapseModel(Host());
        model.SetCollapsed(_replace, false);
        List<IRulePanelModel> events = Record(model);

        _replace.Enabled = true;

        Assert.False(model.IsCollapsed(_replace));
        Assert.Empty(events);
    }

    [Fact]
    public void SettingChange_WhileOff_DoesNotOpen()
    {
        var model = new PanelCollapseModel(Host());

        _replace.Text = "a";

        Assert.True(model.IsCollapsed(_replace));
    }

    [Fact]
    public void Untick_StaysOpen()
    {
        _replace.Enabled = true;
        var model = new PanelCollapseModel(Host());
        List<IRulePanelModel> events = Record(model);

        _replace.Enabled = false;

        Assert.False(model.IsCollapsed(_replace));
        Assert.Empty(events);
    }

    [Fact]
    public void Untick_StaysCollapsed()
    {
        _replace.Enabled = true;
        var model = new PanelCollapseModel(Host());
        model.SetCollapsed(_replace, true);
        List<IRulePanelModel> events = Record(model);

        _replace.Enabled = false;

        Assert.True(model.IsCollapsed(_replace));
        Assert.Empty(events);
    }

    [Fact]
    public void CollapsedWhileOn_StaysCollapsedOnSettingChange()
    {
        _replace.Enabled = true;
        var model = new PanelCollapseModel(Host());
        model.SetCollapsed(_replace, true);

        _replace.Text = "a";

        Assert.True(model.IsCollapsed(_replace));
    }

    [Fact]
    public void Reset_LeavesCollapseAlone()
    {
        _replace.Enabled = true;
        _replace.Text = "a";
        var model = new PanelCollapseModel(Host());
        List<IRulePanelModel> events = Record(model);

        _replace.Reset();

        Assert.False(_replace.Enabled);
        Assert.False(model.IsCollapsed(_replace));
        Assert.Empty(events);
    }

    [Fact]
    public void Reset_WhileCollapsed_StaysCollapsed()
    {
        _replace.Enabled = true;
        _replace.Text = "a";
        var model = new PanelCollapseModel(Host());
        model.SetCollapsed(_replace, true);

        _replace.Reset();

        Assert.True(model.IsCollapsed(_replace));
    }

    [Fact]
    public void Untick_ThenTick_OpensAgain()
    {
        _replace.Enabled = true;
        var model = new PanelCollapseModel(Host());
        model.SetCollapsed(_replace, true);
        _replace.Enabled = false;

        _replace.Enabled = true;

        Assert.False(model.IsCollapsed(_replace));
    }

    [Fact]
    public void OpenByHand_DoesNotTick()
    {
        var model = new PanelCollapseModel(Host());

        model.Toggle(_replace);

        Assert.False(model.IsCollapsed(_replace));
        Assert.False(_replace.Enabled);
    }

    [Fact]
    public void ApplyPreset_EnablingOpens()
    {
        RulePanelHostModel host = Host();
        var model = new PanelCollapseModel(host);
        List<IRulePanelModel> events = Record(model);
        var preset = new Preset("p", new[]
        {
            new PresetPanel(RuleStage.Replace, false, new Dictionary<string, string> { ["Text"] = "x" }),
            new PresetPanel(RuleStage.Add, true, new Dictionary<string, string> { ["Text"] = "y" }),
        });

        host.ApplyPreset(preset);

        Assert.True(model.IsCollapsed(_replace));
        Assert.False(model.IsCollapsed(_add));
        Assert.True(model.IsCollapsed(_case));
        Assert.Equal(new IRulePanelModel[] { _add }, events);
    }

    [Fact]
    public void States_ReportsEveryPanel()
    {
        _case.Enabled = true;
        var model = new PanelCollapseModel(Host());
        model.Toggle(_add);

        IReadOnlyDictionary<RuleStage, bool> states = model.States;

        Assert.Equal(3, states.Count);
        Assert.True(states[RuleStage.Replace]);
        Assert.False(states[RuleStage.Add]);
        Assert.False(states[RuleStage.Case]);
        Assert.NotSame(states, model.States);
    }

    [Fact]
    public void States_RoundTrip()
    {
        var first = new PanelCollapseModel(Host());
        first.Toggle(_case);

        var second = new PanelCollapseModel(Host(), first.States);

        Assert.Equal(first.States, second.States);
    }

    [Fact]
    public void Dispose_StopsTracking()
    {
        var model = new PanelCollapseModel(Host());
        model.Dispose();

        _replace.Enabled = true;

        Assert.True(model.IsCollapsed(_replace));
        model.Dispose(); // safe twice
    }
}
