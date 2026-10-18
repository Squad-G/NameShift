using System;
using NameShift.Core;
using NameShift.Ui.Rules;

namespace NameShift.Ui.Tests.Rules;

/// <summary>A test-only panel model with one string setting.</summary>
internal sealed class FakeRulePanelModel : RulePanelModel
{
    private readonly Func<string, IRenameRule?> _build;
    private string _text = "";

    public FakeRulePanelModel(RuleStage stage, Func<string, IRenameRule?> build, string title = "&Fake")
        : base(stage, title)
    {
        _build = build;
    }

    public string Text
    {
        get => _text;
        set => SetField(ref _text, value);
    }

    public override bool IsDefault => Text.Length == 0;

    public override IRenameRule? BuildRule() => _build(Text);

    protected override void ResetSettings() => Text = "";

    public static FakeRulePanelModel Replace() =>
        new FakeRulePanelModel(RuleStage.Replace, t => new ReplaceRule(t, "_"), "&Replace");

    public static FakeRulePanelModel Add() =>
        new FakeRulePanelModel(RuleStage.Add, t => new AddRule(prefix: t), "&Add");

    public static FakeRulePanelModel Case() =>
        new FakeRulePanelModel(RuleStage.Case, _ => new CaseRule(CaseMode.Upper), "&Case");
}