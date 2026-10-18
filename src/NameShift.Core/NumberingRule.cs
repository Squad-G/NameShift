using System;
using System.Globalization;

namespace NameShift.Core;

/// <summary>Numbers items in the order of the list given to the pipeline (the extension is never touched).</summary>
/// <remarks>The engine does not sort: the app passes the list in the order it wants, and only the items to number.</remarks>
public sealed class NumberingRule : IRenameRule
{
    /// <summary>The largest pad: a Windows name holds at most 255 characters.</summary>
    public const int MaxPad = 255;

    /// <summary>Creates the rule; the separator goes between the number and the name.</summary>
    /// <exception cref="ArgumentOutOfRangeException">mode is not defined, or pad is outside 0..MaxPad.</exception>
    /// <exception cref="ArgumentNullException">separator is null.</exception>
    public NumberingRule(NumberingMode mode = NumberingMode.Suffix, int start = 1, int step = 1, int pad = 0,
        string separator = "", int? insertPosition = null)
    {
        if (!Enum.IsDefined(typeof(NumberingMode), mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown numbering mode.");
        }

        if (pad < 0 || pad > MaxPad)
        {
            throw new ArgumentOutOfRangeException(nameof(pad), pad, "The pad must be between 0 and " + MaxPad + ".");
        }

        Mode = mode;
        Start = start;
        Step = step;
        Pad = pad;
        Separator = separator ?? throw new ArgumentNullException(nameof(separator));
        InsertPosition = insertPosition;
    }

    /// <summary>Where the number goes.</summary>
    public NumberingMode Mode { get; }

    /// <summary>The number of the first item; below zero makes every item an error.</summary>
    public int Start { get; }

    /// <summary>The difference between two items' numbers; may be negative or 0.</summary>
    public int Step { get; }

    /// <summary>The minimum number of digits (0 = no padding); a wider number is never cut.</summary>
    public int Pad { get; }

    /// <summary>Text put between the number and the name; empty by default.</summary>
    public string Separator { get; }

    /// <summary>The 1-based position in Insert mode; null or below 1 means the rule does nothing.</summary>
    public int? InsertPosition { get; }

    /// <inheritdoc />
    public RuleStage Stage => RuleStage.Numbering;

    /// <inheritdoc />
    public bool Enabled => Mode != NumberingMode.Insert || InsertPosition >= 1;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The number is below zero.</exception>
    public NameParts Apply(NameParts name, ItemContext context)
    {
        var value = ValueFor(context);
        if (value < 0)
        {
            throw new InvalidOperationException("The number is below zero.");
        }

        var number = Format(value);
        var baseName = name.BaseName;
        switch (Mode)
        {
            case NumberingMode.Prefix:
                return name.WithBaseName(number + Separator + baseName);
            case NumberingMode.Suffix:
                return name.WithBaseName(baseName + Separator + number);
            default:
                var index = TextPosition.ToInsertIndex(InsertPosition!.Value, baseName.Length, fromEnd: false);
                return name.WithBaseName(baseName.Insert(index, number + Separator));
        }
    }

    private long ValueFor(ItemContext context) => Start + (long)context.Index * Step;

    private string Format(long value) =>
        value.ToString(CultureInfo.InvariantCulture).PadLeft(Pad, '0');
}
