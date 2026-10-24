using System;
using System.Collections.Generic;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The settings of the Case panel; BuildRule maps them to the Core <see cref="CaseRule"/>.</summary>
public sealed class CasePanelModel : RulePanelModel
{
    private static readonly CaseMode[] ModeList =
    {
        CaseMode.Same, CaseMode.Lower, CaseMode.Upper, CaseMode.Title, CaseMode.Sentence,
    };

    private CaseMode _mode = CaseMode.Same;
    private string _exceptionsText = "";

    public CasePanelModel()
        : base(RuleStage.Case, Strings.CaseTitle)
    {
    }

    /// <summary>The list order: Same, Lower, Upper, Title, Sentence.</summary>
    public static IReadOnlyList<CaseMode> Modes => ModeList;

    /// <summary>The mode's position in <see cref="Modes"/>, or -1 when it is not listed.</summary>
    public static int IndexOf(CaseMode mode) => Array.IndexOf(ModeList, mode);

    /// <exception cref="ArgumentOutOfRangeException">The mode is not a defined value (a programmer error).</exception>
    public CaseMode Mode
    {
        get => _mode;
        set
        {
            if (Array.IndexOf(ModeList, value) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown case mode.");
            }

            SetField(ref _mode, value);
        }
    }

    public string ExceptionsText
    {
        get => _exceptionsText;
        set => SetField(ref _exceptionsText, value ?? "");
    }

    /// <summary>The engine uses the exceptions in Title mode only.</summary>
    public bool ExceptionsApply => Mode == CaseMode.Title;

    public override bool IsDefault => Mode == CaseMode.Same && ExceptionsText.Length == 0;

    /// <summary>The rule, or null in Same mode (the rule would do nothing).</summary>
    public override IRenameRule? BuildRule() =>
        Mode == CaseMode.Same ? null : new CaseRule(Mode, CaseRule.ParseExceptions(ExceptionsText));

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, string> SaveSettings() => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [nameof(Mode)] = Mode.ToString(),
        [nameof(ExceptionsText)] = ExceptionsText,
    };

    /// <inheritdoc />
    protected override void ApplySettings(IReadOnlyDictionary<string, string> settings)
    {
        if (TryGetEnum(settings, nameof(Mode), out CaseMode mode) && IndexOf(mode) >= 0)
        {
            Mode = mode;
        }

        if (TryGetText(settings, nameof(ExceptionsText), out string exceptionsText))
        {
            ExceptionsText = exceptionsText;
        }
    }

    protected override void ResetSettings()
    {
        Mode = CaseMode.Same;
        ExceptionsText = "";
    }
}
