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

    private static readonly NumeralSystem[] SystemList =
    {
        NumeralSystem.Decimal, NumeralSystem.Hexadecimal, NumeralSystem.Letters, NumeralSystem.Roman,
    };

    private static readonly bool[] CaseList = { true, false };

    private NumberingMode _mode = NumberingMode.Suffix;
    private NumeralSystem _numerals = NumeralSystem.Decimal;
    private bool _upperCase = true;
    private string _startText = "1";
    private string _stepText = "1";
    private string _padText = "0";
    private string _separator = DefaultSeparator;
    private string _insertPositionText = "";
    private bool _restartPerFolder;

    public NumberingPanelModel()
        : base(RuleStage.Numbering, Strings.NumberingTitle)
    {
    }

    /// <summary>The list order: Prefix, Suffix, Insert.</summary>
    public static IReadOnlyList<NumberingMode> Modes => ModeList;

    /// <summary>The mode's position in <see cref="Modes"/>, or -1 when it is not listed.</summary>
    public static int IndexOf(NumberingMode mode) => Array.IndexOf(ModeList, mode);

    /// <summary>The list order: Decimal, Hexadecimal, Letters, Roman.</summary>
    public static IReadOnlyList<NumeralSystem> Systems => SystemList;

    /// <summary>The system's position in <see cref="Systems"/>, or -1 when it is not listed.</summary>
    public static int IndexOf(NumeralSystem numerals) => Array.IndexOf(SystemList, numerals);

    /// <summary>The list order of the letter case: capital letters (true), then small letters (false).</summary>
    public static IReadOnlyList<bool> Cases => CaseList;

    /// <summary>The position of a case in <see cref="Cases"/>.</summary>
    public static int IndexOfCase(bool upperCase) => Array.IndexOf(CaseList, upperCase);

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

    /// <summary>The number system of the start and of the numbers written. Changing it picks the system's usual
    /// case and rewrites the start when its value can be written in the new system.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The system is not a defined value (a programmer error).</exception>
    public NumeralSystem Numerals
    {
        get => _numerals;
        set
        {
            if (Array.IndexOf(SystemList, value) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown numeral system.");
            }

            if (value == _numerals)
            {
                return;
            }

            int? oldStart = Start;
            _numerals = value;
            _upperCase = UsualCase(value);
            if (oldStart is int number && CanWrite(number, value))
            {
                _startText = NumberText.Format(number, value, 0, _upperCase);
            }

            OnChanged();
        }
    }

    /// <summary>Capital letters in Hexadecimal and Roman numerals; kept until the system changes.</summary>
    public bool UpperCase
    {
        get => _upperCase;
        set => SetField(ref _upperCase, value);
    }

    /// <summary>The first number as typed, in the chosen system.</summary>
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

    /// <summary>True: the number starts again at the start value in each folder of the list (NumberingRule.BreakPerFolder).
    /// Changes nothing while the list holds one folder.</summary>
    public bool RestartPerFolder
    {
        get => _restartPerFolder;
        set => SetField(ref _restartPerFolder, value);
    }

    /// <summary>The first number read in the chosen system, or null when it cannot be read.</summary>
    public int? Start
    {
        get
        {
            if (Numerals == NumeralSystem.Decimal)
            {
                return ParseWhole(StartText) is int start && start >= 0 ? start : (int?)null;
            }

            return NumberText.TryParse(StartText, Numerals, out int value) ? value : (int?)null;
        }
    }

    /// <summary>The step, or null when it is not a whole number.</summary>
    public int? Step => ParseWhole(StepText);

    /// <summary>The minimum number of digits, or null when it is not a whole number from 0 to the engine's maximum.</summary>
    public int? Pad => ParseWhole(PadText) is int pad && pad >= 0 && pad <= NumberingRule.MaxPad ? pad : (int?)null;

    /// <summary>The typed position, or null when it is empty or not a whole number from 1.</summary>
    public int? InsertPosition => TextPosition.Parse(InsertPositionText);

    /// <summary>The position is used in Insert mode only.</summary>
    public bool InsertPositionApplies => Mode == NumberingMode.Insert;

    /// <summary>The digits box applies in Decimal and Hexadecimal; Letters and Roman ignore the pad.</summary>
    public bool PadApplies => Numerals == NumeralSystem.Decimal || Numerals == NumeralSystem.Hexadecimal;

    /// <summary>The letter case applies in every system but Decimal.</summary>
    public bool CaseApplies => Numerals != NumeralSystem.Decimal;

    /// <summary>The message for an unusable start, or "" when it is fine.</summary>
    public string StartError
    {
        get
        {
            if (Start != null)
            {
                return "";
            }

            switch (Numerals)
            {
                case NumeralSystem.Hexadecimal:
                    return Strings.NumberingStartInvalidHexadecimal;
                case NumeralSystem.Letters:
                    return Strings.NumberingStartInvalidLetters;
                case NumeralSystem.Roman:
                    return Strings.NumberingStartInvalidRoman;
                default:
                    return Strings.NumberingStartInvalid;
            }
        }
    }

    /// <summary>The message for an unusable step, or "" when it is fine.</summary>
    public string StepError => Step == null ? Strings.NumberingStepInvalid : "";

    /// <summary>The message for an unusable pad, or "" when it is fine.</summary>
    public string PadError => PadApplies && Pad == null
        ? string.Format(CultureInfo.CurrentCulture, Strings.NumberingPadInvalid, NumberingRule.MaxPad)
        : "";

    /// <summary>The message for an unusable position, or "" when it is fine or not used.</summary>
    public string InsertPositionError =>
        InsertPositionApplies && InsertPosition == null ? Strings.NumberingPositionInvalid : "";

    public override bool IsDefault =>
        Mode == NumberingMode.Suffix && Numerals == NumeralSystem.Decimal && UpperCase && StartText == "1" && StepText == "1" && PadText == "0"
        && Separator == DefaultSeparator && InsertPositionText.Length == 0 && !RestartPerFolder;

    /// <summary>The rule, or null while a field that applies is invalid (numbering is then skipped).</summary>
    public override IRenameRule? BuildRule()
    {
        if (Start is not int start || Step is not int step)
        {
            return null;
        }

        int pad = 0;
        if (PadApplies)
        {
            if (Pad is not int typedPad)
            {
                return null;
            }

            pad = typedPad;
        }

        if (InsertPositionApplies && InsertPosition == null)
        {
            return null;
        }

        return new NumberingRule(Mode, start, step, pad, Separator, InsertPositionApplies ? InsertPosition : null,
            Numerals, UpperCase, breakPerFolder: RestartPerFolder);
    }

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, string> SaveSettings() => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [nameof(Numerals)] = Numerals.ToString(),
        [nameof(UpperCase)] = BoolText(UpperCase),
        [nameof(StartText)] = StartText,
        [nameof(Mode)] = Mode.ToString(),
        [nameof(StepText)] = StepText,
        [nameof(PadText)] = PadText,
        [nameof(Separator)] = Separator,
        [nameof(InsertPositionText)] = InsertPositionText,
        [nameof(RestartPerFolder)] = BoolText(RestartPerFolder),
    };

    /// <inheritdoc />
    /// <remarks>A preset with a system other than Decimal and no StartText key gets the start the panel itself
    /// shows after switching to that system (Roman: "I"), and that system's usual case when UpperCase is missing.</remarks>
    protected override void ApplySettings(IReadOnlyDictionary<string, string> settings)
    {
        // Numerals first: its setter rewrites StartText and resets UpperCase, so the saved case and start come after it.
        if (TryGetEnum(settings, nameof(Numerals), out NumeralSystem numerals) && IndexOf(numerals) >= 0)
        {
            Numerals = numerals;
        }

        if (TryGetBool(settings, nameof(UpperCase), out bool upperCase))
        {
            UpperCase = upperCase;
        }

        if (TryGetText(settings, nameof(StartText), out string startText))
        {
            StartText = startText;
        }

        if (TryGetEnum(settings, nameof(Mode), out NumberingMode mode) && IndexOf(mode) >= 0)
        {
            Mode = mode;
        }

        if (TryGetText(settings, nameof(StepText), out string stepText))
        {
            StepText = stepText;
        }

        if (TryGetText(settings, nameof(PadText), out string padText))
        {
            PadText = padText;
        }

        if (TryGetText(settings, nameof(Separator), out string separator))
        {
            Separator = separator;
        }

        if (TryGetText(settings, nameof(InsertPositionText), out string insertPositionText))
        {
            InsertPositionText = insertPositionText;
        }

        if (TryGetBool(settings, nameof(RestartPerFolder), out bool restart))
        {
            RestartPerFolder = restart;
        }
    }

    protected override void ResetSettings()
    {
        Mode = NumberingMode.Suffix;
        Numerals = NumeralSystem.Decimal;
        UpperCase = true;
        StartText = "1";
        StepText = "1";
        PadText = "0";
        Separator = DefaultSeparator;
        InsertPositionText = "";
        RestartPerFolder = false;
    }

    private static bool UsualCase(NumeralSystem numerals) => numerals != NumeralSystem.Letters;

    private static bool CanWrite(int value, NumeralSystem numerals)
    {
        switch (numerals)
        {
            case NumeralSystem.Letters:
                return value >= 1;
            case NumeralSystem.Roman:
                return value >= 1 && value <= NumberText.MaxRoman;
            default:
                return value >= 0;
        }
    }

    private static int? ParseWhole(string text) =>
        int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : (int?)null;
}
