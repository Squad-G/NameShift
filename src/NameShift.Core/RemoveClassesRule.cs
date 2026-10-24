using System.Globalization;
using System.Text;

namespace NameShift.Core;

/// <summary>Removes whole classes of characters from the base name: accents (marks U+0300 to U+036F, after Unicode
/// decomposition), then digits (Unicode decimal digits), symbols (Unicode punctuation and symbol categories) and
/// high characters (above U+007F) in one pass. A surrogate pair is kept or removed whole. The extension is never
/// touched.</summary>
public sealed class RemoveClassesRule : IRenameRule
{
    /// <summary>Creates the rule; every option is off by default.</summary>
    public RemoveClassesRule(bool digits = false, bool symbols = false, bool accents = false,
        bool highCharacters = false)
    {
        Digits = digits;
        Symbols = symbols;
        Accents = accents;
        HighCharacters = highCharacters;
    }

    /// <summary>True: Unicode decimal digits (what <c>char.IsDigit</c> accepts) are removed.</summary>
    public bool Digits { get; }

    /// <summary>True: Unicode punctuation and symbol characters are removed.</summary>
    public bool Symbols { get; }

    /// <summary>True: combining marks U+0300 to U+036F are removed (letters are decomposed first, then recomposed).</summary>
    public bool Accents { get; }

    /// <summary>True: every character above U+007F is removed.</summary>
    public bool HighCharacters { get; }

    /// <inheritdoc />
    public RuleStage Stage => RuleStage.Remove;

    /// <summary>False when no option is on.</summary>
    public bool Enabled => Digits || Symbols || Accents || HighCharacters;

    /// <inheritdoc />
    public NameParts Apply(NameParts name, ItemContext context)
    {
        var text = name.BaseName;
        if (Accents)
        {
            text = RemoveAccents(text);
        }

        if (Digits || Symbols || HighCharacters)
        {
            text = RemoveUnits(text);
        }

        return name.WithBaseName(text);
    }

    private static string RemoveAccents(string s)
    {
        if (HasLoneSurrogate(s))
        {
            return s;
        }

        var decomposed = s.Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        var removed = false;
        foreach (var c in decomposed)
        {
            if (c >= '\u0300' && c <= '\u036F')
            {
                removed = true;
            }
            else
            {
                result.Append(c);
            }
        }

        return removed ? result.ToString().Normalize(NormalizationForm.FormC) : s;
    }

    private string RemoveUnits(string s)
    {
        var result = new StringBuilder(s.Length);
        var i = 0;
        while (i < s.Length)
        {
            var length = UnitLength(s, i);
            if (!IsRemoved(s, i))
            {
                result.Append(s, i, length);
            }

            i += length;
        }

        return result.ToString();
    }

    private bool IsRemoved(string s, int index)
    {
        if (HighCharacters && s[index] > '\u007F')
        {
            return true;
        }

        if (!Digits && !Symbols)
        {
            return false;
        }

        switch (CharUnicodeInfo.GetUnicodeCategory(s, index))
        {
            case UnicodeCategory.DecimalDigitNumber:
                return Digits;
            case UnicodeCategory.ConnectorPunctuation:
            case UnicodeCategory.DashPunctuation:
            case UnicodeCategory.OpenPunctuation:
            case UnicodeCategory.ClosePunctuation:
            case UnicodeCategory.InitialQuotePunctuation:
            case UnicodeCategory.FinalQuotePunctuation:
            case UnicodeCategory.OtherPunctuation:
            case UnicodeCategory.MathSymbol:
            case UnicodeCategory.CurrencySymbol:
            case UnicodeCategory.ModifierSymbol:
            case UnicodeCategory.OtherSymbol:
                return Symbols;
            default:
                return false;
        }
    }

    private static bool HasLoneSurrogate(string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(s[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static int UnitLength(string s, int index) =>
        char.IsHighSurrogate(s[index]) && index + 1 < s.Length && char.IsLowSurrogate(s[index + 1]) ? 2 : 1;
}
