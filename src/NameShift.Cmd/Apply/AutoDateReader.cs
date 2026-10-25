using System.Collections.Generic;
using NameShift.Core;
using NameShift.Ui.Rules;
using static NameShift.Cmd.OptionArgs;

namespace NameShift.Cmd;

/// <summary>Reads the Auto date panel's options: <c>--date</c> and <c>--date-*</c>. Any one of them turns the panel
/// on, with the panel's defaults for the rest.</summary>
internal sealed class AutoDateReader : ApplyOptionReader
{
    internal const string DateOption = "--date";
    internal const string SourceOption = "--date-source";
    internal const string FormatOption = "--date-format";
    internal const string PlacementOption = "--date-placement";
    internal const string SeparatorOption = "--date-separator";

    private static readonly string[] OwnWords =
    {
        DateOption, SourceOption, FormatOption, PlacementOption, SeparatorOption,
    };

    private static readonly KeyValuePair<string, DateSource>[] SourceValues =
    {
        new KeyValuePair<string, DateSource>("modified", DateSource.Modified),
        new KeyValuePair<string, DateSource>("created", DateSource.Created),
    };

    private static readonly KeyValuePair<string, DatePlacement>[] PlacementChoices =
    {
        new KeyValuePair<string, DatePlacement>("prefix", DatePlacement.Prefix),
        new KeyValuePair<string, DatePlacement>("suffix", DatePlacement.Suffix),
    };

    private bool _any;
    private bool _date;
    private DateSource? _source;
    private DatePlacement? _placement;
    private string? _format;
    private string? _separator;

    public override IReadOnlyList<string> Words => OwnWords;

    protected override OptionRead ReadOwn(IReadOnlyList<string> args, ref int i, string word)
    {
        ParsedCommand? error;
        bool ok;
        if (Is(word, DateOption))
        {
            ok = TakeSwitch(ref _date, DateOption, out error);
        }
        else if (Is(word, SourceOption))
        {
            ok = TakeChoice(args, ref i, word, SourceValues, Strings.DateSourceValues, ref _source, out error);
        }
        else if (Is(word, PlacementOption))
        {
            ok = TakeChoice(args, ref i, word, PlacementChoices, Strings.PlacementValues, ref _placement, out error);
        }
        else if (Is(word, FormatOption))
        {
            ok = ReadFormat(args, ref i, out error);
        }
        else
        {
            ok = TakeText(args, ref i, word, ref _separator, out error);
        }

        _any = true;
        return OptionRead.From(ok, error);
    }

    private bool ReadFormat(IReadOnlyList<string> args, ref int i, out ParsedCommand? error)
    {
        if (!TakeText(args, ref i, FormatOption, ref _format, out error))
        {
            return false;
        }

        if (_format!.Length == 0)
        {
            error = Fail(Format(Strings.EmptyOptionText, FormatOption));
            return false;
        }

        string problem = new AutoDatePanelModel { Format = _format }.FormatError;
        if (problem.Length > 0)
        {
            error = Fail(Format(Strings.OptionError, FormatOption, WithoutSkipSentence(problem)));
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

        var model = new AutoDatePanelModel { Enabled = true };
        if (_source != null)
        {
            model.Source = _source.Value;
        }

        if (_placement != null)
        {
            model.Placement = _placement.Value;
        }

        if (_format != null)
        {
            model.Format = _format;
        }

        if (_separator != null)
        {
            model.Separator = _separator;
        }

        draft.Panels.Add(model);
        return null;
    }
}
