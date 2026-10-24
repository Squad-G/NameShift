using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NameShift.Ui.Tests;

/// <summary>The source-scan rules of the window style (spacing, fonts, colours): text in, a list of (line, message) out.</summary>
internal static class StyleScanner
{
    private static readonly Regex PaddingStart = new Regex(@"new\s+Padding\s*\(");
    private static readonly Regex NumberLiteral = new Regex(@"(?<![\w.])\d+(\.\d+)?[fFdDmMuUlL]*");
    private static readonly Regex NamedFont = new Regex(@"new\s+Font\s*\(\s*[@$]*""");
    private static readonly Regex FontFamily = new Regex(@"new\s+FontFamily\s*\(");
    private static readonly Regex ColourName = new Regex(@"\bColor\.(?!Empty\b|Transparent\b)[A-Z]\w*");
    private static readonly Regex ColourTranslator = new Regex(@"\bColorTranslator\b");

    /// <summary>Every numeric literal other than a lone 0 inside the argument list of a <c>new Padding(</c>; reported on the
    /// line where that expression starts.</summary>
    public static List<(int Line, string Message)> FindPaddingLiterals(string text)
    {
        text = Normalise(text);
        var found = new List<(int, string)>();
        foreach (Match start in PaddingStart.Matches(text))
        {
            int depth = 1;
            int index = start.Index + start.Length;
            int begin = index;
            while (index < text.Length && depth > 0)
            {
                if (text[index] == '(')
                {
                    depth++;
                }
                else if (text[index] == ')')
                {
                    depth--;
                }

                index++;
            }

            string arguments = text.Substring(begin, Math.Max(0, index - 1 - begin));
            foreach (Match number in NumberLiteral.Matches(arguments))
            {
                if (number.Value != "0")
                {
                    found.Add((LineOf(text, start.Index), "Padding uses the literal " + number.Value + "; use LogicalToDeviceUnits(Spacing.X)"));
                    break;
                }
            }
        }

        return found;
    }

    /// <summary>A font made from a family name, or any <c>new FontFamily(</c>: the window uses the one system font.</summary>
    public static List<(int Line, string Message)> FindNamedFonts(string text)
    {
        text = Normalise(text);
        var found = new List<(int, string)>();
        foreach (Match m in NamedFont.Matches(text))
        {
            found.Add((LineOf(text, m.Index), "a named font; use the inherited system font"));
        }

        foreach (Match m in FontFamily.Matches(text))
        {
            found.Add((LineOf(text, m.Index), "a font family; use the inherited system font"));
        }

        return found;
    }

    /// <summary>A colour that is not a system colour or a Palette entry; <c>Palette.cs</c> itself is not scanned.</summary>
    public static List<(int Line, string Message)> FindColours(string text)
    {
        text = Normalise(text);
        var found = new List<(int, string)>();
        foreach (Match m in ColourName.Matches(text))
        {
            found.Add((LineOf(text, m.Index), m.Value + " is not a system colour; add it to Palette"));
        }

        foreach (Match m in ColourTranslator.Matches(text))
        {
            found.Add((LineOf(text, m.Index), "ColorTranslator is not allowed; add the colour to Palette"));
        }

        return found;
    }

    /// <summary>The 1-based line of every use of the word <paramref name="word"/>.</summary>
    public static List<int> FindWord(string text, string word)
    {
        text = Normalise(text);
        var found = new List<int>();
        foreach (Match m in Regex.Matches(text, @"\b" + Regex.Escape(word) + @"\b"))
        {
            found.Add(LineOf(text, m.Index));
        }

        return found;
    }

    private static string Normalise(string text) => text.Replace("\r\n", "\n");

    private static int LineOf(string text, int index)
    {
        int line = 1;
        for (int i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }
}
