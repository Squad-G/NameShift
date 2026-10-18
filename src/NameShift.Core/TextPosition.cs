using System;
using System.Globalization;
using System.Linq;

namespace NameShift.Core;

/// <summary>The 1-based positions users type in positional rules (Add, Numbering, Remove), one rule everywhere.</summary>
public static class TextPosition
{
    /// <summary>Reads a typed position: null when it is empty, not a whole number, or below 1 (the part is then ignored).
    /// A whole number too large to store is read as <see cref="int.MaxValue"/>, so it is clamped like any other
    /// out-of-range position.</summary>
    public static int? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text!.Trim();
        if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return number >= 1 ? number : (int?)null;
        }

        return IsPositiveWholeNumber(trimmed) ? int.MaxValue : (int?)null;
    }

    private static bool IsPositiveWholeNumber(string text)
    {
        var digits = text.StartsWith("+", StringComparison.Ordinal) ? text.Substring(1) : text;
        return digits.Length > 0 && digits.All(c => c >= '0' && c <= '9');
    }

    /// <summary>The 0-based insertion index in a text of <paramref name="length"/> characters, clamped to [0, length].
    /// From the start, position p leaves p - 1 characters in front; from the end, position p leaves p characters after.</summary>
    /// <exception cref="ArgumentOutOfRangeException">position is below 1, or length is negative.</exception>
    public static int ToInsertIndex(int position, int length, bool fromEnd)
    {
        if (position < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, "A position starts at 1.");
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), length, "A length cannot be negative.");
        }

        return fromEnd ? Math.Max(length - position, 0) : Math.Min(position - 1, length);
    }
}
