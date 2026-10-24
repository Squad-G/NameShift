using System;
using System.Linq;
using NameShift.Core;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Rules;

public sealed class FolderNamePanelModelTests
{
    [Fact]
    public void Stage_IsFolderName()
    {
        Assert.Equal(RuleStage.FolderName, new FolderNamePanelModel().Stage);
    }

    [Fact]
    public void Defaults()
    {
        var model = new FolderNamePanelModel();

        Assert.Equal(FolderNamePlacement.Prefix, model.Placement);
        Assert.Equal("1", model.LevelsText);
        Assert.Equal("_", model.Separator);
        Assert.True(model.IsDefault);
        Assert.False(model.Enabled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EachSetting_MakesItNonDefault_AndResetRestores(int setting)
    {
        var model = new FolderNamePanelModel();

        switch (setting)
        {
            case 0:
                model.Placement = FolderNamePlacement.Suffix;
                break;
            case 1:
                model.LevelsText = "2";
                break;
            default:
                model.Separator = "-";
                break;
        }

        Assert.False(model.IsDefault);

        model.Enabled = true;
        model.Reset();

        Assert.True(model.IsDefault);
        Assert.False(model.Enabled);
    }

    [Fact]
    public void LevelsText_AsTyped_NotDefault()
    {
        var model = new FolderNamePanelModel { LevelsText = " 1" };

        Assert.Equal(" 1", model.LevelsText);
        Assert.False(model.IsDefault);
        Assert.Equal(1, model.Levels);
    }

    [Fact]
    public void Lists_InEnumOrder_WithTexts()
    {
        Assert.Equal(
            new[] { FolderNamePlacement.Prefix, FolderNamePlacement.Suffix },
            FolderNamePanelModel.Placements.ToArray());
        for (int i = 0; i < FolderNamePanelModel.Placements.Count; i++)
        {
            Assert.Equal(i, FolderNamePanelModel.IndexOf(FolderNamePanelModel.Placements[i]));
        }

        Assert.Equal(-1, FolderNamePanelModel.IndexOf((FolderNamePlacement)9));
        Assert.Equal("Before the name", FolderNamePlacementText.For(FolderNamePlacement.Prefix));
        Assert.Equal("After the name", FolderNamePlacementText.For(FolderNamePlacement.Suffix));
    }

    [Fact]
    public void UndefinedPlacement_Throws()
    {
        var model = new FolderNamePanelModel();

        Assert.Throws<ArgumentOutOfRangeException>(() => model.Placement = (FolderNamePlacement)9);
        Assert.Throws<ArgumentOutOfRangeException>(() => FolderNamePlacementText.For((FolderNamePlacement)9));
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("3", 3)]
    [InlineData("0", null)]
    [InlineData("-1", null)]
    [InlineData("abc", null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    public void Levels_Parse_AndInvalid(string text, int? expected)
    {
        var model = new FolderNamePanelModel { LevelsText = text };

        Assert.Equal(expected, model.Levels);
        Assert.Equal(expected == null, model.LevelsInvalid);
    }

    [Fact]
    public void Levels_TooLarge_IsMaxValue()
    {
        var model = new FolderNamePanelModel { Enabled = true, LevelsText = "99999999999999999999" };

        Assert.Equal(int.MaxValue, model.Levels);
        Assert.False(model.LevelsInvalid);

        var host = new RulePanelHostModel(new IRulePanelModel[] { model });
        var results = new RenamePipeline(host.Rules).Run(new[] { new RenameItem("a.jpg", @"C:\Photos\Paris") });
        Assert.Equal("Photos_Paris_a.jpg", results[0].NewName);
    }

    [Fact]
    public void Strings_LevelsInvalid_Wording()
    {
        Assert.Equal(
            "Type a whole number from 1. Until then the folder name is skipped.",
            Strings.FolderNameLevelsInvalid);
    }

    [Fact]
    public void BuildRule_MapsPlacement()
    {
        var rule = Assert.IsType<FolderNameRule>(
            new FolderNamePanelModel { Placement = FolderNamePlacement.Suffix }.BuildRule());

        Assert.Equal(FolderNamePlacement.Suffix, rule.Placement);
        Assert.Equal(RuleStage.FolderName, rule.Stage);
    }

    [Fact]
    public void BuildRule_MapsLevels()
    {
        var rule = Assert.IsType<FolderNameRule>(new FolderNamePanelModel { LevelsText = "2" }.BuildRule());

        Assert.Equal(2, rule.Levels);
        Assert.Equal(RuleStage.FolderName, rule.Stage);
    }

    [Fact]
    public void BuildRule_MapsSeparator()
    {
        var rule = Assert.IsType<FolderNameRule>(new FolderNamePanelModel { Separator = " - " }.BuildRule());

        Assert.Equal(" - ", rule.Separator);
        Assert.Equal(RuleStage.FolderName, rule.Stage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("abc")]
    public void BuildRule_InvalidLevels_NullNoThrow(string text)
    {
        var model = new FolderNamePanelModel { LevelsText = text };
        IRenameRule? rule = null;

        var exception = Record.Exception(() => rule = model.BuildRule());

        Assert.Null(exception);
        Assert.Null(rule);
    }

    [Fact]
    public void Reset_RaisesChangedOnce_AllDefaults()
    {
        var model = new FolderNamePanelModel
        {
            Enabled = true, Placement = FolderNamePlacement.Suffix, LevelsText = "2", Separator = "-",
        };
        int raised = 0;
        model.Changed += (_, _) => raised++;

        model.Reset();

        Assert.Equal(1, raised);
        Assert.Equal(FolderNamePlacement.Prefix, model.Placement);
        Assert.Equal("1", model.LevelsText);
        Assert.Equal("_", model.Separator);
        Assert.False(model.Enabled);
    }

    [Fact]
    public void EnabledDefaults_BuildRule()
    {
        var rule = Assert.IsType<FolderNameRule>(new FolderNamePanelModel { Enabled = true }.BuildRule());

        Assert.Equal(FolderNamePlacement.Prefix, rule.Placement);
        Assert.Equal(1, rule.Levels);
        Assert.Equal("_", rule.Separator);
    }
}
