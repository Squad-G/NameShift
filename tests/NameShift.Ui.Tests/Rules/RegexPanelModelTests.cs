using System;
using NameShift.Core;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Rules;

public sealed class RegexPanelModelTests
{
    [Fact]
    public void Stage_IsRegEx()
    {
        Assert.Equal(RuleStage.RegEx, new RegexPanelModel().Stage);
    }

    [Fact]
    public void Defaults_EmptyFalse_IsDefault_Unchecked()
    {
        var model = new RegexPanelModel();

        Assert.Equal("", model.Pattern);
        Assert.Equal("", model.Replacement);
        Assert.False(model.IgnoreCase);
        Assert.False(model.IncludeExtension);
        Assert.True(model.IsDefault);
        Assert.False(model.Enabled);
        Assert.Null(model.BuildRule());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EachSetting_MakesItNonDefault_AndResetRestores(int setting)
    {
        var model = new RegexPanelModel();

        switch (setting)
        {
            case 0:
                model.Pattern = "x";
                break;
            case 1:
                model.Replacement = "x";
                break;
            case 2:
                model.IgnoreCase = true;
                break;
            default:
                model.IncludeExtension = true;
                break;
        }

        Assert.False(model.IsDefault);

        model.Enabled = true;
        model.Reset();

        Assert.True(model.IsDefault);
        Assert.False(model.Enabled);
    }

    [Fact]
    public void Text_NotTrimmed_NullBecomesEmpty()
    {
        var model = new RegexPanelModel { Pattern = " a ", Replacement = null! };

        Assert.Equal(" a ", model.Pattern);
        Assert.Equal("", model.Replacement);

        model.Pattern = null!;
        Assert.Equal("", model.Pattern);
    }

    [Fact]
    public void BuildRule_MapsPattern()
    {
        var rule = Assert.IsType<RegexRule>(new RegexPanelModel { Pattern = @"IMG_(\d+)" }.BuildRule());

        Assert.Equal(@"IMG_(\d+)", rule.Pattern);
        Assert.Equal(RuleStage.RegEx, rule.Stage);
    }

    [Fact]
    public void BuildRule_MapsReplacement()
    {
        var rule = Assert.IsType<RegexRule>(new RegexPanelModel { Pattern = "a", Replacement = "Photo $1 " }.BuildRule());

        Assert.Equal("Photo $1 ", rule.Replacement);
    }

    [Fact]
    public void BuildRule_MapsIgnoreCase()
    {
        var on = Assert.IsType<RegexRule>(new RegexPanelModel { Pattern = "a", IgnoreCase = true }.BuildRule());
        var off = Assert.IsType<RegexRule>(new RegexPanelModel { Pattern = "a" }.BuildRule());

        Assert.True(on.IgnoreCase);
        Assert.False(off.IgnoreCase);
    }

    [Fact]
    public void BuildRule_MapsIncludeExtension()
    {
        var on = Assert.IsType<RegexRule>(new RegexPanelModel { Pattern = "a", IncludeExtension = true }.BuildRule());
        var off = Assert.IsType<RegexRule>(new RegexPanelModel { Pattern = "a" }.BuildRule());

        Assert.True(on.IncludeExtension);
        Assert.False(off.IncludeExtension);
    }

    [Fact]
    public void BuildRule_EmptyPattern_Null()
    {
        Assert.Null(new RegexPanelModel { Replacement = "x" }.BuildRule());
    }

    [Theory]
    [InlineData("(")]
    [InlineData("[a-")]
    [InlineData("*a")]
    public void BuildRule_InvalidPattern_NullNoThrow(string pattern)
    {
        var model = new RegexPanelModel { Pattern = pattern, Replacement = "x" };
        IRenameRule? rule = null;

        var exception = Record.Exception(() => rule = model.BuildRule());

        Assert.Null(exception);
        Assert.Null(rule);
    }

    [Fact]
    public void PatternError_Live()
    {
        var model = new RegexPanelModel();
        Assert.Equal("", model.PatternError);

        model.Pattern = "(";
        Assert.NotEqual("", model.PatternError);
        Assert.Contains(RegexRule.Validate("(")!, model.PatternError);

        model.Pattern = "(a)";
        Assert.Equal("", model.PatternError);
    }

    [Fact]
    public void PatternError_UsesTheStringsFrame()
    {
        string[] frame = Strings.RegexPatternInvalid.Split(new[] { "{0}" }, StringSplitOptions.None);
        Assert.Equal(2, frame.Length);

        string error = new RegexPanelModel { Pattern = "(" }.PatternError;

        Assert.StartsWith(frame[0], error);
        Assert.EndsWith(frame[1], error);
        Assert.Contains(RegexRule.Validate("(")!, error);
    }

    [Fact]
    public void Pattern_EachAssignment_RaisesChangedOnce()
    {
        var model = new RegexPanelModel();
        int raised = 0;
        model.Changed += (_, _) => raised++;

        model.Pattern = "(";
        Assert.Equal(1, raised);

        model.Pattern = "(a)";
        Assert.Equal(2, raised);

        model.Pattern = "(a)";
        Assert.Equal(2, raised);
    }

    [Fact]
    public void Reset_RaisesChangedOnce()
    {
        var model = new RegexPanelModel
        {
            Enabled = true, Pattern = "a", Replacement = "b", IgnoreCase = true, IncludeExtension = true,
        };
        int raised = 0;
        model.Changed += (_, _) => raised++;

        model.Reset();

        Assert.Equal(1, raised);
        Assert.True(model.IsDefault);
    }

    [Fact]
    public void EnabledDefaults_NullRule_PreviewUnchanged()
    {
        var model = new RegexPanelModel { Enabled = true };
        var host = new RulePanelHostModel(new IRulePanelModel[] { model });

        Assert.Empty(host.Rules);
        var results = new RenamePipeline(host.Rules).Run(new[] { new RenameItem("a.txt", @"C:\f") });
        Assert.Equal(RenameStatus.Unchanged, results[0].Status);
    }
}
