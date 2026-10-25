using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NameShift.Ui.Tests;

/// <summary>NS-94 AC 6: finds a verbatim path literal an editor broke (<c>@"C:</c> at the end of a line, from a path whose
/// backslash escape was read as a line break). Not a full C# lexer: it skips regular strings and character literals on
/// the line, and reads comments like code.</summary>
internal static class BrokenLiteralScanner
{
    public const string Reason = "verbatim path literal not closed on its line";

    // A drive letter, a colon, and optionally a backslash with path characters (no space) up to the end of the line.
    private static readonly Regex PathStart = new Regex(@"^[A-Za-z]:(\\[^\s""]*)?$");

    /// <summary>Every line (1-based) holding a verbatim string that is opened, not closed on the line, and starts like a path.</summary>
    public static List<(int Line, string Reason)> Find(string text)
    {
        var found = new List<(int Line, string Reason)>();
        string[] lines = text.Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            if (IsBroken(lines[index].TrimEnd('\r')))
            {
                found.Add((index + 1, Reason));
            }
        }

        return found;
    }

    private static bool IsBroken(string line)
    {
        int i = 0;
        while (i < line.Length)
        {
            char c = line[i];
            if (c == '\'')
            {
                i = SkipCharLiteral(line, i);
            }
            else if (c == '@' || c == '$')
            {
                int quote = i;
                bool verbatim = false;
                while (quote < line.Length && (line[quote] == '@' || line[quote] == '$'))
                {
                    verbatim |= line[quote] == '@';
                    quote++;
                }

                if (quote >= line.Length || line[quote] != '"')
                {
                    i = quote;
                }
                else if (verbatim)
                {
                    int closed = SkipVerbatim(line, quote + 1);
                    if (closed < 0)
                    {
                        return PathStart.IsMatch(line.Substring(quote + 1));
                    }

                    i = closed;
                }
                else
                {
                    i = SkipRegular(line, quote);
                }
            }
            else if (c == '"')
            {
                i = SkipRegular(line, i);
            }
            else
            {
                i++;
            }
        }

        return false;
    }

    /// <summary>The index after the closing quote of the regular string opened at <paramref name="open"/> (the line's end when unclosed).</summary>
    private static int SkipRegular(string line, int open)
    {
        int i = open + 1;
        while (i < line.Length)
        {
            if (line[i] == '\\')
            {
                i += 2;
            }
            else if (line[i] == '"')
            {
                return i + 1;
            }
            else
            {
                i++;
            }
        }

        return line.Length;
    }

    /// <summary>The index after the closing quote of a verbatim string whose text starts at <paramref name="start"/>, a doubled quote
    /// being text; -1 when the line ends first.</summary>
    private static int SkipVerbatim(string line, int start)
    {
        int i = start;
        while (i < line.Length)
        {
            if (line[i] == '"')
            {
                if (i + 1 < line.Length && line[i + 1] == '"')
                {
                    i += 2;
                    continue;
                }

                return i + 1;
            }

            i++;
        }

        return -1;
    }

    /// <summary>Past a character literal such as <c>'x'</c> or <c>'\n'</c>; one past a lone apostrophe (a comment's) otherwise.</summary>
    private static int SkipCharLiteral(string line, int open)
    {
        if (open + 2 < line.Length && line[open + 1] != '\\' && line[open + 2] == '\'')
        {
            return open + 3;
        }

        if (open + 1 < line.Length && line[open + 1] == '\\')
        {
            int close = line.IndexOf('\'', open + 3);
            if (close > 0 && close - open <= 10)
            {
                return close + 1;
            }
        }

        return open + 1;
    }
}
