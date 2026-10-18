using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NameShift.Core;

/// <summary>Changes the capitalisation of the base name, with culture-invariant casing (the extension is never touched).</summary>
public sealed class CaseRule : IRenameRule
{
    private readonly Dictionary<string, string> exceptionsByWord;

    /// <summary>Creates the rule; <paramref name="exceptions"/> (title mode only) is trimmed, blank entries are dropped.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined <see cref="CaseMode"/>.</exception>
    public CaseRule(CaseMode mode, IEnumerable<string>? exceptions = null)
    {
        if (!Enum.IsDefined(typeof(CaseMode), mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a defined case mode.");
        }

        Mode = mode;
        var normalised = new List<string>();
        exceptionsByWord = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in exceptions ?? Enumerable.Empty<string>())
        {
            var trimmed = entry?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            var word = trimmed!;
            normalised.Add(word);
            if (!exceptionsByWord.ContainsKey(word))
            {
                exceptionsByWord.Add(word, word);
            }
        }

        Exceptions = normalised;
    }

    /// <summary>How the base name is rewritten.</summary>
    public CaseMode Mode { get; }

    /// <summary>The title-mode exceptions, trimmed, in the given order.</summary>
    public IReadOnlyList<string> Exceptions { get; }

    /// <inheritdoc />
    public RuleStage Stage => RuleStage.Case;

    /// <inheritdoc />
    public bool Enabled => Mode != CaseMode.Same;

    /// <summary>Splits comma-separated exceptions text ("of, the, USA") into trimmed, non-blank entries.</summary>
    public static IReadOnlyList<string> ParseExceptions(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        return text!.Split(new[] { ',' })
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .ToList();
    }

    /// <inheritdoc />
    public NameParts Apply(NameParts name, ItemContext context) => Mode switch
    {
        CaseMode.Lower => name.WithBaseName(name.BaseName.ToLowerInvariant()),
        CaseMode.Upper => name.WithBaseName(name.BaseName.ToUpperInvariant()),
        CaseMode.Title => name.WithBaseName(ToTitle(name.BaseName)),
        CaseMode.Sentence => name.WithBaseName(ToSentence(name.BaseName)),
        _ => name,
    };

    private static bool IsWordCharacter(char c)
    {
        if (char.IsLetterOrDigit(c) || char.IsSurrogate(c) || c == '\'' || c == '’')
        {
            return true;
        }

        var category = CharUnicodeInfo.GetUnicodeCategory(c);
        return category == UnicodeCategory.NonSpacingMark
            || category == UnicodeCategory.SpacingCombiningMark
            || category == UnicodeCategory.EnclosingMark;
    }

    /// <summary>Upper-cases the first letter-or-digit of <paramref name="word"/> when it is a letter.</summary>
    private static string Capitalise(string word)
    {
        // A word starting with a surrogate-pair letter (outside the BMP) is not capitalised: rare, out of scope.
        for (var i = 0; i < word.Length; i++)
        {
            if (!char.IsLetterOrDigit(word[i]))
            {
                continue;
            }

            return char.IsLetter(word[i])
                ? word.Substring(0, i) + char.ToUpperInvariant(word[i]) + word.Substring(i + 1)
                : word;
        }

        return word;
    }

    private string ToTitle(string baseName)
    {
        var lowered = baseName.ToLowerInvariant();
        var result = new StringBuilder(lowered.Length);
        var first = true;
        var i = 0;
        while (i < lowered.Length)
        {
            if (!IsWordCharacter(lowered[i]))
            {
                result.Append(lowered[i]);
                i++;
                continue;
            }

            var start = i;
            while (i < lowered.Length && IsWordCharacter(lowered[i]))
            {
                i++;
            }

            result.Append(TitleWord(lowered.Substring(start, i - start), first));
            first = false;
        }

        return result.ToString();
    }

    /// <summary>
    /// A listed word is written as listed; the first word of the name is still capitalised unless its listed form
    /// already contains an upper-case letter (<c>iPhone</c>, <c>USA</c>).
    /// </summary>
    private string TitleWord(string lowerWord, bool isFirstWord)
    {
        if (!exceptionsByWord.TryGetValue(lowerWord, out var listed))
        {
            return Capitalise(lowerWord);
        }

        return isFirstWord && !listed.Any(char.IsUpper) ? Capitalise(listed) : listed;
    }

    private static string ToSentence(string baseName)
    {
        var chars = baseName.ToLowerInvariant().ToCharArray();
        var startsSentence = true;
        for (var i = 0; i < chars.Length; i++)
        {
            if (startsSentence && char.IsLetterOrDigit(chars[i]))
            {
                chars[i] = char.ToUpperInvariant(chars[i]);
                startsSentence = false;
            }
            else if (chars[i] == '.' && i + 1 < chars.Length && char.IsWhiteSpace(chars[i + 1]))
            {
                startsSentence = true;
            }
        }

        return new string(chars);
    }
}
