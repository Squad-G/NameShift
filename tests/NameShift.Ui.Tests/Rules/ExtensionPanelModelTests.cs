using System;
using System.Linq;
using NameShift.Core;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Rules;

public sealed class ExtensionPanelModelTests
{
    [Fact]
    public void Modes_AreInStoryOrder_WithTexts()
    {
        Assert.Equal(
            new[] { ExtensionMode.Same, ExtensionMode.Lower, ExtensionMode.Upper, ExtensionMode.Fixed, ExtensionMode.Extra, ExtensionMode.Remove },
            ExtensionPanelModel.Modes.ToArray());

        string[] texts = ExtensionPanelModel.Modes.Select(ExtensionModeText.For).ToArray();
        Assert.All(texts, t => Assert.False(string.IsNullOrEmpty(t)));
        Assert.Equal(texts.Length, texts.Distinct().Count());
        Assert.Equal(Strings.ExtensionModeSame, ExtensionModeText.For(ExtensionMode.Same));
        Assert.Equal(Strings.ExtensionModeLower, ExtensionModeText.For(ExtensionMode.Lower));
        Assert.Equal(Strings.ExtensionModeUpper, ExtensionModeText.For(ExtensionMode.Upper));
        Assert.Equal(Strings.ExtensionModeFixed, ExtensionModeText.For(ExtensionMode.Fixed));
        Assert.Equal(Strings.ExtensionModeExtra, ExtensionModeText.For(ExtensionMode.Extra));
        Assert.Equal(Strings.ExtensionModeRemove, ExtensionModeText.For(ExtensionMode.Remove));
    }

    [Fact]
    public void IndexOf_RoundTripsEveryMode_AndRejectsUnlisted()
    {
        for (int i = 0; i < ExtensionPanelModel.Modes.Count; i++)
        {
            Assert.Equal(i, ExtensionPanelModel.IndexOf(ExtensionPanelModel.Modes[i]));
        }

        Assert.Equal(-1, ExtensionPanelModel.IndexOf((ExtensionMode)99));
    }

    [Theory]
    [InlineData(ExtensionMode.Lower)]
    [InlineData(ExtensionMode.Upper)]
    [InlineData(ExtensionMode.Fixed)]
    [InlineData(ExtensionMode.Extra)]
    [InlineData(ExtensionMode.Remove)]
    public void BuildRule_MapsModeAndText(ExtensionMode mode)
    {
        var model = new ExtensionPanelModel { Mode = mode, Text = "trip" };

        var rule = Assert.IsType<ExtensionRule>(model.BuildRule());

        Assert.Equal(mode, rule.Mode);
        Assert.Equal("trip", rule.Value);
        Assert.Equal(RuleStage.Extension, rule.Stage);
    }

    [Fact]
    public void BuildRule_Same_Null()
    {
        Assert.Null(new ExtensionPanelModel { Mode = ExtensionMode.Same, Text = "x" }.BuildRule());
    }

    [Fact]
    public void Defaults_SameAndEmpty_IsDefault()
    {
        var model = new ExtensionPanelModel();

        Assert.Equal(ExtensionMode.Same, model.Mode);
        Assert.Equal("", model.Text);
        Assert.True(model.IsDefault);

        model.Text = "x";

        Assert.False(model.IsDefault);
    }

    [Theory]
    [InlineData(ExtensionMode.Same, false)]
    [InlineData(ExtensionMode.Lower, false)]
    [InlineData(ExtensionMode.Upper, false)]
    [InlineData(ExtensionMode.Fixed, true)]
    [InlineData(ExtensionMode.Extra, true)]
    [InlineData(ExtensionMode.Remove, false)]
    public void TextApplies_OnlyInFixedAndExtra(ExtensionMode mode, bool expected)
    {
        Assert.Equal(expected, new ExtensionPanelModel { Mode = mode }.TextApplies);
    }

    [Fact]
    public void Text_IsKeptWhenTheModeChanges()
    {
        var model = new ExtensionPanelModel { Mode = ExtensionMode.Fixed, Text = "trip" };

        model.Mode = ExtensionMode.Remove;

        Assert.Equal("trip", model.Text);
    }

    [Theory]
    [InlineData("Mode")]
    [InlineData("Text")]
    public void EachSetting_MakesItNonDefault_AndResetRestores(string setting)
    {
        var model = new ExtensionPanelModel { Enabled = true };
        Assert.True(new ExtensionPanelModel().IsDefault);

        if (setting == "Mode")
        {
            model.Mode = ExtensionMode.Upper;
        }
        else
        {
            model.Text = "x";
        }

        Assert.False(model.IsDefault);

        model.Reset();

        Assert.True(model.IsDefault);
        Assert.False(model.Enabled);
        Assert.Equal(ExtensionMode.Same, model.Mode);
        Assert.Equal("", model.Text);
    }

    [Fact]
    public void Reset_RaisesChangedOnce()
    {
        var model = new ExtensionPanelModel { Enabled = true, Mode = ExtensionMode.Fixed, Text = "x" };
        int raised = 0;
        model.Changed += (_, _) => raised++;

        model.Reset();

        Assert.Equal(1, raised);
    }

    [Fact]
    public void Stage_IsExtension()
    {
        Assert.Equal(RuleStage.Extension, new ExtensionPanelModel().Stage);
    }

    [Fact]
    public void Mode_Undefined_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExtensionPanelModel { Mode = (ExtensionMode)99 });
        Assert.Throws<ArgumentOutOfRangeException>(() => ExtensionModeText.For((ExtensionMode)99));
    }

    [Fact]
    public void NullText_BecomesEmpty()
    {
        var model = new ExtensionPanelModel { Text = "x" };

        model.Text = null!;

        Assert.Equal("", model.Text);
    }

    [Fact]
    public void Strings_ExtensionNote_Wording()
    {
        Assert.Equal("Only renames the file. It does not convert its content.", Strings.ExtensionNote);
        Assert.DoesNotContain("&", Strings.ExtensionNote);
    }
}
