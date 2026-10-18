using NameShift.Core;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Rules;

public sealed class AddPanelModelTests
{
    [Fact]
    public void BuildRule_MapsEveryOption()
    {
        var model = new AddPanelModel
        {
            Prefix = "p", Insert = "i", InsertPositionText = "2", InsertFromEnd = true, Suffix = "s", WordSpace = true,
        };

        var rule = Assert.IsType<AddRule>(model.BuildRule());

        Assert.Equal("p", rule.Prefix);
        Assert.Equal("i", rule.Insert);
        Assert.Equal(2, rule.InsertPosition);
        Assert.True(rule.InsertFromEnd);
        Assert.Equal("s", rule.Suffix);
        Assert.True(rule.WordSpace);
        Assert.Equal(RuleStage.Add, rule.Stage);
    }

    [Fact]
    public void BuildRule_Default_Null()
    {
        Assert.Null(new AddPanelModel().BuildRule());
    }

    [Theory]
    [InlineData("Prefix")]
    [InlineData("Insert")]
    [InlineData("Position")]
    [InlineData("FromEnd")]
    [InlineData("Suffix")]
    [InlineData("WordSpace")]
    public void EachSetting_MakesItNonDefault_AndResetRestores(string setting)
    {
        var model = new AddPanelModel { Enabled = true };
        Assert.True(new AddPanelModel().IsDefault);

        switch (setting)
        {
            case "Prefix": model.Prefix = "x"; break;
            case "Insert": model.Insert = "x"; break;
            case "Position": model.InsertPositionText = "3"; break;
            case "FromEnd": model.InsertFromEnd = true; break;
            case "Suffix": model.Suffix = "x"; break;
            default: model.WordSpace = true; break;
        }

        Assert.False(model.IsDefault);

        model.Reset();

        Assert.True(model.IsDefault);
        Assert.False(model.Enabled);
        Assert.Equal("", model.Prefix);
        Assert.Equal("", model.Insert);
        Assert.Equal("", model.InsertPositionText);
        Assert.False(model.InsertFromEnd);
        Assert.Equal("", model.Suffix);
        Assert.False(model.WordSpace);
    }

    [Theory]
    [InlineData("x", "", true)]
    [InlineData("x", "abc", true)]
    [InlineData("x", "0", true)]
    [InlineData("x", "-1", true)]
    [InlineData("", "abc", true)]
    [InlineData("", "", false)]
    [InlineData("x", "2", false)]
    [InlineData("x", " 3 ", false)]
    [InlineData("", "2", false)]
    public void InsertPositionInvalid(string insert, string position, bool expected)
    {
        var model = new AddPanelModel { Insert = insert, InsertPositionText = position };

        Assert.Equal(expected, model.InsertPositionInvalid);
    }

    [Fact]
    public void InvalidPosition_InsertIgnored_OtherPartsKept()
    {
        var model = new AddPanelModel { Prefix = "p", Insert = "x", InsertPositionText = "abc" };

        var rule = Assert.IsType<AddRule>(model.BuildRule());

        Assert.Null(rule.InsertPosition);
        Assert.Equal("p", rule.Prefix);
        var item = new RenameItem("name.txt", @"C:\f");
        Assert.Equal("pname.txt", new RenamePipeline(new[] { rule }).Run(new[] { item })[0].NewName);
    }

    [Fact]
    public void BuildRule_NonNumericPosition_OnlyInsert_Null()
    {
        Assert.Null(new AddPanelModel { Insert = "x", InsertPositionText = "abc" }.BuildRule());
    }

    [Fact]
    public void PositionLabel_FollowsFromEnd()
    {
        var model = new AddPanelModel();
        Assert.Equal(Strings.AddPosition, model.InsertPositionLabel);

        int raised = 0;
        model.Changed += (_, _) => raised++;
        model.InsertFromEnd = true;

        Assert.Equal(1, raised);
        Assert.Equal(Strings.AddPositionFromEnd, model.InsertPositionLabel);
        Assert.Contains(
            "left after",
            RulePanelText.PlainTitle(Strings.AddPositionFromEnd),
            System.StringComparison.OrdinalIgnoreCase);
    }
}
