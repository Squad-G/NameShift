using System;
using System.Collections.Generic;
using NameShift.Ui.Tests.Rules;
using Xunit;

namespace NameShift.Ui.Tests;

/// <summary>NS-72 AC 14: the Alt keys of the window's top level texts. The rule panel titles and the menu title must
/// all have different letters, and so must the items of one menu. NS-70 adds the strings of its filter row here.
/// (Letters of labels inside a panel may repeat a title letter of another panel: <see cref="MnemonicCheck"/>.)</summary>
public sealed class WindowMnemonicTests
{
    private static readonly string[] PanelTitles =
    {
        Strings.ReplaceTitle, Strings.CaseTitle, Strings.RemoveTitle, Strings.AddTitle,
        Strings.NumberingTitle, Strings.NameTitle, Strings.ExtensionTitle, Strings.RegexTitle,
        Strings.MoveCopyTitle, Strings.AutoDateTitle, Strings.FolderNameTitle,
    };

    [Fact]
    public void TitleLetters_Distinct()
    {
        var texts = new List<string>(PanelTitles) { Strings.PresetsMenu };

        AssertDistinctLetters(texts);
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
