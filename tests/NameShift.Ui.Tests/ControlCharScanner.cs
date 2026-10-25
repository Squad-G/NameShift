using System.Collections.Generic;

namespace NameShift.Ui.Tests;

/// <summary>Finds control characters that a reader cannot see: everything below U+0020 except tab, line feed and carriage
/// return, and U+007F.</summary>
internal static class ControlCharScanner
{
    /// <summary>One entry per control character, in text order. Lines are 1-based and split on line feed.</summary>
    public static List<(int Line, int CodePoint)> Find(string text)
    {
        var found = new List<(int Line, int CodePoint)>();
        int line = 1;
        foreach (char c in text)
        {
            if (c == '\n')
            {
                line++;
            }
            else if (c == '\t' || c == '\r')
            {
                continue;
            }
            else if (c < ' ' || c == '\u007F')
            {
                found.Add((line, c));
            }
        }

        return found;
    }
}
