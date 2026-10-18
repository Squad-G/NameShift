using System;
using System.Linq;
using NameShift.Core;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Rules;

public sealed class CasePanelModelTests
{
    [Fact]
    public void Modes_AreInStoryOrder()
    {
        Assert.Equal(
            new[] { CaseMode.Same, CaseMode.Lower, CaseMode.Upper, CaseMode.Title, CaseMode.Sentence },
            CasePanelModel.Modes.ToArray());

        string[] texts = CasePanelModel.Modes.Select(CaseModeText.For).ToArray();
        Assert.All(texts, t => Assert.False(string.IsNullOrEmpty(t)));
        Assert.Equal(texts.Length, texts.Distinct().Count());
    }

    [Fact]
    public void BuildRule_MapsModeAndExceptions()
    {
        var model = new CasePanelModel { Mode = CaseMode.Title, ExceptionsText = " of, the ,,USA " };

        var rule = Assert.IsType<CaseRule>(model.BuildRule());

        Assert.Equal(CaseMode.Title, rule.Mode);
        Assert.Equal(new[] { "of", "the", "USA" }, rule.Exceptions.ToArray());
        Assert.Equal(RuleStage.Case, rule.Stage);
    }

    [Fact]
    public void BuildRule_Same_Null()
    {
        Assert.Null(new CasePanelModel { Mode = CaseMode.Same, ExceptionsText = "x" }.BuildRule());
    }

    [Theory]
    [InlineData(CaseMode.Same, false)]
    [InlineData(CaseMode.Lower, false)]
    [InlineData(CaseMode.Upper, false)]
    [InlineData(CaseMode.Title, true)]
    [InlineData(CaseMode.Sentence, false)]
    public void ExceptionsApply_OnlyInTitle(CaseMode mode, bool expected)
    {
        Assert.Equal(expected, new CasePanelModel { Mode = mode }.ExceptionsApply);
    }

    [Fact]
    public void Mode_Undefined_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CasePanelModel { Mode = (CaseMode)99 });
        Assert.Throws<ArgumentOutOfRangeException>(() => CaseModeText.For((CaseMode)99));
    }

    [Fact]
    public void ExceptionsText_IsKeptWhenTheModeChanges()
    {
        var model = new CasePanelModel { Mode = CaseMode.Title, ExceptionsText = "of" };

        model.Mode = CaseMode.Lower;

        Assert.Equal("of", model.ExceptionsText);
    }

    [Theory]
    [InlineData("Mode")]
    [InlineData("Exceptions")]
    public void EachSetting_MakesItNonDefault_AndResetRestores(string setting)
    {
        var model = new CasePanelModel { Enabled = true };
        Assert.True(new CasePanelModel().IsDefault);

        if (setting == "Mode")
        {
            model.Mode = CaseMode.Upper;
        }
        else
        {
            model.ExceptionsText = "x";
        }

        Assert.False(model.IsDefault);

        model.Reset();

        Assert.True(model.IsDefault);
        Assert.False(model.Enabled);
        Assert.Equal(CaseMode.Same, model.Mode);
        Assert.Equal("", model.ExceptionsText);
    }
}
