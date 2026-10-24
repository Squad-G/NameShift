using System;

namespace NameShift.Core;

/// <summary>Removes characters from the base name by position: the first n, then the last n, then a 1-based inclusive
/// range, each on the result of the previous part. Counts are UTF-16 characters; a cut never splits a surrogate pair
/// (the whole pair goes). The extension is never touched.</summary>
public sealed class RemovePositionRule : IRenameRule
{
    /// <summary>Creates the rule; every part is optional and the constructor never throws.</summary>
    public RemovePositionRule(int? first = null, int? last = null, int? from = null, int? to = null)
    {
        First = first;
        Last = last;
        From = from;
        To = to;
    }

    /// <summary>Characters removed from the start; null, 0 or negative removes nothing.</summary>
    public int? First { get; }

    /// <summary>Characters removed from the end; null, 0 or negative removes nothing.</summary>
    public int? Last { get; }

    /// <summary>1-based start of the removed range (inclusive); the range applies only when From is at least 1 and To is at least From.</summary>
    public int? From { get; }

    /// <summary>1-based end of the removed range (inclusive), clamped to the length.</summary>
    public int? To { get; }

    /// <inheritdoc />
    public RuleStage Stage => RuleStage.Remove;

    /// <summary>True when at least one part applies (First above 0, Last above 0, or a valid range).</summary>
    public bool Enabled => FirstApplies || LastApplies || RangeApplies;

    private bool FirstApplies => First > 0;

    private bool LastApplies => Last > 0;

    private bool RangeApplies => From >= 1 && To >= From;

    /// <inheritdoc />
    public NameParts Apply(NameParts name, ItemContext context)
    {
        var text = name.BaseName;
        if (FirstApplies)
        {
            text = RemoveFirst(text, First!.Value);
        }

        if (LastApplies)
        {
            text = RemoveLast(text, Last!.Value);
        }

        if (RangeApplies)
        {
            text = RemoveRange(text, From!.Value, To!.Value);
        }

        return name.WithBaseName(text);
    }

    private static string RemoveFirst(string text, int count)
    {
        var end = Math.Min(count, text.Length);
        if (end < text.Length && IsSplit(text, end))
        {
            end++;
        }

        return text.Substring(end);
    }

    private static string RemoveLast(string text, int count)
    {
        var start = text.Length - Math.Min(count, text.Length);
        if (start > 0 && IsSplit(text, start))
        {
            start--;
        }

        return text.Substring(0, start);
    }

    private static string RemoveRange(string text, int from, int to)
    {
        var start = from - 1;
        if (start >= text.Length)
        {
            return text;
        }

        var end = Math.Min(to, text.Length);
        if (start > 0 && IsSplit(text, start))
        {
            start--;
        }

        if (end < text.Length && IsSplit(text, end))
        {
            end++;
        }

        return text.Remove(start, end - start);
    }

    /// <summary>True when a cut at <paramref name="index"/> (0 &lt; index &lt; length) falls inside a surrogate pair.</summary>
    private static bool IsSplit(string text, int index) =>
        char.IsLowSurrogate(text[index]) && char.IsHighSurrogate(text[index - 1]);
}
