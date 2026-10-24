using System;

namespace NameShift.Core;

/// <summary>Numbers items in the order of the list given to the pipeline (the extension is never touched).</summary>
/// <remarks>The engine does not sort: the app passes the list in the order it wants, and only the items to number.
/// Items are numbered in the order of the list given to the pipeline, or within each folder when BreakPerFolder is set.
/// The number is written in decimal, hexadecimal, letters or Roman numerals (<see cref="NumeralSystem"/>).</remarks>
public sealed class NumberingRule : IRenameRule
{
    /// <summary>The largest pad: a Windows name holds at most 255 characters.</summary>
    public const int MaxPad = 255;

    /// <summary>Creates the rule; the separator goes between the number and the name.</summary>
    /// <exception cref="ArgumentOutOfRangeException">mode is not defined, pad is outside 0..MaxPad, or numerals is not defined.</exception>
    /// <exception cref="ArgumentNullException">separator is null.</exception>
    public NumberingRule(NumberingMode mode = NumberingMode.Suffix, int start = 1, int step = 1, int pad = 0,
        string separator = "", int? insertPosition = null, NumeralSystem numerals = NumeralSystem.Decimal,
        bool? upperCase = null, bool breakPerFolder = false)
    {
        if (!Enum.IsDefined(typeof(NumberingMode), mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown numbering mode.");
        }

        if (pad < 0 || pad > MaxPad)
        {
            throw new ArgumentOutOfRangeException(nameof(pad), pad, "The pad must be between 0 and " + MaxPad + ".");
        }

        if (!Enum.IsDefined(typeof(NumeralSystem), numerals))
        {
            throw new ArgumentOutOfRangeException(nameof(numerals), numerals, "Unknown numeral system.");
        }

        Mode = mode;
        Start = start;
        Step = step;
        Pad = pad;
        Separator = separator ?? throw new ArgumentNullException(nameof(separator));
        InsertPosition = insertPosition;
        Numerals = numerals;
        UpperCase = upperCase ?? (numerals == NumeralSystem.Hexadecimal || numerals == NumeralSystem.Roman);
        BreakPerFolder = breakPerFolder;
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

    /// <summary>How the number is written.</summary>
    public NumeralSystem Numerals { get; }

    /// <summary>Upper case letters (Hexadecimal, Letters, Roman); the system's own default unless given, no effect in Decimal.</summary>
    public bool UpperCase { get; }

    /// <summary>True: the number restarts at Start in each folder (it counts the item's position in its folder,
    /// ItemContext.FolderIndex, instead of its position in the list).</summary>
    public bool BreakPerFolder { get; }

    /// <inheritdoc />
    public RuleStage Stage => RuleStage.Numbering;

    /// <inheritdoc />
    public bool Enabled => Mode != NumberingMode.Insert || InsertPosition >= 1;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The number is below zero, or has no form in the system (Letters, Roman).</exception>
    public NameParts Apply(NameParts name, ItemContext context)
    {
        var value = ValueFor(context);
        if (value < 0)
        {
            throw new InvalidOperationException("The number is below zero.");
        }

        var number = NumberText.Format(value, Numerals, Pad, UpperCase);
        var baseName = name.BaseName;
        switch (Mode)
        {
            case NumberingMode.Prefix:
                return name.WithBaseName(number + Separator + baseName);
            case NumberingMode.Suffix:
                return name.WithBaseName(baseName + Separator + number);
            default:
                var index = TextPosition.ToInsertIndex(InsertPosition!.Value, baseName.Length, fromEnd: false);
                var separator = index < baseName.Length ? Separator : "";
                return name.WithBaseName(baseName.Insert(index, number + separator));
        }
    }

    private long ValueFor(ItemContext context) => Start + (long)(BreakPerFolder ? context.FolderIndex : context.Index) * Step;
}
