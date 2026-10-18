using NameShift.Core;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Rules;

public sealed class ReplacePanelModelTests
{
    [Fact]
    public void BuildRule_MapsEveryOption()
    {
        var model = new ReplacePanelModel { Find = "a", With = "b", MatchCase = true, IncludeExtension = true };

        var rule = Assert.IsType<ReplaceRule>(model.BuildRule());

        Assert.Equal("a", rule.Find);
        Assert.Equal("b", rule.With);
        Assert.True(rule.MatchCase);
        Assert.True(rule.IncludeExtension);
        Assert.Equal(RuleStage.Replace, rule.Stage);
    }

    [Fact]
    public void BuildRule_EmptyFind_Null()
    {
        Assert.Null(new ReplacePanelModel { With = "b" }.BuildRule());
    }

    [Theory]
    [InlineData("Find")]
    [InlineData("With")]
    [InlineData("MatchCase")]
    [InlineData("IncludeExtension")]
    public void EachSetting_MakesItNonDefault_AndResetRestores(string setting)
    {
        var model = new ReplacePanelModel { Enabled = true };
        Assert.True(new ReplacePanelModel().IsDefault);

        switch (setting)
        {
            case "Find": model.Find = "x"; break;
            case "With": model.With = "x"; break;
            case "MatchCase": model.MatchCase = true; break;
            default: model.IncludeExtension = true; break;
        }

        Assert.False(model.IsDefault);

        model.Reset();

        Assert.True(model.IsDefault);
        Assert.False(model.Enabled);
        Assert.Equal("", model.Find);
        Assert.Equal("", model.With);
        Assert.False(model.MatchCase);
        Assert.False(model.IncludeExtension);
    }

    [Fact]
    public void NullStrings_BecomeEmpty()
    {
        var model = new ReplacePanelModel { Find = "a", With = "b" };

        model.Find = null!;
        model.With = null!;

        Assert.Equal("", model.Find);
        Assert.Equal("", model.With);
    }
}
