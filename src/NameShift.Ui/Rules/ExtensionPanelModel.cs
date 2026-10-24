using System;
using System.Collections.Generic;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The settings of the Extension panel; BuildRule maps them to the Core <see cref="ExtensionRule"/>.</summary>
public sealed class ExtensionPanelModel : RulePanelModel
{
    private static readonly ExtensionMode[] ModeList =
    {
        ExtensionMode.Same, ExtensionMode.Lower, ExtensionMode.Upper, ExtensionMode.Fixed, ExtensionMode.Extra, ExtensionMode.Remove,
    };

    private ExtensionMode _mode = ExtensionMode.Same;
    private string _text = "";

    public ExtensionPanelModel()
        : base(RuleStage.Extension, Strings.ExtensionTitle)
    {
    }

    /// <summary>The list order: Same, Lower, Upper, Fixed, Extra, Remove.</summary>
    public static IReadOnlyList<ExtensionMode> Modes => ModeList;

    /// <summary>The mode's position in <see cref="Modes"/>, or -1 when it is not listed.</summary>
    public static int IndexOf(ExtensionMode mode) => Array.IndexOf(ModeList, mode);

    /// <exception cref="ArgumentOutOfRangeException">The mode is not a defined value (a programmer error).</exception>
    public ExtensionMode Mode
    {
        get => _mode;
        set
        {
            if (Array.IndexOf(ModeList, value) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown extension mode.");
            }

            SetField(ref _mode, value);
        }
    }

    public string Text
    {
        get => _text;
        set => SetField(ref _text, value ?? "");
    }

    /// <summary>The engine uses the text in Fixed and Extra modes only.</summary>
    public bool TextApplies => Mode == ExtensionMode.Fixed || Mode == ExtensionMode.Extra;

    public override bool IsDefault => Mode == ExtensionMode.Same && Text.Length == 0;

    /// <summary>The rule, or null in Same mode (the rule would do nothing).</summary>
    public override IRenameRule? BuildRule() =>
        Mode == ExtensionMode.Same ? null : new ExtensionRule(Mode, Text);

    protected override void ResetSettings()
    {
        Mode = ExtensionMode.Same;
        Text = "";
    }
}
