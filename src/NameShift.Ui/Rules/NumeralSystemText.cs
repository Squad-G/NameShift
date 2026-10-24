using System;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The list text of each number system.</summary>
public static class NumeralSystemText
{
    /// <exception cref="ArgumentOutOfRangeException">The system is not a defined value.</exception>
    public static string For(NumeralSystem numerals)
    {
        switch (numerals)
        {
            case NumeralSystem.Decimal:
                return Strings.NumberingSystemDecimal;
            case NumeralSystem.Hexadecimal:
                return Strings.NumberingSystemHexadecimal;
            case NumeralSystem.Letters:
                return Strings.NumberingSystemLetters;
            case NumeralSystem.Roman:
                return Strings.NumberingSystemRoman;
            default:
                throw new ArgumentOutOfRangeException(nameof(numerals), numerals, "Unknown numeral system.");
        }
    }
}
