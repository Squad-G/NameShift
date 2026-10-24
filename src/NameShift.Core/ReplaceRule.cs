using System;
using System.Text;

namespace NameShift.Core;

/// <summary>Replaces every occurrence of a literal text, left to right, in the base name or the whole name.</summary>
public sealed class ReplaceRule : IRenameRule
{
    /// <summary>Creates the rule; <paramref name="with"/> may be empty (the found text is removed).</summary>
    public ReplaceRule(string find, string with, bool matchCase = false, bool includeExtension = false, bool keepCase = false)
    {
        Find = find ?? throw new ArgumentNullException(nameof(find));
        With = with ?? throw new ArgumentNullException(nameof(with));
        MatchCase = matchCase;
        IncludeExtension = includeExtension;
        KeepCase = keepCase;
    }

    /// <summary>The text to look for, matched literally.</summary>
    public string Find { get; }

    /// <summary>The replacement; inserted as typed unless <see cref="KeepCase"/>.</summary>
    public string With { get; }

    /// <summary>False (default): the match ignores case (ordinal, never culture-dependent).</summary>
    public bool MatchCase { get; }

    /// <summary>False (default): only the base name is searched; true: the whole name, extension included.</summary>
    public bool IncludeExtension { get; }

    /// <summary>False (default): the replacement is inserted as typed. True: it follows each found text's case pattern
    /// (all upper: upper-cased; capitalised: <see cref="CaseRule"/>'s capitalise; anything else: as typed).</summary>
    public bool KeepCase { get; }

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

    private enum FoundCase
    {
        AsTyped,
        Upper,
        Capitalised,
    }

    /// <summary>The text inserted for one match: <see cref="With"/>, shaped by the found text when <see cref="KeepCase"/>.</summary>
    private string ReplacementFor(string found)
    {
        if (!KeepCase || With.Length == 0)
        {
            return With;
        }

        switch (PatternOf(found))
        {
            case FoundCase.Upper: return With.ToUpperInvariant();
            case FoundCase.Capitalised: return CaseRule.Capitalise(With);
            default: return With;
        }
    }

    /// <summary>Reads the letters only: two or more, all upper = Upper; first upper and the rest lower (or a single
    /// upper letter) = Capitalised; anything else, including no letters, = AsTyped.</summary>
    private static FoundCase PatternOf(string found)
    {
        var letters = 0;
        var upper = 0;
        var lowerAfterFirst = 0;
        var firstIsUpper = false;
        foreach (var c in found)
        {
            if (!char.IsLetter(c))
            {
                continue;
            }

            if (letters == 0)
            {
                firstIsUpper = char.IsUpper(c);
            }
            else if (char.IsLower(c))
            {
                lowerAfterFirst++;
            }

            if (char.IsUpper(c))
            {
                upper++;
            }

            letters++;
        }

        if (letters >= 2 && upper == letters)
        {
            return FoundCase.Upper;
        }

        return firstIsUpper && lowerAfterFirst == letters - 1 ? FoundCase.Capitalised : FoundCase.AsTyped;
    }
}
