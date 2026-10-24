using System;
using System.Collections.Generic;

namespace NameShift.Ui.Tests.Rules;

/// <summary>David's Alt-key rule: inside one rule panel the title and its labels have distinct letters; panel titles
/// are distinct among themselves. Repeats across panels are allowed.</summary>
internal static class MnemonicCheck
{
    /// <summary>The broken rules, as readable lines ("Replace: C used by &Case and Match the &case..."); empty when
    /// the rule holds. Each panel is its title followed by its labels.</summary>
    public static IReadOnlyList<string> Problems(IReadOnlyList<(string Title, string[] Labels)> panels)
    {
        var problems = new List<string>();
        var titleLetters = new Dictionary<char, string>();
        foreach (var (title, labels) in panels)
        {
            var inPanel = new Dictionary<char, string>();
            foreach (var text in Prepend(title, labels))
            {
                var letter = LetterOf(text);
                if (inPanel.TryGetValue(letter, out var other))
                {
                    problems.Add($"{title}: {letter} used by {other} and {text}");
                }
                else
                {
                    inPanel.Add(letter, text);
                }
            }

            var titleLetter = LetterOf(title);
            if (titleLetters.TryGetValue(titleLetter, out var otherTitle))
            {
                problems.Add($"Panel titles: {titleLetter} used by {otherTitle} and {title}");
            }
            else
            {
                titleLetters.Add(titleLetter, title);
            }
        }

        return problems;
    }

    /// <summary>The upper-cased letter after the single '&' ("&&" is a literal ampersand); throws when there is none.</summary>
    public static char LetterOf(string text)
    {
        for (var i = 0; i < text.Length - 1; i++)
        {
            if (text[i] != '&')
            {
                continue;
            }

            if (text[i + 1] == '&')
            {
                i++;
                continue;
            }

            return char.ToUpperInvariant(text[i + 1]);
        }

        throw new ArgumentException("No mnemonic in: " + text, nameof(text));
    }

    private static IEnumerable<string> Prepend(string first, string[] rest)
    {
        yield return first;
        foreach (var text in rest)
        {
            yield return text;
        }
    }
}
