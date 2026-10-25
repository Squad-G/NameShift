using System;
using System.Collections.Generic;
using System.Globalization;

namespace NameShift.Cmd;

/// <summary>The helpers every reader shares: word comparison, taking values, usage errors.</summary>
internal static class OptionArgs
{
    public static bool Is(string arg, string word) => string.Equals(arg, word, StringComparison.OrdinalIgnoreCase);

    /// <summary>True for a word of <see cref="CommandLineParser.Words"/> that starts with a dash.</summary>
    public static bool IsOptionWord(string arg)
    {
        if (!arg.StartsWith("-", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (string word in CommandLineParser.Words)
        {
            if (Is(arg, word))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The one value after an option. A value that is an option word counts as missing (`--prefix --dry-run`
    /// must not turn --dry-run into the prefix); other values may start with a dash (`--number-separator -`).</summary>
    public static bool TakeOne(IReadOnlyList<string> args, ref int i, string option, out string? value,
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
    public static bool TakeText(IReadOnlyList<string> args, ref int i, string option, ref string? target,
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

    public static bool TryChoice<T>(string text, KeyValuePair<string, T>[] values, out T result)
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

    /// <summary>The window's error texts end with a sentence that says numbering is skipped; on the command line the run
    /// fails instead, so the sentence is removed.</summary>
    public static string WithoutSkipSentence(string problem)
    {
        string sentence = Strings.NumberingSkippedSentence;
        return problem.EndsWith(sentence, StringComparison.Ordinal)
            ? problem.Substring(0, problem.Length - sentence.Length).TrimEnd()
            : problem;
    }

    public static string Format(string format, params object[] values) =>
        string.Format(CultureInfo.InvariantCulture, format, values);

    /// <summary>A usage error: the message, one space, the usage line, one space, the pointer to the help.</summary>
    public static ParsedCommand Fail(string message) =>
        ParsedCommand.Failed(message + " " + Strings.Usage + " " + Strings.HelpPointer);
}
