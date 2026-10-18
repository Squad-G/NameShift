using System;
using System.Collections.Generic;
using System.Linq;
using NameShift.Core;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Rules;

public sealed class NumberingPanelModelTests
{
    [Fact]
    public void Modes_AreInListOrder()
    {
        Assert.Equal(
            new[] { NumberingMode.Prefix, NumberingMode.Suffix, NumberingMode.Insert },
            NumberingPanelModel.Modes.ToArray());

        string[] texts = NumberingPanelModel.Modes.Select(NumberingModeText.For).ToArray();
        Assert.All(texts, t => Assert.False(string.IsNullOrEmpty(t)));
        Assert.Equal(texts.Length, texts.Distinct().Count());
        for (int i = 0; i < NumberingPanelModel.Modes.Count; i++)
        {
            Assert.Equal(i, NumberingPanelModel.IndexOf(NumberingPanelModel.Modes[i]));
        }

        Assert.Equal(-1, NumberingPanelModel.IndexOf((NumberingMode)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => NumberingModeText.For((NumberingMode)99));
    }

    [Fact]
    public void BuildRule_MapsEveryOption()
    {
        var model = new NumberingPanelModel
        {
            Mode = NumberingMode.Insert, StartText = "5", StepText = "2", PadText = "3", Separator = "-",
            InsertPositionText = "2",
        };

        var rule = Assert.IsType<NumberingRule>(model.BuildRule());

        Assert.Equal(NumberingMode.Insert, rule.Mode);
        Assert.Equal(5, rule.Start);
        Assert.Equal(2, rule.Step);
        Assert.Equal(3, rule.Pad);
        Assert.Equal("-", rule.Separator);
        Assert.Equal(2, rule.InsertPosition);
        Assert.Equal(RuleStage.Numbering, rule.Stage);
        Assert.Equal(9, (int)rule.Stage);
    }

    [Theory]
    [InlineData(NumberingMode.Prefix)]
    [InlineData(NumberingMode.Suffix)]
    public void BuildRule_PrefixAndSuffix_IgnorePosition(NumberingMode mode)
    {
        var model = new NumberingPanelModel { Mode = mode, InsertPositionText = "abc" };

        var rule = Assert.IsType<NumberingRule>(model.BuildRule());

        Assert.Equal(mode, rule.Mode);
        Assert.Null(rule.InsertPosition);
        Assert.Equal("", model.InsertPositionError);
    }

    [Fact]
    public void Mode_Unlisted_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new NumberingPanelModel { Mode = (NumberingMode)99 });
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("99999999999")]
    [InlineData("\u0663")]
    public void StartText_Invalid(string text)
    {
        var model = new NumberingPanelModel { StartText = text };

        Assert.Null(model.Start);
        Assert.Equal(Strings.NumberingStartInvalid, model.StartError);
        Assert.Null(model.BuildRule());
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData(" 7 ", 7)]
    [InlineData("+3", 3)]
    public void StartText_Valid(string text, int expected)
    {
        var model = new NumberingPanelModel { StartText = text };

        Assert.Equal(expected, model.Start);
        Assert.Equal("", model.StartError);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.5")]
    [InlineData("99999999999")]
    public void StepText_Invalid(string text)
    {
        var model = new NumberingPanelModel { StepText = text };

        Assert.Null(model.Step);
        Assert.Equal(Strings.NumberingStepInvalid, model.StepError);
        Assert.Null(model.BuildRule());
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("-2", -2)]
    [InlineData("10", 10)]
    public void StepText_Valid(string text, int expected)
    {
        var model = new NumberingPanelModel { StepText = text };

        Assert.Equal(expected, model.Step);
        Assert.Equal("", model.StepError);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("256")]
    [InlineData("99999999999")]
    public void PadText_Invalid(string text)
    {
        var model = new NumberingPanelModel { PadText = text };

        Assert.Null(model.Pad);
        Assert.Contains("255", model.PadError);
        Assert.Null(model.BuildRule());
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("255", 255)]
    [InlineData(" 3 ", 3)]
    public void PadText_Valid(string text, int expected)
    {
        var model = new NumberingPanelModel { PadText = text };

        Assert.Equal(expected, model.Pad);
        Assert.Equal("", model.PadError);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("abc")]
    public void InsertPosition_Invalid_InInsertMode(string text)
    {
        var model = new NumberingPanelModel { Mode = NumberingMode.Insert, InsertPositionText = text };

        Assert.Equal(Strings.NumberingPositionInvalid, model.InsertPositionError);
        Assert.Null(model.BuildRule());
    }

    [Fact]
    public void InsertPositionApplies_FollowsMode()
    {
        foreach (NumberingMode mode in NumberingPanelModel.Modes)
        {
            Assert.Equal(mode == NumberingMode.Insert, new NumberingPanelModel { Mode = mode }.InsertPositionApplies);
        }
    }

    [Fact]
    public void Defaults()
    {
        var model = new NumberingPanelModel();

        Assert.Equal(NumberingMode.Suffix, model.Mode);
        Assert.Equal("1", model.StartText);
        Assert.Equal("1", model.StepText);
        Assert.Equal("0", model.PadText);
        Assert.Equal("_", model.Separator);
        Assert.Equal("", model.InsertPositionText);
        Assert.True(model.IsDefault);
        Assert.False(model.Enabled);
        var rule = Assert.IsType<NumberingRule>(model.BuildRule());
        Assert.Equal(NumberingMode.Suffix, rule.Mode);
        Assert.Equal(1, rule.Start);
        Assert.Equal(1, rule.Step);
        Assert.Equal(0, rule.Pad);
        Assert.Equal("_", rule.Separator);
        Assert.Null(rule.InsertPosition);
        Assert.NotEqual(new NumberingRule().Separator, model.Separator); // the panel's default differs from the engine's
    }

    [Theory]
    [InlineData("Mode")]
    [InlineData("Start")]
    [InlineData("Step")]
    [InlineData("Pad")]
    [InlineData("Separator")]
    [InlineData("Position")]
    public void EachSetting_MakesItNonDefault_AndResetRestores(string setting)
    {
        var model = new NumberingPanelModel { Enabled = true };

        Change(model, setting);

        Assert.False(model.IsDefault);

        model.Reset();

        Assert.True(model.IsDefault);
        Assert.False(model.Enabled);
        Assert.Equal(NumberingMode.Suffix, model.Mode);
        Assert.Equal("1", model.StartText);
        Assert.Equal("1", model.StepText);
        Assert.Equal("0", model.PadText);
        Assert.Equal("_", model.Separator);
        Assert.Equal("", model.InsertPositionText);
    }

    public static IEnumerable<object[]> SettingChanges() => new[]
    {
        Row(m => m.Mode = NumberingMode.Prefix),
        Row(m => m.StartText = "2"),
        Row(m => m.StepText = "2"),
        Row(m => m.PadText = "2"),
        Row(m => m.Separator = "-"),
        Row(m => m.InsertPositionText = "2"),
    };

    [Theory]
    [MemberData(nameof(SettingChanges))]
    public void SettingChange_RaisesChangedOnceAtOnce(NumberingPanelModel model, Action change)
    {
        int raised = 0;
        model.Changed += (_, _) => raised++;

        change();
        Assert.Equal(1, raised);

        change(); // same value again
        Assert.Equal(1, raised);
    }

    [Fact]
    public void NumbersFollowListOrder()
    {
        var model = new NumberingPanelModel { Enabled = true };
        var host = new RulePanelHostModel(new IRulePanelModel[] { model });
        var items = new[]
        {
            new RenameItem("b.txt", @"C:\f"), new RenameItem("a.txt", @"C:\f"), new RenameItem("c.txt", @"C:\f"),
        };

        var results = new RenamePipeline(host.Rules).Run(items);

        Assert.Equal(new[] { "b_1.txt", "a_2.txt", "c_3.txt" }, results.Select(r => r.NewName).ToArray());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("+3")]
    [InlineData("256")]
    [InlineData("99999999999999999999")]
    [InlineData("1.5")]
    [InlineData("\u0663")]
    [InlineData("\0")]
    public void BuildRule_NeverThrows(string? text)
    {
        foreach (NumberingMode mode in NumberingPanelModel.Modes)
        {
            var model = new NumberingPanelModel
            {
                Mode = mode, StartText = text!, StepText = text!, PadText = text!, Separator = text!,
                InsertPositionText = text!,
            };

            Assert.Null(Record.Exception(() => model.BuildRule()));
        }
    }

    [Fact]
    public void BuildRule_NonNumericPosition_Null()
    {
        var model = new NumberingPanelModel { Mode = NumberingMode.Insert, InsertPositionText = "abc" };

        Assert.Null(model.BuildRule());
    }

    private static void Change(NumberingPanelModel model, string setting)
    {
        switch (setting)
        {
            case "Mode":
                model.Mode = NumberingMode.Insert;
                break;
            case "Start":
                model.StartText = "2";
                break;
            case "Step":
                model.StepText = "2";
                break;
            case "Pad":
                model.PadText = "2";
                break;
            case "Separator":
                model.Separator = "-";
                break;
            default:
                model.InsertPositionText = "2";
                break;
        }
    }

    private static object[] Row(Action<NumberingPanelModel> set)
    {
        var model = new NumberingPanelModel();
        return new object[] { model, (Action)(() => set(model)) };
    }
}
