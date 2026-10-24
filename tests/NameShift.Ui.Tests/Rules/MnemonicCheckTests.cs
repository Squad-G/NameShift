using System;
using Xunit;

namespace NameShift.Ui.Tests.Rules;

public sealed class MnemonicCheckTests
{
    [Fact]
    public void SameLetterInTwoPanels_Passes()
    {
        var panels = new (string Title, string[] Labels)[]
        {
            ("&Replace", new[] { "Match the &case" }),
            ("&Case", new[] { "M&ode" }),
        };

        Assert.Empty(MnemonicCheck.Problems(panels));
    }

    [Fact]
    public void RepeatInsideAPanel_Fails()
    {
        var panels = new (string Title, string[] Labels)[] { ("&Replace", new[] { "&Find", "&First" }) };

        Assert.Single(MnemonicCheck.Problems(panels));
    }

    [Fact]
    public void TitleRepeatsALabelOfItsOwnPanel_Fails()
    {
        var panels = new (string Title, string[] Labels)[] { ("&Replace", new[] { "&Rename" }) };

        Assert.Single(MnemonicCheck.Problems(panels));
    }

    [Fact]
    public void TwoPanelsSameTitleLetter_Fails()
    {
        var panels = new (string Title, string[] Labels)[]
        {
            ("&Add", Array.Empty<string>()),
            ("&Append", Array.Empty<string>()),
        };

        Assert.Single(MnemonicCheck.Problems(panels));
    }

    [Fact]
    public void LetterOf_SkipsDoubleAmpersand_AndUpperCases()
    {
        Assert.Equal('J', MnemonicCheck.LetterOf("Tom && &jerry"));
        Assert.Throws<ArgumentException>(() => MnemonicCheck.LetterOf("no mnemonic"));
    }
}
