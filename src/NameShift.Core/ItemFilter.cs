using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NameShift.Core;

/// <summary>A read-only yes/no on an item's own facts: a mask on the full name, a regular expression and a length
/// range on the base name, the file and folder kinds, and hidden or system items. Everything is parsed once, in the
/// constructor. A bad pattern or a minimum above the maximum switches off only that part and is reported in
/// <see cref="Error"/>. The filter never touches the disk and never changes an item.</summary>
public sealed class ItemFilter
{
    /// <summary>The <see cref="Error"/> text when the minimum length is larger than the maximum length.</summary>
    public const string LengthRangeError = "The minimum length is larger than the maximum length.";

    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);

    private readonly string[] _maskParts;
    private readonly bool _maskMatchesAll;
    private readonly Regex? _regex;
    private readonly int? _min;
    private readonly int? _max;

    /// <summary>Creates the filter; every part left at its default holds for every item (except hidden and system
    /// items, which are out unless <paramref name="includeHidden"/> is true).</summary>
    /// <param name="mask">Parts separated by ";" matched against the full name: "*" is any text, "?" is one
    /// character, anything else is literal, and the case is ignored.</param>
    /// <param name="pattern">A .NET regular expression searched in the base name; empty means no condition.</param>
    /// <param name="ignoreCase">True: the pattern ignores case (invariant culture).</param>
    /// <param name="includeFiles">False: files are out.</param>
    /// <param name="includeFolders">False: folders are out.</param>
    /// <param name="includeHidden">False: items with the Hidden or System attribute are out.</param>
    /// <param name="minLength">The smallest base-name length (UTF-16 units), or null for no minimum.</param>
    /// <param name="maxLength">The largest base-name length (UTF-16 units), or null for no maximum.</param>
    /// <exception cref="ArgumentNullException"><paramref name="mask"/> or <paramref name="pattern"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="minLength"/> or <paramref name="maxLength"/> is
    /// negative.</exception>
    public ItemFilter(
        string mask = "",
        string pattern = "",
        bool ignoreCase = false,
        bool includeFiles = true,
        bool includeFolders = true,
        bool includeHidden = false,
        int? minLength = null,
        int? maxLength = null)
    {
        Mask = mask ?? throw new ArgumentNullException(nameof(mask));
        Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
        if (minLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minLength), "The minimum length cannot be negative.");
        }

        if (maxLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxLength), "The maximum length cannot be negative.");
        }

        IgnoreCase = ignoreCase;
        IncludeFiles = includeFiles;
        IncludeFolders = includeFolders;
        IncludeHidden = includeHidden;
        MinLength = minLength;
        MaxLength = maxLength;

        var parts = new List<string>();
        foreach (string raw in mask.Split(';'))
        {
            string part = raw.Trim();
            if (part.Length == 0)
            {
                continue;
            }

            parts.Add(part);
            if (part == "*.*" || part == "*")
            {
                _maskMatchesAll = true;
            }
        }

        _maskParts = parts.ToArray();

        string? patternError = null;
        if (pattern.Length > 0)
        {
            patternError = RegexRule.Validate(pattern);
            if (patternError is null)
            {
                RegexOptions options = RegexOptions.CultureInvariant;
                if (ignoreCase)
                {
                    options |= RegexOptions.IgnoreCase;
                }

                _regex = new Regex(pattern, options, MatchTimeout);
            }
        }

        string? lengthError = null;
        if (minLength.HasValue && maxLength.HasValue && minLength.Value > maxLength.Value)
        {
            lengthError = LengthRangeError;
        }
        else
        {
            _min = minLength;
            _max = maxLength;
        }

        Error = patternError is not null && lengthError is not null
            ? patternError + " " + lengthError
            : patternError ?? lengthError;
    }

    /// <summary>The mask, as typed (never trimmed).</summary>
    public string Mask { get; }

    /// <summary>The regular expression, as typed (never trimmed).</summary>
    public string Pattern { get; }

    /// <summary>True: the pattern ignores case.</summary>
    public bool IgnoreCase { get; }

    /// <summary>False: files are out.</summary>
    public bool IncludeFiles { get; }

    /// <summary>False: folders are out.</summary>
    public bool IncludeFolders { get; }

    /// <summary>False: items with the Hidden or System attribute are out.</summary>
    public bool IncludeHidden { get; }

    /// <summary>The smallest base-name length, as given; null for none.</summary>
    public int? MinLength { get; }

    /// <summary>The largest base-name length, as given; null for none.</summary>
    public int? MaxLength { get; }

    /// <summary>Null when every part is fine; otherwise the message of the part that was switched off (the
    /// pattern's first, then <see cref="LengthRangeError"/>, joined by one space).</summary>
    public string? Error { get; }

    /// <summary>True when the item passes every condition. An item whose pattern match times out is left out.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public bool Includes(RenameItem item)
    {
        if (item is null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        if (!(item.Kind == ItemKind.File ? IncludeFiles : IncludeFolders))
        {
            return false;
        }

        if (!IncludeHidden && (item.Attributes & (ItemAttributes.Hidden | ItemAttributes.System)) != 0)
        {
            return false;
        }

        if (_maskParts.Length > 0 && !_maskMatchesAll && !MatchesMask(item.Name))
        {
            return false;
        }

        if (_min.HasValue || _max.HasValue || _regex is not null)
        {
            string baseName = NameParts.Split(item.Name, item.Kind).BaseName;
            int length = baseName.Length;
            if ((_min.HasValue && length < _min.Value) || (_max.HasValue && length > _max.Value))
            {
                return false;
            }

            if (_regex is not null)
            {
                try
                {
                    return _regex.IsMatch(baseName);
                }
                catch (RegexMatchTimeoutException)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>The included items: the same instances, in input order, in a new list.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> or one of its items is null.</exception>
    public IReadOnlyList<RenameItem> Apply(IReadOnlyList<RenameItem> items)
    {
        if (items is null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        var included = new List<RenameItem>();
        foreach (RenameItem item in items)
        {
            if (Includes(item))
            {
                included.Add(item);
            }
        }

        return included;
    }

    private bool MatchesMask(string name)
    {
        foreach (string part in _maskParts)
        {
            if (WildcardMatch(name, part))
            {
                return true;
            }
        }

        return false;
    }

    // Iterative matcher with one backtrack point (the last '*'): O(name x part) at worst, no regular expression.
    private static bool WildcardMatch(string name, string part)
    {
        int n = 0;
        int p = 0;
        int star = -1;
        int resume = 0;

        while (n < name.Length)
        {
            if (p < part.Length && part[p] == '*')
            {
                star = p++;
                resume = n;
            }
            else if (p < part.Length && (part[p] == '?' || SameChar(part[p], name[n])))
            {
                p++;
                n++;
            }
            else if (star >= 0)
            {
                p = star + 1;
                n = ++resume;
            }
            else
            {
                return false;
            }
        }

        while (p < part.Length && part[p] == '*')
        {
            p++;
        }

        return p == part.Length;
    }

    private static bool SameChar(char a, char b) => char.ToUpperInvariant(a) == char.ToUpperInvariant(b);
}
