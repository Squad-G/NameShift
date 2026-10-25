using System;
using System.Collections.Generic;
using NameShift.Ui.Rules;
using static NameShift.Cmd.OptionArgs;

namespace NameShift.Cmd;

/// <summary>Reads the RegEx panel's options: <c>--regex &lt;pattern&gt; &lt;replacement&gt;</c> and its two
/// switches.</summary>
internal sealed class RegexReader : ApplyOptionReader
{
    internal const string RegexOption = "--regex";
    internal const string IgnoreCaseOption = "--regex-ignore-case";
    internal const string ExtensionOption = "--regex-extension";

    private static readonly string[] OwnWords = { RegexOption, IgnoreCaseOption, ExtensionOption };

    private readonly HashSet<string> _switches = new HashSet<string>(StringComparer.Ordinal);
    private string? _pattern;
    private string? _replacement;

    public override IReadOnlyList<string> Words => OwnWords;

    protected override OptionRead ReadOwn(IReadOnlyList<string> args, ref int i, string word)
    {
        if (Is(word, RegexOption))
        {
            return ReadPattern(args, ref i);
        }

        if (!_switches.Add(word))
        {
            return OptionRead.Failed(Fail(Format(Strings.RepeatedOption, word)));
        }

        return OptionRead.Taken;
    }

    private OptionRead ReadPattern(IReadOnlyList<string> args, ref int i)
    {
        // As --replace: a value that is an option word means a value was left out.
        if (args.Count - i - 1 < 2 || IsOptionWord(args[i + 1]) || IsOptionWord(args[i + 2]))
        {
            return OptionRead.Failed(Fail(Strings.RegexNeedsValues));
        }

        if (_pattern != null)
        {
            return OptionRead.Failed(Fail(Format(Strings.RepeatedOption, RegexOption)));
        }

        string pattern = args[i + 1];
        if (pattern.Length == 0)
        {
            return OptionRead.Failed(Fail(Format(Strings.EmptyOptionText, RegexOption)));
        }

        string problem = new RegexPanelModel { Pattern = pattern }.PatternError;
        if (problem.Length > 0)
        {
            return OptionRead.Failed(Fail(Format(Strings.OptionError, RegexOption, WithoutSkipSentence(problem))));
        }

        _pattern = pattern;
        _replacement = args[i + 2];
        i += 2;
        return OptionRead.Taken;
    }

    public override ParsedCommand? Finish(ApplyDraft draft)
    {
        foreach (string word in new[] { IgnoreCaseOption, ExtensionOption })
        {
            if (_switches.Contains(word) && _pattern == null)
            {
                return Fail(Format(Strings.NeedsOption, word, RegexOption));
            }
        }

        if (_pattern == null)
        {
            return null;
        }

        draft.Panels.Add(new RegexPanelModel
        {
            Pattern = _pattern,
            Replacement = _replacement ?? "",
            IgnoreCase = _switches.Contains(IgnoreCaseOption),
            IncludeExtension = _switches.Contains(ExtensionOption),
            Enabled = true,
        });
        return null;
    }
}
