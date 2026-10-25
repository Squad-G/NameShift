using System.Collections.Generic;
using NameShift.Core;
using NameShift.Ui.Rules;
using static NameShift.Cmd.OptionArgs;

namespace NameShift.Cmd;

/// <summary>Reads the Numbering panel's options: <c>--number</c> and <c>--number-*</c>.</summary>
internal sealed class NumberingReader : ApplyOptionReader
{
    internal const string NumberOption = "--number";
    internal const string NumberModeOption = "--number-mode";
    internal const string NumberStartOption = "--number-start";
    internal const string NumberStepOption = "--number-step";
    internal const string NumberPadOption = "--number-pad";
    internal const string NumberSeparatorOption = "--number-separator";
    internal const string NumberPositionOption = "--number-position";
    internal const string NumberSystemOption = "--number-system";
    internal const string NumberCaseOption = "--number-case";

    private static readonly string[] OwnWords =
    {
        NumberOption, NumberModeOption, NumberStartOption, NumberStepOption, NumberPadOption, NumberSeparatorOption,
        NumberPositionOption, NumberSystemOption, NumberCaseOption,
    };

    private static readonly KeyValuePair<string, NumberingMode>[] ModeValues =
    {
        new KeyValuePair<string, NumberingMode>("prefix", NumberingMode.Prefix),
        new KeyValuePair<string, NumberingMode>("suffix", NumberingMode.Suffix),
        new KeyValuePair<string, NumberingMode>("insert", NumberingMode.Insert),
    };

    // hex is the only spelling of Hexadecimal.
    private static readonly KeyValuePair<string, NumeralSystem>[] SystemValues =
    {
        new KeyValuePair<string, NumeralSystem>("decimal", NumeralSystem.Decimal),
        new KeyValuePair<string, NumeralSystem>("hex", NumeralSystem.Hexadecimal),
        new KeyValuePair<string, NumeralSystem>("letters", NumeralSystem.Letters),
        new KeyValuePair<string, NumeralSystem>("roman", NumeralSystem.Roman),
    };

    private static readonly KeyValuePair<string, bool>[] LetterCaseValues =
    {
        new KeyValuePair<string, bool>("upper", true),
        new KeyValuePair<string, bool>("lower", false),
    };

    private bool _number;
    private bool _anyNumbering;
    private NumberingMode? _mode;
    private NumeralSystem? _system;
    private bool? _upper;
    private string? _start;
    private string? _step;
    private string? _pad;
    private string? _separator;
    private string? _position;

    public override IReadOnlyList<string> Words => OwnWords;

    protected override OptionRead ReadOwn(IReadOnlyList<string> args, ref int i, string word)
    {
        ParsedCommand? error;
        bool ok;
        _anyNumbering = true;
        if (Is(word, NumberOption))
        {
            ok = TakeSwitch(ref _number, NumberOption, out error);
        }
        else if (Is(word, NumberModeOption))
        {
            ok = TakeChoice(args, ref i, NumberModeOption, ModeValues, Strings.NumberModeValues, ref _mode, out error);
        }
        else if (Is(word, NumberSystemOption))
        {
            ok = TakeChoice(args, ref i, NumberSystemOption, SystemValues, Strings.NumberSystemValues, ref _system,
                out error);
        }
        else if (Is(word, NumberCaseOption))
        {
            ok = TakeChoice(args, ref i, NumberCaseOption, LetterCaseValues, Strings.NumberCaseValues, ref _upper,
                out error);
        }
        else
        {
            ok = TakeRawText(args, ref i, word, out error);
        }

        return OptionRead.From(ok, error);
    }

    /// <summary>The five options whose value the panel model checks later.</summary>
    private bool TakeRawText(IReadOnlyList<string> args, ref int i, string word, out ParsedCommand? error)
    {
        if (Is(word, NumberStartOption))
        {
            return TakeText(args, ref i, NumberStartOption, ref _start, out error);
        }

        if (Is(word, NumberStepOption))
        {
            return TakeText(args, ref i, NumberStepOption, ref _step, out error);
        }

        if (Is(word, NumberPadOption))
        {
            return TakeText(args, ref i, NumberPadOption, ref _pad, out error);
        }

        if (Is(word, NumberSeparatorOption))
        {
            return TakeText(args, ref i, NumberSeparatorOption, ref _separator, out error);
        }

        return TakeText(args, ref i, NumberPositionOption, ref _position, out error);
    }

    public override ParsedCommand? Finish(ApplyDraft draft)
    {
        if (!_anyNumbering)
        {
            return null;
        }

        var numbering = new NumberingPanelModel();

        // The Numerals setter rewrites the start and resets the case to the system's usual one, so it comes before
        // UpperCase and StartText, whatever order the options were typed in.
        if (_mode != null)
        {
            numbering.Mode = _mode.Value;
        }

        if (_system != null)
        {
            numbering.Numerals = _system.Value;
        }

        if (_upper != null)
        {
            numbering.UpperCase = _upper.Value;
        }

        if (_start != null)
        {
            numbering.StartText = _start;
        }

        if (_step != null)
        {
            numbering.StepText = _step;
        }

        if (_pad != null)
        {
            numbering.PadText = _pad;
        }

        if (_separator != null)
        {
            numbering.Separator = _separator;
        }

        if (_position != null)
        {
            numbering.InsertPositionText = _position;
        }

        numbering.Enabled = true;

        ParsedCommand? problem = FirstProblem(numbering);
        if (problem != null)
        {
            return problem;
        }

        draft.Panels.Add(numbering);
        return null;
    }

    /// <summary>The window's own error text, only for the fields that apply, as the window shows it.</summary>
    private static ParsedCommand? FirstProblem(NumberingPanelModel numbering)
    {
        string? problem = null;
        string? problemOption = null;
        if (numbering.StartError.Length > 0)
        {
            problem = numbering.StartError;
            problemOption = NumberStartOption;
        }
        else if (numbering.StepError.Length > 0)
        {
            problem = numbering.StepError;
            problemOption = NumberStepOption;
        }
        else if (numbering.PadApplies && numbering.PadError.Length > 0)
        {
            problem = numbering.PadError;
            problemOption = NumberPadOption;
        }
        else if (numbering.InsertPositionApplies && numbering.InsertPositionError.Length > 0)
        {
            problem = numbering.InsertPositionError;
            problemOption = NumberPositionOption;
        }

        return problem == null
            ? null
            : Fail(Format(Strings.OptionError, problemOption!, WithoutSkipSentence(problem)));
    }
}
