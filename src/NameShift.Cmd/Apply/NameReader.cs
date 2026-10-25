using System;
using System.Collections.Generic;
using NameShift.Core;
using NameShift.Ui.Rules;
using static NameShift.Cmd.OptionArgs;

namespace NameShift.Cmd;

/// <summary>Reads the Name panel's options: <c>--name-fixed</c>, <c>--name-empty</c> and <c>--name-reverse</c>, one
/// flag per mode.</summary>
internal sealed class NameReader : ApplyOptionReader
{
    internal const string FixedOption = "--name-fixed";
    internal const string EmptyOption = "--name-empty";
    internal const string ReverseOption = "--name-reverse";

    private static readonly string[] OwnWords = { FixedOption, EmptyOption, ReverseOption };

    private NameMode? _mode;
    private string _text = "";

    public override IReadOnlyList<string> Words => OwnWords;

    protected override OptionRead ReadOwn(IReadOnlyList<string> args, ref int i, string word)
    {
        string? value = null;
        if (string.Equals(word, FixedOption, StringComparison.Ordinal)
            && !TakeOne(args, ref i, FixedOption, out value, out ParsedCommand? error))
        {
            return OptionRead.Failed(error!);
        }

        NameMode mode = word == FixedOption ? NameMode.Fixed
            : word == EmptyOption ? NameMode.Remove : NameMode.Reverse;
        if (_mode == mode)
        {
            return OptionRead.Failed(Fail(Format(Strings.RepeatedOption, word)));
        }

        if (_mode != null)
        {
            return OptionRead.Failed(Fail(Format(Strings.OneModeOnly, Strings.NameModeOptions)));
        }

        if (value != null && string.IsNullOrWhiteSpace(value))
        {
            return OptionRead.Failed(Fail(Format(Strings.BlankText, FixedOption)));
        }

        _mode = mode;
        _text = value ?? "";
        return OptionRead.Taken;
    }

    public override ParsedCommand? Finish(ApplyDraft draft)
    {
        if (_mode != null)
        {
            draft.Panels.Add(new NamePanelModel { Mode = _mode.Value, Text = _text, Enabled = true });
        }

        return null;
    }
}
