using System.Collections.Generic;
using NameShift.Core;
using NameShift.Ui.Rules;
using static NameShift.Cmd.OptionArgs;
using UiStrings = NameShift.Ui.Strings;

namespace NameShift.Cmd;

/// <summary>Reads the Append folder name panel's options: <c>--folder-name</c> and <c>--folder-*</c>. Any one of them
/// turns the panel on, with the panel's defaults for the rest.</summary>
internal sealed class FolderNameReader : ApplyOptionReader
{
    internal const string FolderNameOption = "--folder-name";
    internal const string LevelsOption = "--folder-levels";
    internal const string PlacementOption = "--folder-placement";
    internal const string SeparatorOption = "--folder-separator";

    private static readonly string[] OwnWords = { FolderNameOption, LevelsOption, PlacementOption, SeparatorOption };

    private static readonly KeyValuePair<string, FolderNamePlacement>[] PlacementChoices =
    {
        new KeyValuePair<string, FolderNamePlacement>("prefix", FolderNamePlacement.Prefix),
        new KeyValuePair<string, FolderNamePlacement>("suffix", FolderNamePlacement.Suffix),
    };

    private bool _any;
    private bool _switch;
    private string? _levels;
    private FolderNamePlacement? _placement;
    private string? _separator;

    public override IReadOnlyList<string> Words => OwnWords;

    protected override OptionRead ReadOwn(IReadOnlyList<string> args, ref int i, string word)
    {
        ParsedCommand? error;
        bool ok;
        if (Is(word, FolderNameOption))
        {
            ok = TakeSwitch(ref _switch, FolderNameOption, out error);
        }
        else if (Is(word, LevelsOption))
        {
            ok = ReadLevels(args, ref i, out error);
        }
        else if (Is(word, PlacementOption))
        {
            ok = TakeChoice(args, ref i, word, PlacementChoices, Strings.PlacementValues, ref _placement, out error);
        }
        else
        {
            ok = TakeText(args, ref i, word, ref _separator, out error);
        }

        _any = true;
        return OptionRead.From(ok, error);
    }

    private bool ReadLevels(IReadOnlyList<string> args, ref int i, out ParsedCommand? error)
    {
        if (!TakeText(args, ref i, LevelsOption, ref _levels, out error))
        {
            return false;
        }

        if (TextPosition.Parse(_levels!) == null)
        {
            error = Fail(Format(Strings.OptionError, LevelsOption,
                WithoutSkipSentence(UiStrings.FolderNameLevelsInvalid)));
            return false;
        }

        return true;
    }

    public override ParsedCommand? Finish(ApplyDraft draft)
    {
        if (!_any)
        {
            return null;
        }

        var model = new FolderNamePanelModel { Enabled = true };
        if (_levels != null)
        {
            model.LevelsText = _levels;
        }

        if (_placement != null)
        {
            model.Placement = _placement.Value;
        }

        if (_separator != null)
        {
            model.Separator = _separator;
        }

        draft.Panels.Add(model);
        return null;
    }
}
