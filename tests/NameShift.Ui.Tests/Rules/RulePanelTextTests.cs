using NameShift.Core;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Rules;

public sealed class RulePanelTextTests
{
    [Fact]
    public void Heading_ShowsStageNumberAndTitle()
    {
        Assert.Equal("3. &Replace", RulePanelText.Heading(RuleStage.Replace, "&Replace"));
    }

    [Fact]
    public void PlainTitle_DropsMnemonicKeepsDoubleAmpersand()
    {
        Assert.Equal("Replace", RulePanelText.PlainTitle("&Replace"));
        Assert.Equal("Tom & Jerry", RulePanelText.PlainTitle("Tom && Jerry"));
    }

    [Fact]
    public void ResetName_UsesPlainTitle()
    {
        Assert.Equal("Reset Replace", RulePanelText.ResetName("&Replace"));
    }
}