using System.Collections.Generic;
using NameShift.Core;
using NameShift.Ui.Rules;
using static NameShift.Cmd.OptionArgs;
using UiStrings = NameShift.Ui.Strings;

namespace NameShift.Cmd;

/// <summary>Reads the Move or copy panel's options: <c>--move &lt;n&gt;</c> or <c>--copy &lt;n&gt;</c> (the mode and
/// the count) and four <c>--move-...</c> modifiers shared by both modes.</summary>
internal sealed class MoveCopyReader : ApplyOptionReader
{
    internal const string MoveOption = "--move";
    internal const string CopyOption = "--copy";
    internal const string FromOption = "--move-from";
    internal const string ToOption = "--move-to";
    internal const string PositionOption = "--move-position";
    internal const string SeparatorOption = "--move-separator";

    private static readonly string[] OwnWords =
    {
        MoveOption, CopyOption, FromOption, ToOption, PositionOption, SeparatorOption,
    };

    private static readonly KeyValuePair<string, MoveCopySource>[] FromValues =
    {
        new KeyValuePair<string, MoveCopySource>("start", MoveCopySource.Start),
        new KeyValuePair<string, MoveCopySource>("end", MoveCopySource.End),
    };

    private static readonly KeyValuePair<string, MoveCopyTarget>[] ToValues =
    {
        new KeyValuePair<string, MoveCopyTarget>("start", MoveCopyTarget.Start),
        new KeyValuePair<string, MoveCopyTarget>("end", MoveCopyTarget.End),
        new KeyValuePair<string, MoveCopyTarget>("position", MoveCopyTarget.Position),
    };

    private string? _modeWord;
    private string? _count;
    private MoveCopySource? _source;
    private MoveCopyTarget? _target;
    private string? _position;
    private string? _separator;

    public override IReadOnlyList<string> Words => OwnWords;

    protected override OptionRead ReadOwn(IReadOnlyList<string> args, ref int i, string word)
    {
        ParsedCommand? error;
        bool ok;
        if (Is(word, MoveOption) || Is(word, CopyOption))
        {
            ok = ReadMode(args, ref i, word, out error);
        }
        else if (Is(word, FromOption))
        {
            ok = TakeChoice(args, ref i, word, FromValues, Strings.MoveFromValues, ref _source, out error);
        }
        else if (Is(word, ToOption))
        {
            ok = TakeChoice(args, ref i, word, ToValues, Strings.MoveToValues, ref _target, out error);
        }
        else if (Is(word, PositionOption))
        {
            ok = TakeText(args, ref i, word, ref _position, out error);
        }
        else
        {
            ok = TakeText(args, ref i, word, ref _separator, out error);
        }

        return OptionRead.From(ok, error);
    }

    private bool ReadMode(IReadOnlyList<string> args, ref int i, string word, out ParsedCommand? error)
    {
        if (!TakeOne(args, ref i, word, out string? value, out error))
        {
            return false;
        }

        if (_modeWord != null && Is(_modeWord, word))
        {
            error = Fail(Format(Strings.RepeatedOption, word));
            return false;
        }

        if (_modeWord != null)
        {
            error = Fail(Format(Strings.OneModeOnly, Strings.MoveModeOptions));
            return false;
        }

        if (TextPosition.Parse(value!) == null)
        {
            error = Fail(Format(Strings.OptionError, word,
                WithoutSkipSentence(UiStrings.MoveCopyNumberInvalid)));
            return false;
        }

        _modeWord = word;
        _count = value;
        return true;
    }

    public override ParsedCommand? Finish(ApplyDraft draft)
    {
        if (_modeWord == null)
        {
            return NeedsMode();
        }

        if (_target == MoveCopyTarget.Position && (_position == null || TextPosition.Parse(_position) == null))
        {
            return Fail(Format(Strings.OptionError, PositionOption,
                WithoutSkipSentence(UiStrings.MoveCopyNumberInvalid)));
        }

        var model = new MoveCopyPanelModel
        {
            Mode = Is(_modeWord, CopyOption) ? MoveCopyMode.Copy : MoveCopyMode.Move,
            CountText = _count!,
            PositionText = _position ?? "",
            Separator = _separator ?? "",
            Enabled = true,
        };
        if (_source != null)
        {
            model.Source = _source.Value;
        }

        if (_target != null)
        {
            model.Target = _target.Value;
        }

        draft.Panels.Add(model);
        return null;
    }

    /// <summary>Without --move or --copy, the first modifier given (in word order) needs one.</summary>
    private ParsedCommand? NeedsMode()
    {
        if (_source != null)
        {
            return Fail(Format(Strings.NeedsOption, FromOption, Strings.MoveModeOptions));
        }

        if (_target != null)
        {
            return Fail(Format(Strings.NeedsOption, ToOption, Strings.MoveModeOptions));
        }

        if (_position != null)
        {
            return Fail(Format(Strings.NeedsOption, PositionOption, Strings.MoveModeOptions));
        }

        if (_separator != null)
        {
            return Fail(Format(Strings.NeedsOption, SeparatorOption, Strings.MoveModeOptions));
        }

        return null;
    }
}
