using System;
using System.Text;

namespace NameShift.Core;

/// <summary>Replaces every occurrence of a literal text, left to right, in the base name or the whole name.</summary>
public sealed class ReplaceRule : IRenameRule
{
    /// <summary>Creates the rule; <paramref name="with"/> may be empty (the found text is removed).</summary>
    public ReplaceRule(string find, string with, bool matchCase = false, bool includeExtension = false)
    {
        Find = find ?? throw new ArgumentNullException(nameof(find));
        With = with ?? throw new ArgumentNullException(nameof(with));
        MatchCase = matchCase;
        IncludeExtension = includeExtension;
    }

    /// <summary>The text to look for, matched literally.</summary>
    public string Find { get; }

    /// <summary>The replacement, inserted exactly as typed.</summary>
    public string With { get; }

    /// <summary>False (default): the match ignores case (ordinal, never culture-dependent).</summary>
    public bool MatchCase { get; }

    /// <summary>False (default): only the base name is searched; true: the whole name, extension included.</summary>
    public bool IncludeExtension { get; }

    /// <inheritdoc />
    public RuleStage Stage => RuleStage.Replace;

    /// <inheritdoc />
    public bool Enabled => Find.Length > 0;

    /// <inheritdoc />
    public NameParts Apply(NameParts name, ItemContext context)
    {
        if (Find.Length == 0)
        {
            return name;
        }

        return IncludeExtension
            ? NameParts.Split(ReplaceAll(name.ToFullName()), context.Item.Kind)
            : name.WithBaseName(ReplaceAll(name.BaseName));
    }

    private string ReplaceAll(string text)
    {
        var comparison = MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var at = text.IndexOf(Find, 0, comparison);
        if (at < 0)
        {
            return text;
        }

        var result = new StringBuilder();
        var start = 0;
        while (at >= 0)
        {
            var found = text.Substring(at, Find.Length);
            result.Append(text, start, at - start).Append(ReplacementFor(found));
            start = at + Find.Length;
            at = text.IndexOf(Find, start, comparison);
        }

        return result.Append(text, start, text.Length - start).ToString();
    }

    /// <summary>The text inserted for one match. Today always <see cref="With"/>; NS-16 adds the keep-case option here.</summary>
    private string ReplacementFor(string found) => With;
}
