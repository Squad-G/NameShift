using System;

namespace NameShift.Core;

/// <summary>Moves or copies characters from the start or end of the base name to the start, the end or a 1-based
/// position of the remainder, with a separator between the two parts when the remainder is not empty. Counts are
/// UTF-16 characters; a surrogate pair is never split. The extension is never touched.</summary>
public sealed class MoveCopyRule : IRenameRule
{
    /// <summary>Creates the rule.</summary>
    /// <exception cref="ArgumentOutOfRangeException">mode, source or target is not a defined value.</exception>
    /// <exception cref="ArgumentNullException">separator is null.</exception>
    public MoveCopyRule(MoveCopyMode mode = MoveCopyMode.Move, MoveCopySource source = MoveCopySource.Start,
        int count = 0, MoveCopyTarget target = MoveCopyTarget.End, int? position = null, string separator = "")
    {
        if (!Enum.IsDefined(typeof(MoveCopyMode), mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown move or copy mode.");
        }

        if (!Enum.IsDefined(typeof(MoveCopySource), source))
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown move or copy source.");
        }

        if (!Enum.IsDefined(typeof(MoveCopyTarget), target))
        {
            throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown move or copy target.");
        }

        Mode = mode;
        Source = source;
        Count = count;
        Target = target;
        Position = position;
        Separator = separator ?? throw new ArgumentNullException(nameof(separator));
    }

    /// <summary>Whether the taken text is cut out (Move) or left in place (Copy).</summary>
    public MoveCopyMode Mode { get; }

    /// <summary>Which end of the base name the characters are taken from.</summary>
    public MoveCopySource Source { get; }

    /// <summary>Characters taken; 0 or below disables the rule; more than the base name takes it all.</summary>
    public int Count { get; }

    /// <summary>Where the taken text goes in the remainder.</summary>
    public MoveCopyTarget Target { get; }

    /// <summary>1-based position in the remainder, used only with target Position; null or below 1 disables the rule
    /// then; past the end means the end.</summary>
    public int? Position { get; }

    /// <summary>Between the taken text and the remainder; may be empty.</summary>
    public string Separator { get; }

    /// <inheritdoc />
    public RuleStage Stage => RuleStage.MoveCopy;

    /// <summary>True when Count is at least 1 and, for target Position, Position is at least 1.</summary>
    public bool Enabled => Count >= 1 && (Target != MoveCopyTarget.Position || Position >= 1);

    /// <inheritdoc />
    public NameParts Apply(NameParts name, ItemContext context)
    {
        if (!Enabled)
        {
            return name;
        }

        var text = name.BaseName;

        string taken;
        string rest;
        if (Source == MoveCopySource.Start)
        {
            var n = Math.Min(Count, text.Length);
            if (n < text.Length && IsSplit(text, n))
            {
                n++;
            }

            taken = text.Substring(0, n);
            rest = Mode == MoveCopyMode.Move ? text.Substring(n) : text;
        }
        else
        {
            var start = text.Length - Math.Min(Count, text.Length);
            if (start > 0 && IsSplit(text, start))
            {
                start--;
            }

            taken = text.Substring(start);
            rest = Mode == MoveCopyMode.Move ? text.Substring(0, start) : text;
        }

        if (rest.Length == 0)
        {
            return name; // Move of the whole base name (or an empty base): nothing changes.
        }

        var index = InsertIndex(rest);
        var result = index < rest.Length
            ? rest.Substring(0, index) + taken + Separator + rest.Substring(index)
            : rest + Separator + taken;
        return name.WithBaseName(result);
    }

    private int InsertIndex(string rest)
    {
        switch (Target)
        {
            case MoveCopyTarget.Start:
                return 0;
            case MoveCopyTarget.End:
                return rest.Length;
            default:
                var index = TextPosition.ToInsertIndex(Position!.Value, rest.Length, fromEnd: false);
                return index > 0 && index < rest.Length && IsSplit(rest, index) ? index + 1 : index;
        }
    }

    /// <summary>True when a cut at <paramref name="index"/> (0 &lt; index &lt; length) falls inside a surrogate pair.</summary>
    private static bool IsSplit(string text, int index) =>
        char.IsLowSurrogate(text[index]) && char.IsHighSurrogate(text[index - 1]);
}
