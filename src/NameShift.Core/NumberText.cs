using System;
using System.Globalization;
using System.Text;

namespace NameShift.Core;

/// <summary>Writes and reads whole numbers in the systems of <see cref="NumeralSystem"/> (invariant culture, ASCII only).</summary>
public static class NumberText
{
    /// <summary>The largest value Roman numerals can write.</summary>
    public const int MaxRoman = 3999;

    private const string NoLettersForm = "The number cannot be written in letters (from 1).";
    private const string NoRomanForm = "The number cannot be written in Roman numerals (1 to 3999).";

    private static readonly int[] RomanValues = [1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1];

    private static readonly string[] RomanSymbols =
        ["M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I"];

    /// <summary>Writes a value; the pad is the minimum width in Decimal and Hexadecimal and is ignored in Letters and Roman.</summary>
    /// <exception cref="ArgumentOutOfRangeException">numerals is not defined, pad is outside 0..NumberingRule.MaxPad, or value is below zero.</exception>
    /// <exception cref="InvalidOperationException">The value has no form: below 1 in Letters, outside 1..MaxRoman in Roman.</exception>
    public static string Format(long value, NumeralSystem numerals, int pad, bool upperCase)
    {
        if (!Enum.IsDefined(typeof(NumeralSystem), numerals))
        {
            throw new ArgumentOutOfRangeException(nameof(numerals), numerals, "Unknown numeral system.");
        }

        if (pad < 0 || pad > NumberingRule.MaxPad)
        {
            throw new ArgumentOutOfRangeException(nameof(pad), pad,
                "The pad must be between 0 and " + NumberingRule.MaxPad + ".");
        }

        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The value cannot be below zero.");
        }

        switch (numerals)
        {
            case NumeralSystem.Decimal:
                return value.ToString(CultureInfo.InvariantCulture).PadLeft(pad, '0');
            case NumeralSystem.Hexadecimal:
                return value.ToString(upperCase ? "X" : "x", CultureInfo.InvariantCulture).PadLeft(pad, '0');
            case NumeralSystem.Letters:
                return FormatLetters(value, upperCase);
            default:
                return FormatRoman(value, upperCase);
        }
    }

    /// <summary>Reads a value written in a system (surrounding white space is ignored); never throws on the text.</summary>
    /// <param name="text">The text; null fails.</param>
    /// <param name="numerals">The system the text is written in.</param>
    /// <param name="value">The value, or 0 when the method returns false.</param>
    /// <returns>True when the text is a whole number of that system that fits in an int.</returns>
    /// <exception cref="ArgumentOutOfRangeException">numerals is not defined.</exception>
    public static bool TryParse(string? text, NumeralSystem numerals, out int value)
    {
        if (!Enum.IsDefined(typeof(NumeralSystem), numerals))
        {
            throw new ArgumentOutOfRangeException(nameof(numerals), numerals, "Unknown numeral system.");
        }

        value = 0;
        if (text == null)
        {
            return false;
        }

        var t = text.Trim();
        if (t.Length == 0)
        {
            return false;
        }

        int result;
        bool ok;
        switch (numerals)
        {
            case NumeralSystem.Decimal:
                ok = TryParseDigits(t, 10, out result);
                break;
            case NumeralSystem.Hexadecimal:
                ok = TryParseDigits(t, 16, out result);
                break;
            case NumeralSystem.Letters:
                ok = TryParseLetters(t, out result);
                break;
            default:
                ok = TryParseRoman(t, out result);
                break;
        }

        if (!ok)
        {
            return false;
        }

        value = result;
        return true;
    }

    private static string FormatLetters(long value, bool upperCase)
    {
        if (value < 1)
        {
            throw new InvalidOperationException(NoLettersForm);
        }

        var first = upperCase ? 'A' : 'a';
        var buffer = new char[16];
        var i = buffer.Length;
        var v = value;
        while (v > 0)
        {
            v--;
            buffer[--i] = (char)(first + (int)(v % 26));
            v /= 26;
        }

        return new string(buffer, i, buffer.Length - i);
    }

    private static string FormatRoman(long value, bool upperCase)
    {
        if (value < 1 || value > MaxRoman)
        {
            throw new InvalidOperationException(NoRomanForm);
        }

        var builder = new StringBuilder();
        var rest = (int)value;
        for (var i = 0; i < RomanValues.Length; i++)
        {
            while (rest >= RomanValues[i])
            {
                builder.Append(RomanSymbols[i]);
                rest -= RomanValues[i];
            }
        }

        var text = builder.ToString();
        return upperCase ? text : text.ToLowerInvariant();
    }

    private static bool TryParseDigits(string t, int radix, out int value)
    {
        value = 0;
        long acc = 0;
        foreach (var c in t)
        {
            int digit;
            if (c >= '0' && c <= '9')
            {
                digit = c - '0';
            }
            else if (radix == 16 && c >= 'a' && c <= 'f')
            {
                digit = c - 'a' + 10;
            }
            else if (radix == 16 && c >= 'A' && c <= 'F')
            {
                digit = c - 'A' + 10;
            }
            else
            {
                return false;
            }

            acc = acc * radix + digit;
            if (acc > int.MaxValue)
            {
                return false;
            }
        }

        value = (int)acc;
        return true;
    }

    private static bool TryParseLetters(string t, out int value)
    {
        value = 0;
        long acc = 0;
        foreach (var c in t)
        {
            int letter;
            if (c >= 'a' && c <= 'z')
            {
                letter = c - 'a' + 1;
            }
            else if (c >= 'A' && c <= 'Z')
            {
                letter = c - 'A' + 1;
            }
            else
            {
                return false;
            }

            acc = acc * 26 + letter;
            if (acc > int.MaxValue)
            {
                return false;
            }
        }

        value = (int)acc;
        return true;
    }

    private static bool TryParseRoman(string t, out int value)
    {
        value = 0;
        if (t.Length > 15)
        {
            return false;
        }

        // ASCII-only upper casing: ToUpperInvariant could map a non-ASCII letter onto I, V, X...
        var upper = new char[t.Length];
        for (var i = 0; i < t.Length; i++)
        {
            var c = t[i];
            upper[i] = c >= 'a' && c <= 'z' ? (char)(c - 32) : c;
        }

        var total = 0;
        for (var i = 0; i < upper.Length; i++)
        {
            var current = RomanDigit(upper[i]);
            if (current == 0)
            {
                return false;
            }

            var next = i + 1 < upper.Length ? RomanDigit(upper[i + 1]) : 0;
            total += current < next ? -current : current;
        }

        if (total < 1 || total > MaxRoman
            || !string.Equals(FormatRoman(total, true), new string(upper), StringComparison.Ordinal))
        {
            return false;
        }

        value = total;
        return true;
    }

    private static int RomanDigit(char c)
    {
        switch (c)
        {
            case 'I': return 1;
            case 'V': return 5;
            case 'X': return 10;
            case 'L': return 50;
            case 'C': return 100;
            case 'D': return 500;
            case 'M': return 1000;
            default: return 0;
        }
    }
}
