namespace NameShift.Core;

/// <summary>How <see cref="NumberingRule"/> writes a number.</summary>
public enum NumeralSystem
{
    /// <summary>Digits 0 to 9: 1, 2, 10.</summary>
    Decimal,

    /// <summary>Digits 0 to 9 and A to F: 1, A, 10.</summary>
    Hexadecimal,

    /// <summary>Letters, as spreadsheet columns: a, b, ... z, aa, ab (from 1).</summary>
    Letters,

    /// <summary>Roman numerals: I, IV, X (1 to 3999).</summary>
    Roman,
}
