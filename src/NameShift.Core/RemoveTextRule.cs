using System;
using System.Collections.Generic;
using System.Text;

namespace NameShift.Core;

/// <summary>Removes from the base name, in order: everything before a marker, everything after a marker, every
/// occurrence of a text (optionally whole words only), and listed characters. Ordinal matching, case-insensitive
/// unless MatchCase. The extension is never touched, and a match never splits a surrogate pair.</summary>
public sealed class RemoveTextRule : IRenameRule
{
    private readonly string[] _characterUnits;

    /// <summary>Creates the rule; every part is optional.</summary>
    /// <exception cref="ArgumentNullException">A string argument is null.</exception>
    public RemoveTextRule(string characters = "", string text = "", bool wholeWord = false, bool matchCase = false,
        string cropBefore = "", string cropAfter = "", bool keepMarker = false)
    {
        Characters = characters ?? throw new ArgumentNullException(nameof(characters));
        Text = text ?? throw new ArgumentNullException(nameof(text));
        WholeWord = wholeWord;
        MatchCase = matchCase;
        CropBefore = cropBefore ?? throw new ArgumentNullException(nameof(cropBefore));
        CropAfter = cropAfter ?? throw new ArgumentNullException(nameof(cropAfter));
        KeepMarker = keepMarker;
        _characterUnits = Units(characters);
    }

    /// <summary>Every character listed is removed wherever it occurs; the list has no separators.</summary>
    public string Characters { get; }

    /// <summary>Text removed at every occurrence, left to right without overlap.</summary>
    public string Text { get; }

    /// <summary>True: <see cref="Text"/> counts only between non-letter, non-digit characters (or a name edge).</summary>
    public bool WholeWord { get; }

    /// <summary>False (default): markers, text and characters match ignoring case (ordinal, never culture-dependent).</summary>
    public bool MatchCase { get; }

    /// <summary>Everything before the first occurrence of this marker is removed.</summary>
    public string CropBefore { get; }

    /// <summary>Everything after the first occurrence of this marker (in what is left) is removed.</summary>
    public string CropAfter { get; }

    /// <summary>True: a crop keeps its marker; false (default): the marker goes too.</summary>
    public bool KeepMarker { get; }

    /// <inheritdoc />
    public RuleStage Stage => RuleStage.Remove;

    /// <summary>False when Characters, Text, CropBefore and CropAfter are all empty (the flags alone do nothing).</summary>
    public bool Enabled => Characters.Length > 0 || Text.Length > 0 || CropBefore.Length > 0 || CropAfter.Length > 0;

    private StringComparison Comparison => MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    /// <inheritdoc />
    public NameParts Apply(NameParts name, ItemContext context)
    {
        var text = name.BaseName;
        text = CropBeforeMarker(text);
        text = CropAfterMarker(text);
        text = RemoveText(text);
        text = RemoveCharacters(text);
        return name.WithBaseName(text);
    }

    private string CropBeforeMarker(string s)
    {
        var at = FindOccurrence(s, CropBefore, 0);
        if (at < 0)
        {
            return s;
        }

        return KeepMarker ? s.Substring(at) : s.Substring(at + CropBefore.Length);
    }

    private string CropAfterMarker(string s)
    {
        var at = FindOccurrence(s, CropAfter, 0);
        if (at < 0)
        {
            return s;
        }

        return KeepMarker ? s.Substring(0, at + CropAfter.Length) : s.Substring(0, at);
    }

    private string RemoveText(string s)
    {
        if (Text.Length == 0)
        {
            return s;
        }

        var result = new StringBuilder();
        var copied = 0;
        var from = 0;
        var at = FindOccurrence(s, Text, from);
        while (at >= 0)
        {
            var end = at + Text.Length;
            if (WholeWord && !(IsBoundaryBefore(s, at) && IsBoundaryAfter(s, end)))
            {
                from = at + 1;
            }
            else
            {
                result.Append(s, copied, at - copied);
                copied = end;
                from = end;
            }

            at = FindOccurrence(s, Text, from);
        }

        return result.Append(s, copied, s.Length - copied).ToString();
    }

    private string RemoveCharacters(string s)
    {
        if (_characterUnits.Length == 0)
        {
            return s;
        }

        var result = new StringBuilder(s.Length);
        var i = 0;
        while (i < s.Length)
        {
            var length = UnitLength(s, i);
            var unit = s.Substring(i, length);
            if (!IsListed(unit))
            {
                result.Append(unit);
            }

            i += length;
        }

        return result.ToString();
    }

    private bool IsListed(string unit)
    {
        foreach (var listed in _characterUnits)
        {
            if (string.Equals(unit, listed, Comparison))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The first match of <paramref name="marker"/> at or after <paramref name="from"/> that does not cut a
    /// surrogate pair in two, or -1.</summary>
    private int FindOccurrence(string s, string marker, int from)
    {
        if (marker.Length == 0)
        {
            return -1;
        }

        var at = s.IndexOf(marker, from, Comparison);
        while (at >= 0 && (SplitsPair(s, at) || SplitsPair(s, at + marker.Length)))
        {
            at = s.IndexOf(marker, at + 1, Comparison);
        }

        return at;
    }

    private static bool IsBoundaryBefore(string s, int at)
    {
        if (at == 0)
        {
            return true;
        }

        var neighbour = at - 1;
        if (neighbour > 0 && char.IsLowSurrogate(s[neighbour]) && char.IsHighSurrogate(s[neighbour - 1]))
        {
            neighbour--;
        }

        return !char.IsLetterOrDigit(s, neighbour);
    }

    private static bool IsBoundaryAfter(string s, int end) =>
        end >= s.Length || !char.IsLetterOrDigit(s, end);

    /// <summary>True when a cut at <paramref name="index"/> falls inside a surrogate pair.</summary>
    private static bool SplitsPair(string s, int index) =>
        index > 0 && index < s.Length && char.IsLowSurrogate(s[index]) && char.IsHighSurrogate(s[index - 1]);

    private static int UnitLength(string s, int index) =>
        char.IsHighSurrogate(s[index]) && index + 1 < s.Length && char.IsLowSurrogate(s[index + 1]) ? 2 : 1;

    private static string[] Units(string s)
    {
        var units = new List<string>();
        var i = 0;
        while (i < s.Length)
        {
            var length = UnitLength(s, i);
            units.Add(s.Substring(i, length));
            i += length;
        }

        return units.ToArray();
    }
}
