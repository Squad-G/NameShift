using System;
using System.Text;

namespace NameShift.Core;

/// <summary>Replaces every occurrence of a text with another in the base name.</summary>
public sealed class ReplaceRule(string find, string with, bool matchCase = false) : IRenameRule
{
    /// <inheritdoc />
    public RuleStage Stage => RuleStage.Replace;

    /// <inheritdoc />
    public bool Enabled => find.Length > 0;

    /// <inheritdoc />
    public NameParts Apply(NameParts name, ItemContext context) => name.WithBaseName(Replace(name.BaseName));

    private string Replace(string text)
    {
        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var result = new StringBuilder();
        var start = 0;
        int at;
        while ((at = text.IndexOf(find, start, comparison)) >= 0)
        {
            result.Append(text, start, at - start).Append(with);
            start = at + find.Length;
        }

        return result.Append(text, start, text.Length - start).ToString();
    }
}
