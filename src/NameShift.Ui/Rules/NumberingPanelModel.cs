using System;
using System.Collections.Generic;
using System.Globalization;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The settings of the Numbering panel; BuildRule maps them to the Core <see cref="NumberingRule"/>.</summary>
/// <remarks>The numbers are held as typed. An invalid field is reported by its *Error property and makes
/// BuildRule return null, so numbering is skipped until the value is fixed.</remarks>
public sealed class NumberingPanelModel : RulePanelModel
{
    private const string DefaultSeparator = "_";

    private static readonly NumberingMode[] ModeList =
    {
        NumberingMode.Prefix, NumberingMode.Suffix, NumberingMode.Insert,
    };

    private NumberingMode _mode = NumberingMode.Suffix;
    private string _startText = "1";
    private string _stepText = "1";
    private string _padText = "0";
    private string _separator = DefaultSeparator;
    private string _insertPositionText = "";

    public NumberingPanelModel()
        : base(RuleStage.Numbering, Strings.NumberingTitle)
    {
    }

    /// <summary>The list order: Prefix, Suffix, Insert.</summary>
    public static IReadOnlyList<NumberingMode> Modes => ModeList;

    /// <summary>The mode's position in <see cref="Modes"/>, or -1 when it is not listed.</summary>
    public static int IndexOf(NumberingMode mode) => Array.IndexOf(ModeList, mode);

    /// <exception cref="ArgumentOutOfRangeException">The mode is not a defined value (a programmer error).</exception>
    public NumberingMode Mode
    {
        get => _mode;
        set
        {
            if (Array.IndexOf(ModeList, value) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown numbering mode.");
            }

            SetField(ref _mode, value);
        }
    }

    /// <summary>The first number as typed.</summary>
    public string StartText
    {
        get => _startText;
        set => SetField(ref _startText, value ?? "");
    }

    /// <summary>The step as typed.</summary>
    public string StepText
    {
        get => _stepText;
        set => SetField(ref _stepText, value ?? "");
    }

    /// <summary>The minimum number of digits as typed.</summary>
    public string PadText
    {
        get => _padText;
        set => SetField(ref _padText, value ?? "");
    }

    /// <summary>The text between the number and the name; never trimmed.</summary>
    public string Separator
    {
        get => _separator;
        set => SetField(ref _separator, value ?? "");
    }

    /// <summary>The position as typed (Insert mode only).</summary>
    public string InsertPositionText
    {
        get => _insertPositionText;
        set => SetField(ref _insertPositionText, value ?? "");
    }

    /// <summary>The first number, or null when it is not a whole number from 0.</summary>
    public int? Start => ParseWhole(StartText) is int start && start >= 0 ? start : (int?)null;

    /// <summary>The step, or null when it is not a whole number.</summary>
    public int? Step => ParseWhole(StepText);

    /// <summary>The minimum number of digits, or null when it is not a whole number from 0 to the engine's maximum.</summary>
    public int? Pad => ParseWhole(PadText) is int pad && pad >= 0 && pad <= NumberingRule.MaxPad ? pad : (int?)null;

    /// <summary>The typed position, or null when it is empty or not a whole number from 1.</summary>
    public int? InsertPosition => TextPosition.Parse(InsertPositionText);

    /// <summary>The position is used in Insert mode only.</summary>
    public bool InsertPositionApplies => Mode == NumberingMode.Insert;

    /// <summary>The message for an unusable start, or "" when it is fine.</summary>
    public string StartError => Start == null ? Strings.NumberingStartInvalid : "";

    /// <summary>The message for an unusable step, or "" when it is fine.</summary>
    public string StepError => Step == null ? Strings.NumberingStepInvalid : "";

    /// <summary>The message for an unusable pad, or "" when it is fine.</summary>
    public string PadError => Pad == null
        ? string.Format(CultureInfo.CurrentCulture, Strings.NumberingPadInvalid, NumberingRule.MaxPad)
        : "";

    /// <summary>The message for an unusable position, or "" when it is fine or not used.</summary>
    public string InsertPositionError =>
        InsertPositionApplies && InsertPosition == null ? Strings.NumberingPositionInvalid : "";

    public override bool IsDefault =>
        Mode == NumberingMode.Suffix && StartText == "1" && StepText == "1" && PadText == "0"
        && Separator == DefaultSeparator && InsertPositionText.Length == 0;

    /// <summary>The rule, or null while a field that applies is invalid (numbering is then skipped).</summary>
    public override IRenameRule? BuildRule()
    {
        if (Start is not int start || Step is not int step || Pad is not int pad)
        {
            return null;
        }

        if (InsertPositionApplies && InsertPosition == null)
        {
            return null;
        }

        return new NumberingRule(Mode, start, step, pad, Separator, InsertPositionApplies ? InsertPosition : null);
    }

    protected override void ResetSettings()
    {
        Mode = NumberingMode.Suffix;
        StartText = "1";
        StepText = "1";
        PadText = "0";
        Separator = DefaultSeparator;
        InsertPositionText = "";
    }

    private static int? ParseWhole(string text) =>
        int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : (int?)null;
}
