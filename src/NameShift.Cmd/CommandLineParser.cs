using System;
using System.Collections.Generic;
using System.Globalization;
using NameShift.Core;

namespace NameShift.Cmd;

/// <summary>Reads a command line and dispatches on its first word: <c>help</c> (also <c>--help</c>, <c>-h</c>,
/// <c>/?</c>), <c>--version</c>, <c>apply &lt;folder&gt; [--replace &lt;find&gt; &lt;with&gt;] [--case
/// lower|upper|title|sentence] [--dry-run]</c>, or <c>undo [--dry-run]</c>. Options of <c>apply</c> come in any order;
/// the folder is the one argument that is neither an option nor an option's value. <c>undo</c> takes no folder. Words
/// are compared ordinally, ignoring case.</summary>
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

    private static readonly string[] GlobalWordList =
    {
        ApplyCommand, UndoCommand, HelpWord, HelpOption, HelpShortOption, HelpSlashOption, VersionOption,
    };

    private static readonly string[] ApplyOptionWordList = { ReplaceOption, CaseOption, DryRunOption };

    private static readonly string[] UndoOptionWordList = { DryRunOption };

    private static readonly string[] AllWords = Combine(GlobalWordList, ApplyOptionWordList);

    private static readonly KeyValuePair<string, CaseMode>[] CaseValues =
    {
        new KeyValuePair<string, CaseMode>("lower", CaseMode.Lower),
        new KeyValuePair<string, CaseMode>("upper", CaseMode.Upper),
        new KeyValuePair<string, CaseMode>("title", CaseMode.Title),
        new KeyValuePair<string, CaseMode>("sentence", CaseMode.Sentence),
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
        for (int i = 1; i < args.Count; i++)
        {
            string arg = args[i];
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

                if (!TryCase(args[i + 1], out CaseMode mode))
                {
                    return Fail(Format(Strings.BadCase, args[i + 1]));
                }

                if (caseMode.HasValue)
                {
                    return Fail(Format(Strings.RepeatedOption, CaseOption));
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

        if (folder == null)
        {
            return Fail(Strings.NoFolder);
        }

        // A folder with a quote is what cmd makes of "D:\Pics\" --case lower: one argument that swallowed the options.
        // The runner says so (Strings.QuoteInFolder), which helps more than "give a rule".
        if (find == null && !caseMode.HasValue && folder.IndexOf('"') < 0)
        {
            return Fail(Strings.NoRule);
        }

        return ParsedCommand.Apply(new ApplyOptions(folder, find, with, caseMode, dryRun));
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
