using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NameShift.Core;
using NameShift.Ui.Files;
using NameShift.Ui.Presets;
using NameShift.Ui.Preview;
using NameShift.Ui.Rules;
using NameShift.Ui.Tests.Preview;
using Xunit;
using static NameShift.Ui.Tests.FakeFileListSource;

namespace NameShift.Ui.Tests.Rules;

public sealed class RulePanelHostModelTests
{
    [Fact]
    public void Panels_AreInPipelineOrder()
    {
        var host = new RulePanelHostModel(new IRulePanelModel[]
        {
            FakeRulePanelModel.Add(), FakeRulePanelModel.Replace(), FakeRulePanelModel.Case(),
        });

        Assert.Equal(
            new[] { RuleStage.Replace, RuleStage.Case, RuleStage.Add },
            host.Panels.Select(p => p.Stage).ToArray());
    }

    [Fact]
    public void Ctor_DuplicateStage_Throws()
    {
        Assert.Throws<ArgumentException>(() => new RulePanelHostModel(new IRulePanelModel[]
        {
            FakeRulePanelModel.Replace(), FakeRulePanelModel.Replace(),
        }));
    }

    [Fact]
    public void Ctor_NullPanel_Throws()
    {
        Assert.Throws<ArgumentException>(() => new RulePanelHostModel(new IRulePanelModel[] { null! }));
    }

