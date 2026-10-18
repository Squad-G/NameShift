using System;

namespace NameShift.Core;

/// <summary>Replaces every occurrence of a text with another.</summary>
public sealed class ReplaceRule(string find, string with, bool matchCase = false) : IRenameRule
{
    public bool Enabled => find.Length > 0;

    public string Apply(string name, int index)
    {
        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var result = new System.Text.StringBuilder();
        var start = 0;
        int at;
        while ((at = name.IndexOf(find, start, comparison)) >= 0)
        {
            result.Append(name, start, at - start).Append(with);
            start = at + find.Length;
        }

        return result.Append(name, start, name.Length - start).ToString();
    }
}
