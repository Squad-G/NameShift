using System;
using System.Linq;
using NameShift.Core;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Rules;

public sealed class MoveCopyPanelModelTests
{
    [Fact]
    public void Stage_IsMoveCopy()
    {
        Assert.Equal(RuleStage.MoveCopy, new MoveCopyPanelModel().Stage);
    }

    [Fact]
    public void Defaults()
    {
        var model = new MoveCopyPanelModel();

        Assert.Equal(MoveCopyMode.Move, model.Mode);
        Assert.Equal(MoveCopySource.Start, model.Source);
        Assert.Equal("", model.CountText);
        Assert.Equal(MoveCopyTarget.End, model.Target);
        Assert.Equal("", model.PositionText);
        Assert.Equal("", model.Separator);
        Assert.True(model.IsDefault);
        Assert.False(model.Enabled);
        Assert.Null(model.BuildRule());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void EachSetting_MakesItNonDefault_AndResetRestores(int setting)
    {
        var model = new MoveCopyPanelModel();

        switch (setting)
        {
            case 0:
                model.Mode = MoveCopyMode.Copy;
                break;
            case 1:
                model.Source = MoveCopySource.End;
                break;
            case 2:
                model.CountText = "2";
                break;
            case 3:
                model.Target = MoveCopyTarget.Start;
                break;
            case 4:
                model.PositionText = "3";
                break;
            default:
                model.Separator = "_";
                break;
        }

        Assert.False(model.IsDefault);

        model.Enabled = true;
        model.Reset();

        Assert.True(model.IsDefault);
        Assert.False(model.Enabled);
    }

    [Fact]
    public void Lists_InEnumOrder_WithTexts()
    {
        Assert.Equal(new[] { MoveCopyMode.Move, MoveCopyMode.Copy }, MoveCopyPanelModel.Modes.ToArray());
        Assert.Equal(new[] { MoveCopySource.Start, MoveCopySource.End }, MoveCopyPanelModel.Sources.ToArray());
        Assert.Equal(
            new[] { MoveCopyTarget.Start, MoveCopyTarget.End, MoveCopyTarget.Position },
            MoveCopyPanelModel.Targets.ToArray());

        for (int i = 0; i < MoveCopyPanelModel.Modes.Count; i++)
        {
            Assert.Equal(i, MoveCopyPanelModel.IndexOf(MoveCopyPanelModel.Modes[i]));
        }

        for (int i = 0; i < MoveCopyPanelModel.Sources.Count; i++)
        {
            Assert.Equal(i, MoveCopyPanelModel.IndexOf(MoveCopyPanelModel.Sources[i]));
        }

        for (int i = 0; i < MoveCopyPanelModel.Targets.Count; i++)
        {
            Assert.Equal(i, MoveCopyPanelModel.IndexOf(MoveCopyPanelModel.Targets[i]));
        }

        Assert.Equal(-1, MoveCopyPanelModel.IndexOf((MoveCopyMode)9));
        Assert.Equal(-1, MoveCopyPanelModel.IndexOf((MoveCopySource)9));
        Assert.Equal(-1, MoveCopyPanelModel.IndexOf((MoveCopyTarget)9));

        string[] modes = MoveCopyPanelModel.Modes.Select(MoveCopyModeText.For).ToArray();
        string[] sources = MoveCopyPanelModel.Sources.Select(MoveCopySourceText.For).ToArray();
        string[] targets = MoveCopyPanelModel.Targets.Select(MoveCopyTargetText.For).ToArray();
        Assert.All(modes.Concat(sources).Concat(targets), t => Assert.False(string.IsNullOrEmpty(t)));
        Assert.Equal(new[] { "Move", "Copy" }, modes);
        Assert.Equal(new[] { "Start of the name", "End of the name" }, sources);
        Assert.Equal(new[] { "Start of the name", "End of the name", "A position" }, targets);
    }

    [Fact]
    public void UndefinedEnum_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MoveCopyPanelModel { Mode = (MoveCopyMode)9 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MoveCopyPanelModel { Source = (MoveCopySource)9 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MoveCopyPanelModel { Target = (MoveCopyTarget)9 });
        Assert.Throws<ArgumentOutOfRangeException>(() => MoveCopyModeText.For((MoveCopyMode)9));
        Assert.Throws<ArgumentOutOfRangeException>(() => MoveCopySourceText.For((MoveCopySource)9));
        Assert.Throws<ArgumentOutOfRangeException>(() => MoveCopyTargetText.For((MoveCopyTarget)9));
    }

    [Theory]
    [InlineData("2", 2, false)]
    [InlineData("", null, false)]
    [InlineData("  ", null, false)]
    [InlineData("0", null, true)]
    [InlineData("-2", null, true)]
    [InlineData("abc", null, true)]
    [InlineData("1.5", null, true)]
    public void Count_Parse_AndInvalid(string text, int? count, bool invalid)
    {
        var model = new MoveCopyPanelModel { CountText = text };

        Assert.Equal(count, model.Count);
        Assert.Equal(invalid, model.CountInvalid);
    }

    [Theory]
    [InlineData(MoveCopyTarget.Start, false)]
    [InlineData(MoveCopyTarget.End, false)]
    [InlineData(MoveCopyTarget.Position, true)]
    public void Position_AppliesOnlyForPositionTarget(MoveCopyTarget target, bool applies)
    {
        Assert.Equal(applies, new MoveCopyPanelModel { Target = target }.PositionApplies);
    }

    [Theory]
    [InlineData(MoveCopyTarget.Position, "", true)]
    [InlineData(MoveCopyTarget.Position, "0", true)]
    [InlineData(MoveCopyTarget.Position, "abc", true)]
    [InlineData(MoveCopyTarget.Position, "3", false)]
    [InlineData(MoveCopyTarget.End, "abc", false)]
    [InlineData(MoveCopyTarget.End, "", false)]
    public void PositionInvalid(MoveCopyTarget target, string text, bool invalid)
    {
        Assert.Equal(invalid, new MoveCopyPanelModel { Target = target, PositionText = text }.PositionInvalid);
    }

    [Fact]
    public void Strings_NumberInvalid_Wording()
    {
        Assert.Equal(
            "Type a whole number from 1. Until then nothing is moved or copied.",
            Strings.MoveCopyNumberInvalid);
    }

    [Fact]
    public void BuildRule_OneSettingAtATime()
    {
        var baseRule = Assert.IsType<MoveCopyRule>(new MoveCopyPanelModel { CountText = "2" }.BuildRule());
        Assert.Equal(MoveCopyMode.Move, baseRule.Mode);
        Assert.Equal(MoveCopySource.Start, baseRule.Source);
        Assert.Equal(2, baseRule.Count);
        Assert.Equal(MoveCopyTarget.End, baseRule.Target);
        Assert.Null(baseRule.Position);
        Assert.Equal("", baseRule.Separator);
        Assert.Equal(RuleStage.MoveCopy, baseRule.Stage);

        Assert.Equal(
            MoveCopyMode.Copy,
            Assert.IsType<MoveCopyRule>(new MoveCopyPanelModel { CountText = "2", Mode = MoveCopyMode.Copy }.BuildRule()).Mode);
        Assert.Equal(
            MoveCopySource.End,
            Assert.IsType<MoveCopyRule>(new MoveCopyPanelModel { CountText = "2", Source = MoveCopySource.End }.BuildRule()).Source);
        Assert.Equal(
            3,
            Assert.IsType<MoveCopyRule>(new MoveCopyPanelModel { CountText = "3" }.BuildRule()).Count);
        Assert.Equal(
            MoveCopyTarget.Start,
            Assert.IsType<MoveCopyRule>(new MoveCopyPanelModel { CountText = "2", Target = MoveCopyTarget.Start }.BuildRule()).Target);

        var position = Assert.IsType<MoveCopyRule>(new MoveCopyPanelModel
        {
            CountText = "2", Target = MoveCopyTarget.Position, PositionText = "2",
        }.BuildRule());
        Assert.Equal(MoveCopyTarget.Position, position.Target);
        Assert.Equal(2, position.Position);

        Assert.Equal(
            " _ ",
            Assert.IsType<MoveCopyRule>(new MoveCopyPanelModel { CountText = "2", Separator = " _ " }.BuildRule()).Separator);
    }

    [Theory]
    [InlineData("", MoveCopyTarget.End, "")]
    [InlineData("0", MoveCopyTarget.End, "")]
    [InlineData("abc", MoveCopyTarget.Start, "")]
    [InlineData("2", MoveCopyTarget.Position, "")]
    [InlineData("2", MoveCopyTarget.Position, "0")]
    [InlineData("2", MoveCopyTarget.Position, "abc")]
    [InlineData("", MoveCopyTarget.Position, "3")]
    public void BuildRule_NullCases_NeverThrow(string count, MoveCopyTarget target, string position)
    {
        var model = new MoveCopyPanelModel { CountText = count, Target = target, PositionText = position };
        IRenameRule? rule = null;

        var exception = Record.Exception(() => rule = model.BuildRule());

        Assert.Null(exception);
        Assert.Null(rule);
    }

    [Fact]
    public void Examples_ThroughThePipeline()
    {
        var move = new MoveCopyPanelModel { Enabled = true, CountText = "4", Separator = "_" };
        var moveHost = new RulePanelHostModel(new IRulePanelModel[] { move });
        Assert.Equal(
            "report_2024.txt",
            new RenamePipeline(moveHost.Rules).Run(new[] { new RenameItem("2024report.txt", @"C:\f") })[0].NewName);

        var position = new MoveCopyPanelModel
        {
            Enabled = true, CountText = "2", Target = MoveCopyTarget.Position, PositionText = "3",
        };
        var positionHost = new RulePanelHostModel(new IRulePanelModel[] { position });
        Assert.Equal(
            "cdabef.txt",
            new RenamePipeline(positionHost.Rules).Run(new[] { new RenameItem("abcdef.txt", @"C:\f") })[0].NewName);
    }

    [Fact]
    public void PositionIgnored_WhenTargetIsNotPosition()
    {
        var model = new MoveCopyPanelModel { CountText = "2", PositionText = "3", Target = MoveCopyTarget.End };

        var rule = Assert.IsType<MoveCopyRule>(model.BuildRule());

        Assert.Null(rule.Position);
        Assert.Equal("3", model.PositionText);
        Assert.False(model.PositionApplies);
    }

    [Fact]
    public void Reset_RaisesChangedOnce()
    {
        var model = new MoveCopyPanelModel
        {
            Enabled = true, Mode = MoveCopyMode.Copy, Source = MoveCopySource.End, CountText = "2",
            Target = MoveCopyTarget.Position, PositionText = "3", Separator = "_",
        };
        int raised = 0;
        model.Changed += (_, _) => raised++;

        model.Reset();

        Assert.Equal(1, raised);
        Assert.True(model.IsDefault);
    }
}
