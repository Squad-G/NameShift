using System;
using System.Collections.Generic;
using NameShift.Ui.Tests.Rules;
using Xunit;

namespace NameShift.Ui.Tests;

/// <summary>NS-72 AC 14 and NS-70 AC 10: the Alt keys of the window's top level texts. The rule panel titles, the menu
/// title and the first line of the filter row (Mask, Files, Folders, Hidden, More) must all have different letters, and
/// so must the items of one menu. The filter row's second line has no Alt key (More opens it and focuses its first box),
/// and the rest of the row never uses B, which NS-78's Sub-folders box takes (it is checked with the titles too).
/// (Letters of labels inside a panel may repeat a title
/// letter of another panel: <see cref="MnemonicCheck"/>.)</summary>
public sealed class WindowMnemonicTests
{
    private static readonly string[] PanelTitles =
    {
        Strings.ReplaceTitle, Strings.CaseTitle, Strings.RemoveTitle, Strings.AddTitle,
        Strings.NumberingTitle, Strings.NameTitle, Strings.ExtensionTitle, Strings.RegexTitle,
        Strings.MoveCopyTitle, Strings.AutoDateTitle, Strings.FolderNameTitle,
    };

    private static readonly string[] FilterRowFirstLine =
    {
        Strings.FilterMask, Strings.FilterFiles, Strings.FilterFolders, Strings.FilterHidden, Strings.FilterMore,
    };

    private static readonly string[] FilterRowSecondLine =
    {
        Strings.FilterRegex, Strings.FilterIgnoreCase, Strings.FilterMinLength, Strings.FilterMaxLength,
    };

    [Fact]
    public void TitleLetters_Distinct()
    {
        var texts = new List<string>(PanelTitles) { Strings.PresetsMenu, Strings.FilterSubfolders };
        texts.AddRange(FilterRowFirstLine);

        AssertDistinctLetters(texts);
    }

    [Fact]
    public void Subfolders_UsesB() =>
        Assert.Equal('B', char.ToUpperInvariant(MnemonicCheck.LetterOf(Strings.FilterSubfolders)));

    [Fact]
    public void FilterMoreMarked_SameLetterAsMore() =>
        Assert.Equal(MnemonicCheck.LetterOf(Strings.FilterMore), MnemonicCheck.LetterOf(Strings.FilterMoreMarked));

    [Fact]
    public void Row_DoesNotUseB()
    {
        var texts = new List<string>(FilterRowFirstLine) { Strings.FilterMoreMarked };
        texts.AddRange(FilterRowSecondLine);
        foreach (string text in texts)
        {
            Assert.DoesNotContain("&b", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void SecondLine_HasNoMnemonic()
    {
        foreach (string text in FilterRowSecondLine)
        {
            Assert.DoesNotContain("&", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PresetItemLetters_Distinct() =>
        AssertDistinctLetters(new[] { Strings.PresetsSave, Strings.PresetsLoad, Strings.PresetsDelete });

    [Fact]
    public void NameDialog_LabelHasALetter() => MnemonicCheck.LetterOf(Strings.PresetNameLabel);

    private static void AssertDistinctLetters(IEnumerable<string> texts)
    {
        var seen = new Dictionary<char, string>();
        foreach (string text in texts)
        {
            char letter = MnemonicCheck.LetterOf(text);
            if (seen.TryGetValue(letter, out string? other))
            {
                throw new Xunit.Sdk.XunitException($"{letter} is the Alt key of both {other} and {text}.");
            }

            seen.Add(letter, text);
        }
    }
}
