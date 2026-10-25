using System;
using System.Collections.Generic;
using NameShift.Core;
using NameShift.Ui.Files;
using static NameShift.Cmd.OptionArgs;

namespace NameShift.Cmd;

/// <summary>Reads the window's filter row: <c>--mask</c>, <c>--filter-regex</c>, <c>--filter-ignore-case</c>,
/// <c>--min-length</c>, <c>--max-length</c>, <c>--include-hidden</c> and the three kind words. The settings go into a
/// <see cref="FilterPanelModel"/>, so the texts and the error texts are the window's. Any one of them is a filter.</summary>
internal sealed class FilterReader : ApplyOptionReader
{
    internal const string MaskOption = "--mask";
    internal const string RegexOption = "--filter-regex";
    internal const string IgnoreCaseOption = "--filter-ignore-case";
    internal const string MinLengthOption = "--min-length";
    internal const string MaxLengthOption = "--max-length";
    internal const string IncludeHiddenOption = "--include-hidden";
    internal const string FilesOnlyOption = "--files-only";
    internal const string FoldersOnlyOption = "--folders-only";
    internal const string IncludeFoldersOption = "--include-folders";

    private static readonly string[] OwnWords =
    {
        MaskOption, RegexOption, IgnoreCaseOption, MinLengthOption, MaxLengthOption, IncludeHiddenOption,
        FilesOnlyOption, FoldersOnlyOption, IncludeFoldersOption,
    };

    private bool _any;
    private string? _mask;
    private string? _pattern;
    private bool _ignoreCase;
    private string? _min;
    private string? _max;
    private bool _hidden;
    private string? _kindWord;

    public override IReadOnlyList<string> Words => OwnWords;

    /// <summary>The command line's default filter: the window's, with files only (a script never renames a folder by
    /// accident).</summary>
    internal static FilterPanelModel NewModel() => new FilterPanelModel { IncludeFolders = false };

    protected override OptionRead ReadOwn(IReadOnlyList<string> args, ref int i, string word)
    {
        ParsedCommand? error;
        bool ok;
        if (Is(word, MaskOption))
        {
            ok = ReadMask(args, ref i, out error);
        }
        else if (Is(word, RegexOption))
        {
            ok = ReadPattern(args, ref i, out error);
        }
        else if (Is(word, MinLengthOption))
        {
            ok = ReadLength(args, ref i, MinLengthOption, ref _min, out error);
        }
        else if (Is(word, MaxLengthOption))
        {
            ok = ReadLength(args, ref i, MaxLengthOption, ref _max, out error);
        }
        else if (Is(word, IgnoreCaseOption))
        {
            ok = TakeSwitch(ref _ignoreCase, IgnoreCaseOption, out error);
        }
        else if (Is(word, IncludeHiddenOption))
        {
            ok = TakeSwitch(ref _hidden, IncludeHiddenOption, out error);
        }
        else
        {
            ok = ReadKind(word, out error);
        }

        _any = true;
        return OptionRead.From(ok, error);
    }

    /// <summary>An empty or white-space mask would keep every name, which a script with an empty variable must never
    /// get: it is refused.</summary>
    private bool ReadMask(IReadOnlyList<string> args, ref int i, out ParsedCommand? error)
    {
        if (!TakeText(args, ref i, MaskOption, ref _mask, out error))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_mask))
        {
            error = Fail(Format(Strings.EmptyOptionText, MaskOption));
            return false;
        }

        return true;
    }

    /// <summary>An empty pattern is refused; white space is a valid pattern, as with --regex.</summary>
    private bool ReadPattern(IReadOnlyList<string> args, ref int i, out ParsedCommand? error)
    {
        if (!TakeText(args, ref i, RegexOption, ref _pattern, out error))
        {
            return false;
        }

        if (_pattern!.Length == 0)
        {
            error = Fail(Format(Strings.EmptyOptionText, RegexOption));
            return false;
        }

        string? problem = new FilterPanelModel { Pattern = _pattern }.Error;
        if (problem != null)
        {
            error = Fail(Format(Strings.OptionError, RegexOption, WithoutSkipSentence(problem)));
            return false;
        }

        return true;
    }

    /// <summary>The model reports one joined error for both lengths, so each value is checked in a model of its own to
    /// name the right option.</summary>
    private static bool ReadLength(IReadOnlyList<string> args, ref int i, string option, ref string? target,
        out ParsedCommand? error)
    {
        if (!TakeText(args, ref i, option, ref target, out error))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            error = Fail(Format(Strings.EmptyOptionText, option));
            return false;
        }

        var model = new FilterPanelModel();
        if (option == MinLengthOption)
        {
            model.MinLengthText = target!;
        }
        else
        {
            model.MaxLengthText = target!;
        }

        if (model.Error != null)
        {
            error = Fail(Format(Strings.OptionError, option, WithoutSkipSentence(model.Error)));
            return false;
        }

        return true;
    }

    private bool ReadKind(string word, out ParsedCommand? error)
    {
        if (_kindWord != null && Is(_kindWord, word))
        {
            error = Fail(Format(Strings.RepeatedOption, word));
            return false;
        }

        if (_kindWord != null)
        {
            error = Fail(Format(Strings.OneModeOnly, Strings.FilterKindOptions));
            return false;
        }

        _kindWord = word;
        error = null;
        return true;
    }

    public override ParsedCommand? Finish(ApplyDraft draft)
    {
        if (!_any)
        {
            return null;
        }

        if (_ignoreCase && _pattern == null)
        {
            return Fail(Format(Strings.NeedsOption, IgnoreCaseOption, RegexOption));
        }

        FilterPanelModel model = NewModel();
        model.Mask = _mask ?? "";
        model.Pattern = _pattern ?? "";
        model.IgnoreCase = _ignoreCase;
        model.IncludeHidden = _hidden;
        model.MinLengthText = _min ?? "";
        model.MaxLengthText = _max ?? "";
        if (_kindWord != null && Is(_kindWord, FoldersOnlyOption))
        {
            model.IncludeFolders = true;
            model.IncludeFiles = false;
        }
        else if (_kindWord != null && Is(_kindWord, IncludeFoldersOption))
        {
            model.IncludeFolders = true;
        }

        // Each value passed on its own, so the range is the only error left.
        if (string.Equals(model.Filter.Error, ItemFilter.LengthRangeError, StringComparison.Ordinal))
        {
            return Fail(Format(Strings.OptionError, MinLengthOption, ItemFilter.LengthRangeError));
        }

        draft.Filter = model;
        draft.MaskGiven = _mask != null;
        return null;
    }
}
