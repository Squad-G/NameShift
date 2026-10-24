using System;
using System.Collections.Generic;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The settings of the Name panel; BuildRule maps them to the Core <see cref="NameRule"/>.</summary>
public sealed class NamePanelModel : RulePanelModel
{
    private static readonly NameMode[] ModeList =
    {
        NameMode.Keep, NameMode.Fixed, NameMode.Remove, NameMode.Reverse,
    };

    private NameMode _mode = NameMode.Keep;
    private string _text = "";

    public NamePanelModel()
        : base(RuleStage.Name, Strings.NameTitle)
    {
    }

    /// <summary>The list order: Keep, Fixed, Remove, Reverse.</summary>
    public static IReadOnlyList<NameMode> Modes => ModeList;

    /// <summary>The mode's position in <see cref="Modes"/>, or -1 when it is not listed.</summary>
    public static int IndexOf(NameMode mode) => Array.IndexOf(ModeList, mode);

    /// <exception cref="ArgumentOutOfRangeException">The mode is not a defined value (a programmer error).</exception>
    public NameMode Mode
    {
        get => _mode;
        set
        {
            if (Array.IndexOf(ModeList, value) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown name mode.");
            }

            SetField(ref _mode, value);
        }
    }

    public string Text
    {
        get => _text;
        set => SetField(ref _text, value ?? "");
    }

    /// <summary>The engine uses the text in Fixed mode only.</summary>
    public bool TextApplies => Mode == NameMode.Fixed;

    public override bool IsDefault => Mode == NameMode.Keep && Text.Length == 0;

    /// <summary>The rule, or null in Keep mode (the rule would do nothing).</summary>
    public override IRenameRule? BuildRule() =>
        Mode == NameMode.Keep ? null : new NameRule(Mode, Text);

    protected override void ResetSettings()
    {
        Mode = NameMode.Keep;
        Text = "";
    }
}
