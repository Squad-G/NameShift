using System.Collections.Generic;
using NameShift.Core;
using NameShift.Ui.Rules;
using static NameShift.Cmd.OptionArgs;

namespace NameShift.Cmd;

/// <summary>Reads the Add panel's options: <c>--prefix</c>, <c>--suffix</c>, <c>--insert</c>, <c>--from-end</c> and
/// <c>--word-space</c>.</summary>
internal sealed class AddReader : ApplyOptionReader
{
    internal const string PrefixOption = "--prefix";
    internal const string SuffixOption = "--suffix";
    internal const string InsertOption = "--insert";
    internal const string FromEndOption = "--from-end";
    internal const string WordSpaceOption = "--word-space";

    private static readonly string[] OwnWords =
        { PrefixOption, SuffixOption, InsertOption, FromEndOption, WordSpaceOption };

    private string? _prefix;
    private string? _suffix;
    private string? _insertText;
    private string? _insertPos;
    private bool _fromEnd;
    private bool _wordSpace;
    private bool _anyAdd;

    public override IReadOnlyList<string> Words => OwnWords;

    protected override OptionRead ReadOwn(IReadOnlyList<string> args, ref int i, string word)
    {
        if (Is(word, PrefixOption))
        {
            if (!TakeOne(args, ref i, PrefixOption, out string? value, out ParsedCommand? error))
            {
                return OptionRead.Failed(error!);
            }

            if (_prefix != null)
            {
                return OptionRead.Failed(Fail(Format(Strings.RepeatedOption, PrefixOption)));
            }

            if (value!.Length == 0)
            {
                return OptionRead.Failed(Fail(Strings.EmptyPrefix));
            }

            _prefix = value;
            _anyAdd = true;
            return OptionRead.Taken;
        }

        if (Is(word, SuffixOption))
        {
            if (!TakeOne(args, ref i, SuffixOption, out string? value, out ParsedCommand? error))
            {
                return OptionRead.Failed(error!);
            }

            if (_suffix != null)
            {
                return OptionRead.Failed(Fail(Format(Strings.RepeatedOption, SuffixOption)));
            }

            if (value!.Length == 0)
            {
                return OptionRead.Failed(Fail(Strings.EmptySuffix));
            }

            _suffix = value;
            _anyAdd = true;
            return OptionRead.Taken;
        }

        if (Is(word, InsertOption))
        {
            if (args.Count - i - 1 < 2 || IsOptionWord(args[i + 1]) || IsOptionWord(args[i + 2]))
            {
                return OptionRead.Failed(Fail(Strings.InsertNeedsValues));
            }

            if (_insertText != null)
            {
                return OptionRead.Failed(Fail(Format(Strings.RepeatedOption, InsertOption)));
            }

            if (args[i + 1].Length == 0)
            {
                return OptionRead.Failed(Fail(Strings.EmptyInsert));
            }

            if (TextPosition.Parse(args[i + 2]) == null)
            {
                return OptionRead.Failed(Fail(Format(Strings.BadInsertPosition, args[i + 2])));
            }

            _insertText = args[i + 1];
            _insertPos = args[i + 2];
            i += 2;
            _anyAdd = true;
            return OptionRead.Taken;
        }

        if (Is(word, FromEndOption))
        {
            if (_fromEnd)
            {
                return OptionRead.Failed(Fail(Format(Strings.RepeatedOption, FromEndOption)));
            }

            _fromEnd = true;
            return OptionRead.Taken;
        }

        if (_wordSpace)
        {
            return OptionRead.Failed(Fail(Format(Strings.RepeatedOption, WordSpaceOption)));
        }

        _wordSpace = true;
        _anyAdd = true;
        return OptionRead.Taken;
    }

    public override ParsedCommand? Finish(ApplyDraft draft)
    {
        if (_fromEnd && _insertText == null)
        {
            return Fail(Strings.FromEndNeedsInsert);
        }

        if (_anyAdd)
        {
            draft.Panels.Add(new AddPanelModel
            {
                Prefix = _prefix ?? "",
                Suffix = _suffix ?? "",
                Insert = _insertText ?? "",
                InsertPositionText = _insertPos ?? "",
                InsertFromEnd = _fromEnd,
                WordSpace = _wordSpace,
                Enabled = true,
            });
        }

        return null;
    }
}
