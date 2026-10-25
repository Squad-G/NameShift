using System.Collections.Generic;
using NameShift.Ui.Presets;
using static NameShift.Cmd.OptionArgs;

namespace NameShift.Cmd;

/// <summary>Reads <c>--preset &lt;name&gt;</c>, <c>--save-preset &lt;name&gt;</c> and <c>--overwrite</c>.</summary>
internal sealed class PresetReader : ApplyOptionReader
{
    internal const string PresetOption = "--preset";
    internal const string SavePresetOption = "--save-preset";
    internal const string OverwriteOption = "--overwrite";

    private static readonly string[] OwnWords = { PresetOption, SavePresetOption, OverwriteOption };

    private string? _presetName;
    private string? _saveName;
    private bool _overwrite;

    public override IReadOnlyList<string> Words => OwnWords;

    protected override OptionRead ReadOwn(IReadOnlyList<string> args, ref int i, string word)
    {
        ParsedCommand? error;
        string? value;
        if (Is(word, PresetOption))
        {
            if (!TakeOne(args, ref i, PresetOption, out value, out error))
            {
                return OptionRead.Failed(error!);
            }

            if (_presetName != null)
            {
                return OptionRead.Failed(Fail(Format(Strings.RepeatedOption, PresetOption)));
            }

            if (value!.Trim().Length == 0)
            {
                return OptionRead.Failed(Fail(Format(Strings.OptionNeedsValue, PresetOption)));
            }

            _presetName = value.Trim();
            return OptionRead.Taken;
        }

        if (Is(word, SavePresetOption))
        {
            if (!TakeOne(args, ref i, SavePresetOption, out value, out error))
            {
                return OptionRead.Failed(error!);
            }

            if (_saveName != null)
            {
                return OptionRead.Failed(Fail(Format(Strings.RepeatedOption, SavePresetOption)));
            }

            PresetNameCheck check = PresetNames.Check(value);
            if (check.Verdict == PresetNameVerdict.Empty)
            {
                return OptionRead.Failed(Fail(Strings.PresetNameBlank));
            }

            if (check.Verdict == PresetNameVerdict.TooLong)
            {
                return OptionRead.Failed(Fail(Strings.PresetNameTooLong));
            }

            _saveName = check.Name;
            return OptionRead.Taken;
        }

        if (_overwrite)
        {
            return OptionRead.Failed(Fail(Format(Strings.RepeatedOption, OverwriteOption)));
        }

        _overwrite = true;
        return OptionRead.Taken;
    }

    public override ParsedCommand? Finish(ApplyDraft draft)
    {
        if (_overwrite && _saveName == null)
        {
            return Fail(Strings.OverwriteNeedsSave);
        }

        draft.PresetName = _presetName;
        draft.SaveName = _saveName;
        draft.Overwrite = _overwrite;
        return null;
    }
}
