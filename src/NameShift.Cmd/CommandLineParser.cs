using System;
using System.Collections.Generic;
using System.Globalization;
using NameShift.Core;
using NameShift.Ui.Rules;

namespace NameShift.Cmd;

/// <summary>Reads a command line and dispatches on its first word: <c>help</c> (also <c>--help</c>, <c>-h</c>,
/// <c>apply &lt;folder&gt;</c> with the options <c>--replace &lt;find&gt; &lt;with&gt;</c>, <c>--case
/// lower|upper|title|sentence</c>, the Add panel's <c>--prefix</c>, <c>--suffix</c>, <c>--insert &lt;text&gt;
/// &lt;pos&gt;</c>, <c>--from-end</c> and <c>--word-space</c>, the Numbering panel's <c>--number</c>, <c>--number-mode</c>,
/// <c>--number-start</c>, <c>--number-step</c>, <c>--number-pad</c>, <c>--number-separator</c>,
/// <c>--number-position</c>, <c>--number-system</c> and <c>--number-case</c>, and <c>--dry-run</c>,
/// <c>--quiet|-q</c>, <c>--verbose|-v</c>; or <c>undo [--dry-run]</c>. Options of <c>apply</c> come in any order; the
/// folder is the one argument that is neither an option nor an option's value. <c>undo</c> takes no folder. Words are
/// compared ordinally, ignoring case.</summary>
internal static class CommandLineParser
{
    // Verbs.
    private const string ApplyCommand = "apply";
    private const string UndoCommand = "undo";

    // Global words: help and version.
    private const string HelpWord = "help";
    private const string HelpOption = "--help";
    private const string HelpShortOption = "-h";
    private const string HelpSlashOption = "/?";
    private const string VersionOption = "--version";

    // Options of apply.
    private const string ReplaceOption = "--replace";
    private const string CaseOption = "--case";
    private const string DryRunOption = "--dry-run";
    private const string QuietOption = "--quiet";
    private const string QuietShortOption = "-q";
    private const string VerboseOption = "--verbose";
    private const string VerboseShortOption = "-v";
    private const string PrefixOption = "--prefix";
    private const string SuffixOption = "--suffix";
    private const string InsertOption = "--insert";
    private const string FromEndOption = "--from-end";
    private const string WordSpaceOption = "--word-space";
    private const string NumberOption = "--number";
    private const string NumberModeOption = "--number-mode";
    private const string NumberStartOption = "--number-start";
    private const string NumberStepOption = "--number-step";
    private const string NumberPadOption = "--number-pad";
    private const string NumberSeparatorOption = "--number-separator";
    private const string NumberPositionOption = "--number-position";
    private const string NumberSystemOption = "--number-system";
    private const string NumberCaseOption = "--number-case";

    private static readonly string[] GlobalWordList =
    {
        ApplyCommand, UndoCommand, HelpWord, HelpOption, HelpShortOption, HelpSlashOption, VersionOption,
    };

    private static readonly string[] ApplyOptionWordList =
    {
        ReplaceOption, CaseOption, DryRunOption, QuietOption, QuietShortOption, VerboseOption, VerboseShortOption,
        PrefixOption, SuffixOption, InsertOption, FromEndOption, WordSpaceOption, NumberOption, NumberModeOption,
        NumberStartOption, NumberStepOption, NumberPadOption, NumberSeparatorOption, NumberPositionOption,
        NumberSystemOption, NumberCaseOption,
    };

    private static readonly string[] UndoOptionWordList = { DryRunOption };

    private static readonly string[] AllWords = Combine(GlobalWordList, ApplyOptionWordList);

