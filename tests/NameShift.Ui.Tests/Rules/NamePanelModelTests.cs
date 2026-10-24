using System;
using System.Linq;
using NameShift.Core;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Rules;

public sealed class NamePanelModelTests
{
    [Fact]
    public void Modes_AreInStoryOrder_WithTexts()
    {
        Assert.Equal(
            new[] { NameMode.Keep, NameMode.Fixed, NameMode.Remove, NameMode.Reverse },
            NamePanelModel.Modes.ToArray());

        string[] texts = NamePanelModel.Modes.Select(NameModeText.For).ToArray();
        Assert.All(texts, t => Assert.False(string.IsNullOrEmpty(t)));
        Assert.Equal(texts.Length, texts.Distinct().Count());
    }

    [Theory]
    [InlineData(NameMode.Fixed)]
    [InlineData(NameMode.Remove)]
    [InlineData(NameMode.Reverse)]
    public void BuildRule_MapsModeAndText(NameMode mode)
    {
        var model = new NamePanelModel { Mode = mode, Text = "trip" };

        var rule = Assert.IsType<NameRule>(model.BuildRule());

        Assert.Equal(mode, rule.Mode);
        Assert.Equal("trip", rule.Value);
        Assert.Equal(RuleStage.Name, rule.Stage);
    }

    [Fact]
    public void BuildRule_Keep_Null()
    {
        Assert.Null(new NamePanelModel { Mode = NameMode.Keep, Text = "x" }.BuildRule());
    }

    [Fact]
    public void Defaults_KeepAndEmpty_IsDefault()
    {
        var model = new NamePanelModel();

        Assert.Equal(NameMode.Keep, model.Mode);
        Assert.Equal("", model.Text);
        Assert.True(model.IsDefault);

        model.Text = "x";

        Assert.False(model.IsDefault);
    }

    [Theory]
    [InlineData(NameMode.Keep, false)]
    [InlineData(NameMode.Fixed, true)]
    [InlineData(NameMode.Remove, false)]
    [InlineData(NameMode.Reverse, false)]
    public void TextApplies_OnlyInFixed(NameMode mode, bool expected)
    {
        Assert.Equal(expected, new NamePanelModel { Mode = mode }.TextApplies);
    }

    [Fact]
    public void Text_IsKeptWhenTheModeChanges()
    {
        var model = new NamePanelModel { Mode = NameMode.Fixed, Text = "trip" };

        model.Mode = NameMode.Remove;

        Assert.Equal("trip", model.Text);
    }

    [Theory]
    [InlineData("Mode")]
    [InlineData("Text")]
    public void EachSetting_MakesItNonDefault_AndResetRestores(string setting)
    {
        var model = new NamePanelModel { Enabled = true };
        Assert.True(new NamePanelModel().IsDefault);

        if (setting == "Mode")
        {
            model.Mode = NameMode.Reverse;
        }
        else
        {
            model.Text = "x";
        }

        Assert.False(model.IsDefault);

        model.Reset();

        Assert.True(model.IsDefault);
        Assert.False(model.Enabled);
        Assert.Equal(NameMode.Keep, model.Mode);
        Assert.Equal("", model.Text);
    }

    [Fact]
    public void Reset_RaisesChangedOnce()
    {
        var model = new NamePanelModel { Enabled = true, Mode = NameMode.Fixed, Text = "x" };
        int raised = 0;
        model.Changed += (_, _) => raised++;

        model.Reset();

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Stage_IsName()
    {
        Assert.Equal(RuleStage.Name, new NamePanelModel().Stage);
    }

    [Fact]
    public void Mode_Undefined_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NamePanelModel { Mode = (NameMode)99 });
        Assert.Throws<ArgumentOutOfRangeException>(() => NameModeText.For((NameMode)99));
    }

    [Fact]
    public void NullText_BecomesEmpty()
    {
        var model = new NamePanelModel { Text = "x" };

        model.Text = null!;

        Assert.Equal("", model.Text);
    }
}
