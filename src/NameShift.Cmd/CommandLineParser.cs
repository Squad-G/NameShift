using System;
using System.Collections.Generic;
using System.Globalization;
using NameShift.Core;

namespace NameShift.Cmd;

/// <summary>Reads <c>apply &lt;folder&gt; [--replace &lt;find&gt; &lt;with&gt;] [--case lower|upper|title|sentence]
/// [--dry-run]</c>. Options come in any order; the folder is the one argument that is neither an option nor an option's
/// value. Words are compared ordinally, ignoring case.</summary>
internal static class CommandLineParser
{
    private const string ApplyCommand = "apply";
    private const string ReplaceOption = "--replace";
    private const string CaseOption = "--case";
    private const string DryRunOption = "--dry-run";

    private static readonly KeyValuePair<string, CaseMode>[] CaseValues =
    {
        new KeyValuePair<string, CaseMode>("lower", CaseMode.Lower),
        new KeyValuePair<string, CaseMode>("upper", CaseMode.Upper),
        new KeyValuePair<string, CaseMode>("title", CaseMode.Title),
        new KeyValuePair<string, CaseMode>("sentence", CaseMode.Sentence),
    };

    /// <summary>True with the options; false with the one-line error (a usage error ends with Strings.Usage).</summary>
    public static bool TryParse(IReadOnlyList<string> args, out ApplyOptions? options, out string error)
    {
        if (args == null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        options = null;
        error = "";
        if (args.Count == 0)
        {
            return Fail(Strings.NoCommand, out error);
        }

        if (!Is(args[0], ApplyCommand))
        {
            return Fail(Format(Strings.UnknownCommand, args[0]), out error);
        }

        string? folder = null;
        string? find = null;
        string? with = null;
        CaseMode? caseMode = null;
        bool dryRun = false;
        for (int i = 1; i < args.Count; i++)
        {
            string arg = args[i];
            if (Is(arg, ReplaceOption))
            {
                // A value that is an option word means a value was left out: `--replace IMG --dry-run` must not turn
                // --dry-run into the new text and rename for real. Other values may start with a dash (--replace - _).
                if (args.Count - i - 1 < 2 || IsOptionWord(args[i + 1]) || IsOptionWord(args[i + 2]))
                {
                    return Fail(Strings.ReplaceNeedsValues, out error);
                }

                if (find != null)
                {
                    return Fail(Format(Strings.RepeatedOption, ReplaceOption), out error);
                }

                if (args[i + 1].Length == 0)
                {
                    return Fail(Strings.EmptyFind, out error);
                }

                find = args[i + 1];
                with = args[i + 2];
                i += 2;
            }
            else if (Is(arg, CaseOption))
            {
                if (args.Count - i - 1 < 1)
                {
                    return Fail(Strings.CaseNeedsValue, out error);
                }

                if (!TryCase(args[i + 1], out CaseMode mode))
                {
                    return Fail(Format(Strings.BadCase, args[i + 1]), out error);
                }

                if (caseMode.HasValue)
                {
                    return Fail(Format(Strings.RepeatedOption, CaseOption), out error);
                }

                caseMode = mode;
                i += 1;
            }
            else if (Is(arg, DryRunOption))
            {
                dryRun = true;
            }
            else if (arg.StartsWith("-", StringComparison.Ordinal))
            {
                return Fail(Format(Strings.UnknownOption, arg), out error);
            }
            else if (string.IsNullOrWhiteSpace(arg))
            {
                // An empty variable in a script ("%~1", "$folder"): never fall back to the current directory.
                return Fail(Strings.NoFolder, out error);
            }
            else if (folder != null)
            {
                return Fail(Format(Strings.TwoFolders, folder, arg), out error);
            }
            else
            {
                folder = arg;
            }
        }

        if (folder == null)
        {
            return Fail(Strings.NoFolder, out error);
        }

        // A folder with a quote is what cmd makes of "D:\Pics\" --case lower: one argument that swallowed the options.
        // The runner says so (Strings.QuoteInFolder), which helps more than "give a rule".
        if (find == null && !caseMode.HasValue && folder.IndexOf('"') < 0)
        {
            return Fail(Strings.NoRule, out error);
        }

        options = new ApplyOptions(folder, find, with, caseMode, dryRun);
        return true;
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

    private static bool IsOptionWord(string arg) => Is(arg, ReplaceOption) || Is(arg, CaseOption) || Is(arg, DryRunOption);

    private static string Format(string format, params object[] values) =>
        string.Format(CultureInfo.InvariantCulture, format, values);

    /// <summary>A usage error: the message, one space, then the usage line.</summary>
    private static bool Fail(string message, out string error)
    {
        error = message + " " + Strings.Usage;
        return false;
    }
}
