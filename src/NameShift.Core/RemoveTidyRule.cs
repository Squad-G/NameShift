using System.Text;

namespace NameShift.Core;

/// <summary>Tidies the base name, in this order: runs of spaces (U+0020) collapse to one, white space is trimmed at
/// both ends, leading dots are removed, then white space is trimmed again. Each step runs only when its option is on
/// (the second trim follows the trim option). The extension is never touched.</summary>
public sealed class RemoveTidyRule : IRenameRule
{
    /// <summary>Creates the rule; every option is off by default.</summary>
    public RemoveTidyRule(bool trim = false, bool leadingDots = false, bool doubleSpaces = false)
    {
        Trim = trim;
        LeadingDots = leadingDots;
        DoubleSpaces = doubleSpaces;
    }

    /// <summary>True: leading and trailing white space (what <c>char.IsWhiteSpace</c> accepts) is removed.</summary>
    public bool Trim { get; }

    /// <summary>True: dots at the very start of the base name are removed.</summary>
    public bool LeadingDots { get; }

    /// <summary>True: every run of two or more spaces (U+0020) becomes one space.</summary>
    public bool DoubleSpaces { get; }

    /// <inheritdoc />
    public RuleStage Stage => RuleStage.Remove;

    /// <summary>False when no option is on.</summary>
    public bool Enabled => Trim || LeadingDots || DoubleSpaces;

    /// <inheritdoc />
    public NameParts Apply(NameParts name, ItemContext context)
    {
        var text = name.BaseName;
        if (DoubleSpaces)
        {
            text = CollapseSpaces(text);
        }

        if (Trim)
        {
            text = TrimWhiteSpace(text);
        }

        if (LeadingDots)
        {
            text = text.TrimStart('.');
        }

        if (Trim)
        {
            text = TrimWhiteSpace(text);
        }

        return name.WithBaseName(text);
    }

    private static string CollapseSpaces(string s)
    {
        var result = new StringBuilder(s.Length);
        var previousWasSpace = false;
        foreach (var c in s)
        {
            if (c == ' ' && previousWasSpace)
            {
                continue;
            }

            result.Append(c);
            previousWasSpace = c == ' ';
        }

        return result.ToString();
    }

    private static string TrimWhiteSpace(string s)
    {
        var start = 0;
        var end = s.Length;
        while (start < end && char.IsWhiteSpace(s[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(s[end - 1]))
        {
            end--;
        }

        return s.Substring(start, end - start);
    }
}
