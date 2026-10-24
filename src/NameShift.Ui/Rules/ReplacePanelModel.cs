using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The settings of the Replace panel; BuildRule maps them to the Core <see cref="ReplaceRule"/>.</summary>
public sealed class ReplacePanelModel : RulePanelModel
{
    private string _find = "";
    private string _with = "";
    private bool _matchCase;
    private bool _includeExtension;
    private bool _keepCase;

    public ReplacePanelModel()
        : base(RuleStage.Replace, Strings.ReplaceTitle)
    {
    }

    public string Find
    {
        get => _find;
        set => SetField(ref _find, value ?? "");
    }

    public string With
    {
        get => _with;
        set => SetField(ref _with, value ?? "");
    }

    public bool MatchCase
    {
        get => _matchCase;
        set => SetField(ref _matchCase, value);
    }

    public bool IncludeExtension
    {
        get => _includeExtension;
        set => SetField(ref _includeExtension, value);
    }

    public bool KeepCase
    {
        get => _keepCase;
        set => SetField(ref _keepCase, value);
    }

    public override bool IsDefault =>
        Find.Length == 0 && With.Length == 0 && !MatchCase && !IncludeExtension && !KeepCase;

    /// <summary>The rule, or null while Find is empty (the rule would do nothing).</summary>
    public override IRenameRule? BuildRule() =>
        Find.Length == 0 ? null : new ReplaceRule(Find, With, MatchCase, IncludeExtension, KeepCase);

    protected override void ResetSettings()
    {
        Find = "";
        With = "";
        MatchCase = false;
        IncludeExtension = false;
        KeepCase = false;
    }
}
