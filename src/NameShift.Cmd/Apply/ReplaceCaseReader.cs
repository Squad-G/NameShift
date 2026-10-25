using System.Collections.Generic;
using NameShift.Core;
using static NameShift.Cmd.OptionArgs;

namespace NameShift.Cmd;

/// <summary>Reads <c>--replace &lt;find&gt; &lt;with&gt;</c> and <c>--case</c>.</summary>
internal sealed class ReplaceCaseReader : ApplyOptionReader
{
    internal const string ReplaceOption = "--replace";
    internal const string CaseOption = "--case";

    private static readonly string[] OwnWords = { ReplaceOption, CaseOption };

    private static readonly KeyValuePair<string, CaseMode>[] CaseValues =
    {
        new KeyValuePair<string, CaseMode>("lower", CaseMode.Lower),
        new KeyValuePair<string, CaseMode>("upper", CaseMode.Upper),
        new KeyValuePair<string, CaseMode>("title", CaseMode.Title),
        new KeyValuePair<string, CaseMode>("sentence", CaseMode.Sentence),
    };

    private string? _find;
    private string? _with;
    private CaseMode? _caseMode;

    public override IReadOnlyList<string> Words => OwnWords;

    protected override OptionRead ReadOwn(IReadOnlyList<string> args, ref int i, string word)
    {
        if (Is(word, ReplaceOption))
        {
            // A value that is an option word means a value was left out: `--replace IMG --dry-run` must not turn
            // --dry-run into the new text and rename for real. Other values may start with a dash (--replace - _).
            if (args.Count - i - 1 < 2 || IsOptionWord(args[i + 1]) || IsOptionWord(args[i + 2]))
            {
                return OptionRead.Failed(Fail(Strings.ReplaceNeedsValues));
            }

            if (_find != null)
            {
                return OptionRead.Failed(Fail(Format(Strings.RepeatedOption, ReplaceOption)));
            }

            if (args[i + 1].Length == 0)
            {
                return OptionRead.Failed(Fail(Strings.EmptyFind));
            }

            _find = args[i + 1];
            _with = args[i + 2];
            i += 2;
            return OptionRead.Taken;
        }

        if (args.Count - i - 1 < 1)
        {
            return OptionRead.Failed(Fail(Strings.CaseNeedsValue));
        }

        if (!TryChoice(args[i + 1], CaseValues, out CaseMode parsedCase))
        {
            return OptionRead.Failed(Fail(Format(Strings.BadCase, args[i + 1])));
        }

        if (_caseMode.HasValue)
        {
            return OptionRead.Failed(Fail(Format(Strings.RepeatedOption, CaseOption)));
        }

        _caseMode = parsedCase;
        i += 1;
        return OptionRead.Taken;
    }

    public override ParsedCommand? Finish(ApplyDraft draft)
    {
        draft.Find = _find;
        draft.With = _with;
        draft.Case = _caseMode;
        return null;
    }
}
