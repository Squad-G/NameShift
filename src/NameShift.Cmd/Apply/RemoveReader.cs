using System;
using System.Collections.Generic;
using NameShift.Core;
using NameShift.Ui.Rules;
using static NameShift.Cmd.OptionArgs;
using UiStrings = NameShift.Ui.Strings;

namespace NameShift.Cmd;

/// <summary>Reads the Remove panel's options: four counts, four texts and ten switches, each named after the panel
/// setting it sets.</summary>
internal sealed class RemoveReader : ApplyOptionReader
{
    internal const string FirstOption = "--remove-first";
    internal const string LastOption = "--remove-last";
    internal const string FromOption = "--remove-from";
    internal const string ToOption = "--remove-to";
    internal const string CharsOption = "--remove-chars";
    internal const string TextOption = "--remove-text";
    internal const string BeforeOption = "--remove-before";
    internal const string AfterOption = "--remove-after";
    internal const string WholeWordOption = "--remove-whole-word";
    internal const string MatchCaseOption = "--remove-match-case";
    internal const string KeepMarkerOption = "--remove-keep-marker";
    internal const string DigitsOption = "--remove-digits";
    internal const string SymbolsOption = "--remove-symbols";
    internal const string AccentsOption = "--remove-accents";
    internal const string HighCharsOption = "--remove-high-chars";
    internal const string TrimOption = "--remove-trim";
    internal const string LeadingDotsOption = "--remove-leading-dots";
    internal const string DoubleSpacesOption = "--remove-double-spaces";

    private static readonly string[] CountWords = { FirstOption, LastOption, FromOption, ToOption };
    private static readonly string[] TextWords = { CharsOption, TextOption, BeforeOption, AfterOption };

    private static readonly KeyValuePair<string, Action<RemovePanelModel>>[] Switches =
    {
        Switch(WholeWordOption, m => m.WholeWord = true),
        Switch(MatchCaseOption, m => m.MatchCase = true),
        Switch(KeepMarkerOption, m => m.KeepMarker = true),
        Switch(DigitsOption, m => m.Digits = true),
        Switch(SymbolsOption, m => m.Symbols = true),
        Switch(AccentsOption, m => m.Accents = true),
        Switch(HighCharsOption, m => m.HighCharacters = true),
        Switch(TrimOption, m => m.Trim = true),
        Switch(LeadingDotsOption, m => m.LeadingDots = true),
        Switch(DoubleSpacesOption, m => m.DoubleSpaces = true),
    };

    private static readonly string[] OwnWords = BuildWords();

    private readonly Dictionary<string, string> _counts = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _texts = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly HashSet<string> _switches = new HashSet<string>(StringComparer.Ordinal);

    public override IReadOnlyList<string> Words => OwnWords;

    private static KeyValuePair<string, Action<RemovePanelModel>> Switch(string word, Action<RemovePanelModel> set) =>
        new KeyValuePair<string, Action<RemovePanelModel>>(word, set);

    private static string[] BuildWords()
    {
        var words = new List<string>(CountWords);
        words.AddRange(TextWords);
        foreach (KeyValuePair<string, Action<RemovePanelModel>> entry in Switches)
        {
            words.Add(entry.Key);
        }

        return words.ToArray();
    }

    protected override OptionRead ReadOwn(IReadOnlyList<string> args, ref int i, string word)
    {
        bool isCount = Array.IndexOf(CountWords, word) >= 0;
        bool isText = Array.IndexOf(TextWords, word) >= 0;
        string? value = null;
        if ((isCount || isText) && !TakeOne(args, ref i, word, out value, out ParsedCommand? error))
        {
            return OptionRead.Failed(error!);
        }

        if (_counts.ContainsKey(word) || _texts.ContainsKey(word) || _switches.Contains(word))
        {
            return OptionRead.Failed(Fail(Format(Strings.RepeatedOption, word)));
        }

        if (isCount)
        {
            if ((word == FirstOption || word == LastOption) && TextPosition.Parse(value!) == null)
            {
                return OptionRead.Failed(Fail(Format(Strings.OptionError, word,
                    WithoutSkipSentence(UiStrings.RemovePositionInvalid))));
            }

            _counts[word] = value!;
        }
        else if (isText)
        {
            if (value!.Length == 0)
            {
                return OptionRead.Failed(Fail(Format(Strings.EmptyOptionText, word)));
            }

            _texts[word] = value;
        }
        else
        {
            _switches.Add(word);
        }

        return OptionRead.Taken;
    }

    public override ParsedCommand? Finish(ApplyDraft draft)
    {
        if (_counts.Count + _texts.Count + _switches.Count == 0)
        {
            return null;
        }

        if (_switches.Contains(WholeWordOption) && !_texts.ContainsKey(TextOption))
        {
            return Fail(Format(Strings.NeedsOption, WholeWordOption, TextOption));
        }

        if (_switches.Contains(MatchCaseOption) && _texts.Count == 0)
        {
            return Fail(Format(Strings.NeedsOption, MatchCaseOption, Strings.RemoveMatchCaseNeeds));
        }

        if (_switches.Contains(KeepMarkerOption) && !_texts.ContainsKey(BeforeOption)
            && !_texts.ContainsKey(AfterOption))
        {
            return Fail(Format(Strings.NeedsOption, KeepMarkerOption, Strings.RemoveKeepMarkerNeeds));
        }

        if ((_counts.ContainsKey(FromOption) || _counts.ContainsKey(ToOption)) && !RangeIsValid())
        {
            return Fail(Format(Strings.OptionError, Strings.RemoveRangeOptions,
                WithoutSkipSentence(UiStrings.RemoveRangeInvalid)));
        }

        draft.Panels.Add(BuildModel());
        return null;
    }

    private bool RangeIsValid() =>
        _counts.TryGetValue(FromOption, out string? from) && _counts.TryGetValue(ToOption, out string? to)
        && TextPosition.Parse(from) is int f && TextPosition.Parse(to) is int t && f <= t;

    private RemovePanelModel BuildModel()
    {
        var model = new RemovePanelModel
        {
            FirstText = Get(_counts, FirstOption),
            LastText = Get(_counts, LastOption),
            FromText = Get(_counts, FromOption),
            ToText = Get(_counts, ToOption),
            Characters = Get(_texts, CharsOption),
            Text = Get(_texts, TextOption),
            CropBefore = Get(_texts, BeforeOption),
            CropAfter = Get(_texts, AfterOption),
            Enabled = true,
        };
        foreach (KeyValuePair<string, Action<RemovePanelModel>> entry in Switches)
        {
            if (_switches.Contains(entry.Key))
            {
                entry.Value(model);
            }
        }

        return model;
    }

    private static string Get(Dictionary<string, string> map, string word) =>
        map.TryGetValue(word, out string? value) ? value : "";
}
