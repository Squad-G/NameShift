using System;
using System.Collections.Generic;

namespace NameShift.Cmd;

/// <summary>Reads a command line and dispatches on its first word: <c>help</c> (also <c>--help</c>, <c>-h</c>,
/// <c>apply &lt;folder&gt;</c> with the options <c>--replace &lt;find&gt; &lt;with&gt;</c>, <c>--case
/// lower|upper|title|sentence</c>, the Add panel's <c>--prefix</c>, <c>--suffix</c>, <c>--insert &lt;text&gt;
/// &lt;pos&gt;</c>, <c>--from-end</c> and <c>--word-space</c>, the Numbering panel's <c>--number</c>, <c>--number-mode</c>,
/// <c>--number-start</c>, <c>--number-step</c>, <c>--number-pad</c>, <c>--number-separator</c>,
/// <c>--number-position</c>, <c>--number-system</c> and <c>--number-case</c>, the Name panel's <c>--name-fixed</c>,
/// <c>--name-empty</c> and <c>--name-reverse</c>, the Extension panel's five <c>--ext-...</c> flags, the Remove panel's
/// eighteen <c>--remove-...</c> options, the RegEx panel's <c>--regex</c> options, the Move or copy panel's
/// <c>--move</c>, <c>--copy</c> and <c>--move-...</c> options, the Auto date panel's <c>--date</c> options, the Append
/// folder name panel's <c>--folder-...</c> options, <c>--preset &lt;name&gt;</c> (a saved
/// preset's rules), <c>--save-preset &lt;name&gt;</c> with <c>--overwrite</c>, and <c>--dry-run</c>,
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

    private static readonly string[] GlobalWordList =
    {
        ApplyCommand, UndoCommand, HelpWord, HelpOption, HelpShortOption, HelpSlashOption, VersionOption,
    };

    private static readonly string[] ApplyOptionWordList =
    {
        ReplaceCaseReader.ReplaceOption, ReplaceCaseReader.CaseOption,
        RunSwitchesReader.DryRunOption, RunSwitchesReader.QuietOption, RunSwitchesReader.QuietShortOption,
        RunSwitchesReader.VerboseOption, RunSwitchesReader.VerboseShortOption,
        AddReader.PrefixOption, AddReader.SuffixOption, AddReader.InsertOption, AddReader.FromEndOption,
        AddReader.WordSpaceOption,
        NumberingReader.NumberOption, NumberingReader.NumberModeOption, NumberingReader.NumberStartOption,
        NumberingReader.NumberStepOption, NumberingReader.NumberPadOption, NumberingReader.NumberSeparatorOption,
        NumberingReader.NumberPositionOption, NumberingReader.NumberSystemOption, NumberingReader.NumberCaseOption,
        NameReader.FixedOption, NameReader.EmptyOption, NameReader.ReverseOption,
        ExtensionReader.LowerOption, ExtensionReader.UpperOption, ExtensionReader.FixedOption,
        ExtensionReader.ExtraOption, ExtensionReader.RemoveOption,
        RemoveReader.FirstOption, RemoveReader.LastOption, RemoveReader.FromOption, RemoveReader.ToOption,
        RemoveReader.CharsOption, RemoveReader.TextOption, RemoveReader.BeforeOption, RemoveReader.AfterOption,
        RemoveReader.WholeWordOption, RemoveReader.MatchCaseOption, RemoveReader.KeepMarkerOption,
        RemoveReader.DigitsOption, RemoveReader.SymbolsOption, RemoveReader.AccentsOption,
        RemoveReader.HighCharsOption, RemoveReader.TrimOption, RemoveReader.LeadingDotsOption,
        RemoveReader.DoubleSpacesOption,
        RegexReader.RegexOption, RegexReader.IgnoreCaseOption, RegexReader.ExtensionOption,
        MoveCopyReader.MoveOption, MoveCopyReader.CopyOption, MoveCopyReader.FromOption, MoveCopyReader.ToOption,
        MoveCopyReader.PositionOption, MoveCopyReader.SeparatorOption,
        AutoDateReader.DateOption, AutoDateReader.SourceOption, AutoDateReader.FormatOption,
        AutoDateReader.PlacementOption, AutoDateReader.SeparatorOption,
        FolderNameReader.FolderNameOption, FolderNameReader.LevelsOption, FolderNameReader.PlacementOption,
        FolderNameReader.SeparatorOption,
        PresetReader.PresetOption, PresetReader.SavePresetOption, PresetReader.OverwriteOption,
    };

    private static readonly string[] UndoOptionWordList = { RunSwitchesReader.DryRunOption };

    private static readonly string[] AllWords = Combine(GlobalWordList, ApplyOptionWordList);

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
            if (!Is(args[i], RunSwitchesReader.DryRunOption))
            {
                return Fail(Strings.UndoOnlyDryRun);
            }

            if (dryRun)
            {
                return Fail(Format(Strings.RepeatedOption, RunSwitchesReader.DryRunOption));
            }

            dryRun = true;
        }

        return ParsedCommand.Undo(dryRun);
    }

    private static ParsedCommand ParseApply(IReadOnlyList<string> args) => ParseApply(args, ApplyReaders.Create());

    /// <summary>The seam: offers each argument to the readers in turn, then runs their cross-checks in list order. A
    /// new option group is a new reader in the list; this loop does not change.</summary>
    internal static ParsedCommand ParseApply(IReadOnlyList<string> args, IReadOnlyList<ApplyOptionReader> readers)
    {
        string? folder = null;
        for (int i = 1; i < args.Count; i++)
        {
            string arg = args[i];
            OptionRead read = OptionRead.NotMine;
            foreach (ApplyOptionReader reader in readers)
            {
                read = reader.Read(args, ref i);
                if (read.IsMine)
                {
                    break;
                }
            }

            if (read.Error != null)
            {
                return read.Error;
            }

            if (read.IsMine)
            {
                continue;
            }

            if (arg.StartsWith("-", StringComparison.Ordinal))
            {
                return Fail(Format(Strings.UnknownOption, arg));
            }

            if (string.IsNullOrWhiteSpace(arg))
            {
                // An empty variable in a script ("%~1", "$folder"): never fall back to the current directory.
                return Fail(Strings.NoFolder);
            }

            if (folder != null)
            {
                return Fail(Format(Strings.TwoFolders, folder, arg));
            }

            folder = arg;
        }

        var draft = new ApplyDraft();
        foreach (ApplyOptionReader reader in readers)
        {
            ParsedCommand? error = reader.Finish(draft);
            if (error != null)
            {
                return error;
            }
        }

        if (folder == null)
        {
            return Fail(Strings.NoFolder);
        }

        // A folder with a quote is what cmd makes of "D:\Pics\" --case lower: one argument that swallowed the options.
        // The runner says so (Strings.QuoteInFolder), which helps more than "give a rule".
        if (!draft.HasRule && folder.IndexOf('"') < 0)
        {
            return Fail(Strings.NoRule);
        }

        return ParsedCommand.Apply(draft.Build(folder));
    }

    private static bool Is(string arg, string word) => OptionArgs.Is(arg, word);

    private static bool IsHelpFlag(string arg) => Is(arg, HelpOption) || Is(arg, HelpShortOption) || Is(arg, HelpSlashOption);

    private static string[] Combine(string[] first, string[] second)
    {
        var all = new string[first.Length + second.Length];
        first.CopyTo(all, 0);
        second.CopyTo(all, first.Length);
        return all;
    }

    private static string Format(string format, params object[] values) => OptionArgs.Format(format, values);

    private static ParsedCommand Fail(string message) => OptionArgs.Fail(message);
}
