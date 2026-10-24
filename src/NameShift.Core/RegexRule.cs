using System;
using System.Text.RegularExpressions;

namespace NameShift.Core;

/// <summary>Replaces every match of a .NET regular expression, left to right, in the base name or the whole name.
/// The pattern is built once (culture-invariant, 100 ms match timeout); the replacement uses .NET substitutions
/// ($1, ${name}, $&amp;, $$).</summary>
public sealed class RegexRule : IRenameRule
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    private readonly Regex _regex;

    /// <summary>Creates the rule; <paramref name="replacement"/> may be empty (each match is removed).</summary>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> or <paramref name="replacement"/> is null.</exception>
    /// <exception cref="ArgumentException">The pattern is not a valid .NET regular expression.</exception>
    public RegexRule(string pattern, string replacement, bool ignoreCase = false, bool includeExtension = false)
    {
        Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        Replacement = replacement ?? throw new ArgumentNullException(nameof(replacement));
        IgnoreCase = ignoreCase;
        IncludeExtension = includeExtension;

        try
        {
            _regex = new Regex(pattern, Options(ignoreCase), MatchTimeout);
        }
        catch (ArgumentException ex)
        {
            // .NET Framework throws ArgumentException, .NET 7+ a subclass: callers get the same type on both.
            throw new ArgumentException(ex.Message, nameof(pattern), ex);
        }
    }

    /// <summary>Null when the pattern is valid; otherwise the parser's own message.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> is null.</exception>
    public static string? Validate(string pattern)
    {
        if (pattern is null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }

        try
        {
            _ = new Regex(pattern, RegexOptions.CultureInvariant, MatchTimeout);
            return null;
        }
        catch (ArgumentException ex)
        {
            return ex.Message;
        }
    }

    /// <summary>The regular expression, as typed.</summary>
    public string Pattern { get; }

    /// <summary>The replacement, with .NET substitutions ($1, ${name}, $&amp;, $$).</summary>
    public string Replacement { get; }

    /// <summary>False (default): the match is case-sensitive. True: it ignores case (invariant culture).</summary>
    public bool IgnoreCase { get; }

    /// <summary>False (default): only the base name is searched; true: the whole name, extension included.</summary>
    public bool IncludeExtension { get; }

    /// <inheritdoc />
    public RuleStage Stage => RuleStage.RegEx;

    /// <summary>False when the pattern is empty.</summary>
    public bool Enabled => Pattern.Length > 0;

    /// <inheritdoc />
    public NameParts Apply(NameParts name, ItemContext context)
    {
        if (Pattern.Length == 0)
        {
            return name;
        }

        return IncludeExtension
            ? NameParts.Split(Replace(name.ToFullName()), context.Item.Kind)
            : name.WithBaseName(Replace(name.BaseName));
    }

    private static RegexOptions Options(bool ignoreCase) =>
        ignoreCase ? RegexOptions.CultureInvariant | RegexOptions.IgnoreCase : RegexOptions.CultureInvariant;

    private string Replace(string text)
    {
        try
        {
            return _regex.Replace(text, Replacement);
        }
        catch (RegexMatchTimeoutException ex)
        {
            throw new InvalidOperationException("The pattern took too long on this name (over 100 ms).", ex);
        }
    }
}