    [Fact]
    public void Ctor_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RulePanelHostModel(null!));
    }

    [Fact]
    public void Ctor_UndefinedStage_Throws()
    {
        var panel = new FakeRulePanelModel((RuleStage)99, _ => null);

        Assert.Throws<ArgumentException>(() => new RulePanelHostModel(new IRulePanelModel[] { panel }));
    }

    [Fact]
    public void IsActive_EnabledAndChanged_True()
    {
        var panel = FakeRulePanelModel.Replace();
        panel.Enabled = true;
        panel.Text = "a";

        Assert.True(RulePanelHostModel.IsActive(panel));
    }

    [Fact]
    public void IsActive_EnabledButDefault_False()
    {
        var panel = FakeRulePanelModel.Replace();
        panel.Enabled = true;

        Assert.False(RulePanelHostModel.IsActive(panel));
    }

    [Fact]
    public void IsActive_ChangedButDisabled_False()
    {
        var panel = FakeRulePanelModel.Replace();
        panel.Text = "a";

        Assert.False(RulePanelHostModel.IsActive(panel));
    }

    [Fact]
    public void IsActive_AfterReset_False()
    {
        var panel = FakeRulePanelModel.Replace();
        panel.Enabled = true;
        panel.Text = "a";

        panel.Reset();

        Assert.False(RulePanelHostModel.IsActive(panel));
    }

    [Fact]
    public void CanReset_OnlyWhenSomethingToReset()
    {
        var panel = FakeRulePanelModel.Replace();
        Assert.False(RulePanelHostModel.CanReset(panel));

        panel.Enabled = true;
        Assert.True(RulePanelHostModel.CanReset(panel));

        panel.Enabled = false;
        panel.Text = "a";
        Assert.True(RulePanelHostModel.CanReset(panel));
    }

    [Fact]
    public void Rules_ExcludeDisabledPanels()
    {
        var replace = FakeRulePanelModel.Replace();
        var add = FakeRulePanelModel.Add();
        var host = new RulePanelHostModel(new IRulePanelModel[] { replace, add });

        replace.Text = "a";
        replace.Enabled = true;
        add.Text = "x-";

        Assert.IsType<ReplaceRule>(Assert.Single(host.Rules));
    }

    [Fact]
    public void Reset_LeavesOtherPanelsUntouched()
    {
        var replace = FakeRulePanelModel.Replace();
        var add = FakeRulePanelModel.Add();
        var host = new RulePanelHostModel(new IRulePanelModel[] { replace, add });
        replace.Text = "a";
        replace.Enabled = true;
        add.Text = "x-";
        add.Enabled = true;

        replace.Reset();

        Assert.Equal("x-", add.Text);
        Assert.True(add.Enabled);
        Assert.IsType<AddRule>(Assert.Single(host.Rules));
    }

    [Fact]
    public void SettingChange_RebuildsRulesBeforeRulesChanged()
    {
        var replace = FakeRulePanelModel.Replace();
        var host = new RulePanelHostModel(new IRulePanelModel[] { replace });
        replace.Enabled = true;
        string? seen = null;
        host.RulesChanged += (_, _) => seen = ((ReplaceRule)host.Rules[0]).Find;

        replace.Text = "b";

        Assert.Equal("b", seen);
    }

    [Fact]
    public void SettingChange_RaisesRulesChangedSynchronouslyOnce()
    {
        var replace = FakeRulePanelModel.Replace();
        var host = new RulePanelHostModel(new IRulePanelModel[] { replace });
        int count = 0;
        host.RulesChanged += (_, _) => count++;

        replace.Text = "b";

        Assert.Equal(1, count);
    }

    [Fact]
    public void EnabledToggle_RaisesRulesChanged()
    {
        var replace = FakeRulePanelModel.Replace();
        var host = new RulePanelHostModel(new IRulePanelModel[] { replace });
        replace.Enabled = true;
        int count = 0;
        host.RulesChanged += (_, _) => count++;

        replace.Enabled = false;

        Assert.Equal(1, count);
        Assert.Empty(host.Rules);
    }

    [Fact]
    public void Rules_NullRule_IsSkipped()
    {
        var panel = new FakeRulePanelModel(RuleStage.Replace, _ => null);
        var host = new RulePanelHostModel(new IRulePanelModel[] { panel });

        panel.Enabled = true;

        Assert.Empty(host.Rules);
    }

    [Fact]
    public void Rules_StageMismatch_Throws()
    {
        var panel = new FakeRulePanelModel(RuleStage.Add, t => new ReplaceRule(t, "_"));
        _ = new RulePanelHostModel(new IRulePanelModel[] { panel });

        Assert.Throws<InvalidOperationException>(() => panel.Enabled = true);
    }

    [Fact]
    public void Rules_RunThroughPipelineInStageOrder()
    {
        var add = FakeRulePanelModel.Add();
        var replace = FakeRulePanelModel.Replace();
        var host = new RulePanelHostModel(new IRulePanelModel[] { add, replace });
        add.Text = "x-";
        add.Enabled = true;
        replace.Text = "a";
        replace.Enabled = true;

        var results = new RenamePipeline(host.Rules).Run(new[] { new RenameItem("banana.txt", @"C:\Test") });

        Assert.Equal("x-b_n_n_.txt", results[0].NewName);
    }

    [Fact]
    public void ResetAll_ResetsEveryPanel_RaisesRulesChangedOnce()
    {
        FakeRulePanelModel replace = FakeRulePanelModel.Replace();
        FakeRulePanelModel add = FakeRulePanelModel.Add();
        FakeRulePanelModel casePanel = FakeRulePanelModel.Case();
        var host = new RulePanelHostModel(new IRulePanelModel[] { replace, add, casePanel });
        replace.Text = "a";
        replace.Enabled = true;
        add.Text = "x_";
        add.Enabled = true;
        casePanel.Enabled = true;
        int rulesChanged = 0;
        int panelChanges = 0;
        host.RulesChanged += (_, _) => rulesChanged++;
        foreach (IRulePanelModel panel in host.Panels)
        {
            panel.Changed += (_, _) => panelChanges++;
        }

        host.ResetAll();

        Assert.Equal(1, rulesChanged);
        Assert.Equal(3, panelChanges);
        Assert.All(host.Panels, panel =>
        {
            Assert.False(panel.Enabled);
            Assert.True(panel.IsDefault);
        });
        Assert.Empty(host.Rules);
        Assert.False(host.CanResetAny);
    }

    [Fact]
    public void ResetAll_NothingToReset_DoesNotRaiseRulesChanged()
    {
        var host = new RulePanelHostModel(new IRulePanelModel[] { FakeRulePanelModel.Replace(), FakeRulePanelModel.Add() });
        int rulesChanged = 0;
        host.RulesChanged += (_, _) => rulesChanged++;

        host.ResetAll();

        Assert.Equal(0, rulesChanged);
    }

    [Fact]
    public void ResetAll_ThenAPanelChange_RaisesRulesChangedAgain()
    {
        FakeRulePanelModel replace = FakeRulePanelModel.Replace();
        var host = new RulePanelHostModel(new IRulePanelModel[] { replace });
        replace.Enabled = true;
        host.ResetAll();
        int rulesChanged = 0;
        host.RulesChanged += (_, _) => rulesChanged++;

        replace.Enabled = true;

        Assert.Equal(1, rulesChanged);
    }

    [Fact]
    public void CanResetAny()
    {
        FakeRulePanelModel replace = FakeRulePanelModel.Replace();
        FakeRulePanelModel add = FakeRulePanelModel.Add();
        var host = new RulePanelHostModel(new IRulePanelModel[] { replace, add });

        Assert.False(host.CanResetAny);
        add.Text = "x_";
        Assert.True(host.CanResetAny);
        add.Text = "";
        replace.Enabled = true;
        Assert.True(host.CanResetAny);
    }

    private static RulePanelHostModel RealHost(out CasePanelModel casePanel, out ReplacePanelModel replace, out NumberingPanelModel numbering)
    {
        casePanel = new CasePanelModel();
        replace = new ReplacePanelModel();
        numbering = new NumberingPanelModel();
        return new RulePanelHostModel(new IRulePanelModel[] { casePanel, replace, numbering });
    }

    private static Preset PresetOf(params IRulePanelModel[] models) => Preset.FromPanels("Test", models);

    [Fact]
    public void ApplyPreset_RaisesRulesChangedOnce()
    {
        RulePanelHostModel host = RealHost(out CasePanelModel casePanel, out ReplacePanelModel replace, out _);
        Preset preset = PresetOf(
            new CasePanelModel { Mode = CaseMode.Upper, Enabled = true },
            new ReplacePanelModel { Find = "a", With = "b", MatchCase = true, Enabled = true });
        int rulesChanged = 0;
        host.RulesChanged += (_, _) => rulesChanged++;

        host.ApplyPreset(preset);

        Assert.Equal(1, rulesChanged);
        Assert.Equal(CaseMode.Upper, casePanel.Mode);
        Assert.Equal("a", replace.Find);

        host.ApplyPreset(preset);

        Assert.Equal(2, rulesChanged);
    }

    [Fact]
    public void ApplyPreset_PanelsEqualThePreset()
    {
        RulePanelHostModel host = RealHost(out CasePanelModel casePanel, out ReplacePanelModel replace, out NumberingPanelModel numbering);
        numbering.StartText = "7";
        numbering.Enabled = true;
        casePanel.ExceptionsText = "of";
        Preset preset = PresetOf(
            new CasePanelModel { Mode = CaseMode.Lower, Enabled = true },
            new ReplacePanelModel { Find = "x", With = "y" });

        host.ApplyPreset(preset);

        Assert.Equal(CaseMode.Lower, casePanel.Mode);
        Assert.Equal("", casePanel.ExceptionsText);
        Assert.True(casePanel.Enabled);
        Assert.Equal("x", replace.Find);
        Assert.Equal("y", replace.With);
        Assert.False(replace.Enabled);
        Assert.True(numbering.IsDefault);
        Assert.False(numbering.Enabled);
        CaseRule rule = Assert.IsType<CaseRule>(Assert.Single(host.Rules));
        Assert.Equal(CaseMode.Lower, rule.Mode);
    }

    [Fact]
    public async Task ApplyPreset_OnePreviewRun()
    {
        var source = new FakeFileListSource();
        source.SetEntries(@"C:\A", File("a1.txt"), File("b.txt"));
        var files = new FileListModel(source);
        var delay = new FakeDelay();
        RulePanelHostModel host = RealHost(out _, out _, out _);
        var preview = new PreviewModel(host, files, delay.Delay);
        await files.LoadAsync(@"C:\A");
        await preview.Pending;
        Assert.Empty(delay.Calls);
        Preset preset = PresetOf(
            new CasePanelModel { Mode = CaseMode.Upper, Enabled = true },
            new ReplacePanelModel { Find = "a", With = "x", Enabled = true },
            new NumberingPanelModel { StartText = "5", Enabled = true });

        host.ApplyPreset(preset);
        delay.ReleaseAll();
        await preview.Pending;

        Assert.Single(delay.Calls);
        Assert.Equal(0, delay.Cancelled);
        Assert.Equal("X1_5.txt", preview.CellForRow(0).Text);
    }

    [Fact]
    public void ApplyPreset_UnknownStageForHost_Ignored()
    {
        FakeRulePanelModel replace = FakeRulePanelModel.Replace();
        var host = new RulePanelHostModel(new IRulePanelModel[] { replace });
        var preset = new Preset("Test", new[]
        {
            new PresetPanel(RuleStage.Replace, true, new Dictionary<string, string> { ["Text"] = "a" }),
            new PresetPanel(RuleStage.Add, true, new Dictionary<string, string> { ["Text"] = "b" }),
        });

        host.ApplyPreset(preset);

        Assert.Equal("a", replace.Text);
        Assert.True(replace.Enabled);
        Assert.IsType<ReplaceRule>(Assert.Single(host.Rules));
    }

    [Fact]
    public void ApplyPreset_Null_Throws()
    {
        var host = new RulePanelHostModel(new IRulePanelModel[] { FakeRulePanelModel.Replace() });

        Assert.Throws<ArgumentNullException>(() => host.ApplyPreset(null!));
    }
}
