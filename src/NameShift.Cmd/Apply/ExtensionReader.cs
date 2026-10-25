using System;
using System.Collections.Generic;
using NameShift.Core;
using NameShift.Ui.Rules;
using static NameShift.Cmd.OptionArgs;

namespace NameShift.Cmd;

/// <summary>Reads the Extension panel's options: <c>--ext-lower</c>, <c>--ext-upper</c>, <c>--ext-fixed</c>,
/// <c>--ext-extra</c> and <c>--ext-remove</c>, one flag per mode.</summary>
internal sealed class ExtensionReader : ApplyOptionReader
{
    internal const string LowerOption = "--ext-lower";
    internal const string UpperOption = "--ext-upper";
    internal const string FixedOption = "--ext-fixed";
    internal const string ExtraOption = "--ext-extra";
    internal const string RemoveOption = "--ext-remove";

    private static readonly string[] OwnWords = { LowerOption, UpperOption, FixedOption, ExtraOption, RemoveOption };

    private static readonly KeyValuePair<string, ExtensionMode>[] Modes =
    {
        new KeyValuePair<string, ExtensionMode>(LowerOption, ExtensionMode.Lower),
        new KeyValuePair<string, ExtensionMode>(UpperOption, ExtensionMode.Upper),
        new KeyValuePair<string, ExtensionMode>(FixedOption, ExtensionMode.Fixed),
        new KeyValuePair<string, ExtensionMode>(ExtraOption, ExtensionMode.Extra),
        new KeyValuePair<string, ExtensionMode>(RemoveOption, ExtensionMode.Remove),
    };

    private ExtensionMode? _mode;
    private string _text = "";

    public override IReadOnlyList<string> Words => OwnWords;

    protected override OptionRead ReadOwn(IReadOnlyList<string> args, ref int i, string word)
    {
        string? value = null;
        bool takesText = string.Equals(word, FixedOption, StringComparison.Ordinal)
            || string.Equals(word, ExtraOption, StringComparison.Ordinal);
        if (takesText && !TakeOne(args, ref i, word, out value, out ParsedCommand? error))
        {
            return OptionRead.Failed(error!);
        }

        TryChoice(word, Modes, out ExtensionMode mode);
        if (_mode == mode)
        {
            return OptionRead.Failed(Fail(Format(Strings.RepeatedOption, word)));
        }

        if (_mode != null)
        {
            return OptionRead.Failed(Fail(Format(Strings.OneModeOnly, Strings.ExtensionModeOptions)));
        }

        if (value != null && IsBlankExtension(value))
        {
            return OptionRead.Failed(Fail(Format(Strings.BlankExtension, word)));
        }

        _mode = mode;
        _text = value ?? "";
        return OptionRead.Taken;
    }

    /// <summary>The rule's own trim: only dots and white space (an empty text too) is skipped.</summary>
    private static bool IsBlankExtension(string text)
    {
        foreach (char c in text)
        {
            if (c != '.' && !char.IsWhiteSpace(c))
            {
                return false;
            }
        }

        return true;
    }

    public override ParsedCommand? Finish(ApplyDraft draft)
    {
        if (_mode != null)
        {
            draft.Panels.Add(new ExtensionPanelModel { Mode = _mode.Value, Text = _text, Enabled = true });
        }

        return null;
    }
}
