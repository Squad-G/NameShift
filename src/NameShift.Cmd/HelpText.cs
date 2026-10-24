using System;
using System.Collections.Generic;

namespace NameShift.Cmd;

/// <summary>The help texts, as lines for <c>WriteLine</c> (so the output uses the platform's line break whatever the
/// resource file's). Each verb has its own list of lines; a new verb adds a list and a line in the main help.</summary>
internal static class HelpText
{
    /// <summary>The main help: the usage of apply, the commands, the options and examples, the options of apply, then the
    /// exit codes.</summary>
    public static IReadOnlyList<string> Main()
    {
        var lines = new List<string>();
        lines.AddRange(Lines(Strings.HelpUsageApply));
        lines.Add("");
        lines.AddRange(Lines(Strings.HelpMain));
        lines.Add("");
        lines.AddRange(Lines(Strings.HelpApply));
        lines.Add("");
        lines.Add(Strings.HelpExitCodes);
        foreach (ExitCode code in Enum.GetValues(typeof(ExitCode)))
        {
            lines.Add("  " + (int)code + "  " + ExitCodeText(code));
        }

        return lines;
    }

    /// <summary>The help of apply: its usage, its options and one example.</summary>
    public static IReadOnlyList<string> Apply()
    {
        var lines = new List<string>();
        lines.AddRange(Lines(Strings.HelpUsageApply));
        lines.Add("");
        lines.AddRange(Lines(Strings.HelpApply));
        return lines;
    }

    /// <summary>The one-line meaning of an exit code. A code without a text throws, so the help test fails on it.</summary>
    internal static string ExitCodeText(ExitCode code)
    {
        switch (code)
        {
            case ExitCode.Success:
                return Strings.ExitCode0;
            case ExitCode.InputError:
                return Strings.ExitCode1;
            case ExitCode.Blocked:
                return Strings.ExitCode2;
            case ExitCode.NotRenamed:
                return Strings.ExitCode3;
            case ExitCode.NeedsAttention:
                return Strings.ExitCode4;
            default:
                throw new ArgumentOutOfRangeException(nameof(code), code, "No help text for this exit code.");
        }
    }

    /// <summary>The lines of a resource text: split on LF, a trailing CR dropped.</summary>
    private static IEnumerable<string> Lines(string text)
    {
        foreach (string line in text.Split('\n'))
        {
            yield return line.TrimEnd('\r');
        }
    }
}