    private static readonly KeyValuePair<string, CaseMode>[] CaseValues =
    {
        new KeyValuePair<string, CaseMode>("lower", CaseMode.Lower),
        new KeyValuePair<string, CaseMode>("upper", CaseMode.Upper),
        new KeyValuePair<string, CaseMode>("title", CaseMode.Title),
        new KeyValuePair<string, CaseMode>("sentence", CaseMode.Sentence),
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

    /// <summary>Every word the parser dispatches on: the verbs, the help and version words, and the options of
    /// <c>apply</c>. The <c>--case</c> values are not words. The dispatch and the option guard read this list.</summary>
    public static IReadOnlyList<string> Words => AllWords;

    /// <summary>The verbs and the help and version words: the main help names each of them.</summary>
    internal static IReadOnlyList<string> GlobalWords => GlobalWordList;

    /// <summary>The options of <c>apply</c>: the apply help names each of them.</summary>
    internal static IReadOnlyList<string> ApplyOptionWords => ApplyOptionWordList;

    /// <summary>The options of <c>undo</c>: the undo help names each of them. Not part of <see cref="Words"/> (its
    /// <c>--dry-run</c> is already there).</summary>
    internal static IReadOnlyList<string> UndoOptionWords => UndoOptionWordList;

    /// <summary>What the command line asks for; a usage error carries its one line (message, usage, help pointer).</summary>
    public static ParsedCommand Parse(IReadOnlyList<string> args)
    {
        if (args == null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        if (args.Count == 0)
        {
            return Fail(Strings.NoCommand);
        }

        string first = args[0];
        if (Is(first, HelpOption) || Is(first, HelpShortOption) || Is(first, HelpSlashOption) || Is(first, HelpWord))
        {
            return ParsedCommand.Help();
        }

        if (Is(first, VersionOption))
        {
            return ParsedCommand.Version();
        }

        if (Is(first, ApplyCommand))
        {
            for (int i = 1; i < args.Count; i++)
            {
                if (IsHelpFlag(args[i]))
                {
                    return ParsedCommand.ApplyHelp();
                }
            }

            return ParseApply(args);
        }

        if (Is(first, UndoCommand))
        {
            for (int i = 1; i < args.Count; i++)
            {
                if (IsHelpFlag(args[i]))
                {
                    return ParsedCommand.UndoHelp();
                }
            }

            return ParseUndo(args);
        }

        return Fail(Format(Strings.UnknownCommand, first));
    }

    private static ParsedCommand ParseUndo(IReadOnlyList<string> args)
    {
        bool dryRun = false;
        for (int i = 1; i < args.Count; i++)
        {
            if (!Is(args[i], DryRunOption))
            {
                return Fail(Strings.UndoOnlyDryRun);
            }

            if (dryRun)
            {
                return Fail(Format(Strings.RepeatedOption, DryRunOption));
            }

            dryRun = true;
        }

        return ParsedCommand.Undo(dryRun);
    }

    private static ParsedCommand ParseApply(IReadOnlyList<string> args)
    {
        string? folder = null;
        string? find = null;
        string? with = null;
        CaseMode? caseMode = null;
        bool dryRun = false;
        bool quiet = false;
        bool verbose = false;
        string? prefix = null;
        string? suffix = null;
        string? insertText = null;
        string? insertPos = null;
        bool fromEnd = false;
        bool wordSpace = false;
        bool anyAdd = false;
        bool number = false;
        bool anyNumbering = false;
        NumberingMode? mode = null;
        NumeralSystem? system = null;
        bool? upper = null;
        string? start = null;
        string? step = null;
        string? pad = null;
        string? separator = null;
        string? position = null;
        for (int i = 1; i < args.Count; i++)
        {
            string arg = args[i];
            ParsedCommand? error;
            string? value;
            if (Is(arg, ReplaceOption))
            {
                // A value that is an option word means a value was left out: `--replace IMG --dry-run` must not turn
                // --dry-run into the new text and rename for real. Other values may start with a dash (--replace - _).
                if (args.Count - i - 1 < 2 || IsOptionWord(args[i + 1]) || IsOptionWord(args[i + 2]))
                {
                    return Fail(Strings.ReplaceNeedsValues);
                }

                if (find != null)
                {
                    return Fail(Format(Strings.RepeatedOption, ReplaceOption));
                }

                if (args[i + 1].Length == 0)
                {
                    return Fail(Strings.EmptyFind);
                }

                find = args[i + 1];
                with = args[i + 2];
                i += 2;
            }
            else if (Is(arg, CaseOption))
            {
                if (args.Count - i - 1 < 1)
                {
                    return Fail(Strings.CaseNeedsValue);
                }

                if (!TryCase(args[i + 1], out CaseMode parsedCase))
                {
                    return Fail(Format(Strings.BadCase, args[i + 1]));
                }

                if (caseMode.HasValue)
                {
                    return Fail(Format(Strings.RepeatedOption, CaseOption));
                }

                caseMode = parsedCase;
                i += 1;
            }
            else if (Is(arg, DryRunOption))
            {
                dryRun = true;
            }
            else if (Is(arg, QuietOption) || Is(arg, QuietShortOption))
            {
                if (quiet)
                {
                    return Fail(Format(Strings.RepeatedOption, QuietOption));
                }

                quiet = true;
            }
            else if (Is(arg, VerboseOption) || Is(arg, VerboseShortOption))
            {
                if (verbose)
                {
                    return Fail(Format(Strings.RepeatedOption, VerboseOption));
                }

                verbose = true;
            }
            else if (Is(arg, PrefixOption))
            {
                if (!TakeOne(args, ref i, PrefixOption, out value, out error))
                {
                    return error!;
                }

                if (prefix != null)
                {
                    return Fail(Format(Strings.RepeatedOption, PrefixOption));
                }

                if (value!.Length == 0)
                {
                    return Fail(Strings.EmptyPrefix);
                }

                prefix = value;
                anyAdd = true;
            }
            else if (Is(arg, SuffixOption))
            {
                if (!TakeOne(args, ref i, SuffixOption, out value, out error))
                {
                    return error!;
                }

                if (suffix != null)
                {
                    return Fail(Format(Strings.RepeatedOption, SuffixOption));
                }

                if (value!.Length == 0)
                {
                    return Fail(Strings.EmptySuffix);
                }

                suffix = value;
                anyAdd = true;
            }
            else if (Is(arg, InsertOption))
            {
                if (args.Count - i - 1 < 2 || IsOptionWord(args[i + 1]) || IsOptionWord(args[i + 2]))
                {
                    return Fail(Strings.InsertNeedsValues);
                }

                if (insertText != null)
                {
                    return Fail(Format(Strings.RepeatedOption, InsertOption));
                }

                if (args[i + 1].Length == 0)
                {
                    return Fail(Strings.EmptyInsert);
                }

                if (TextPosition.Parse(args[i + 2]) == null)
                {
                    return Fail(Format(Strings.BadInsertPosition, args[i + 2]));
                }

                insertText = args[i + 1];
                insertPos = args[i + 2];
                i += 2;
                anyAdd = true;
            }
            else if (Is(arg, FromEndOption))
            {
                if (fromEnd)
                {
                    return Fail(Format(Strings.RepeatedOption, FromEndOption));
                }

                fromEnd = true;
            }
            else if (Is(arg, WordSpaceOption))
            {
                if (wordSpace)
                {
                    return Fail(Format(Strings.RepeatedOption, WordSpaceOption));
                }

                wordSpace = true;
                anyAdd = true;
            }
            else if (Is(arg, NumberOption))
            {
                if (number)
                {
                    return Fail(Format(Strings.RepeatedOption, NumberOption));
                }

                number = true;
                anyNumbering = true;
            }
            else if (Is(arg, NumberModeOption))
            {
                if (!TakeOne(args, ref i, NumberModeOption, out value, out error))
                {
                    return error!;
                }

                if (mode != null)
                {
                    return Fail(Format(Strings.RepeatedOption, NumberModeOption));
                }

                if (!TryChoice(value!, ModeValues, out NumberingMode parsedMode))
                {
                    return Fail(Format(Strings.BadChoice, value!, NumberModeOption, Strings.NumberModeValues));
                }

                mode = parsedMode;
                anyNumbering = true;
            }
            else if (Is(arg, NumberSystemOption))
            {
                if (!TakeOne(args, ref i, NumberSystemOption, out value, out error))
                {
                    return error!;
                }

                if (system != null)
                {
                    return Fail(Format(Strings.RepeatedOption, NumberSystemOption));
                }

                if (!TryChoice(value!, SystemValues, out NumeralSystem parsedSystem))
                {
                    return Fail(Format(Strings.BadChoice, value!, NumberSystemOption, Strings.NumberSystemValues));
                }

                system = parsedSystem;
                anyNumbering = true;
            }
            else if (Is(arg, NumberCaseOption))
            {
                if (!TakeOne(args, ref i, NumberCaseOption, out value, out error))
                {
                    return error!;
                }

                if (upper != null)
                {
                    return Fail(Format(Strings.RepeatedOption, NumberCaseOption));
                }

                if (!TryChoice(value!, LetterCaseValues, out bool parsedUpper))
                {
                    return Fail(Format(Strings.BadChoice, value!, NumberCaseOption, Strings.NumberCaseValues));
                }

                upper = parsedUpper;
                anyNumbering = true;
            }
            else if (Is(arg, NumberStartOption))
            {
                if (!TakeText(args, ref i, NumberStartOption, ref start, out error))
                {
                    return error!;
                }

                anyNumbering = true;
            }
            else if (Is(arg, NumberStepOption))
            {
                if (!TakeText(args, ref i, NumberStepOption, ref step, out error))
                {
                    return error!;
                }

                anyNumbering = true;
            }
            else if (Is(arg, NumberPadOption))
            {
                if (!TakeText(args, ref i, NumberPadOption, ref pad, out error))
                {
                    return error!;
                }

                anyNumbering = true;
            }
            else if (Is(arg, NumberSeparatorOption))
            {
                if (!TakeText(args, ref i, NumberSeparatorOption, ref separator, out error))
                {
                    return error!;
                }

                anyNumbering = true;
            }
            else if (Is(arg, NumberPositionOption))
            {
                if (!TakeText(args, ref i, NumberPositionOption, ref position, out error))
                {
                    return error!;
                }

                anyNumbering = true;
            }
            else if (arg.StartsWith("-", StringComparison.Ordinal))
            {
                return Fail(Format(Strings.UnknownOption, arg));
            }
            else if (string.IsNullOrWhiteSpace(arg))
            {
                // An empty variable in a script ("%~1", "$folder"): never fall back to the current directory.
                return Fail(Strings.NoFolder);
            }
            else if (folder != null)
            {
                return Fail(Format(Strings.TwoFolders, folder, arg));
            }
            else
            {
                folder = arg;
            }
        }

        // A conflict is reported before a missing folder or rule: it is the clearer mistake. --verbose wins the tie.
        if (quiet && verbose)
        {
            return Fail(Strings.QuietWithVerbose);
        }

        if (quiet && dryRun)
        {
            return Fail(Strings.QuietWithDryRun);
        }

        if (fromEnd && insertText == null)
        {
            return Fail(Strings.FromEndNeedsInsert);
        }

        AddPanelModel? add = null;
        if (anyAdd || fromEnd)
        {
            add = new AddPanelModel
            {
                Prefix = prefix ?? "",
                Suffix = suffix ?? "",
                Insert = insertText ?? "",
                InsertPositionText = insertPos ?? "",
                InsertFromEnd = fromEnd,
                WordSpace = wordSpace,
                Enabled = true,
            };
        }

        NumberingPanelModel? numbering = null;
        if (anyNumbering)
        {
            numbering = new NumberingPanelModel();

            // The Numerals setter rewrites the start and resets the case to the system's usual one, so it comes before
            // UpperCase and StartText, whatever order the options were typed in.
            if (mode != null)
            {
                numbering.Mode = mode.Value;
            }

            if (system != null)
            {
                numbering.Numerals = system.Value;
            }

            if (upper != null)
            {
                numbering.UpperCase = upper.Value;
            }

            if (start != null)
            {
                numbering.StartText = start;
            }

            if (step != null)
            {
                numbering.StepText = step;
            }

            if (pad != null)
            {
                numbering.PadText = pad;
            }

            if (separator != null)
            {
                numbering.Separator = separator;
            }

            if (position != null)
            {
                numbering.InsertPositionText = position;
            }

            numbering.Enabled = true;

            // The window's own error text, only for the fields that apply, as the window shows it.
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

            if (problem != null)
            {
                return Fail(Format(Strings.OptionError, problemOption!, problem));
            }
        }

        if (folder == null)
        {
            return Fail(Strings.NoFolder);
        }

        // A folder with a quote is what cmd makes of "D:\Pics\" --case lower: one argument that swallowed the options.
        // The runner says so (Strings.QuoteInFolder), which helps more than "give a rule".
        if (find == null && !caseMode.HasValue && add == null && numbering == null && folder.IndexOf('"') < 0)
        {
            return Fail(Strings.NoRule);
        }

        OutputLevel output = quiet ? OutputLevel.Quiet : verbose ? OutputLevel.Verbose : OutputLevel.Normal;
        return ParsedCommand.Apply(new ApplyOptions(
            folder, find, with, caseMode, dryRun, output, new ApplyPanels(add, numbering)));
    }

    /// <summary>The one value after an option. A value that is an option word counts as missing (`--prefix --dry-run`
    /// must not turn --dry-run into the prefix); other values may start with a dash (`--number-separator -`).</summary>
    private static bool TakeOne(IReadOnlyList<string> args, ref int i, string option, out string? value,
        out ParsedCommand? error)
    {
        if (args.Count - i - 1 < 1 || IsOptionWord(args[i + 1]))
        {
            value = null;
            error = Fail(Format(Strings.OptionNeedsValue, option));
            return false;
        }

        value = args[i + 1];
        error = null;
        i += 1;
        return true;
    }

    /// <summary>Takes the raw value of an option that can be given once; the panel model checks it later.</summary>
    private static bool TakeText(IReadOnlyList<string> args, ref int i, string option, ref string? target,
        out ParsedCommand? error)
    {
        if (!TakeOne(args, ref i, option, out string? value, out error))
        {
            return false;
        }

        if (target != null)
        {
            error = Fail(Format(Strings.RepeatedOption, option));
            return false;
        }

        target = value;
        return true;
    }

    private static bool TryChoice<T>(string text, KeyValuePair<string, T>[] values, out T result)
    {
        foreach (KeyValuePair<string, T> value in values)
        {
            if (Is(text, value.Key))
            {
                result = value.Value;
                return true;
            }
        }

        result = default!;
        return false;
    }

    private static bool TryCase(string text, out CaseMode mode)
    {
        foreach (KeyValuePair<string, CaseMode> value in CaseValues)
        {
            if (Is(text, value.Key))
            {
                mode = value.Value;
                return true;
            }
        }

        mode = CaseMode.Same;
        return false;
    }

    private static bool Is(string arg, string word) => string.Equals(arg, word, StringComparison.OrdinalIgnoreCase);

    private static bool IsHelpFlag(string arg) => Is(arg, HelpOption) || Is(arg, HelpShortOption) || Is(arg, HelpSlashOption);

    /// <summary>True for a word of <see cref="Words"/> that starts with a dash.</summary>
    private static bool IsOptionWord(string arg)
    {
        if (!arg.StartsWith("-", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (string word in AllWords)
        {
            if (Is(arg, word))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] Combine(string[] first, string[] second)
    {
        var all = new string[first.Length + second.Length];
        first.CopyTo(all, 0);
        second.CopyTo(all, first.Length);
        return all;
    }

    private static string Format(string format, params object[] values) =>
        string.Format(CultureInfo.InvariantCulture, format, values);

    /// <summary>A usage error: the message, one space, the usage line, one space, the pointer to the help.</summary>
    private static ParsedCommand Fail(string message) =>
        ParsedCommand.Failed(message + " " + Strings.Usage + " " + Strings.HelpPointer);
}
