using System;
using System.Collections.Generic;
using System.Linq;
using NameShift.Core;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Rules;

public sealed class RemovePanelModelTests
{
    private static string RunOne(RemovePanelModel model, string name) =>
        new RenamePipeline([model.BuildRule()!]).Run([new RenameItem(name, @"C:\f")])[0].NewName;

    private static Type[] PartTypes(RemovePanelModel model) =>
        Assert.IsType<RemoveGroupRule>(model.BuildRule()).Parts.Select(p => p.GetType()).ToArray();

    private static T OnlyPart<T>(RemovePanelModel model)
        where T : class, IRenameRule
    {
        var group = Assert.IsType<RemoveGroupRule>(model.BuildRule());
        Assert.Single(group.Parts);
        return Assert.IsType<T>(group.Parts[0]);
    }

    private static RemovePanelModel WithPosition(string first = "", string last = "", string from = "", string to = "") =>
        new() { FirstText = first, LastText = last, FromText = from, ToText = to };

    public static IEnumerable<object[]> Settings() => new[]
    {
        Setting("FirstText", m => m.FirstText = "2", m => m.FirstText),
        Setting("LastText", m => m.LastText = "2", m => m.LastText),
        Setting("FromText", m => m.FromText = "2", m => m.FromText),
        Setting("ToText", m => m.ToText = "2", m => m.ToText),
        Setting("Characters", m => m.Characters = "x", m => m.Characters),
        Setting("Text", m => m.Text = "x", m => m.Text),
        Setting("WholeWord", m => m.WholeWord = true, m => m.WholeWord),
        Setting("MatchCase", m => m.MatchCase = true, m => m.MatchCase),
        Setting("CropBefore", m => m.CropBefore = "x", m => m.CropBefore),
        Setting("CropAfter", m => m.CropAfter = "x", m => m.CropAfter),
        Setting("KeepMarker", m => m.KeepMarker = true, m => m.KeepMarker),
        Setting("Digits", m => m.Digits = true, m => m.Digits),
        Setting("Symbols", m => m.Symbols = true, m => m.Symbols),
        Setting("Accents", m => m.Accents = true, m => m.Accents),
        Setting("HighCharacters", m => m.HighCharacters = true, m => m.HighCharacters),
        Setting("Trim", m => m.Trim = true, m => m.Trim),
        Setting("LeadingDots", m => m.LeadingDots = true, m => m.LeadingDots),
        Setting("DoubleSpaces", m => m.DoubleSpaces = true, m => m.DoubleSpaces),
    };

    private static object[] Setting(string name, Action<RemovePanelModel> set, Func<RemovePanelModel, object> get) =>
        [name, set, get];

    [Fact]
    public void Defaults_EmptyAndFalse_IsDefault_Unchecked()
    {
        var model = new RemovePanelModel();

        Assert.All(
            new[]
            {
                model.FirstText, model.LastText, model.FromText, model.ToText, model.Characters, model.Text,
                model.CropBefore, model.CropAfter,
            },
            s => Assert.Equal("", s));
        Assert.All(
            new[]
            {
                model.WholeWord, model.MatchCase, model.KeepMarker, model.Digits, model.Symbols, model.Accents,
                model.HighCharacters, model.Trim, model.LeadingDots, model.DoubleSpaces,
            },
            Assert.False);
        Assert.True(model.IsDefault);
        Assert.False(model.Enabled);
        Assert.Null(model.BuildRule());
    }

    [Theory]
    [MemberData(nameof(Settings))]
    public void EachSetting_MakesItNonDefault_AndResetRestores(
        string name, Action<RemovePanelModel> set, Func<RemovePanelModel, object> get)
    {
        var model = new RemovePanelModel();
        object defaultValue = get(model);

        set(model);

        Assert.False(model.IsDefault, name);
        Assert.NotEqual(defaultValue, get(model));

        model.Enabled = true;
        model.Reset();

        Assert.True(model.IsDefault);
        Assert.False(model.Enabled);
        Assert.Equal(defaultValue, get(model));
    }

    [Fact]
    public void Reset_RaisesChangedOnce()
    {
        var model = new RemovePanelModel
        {
            Enabled = true, FirstText = "1", Characters = "x", Digits = true, Trim = true, KeepMarker = true,
        };
        var raised = 0;
        model.Changed += (_, _) => raised++;

        model.Reset();

        Assert.Equal(1, raised);
    }

    [Fact]
    public void NullStrings_BecomeEmpty()
    {
        var model = new RemovePanelModel
        {
            FirstText = null!, LastText = null!, FromText = null!, ToText = null!, Characters = null!, Text = null!,
            CropBefore = null!, CropAfter = null!,
        };

        Assert.Equal("", model.FirstText);
        Assert.Equal("", model.LastText);
        Assert.Equal("", model.FromText);
        Assert.Equal("", model.ToText);
        Assert.Equal("", model.Characters);
        Assert.Equal("", model.Text);
        Assert.Equal("", model.CropBefore);
        Assert.Equal("", model.CropAfter);
    }

    [Fact]
    public void BuildRule_FirstAndLast()
    {
        Assert.Equal(2, OnlyPart<RemovePositionRule>(new RemovePanelModel { FirstText = "2" }).First);
        var last = OnlyPart<RemovePositionRule>(new RemovePanelModel { LastText = "3" });
        Assert.Equal(3, last.Last);
        Assert.Null(last.First);
    }

    [Fact]
    public void BuildRule_Range()
    {
        var rule = OnlyPart<RemovePositionRule>(new RemovePanelModel { FromText = "2", ToText = "4" });

        Assert.Equal(2, rule.From);
        Assert.Equal(4, rule.To);
    }

    [Theory]
    [InlineData("Characters", "x")]
    [InlineData("Text", "ab")]
    [InlineData("CropBefore", "-")]
    [InlineData("CropAfter", "-")]
    public void BuildRule_TextSettings_ReachTheTextRule(string setting, string value)
    {
        var model = new RemovePanelModel();
        switch (setting)
        {
            case "Characters": model.Characters = value; break;
            case "Text": model.Text = value; break;
            case "CropBefore": model.CropBefore = value; break;
            default: model.CropAfter = value; break;
        }

        var rule = OnlyPart<RemoveTextRule>(model);

        Assert.Equal(setting == "Characters" ? value : "", rule.Characters);
        Assert.Equal(setting == "Text" ? value : "", rule.Text);
        Assert.Equal(setting == "CropBefore" ? value : "", rule.CropBefore);
        Assert.Equal(setting == "CropAfter" ? value : "", rule.CropAfter);
        Assert.False(rule.WholeWord);
        Assert.False(rule.MatchCase);
        Assert.False(rule.KeepMarker);
    }

    [Theory]
    [InlineData("Digits")]
    [InlineData("Symbols")]
    [InlineData("Accents")]
    [InlineData("HighCharacters")]
    public void BuildRule_ClassFlags_ReachTheClassesRule(string setting)
    {
        var model = new RemovePanelModel();
        switch (setting)
        {
            case "Digits": model.Digits = true; break;
            case "Symbols": model.Symbols = true; break;
            case "Accents": model.Accents = true; break;
            default: model.HighCharacters = true; break;
        }

        var rule = OnlyPart<RemoveClassesRule>(model);

        Assert.Equal(setting == "Digits", rule.Digits);
        Assert.Equal(setting == "Symbols", rule.Symbols);
        Assert.Equal(setting == "Accents", rule.Accents);
        Assert.Equal(setting == "HighCharacters", rule.HighCharacters);
    }

    [Theory]
    [InlineData("Trim")]
    [InlineData("LeadingDots")]
    [InlineData("DoubleSpaces")]
    public void BuildRule_TidyFlags_ReachTheTidyRule(string setting)
    {
        var model = new RemovePanelModel();
        switch (setting)
        {
            case "Trim": model.Trim = true; break;
            case "LeadingDots": model.LeadingDots = true; break;
            default: model.DoubleSpaces = true; break;
        }

        var rule = OnlyPart<RemoveTidyRule>(model);

        Assert.Equal(setting == "Trim", rule.Trim);
        Assert.Equal(setting == "LeadingDots", rule.LeadingDots);
        Assert.Equal(setting == "DoubleSpaces", rule.DoubleSpaces);
    }

    [Theory]
    [InlineData("WholeWord")]
    [InlineData("MatchCase")]
    [InlineData("KeepMarker")]
    public void BuildRule_FlagsNeedText(string flag)
    {
        var model = new RemovePanelModel();
        SetFlag(model, flag);

        Assert.Null(model.BuildRule());
        Assert.False(model.IsDefault);

        if (flag == "KeepMarker")
        {
            model.CropBefore = "-";
        }
        else
        {
            model.Text = "a";
        }

        var rule = OnlyPart<RemoveTextRule>(model);

        Assert.Equal(flag == "WholeWord", rule.WholeWord);
        Assert.Equal(flag == "MatchCase", rule.MatchCase);
        Assert.Equal(flag == "KeepMarker", rule.KeepMarker);
    }

    private static void SetFlag(RemovePanelModel model, string flag)
    {
        switch (flag)
        {
            case "WholeWord": model.WholeWord = true; break;
            case "MatchCase": model.MatchCase = true; break;
            default: model.KeepMarker = true; break;
        }
    }

    [Fact]
    public void BuildRule_AllTogether_PartsInOrder()
    {
        var model = new RemovePanelModel
        {
            FirstText = "1", Characters = "x", Digits = true, Trim = true,
        };

        var rule = Assert.IsType<RemoveGroupRule>(model.BuildRule());

        Assert.Equal(RuleStage.Remove, rule.Stage);
        Assert.Equal(
            new[] { typeof(RemovePositionRule), typeof(RemoveTextRule), typeof(RemoveClassesRule), typeof(RemoveTidyRule) },
            PartTypes(model));
    }

    [Fact]
    public void BuildRule_SkipsDisabledParts() =>
        Assert.Equal(
            new[] { typeof(RemoveClassesRule), typeof(RemoveTidyRule) },
            PartTypes(new RemovePanelModel { Digits = true, Trim = true }));

    [Fact]
    public void Order_PositionBeforeClasses() =>
        Assert.Equal("ab.txt", RunOne(new RemovePanelModel { FirstText = "1", Digits = true }, "12ab.txt"));

    [Fact]
    public void Order_TidyAfterText() =>
        Assert.Equal("a.txt", RunOne(new RemovePanelModel { Characters = "x", Trim = true }, " x a.txt"));

    [Fact]
    public void Order_PositionClassesTidy() =>
        Assert.Equal(
            "Photo.jpg",
            RunOne(new RemovePanelModel { FirstText = "4", Digits = true, Trim = true }, "IMG_ Photo 1.jpg"));

    [Theory]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("abc")]
    [InlineData("1.5")]
    public void FirstOrLast_Unusable_MarkedAndSkipped(string text)
    {
        var first = new RemovePanelModel { FirstText = text, Digits = true };
        var last = new RemovePanelModel { LastText = text, Digits = true };

        Assert.True(first.FirstInvalid);
        Assert.False(first.LastInvalid);
        Assert.True(last.LastInvalid);
        Assert.False(last.FirstInvalid);
        Assert.Equal(new[] { typeof(RemoveClassesRule) }, PartTypes(first));
        Assert.Equal(new[] { typeof(RemoveClassesRule) }, PartTypes(last));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("12")]
    [InlineData(" 3 ")]
    public void FirstOrLast_Usable_NotMarked(string text)
    {
        var model = new RemovePanelModel { FirstText = text, LastText = text };

        Assert.False(model.FirstInvalid);
        Assert.False(model.LastInvalid);
        Assert.NotNull(model.First);
        Assert.NotNull(model.Last);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FirstOrLast_Blank_NotMarked(string text)
    {
        var model = new RemovePanelModel { FirstText = text, LastText = text };

        Assert.False(model.FirstInvalid);
        Assert.False(model.LastInvalid);
    }

    [Theory]
    [InlineData("abc", "5", true)]
    [InlineData("5", "abc", true)]
    [InlineData("3", "", true)]
    [InlineData("", "3", true)]
    [InlineData("5", "2", true)]
    [InlineData("2", "5", false)]
    [InlineData("3", "3", false)]
    [InlineData("", "", false)]
    [InlineData("  ", "", false)]
    public void Range_Validity(string from, string to, bool invalid)
    {
        var model = WithPosition(from: from, to: to);
        model.Digits = true;

        Assert.Equal(invalid, model.FromInvalid);
        Assert.Equal(invalid, model.ToInvalid);
        Assert.Equal(invalid, model.RangeInvalid);
        var parts = Assert.IsType<RemoveGroupRule>(model.BuildRule()).Parts;
        var position = parts.OfType<RemovePositionRule>().SingleOrDefault();
        if (invalid)
        {
            Assert.True(position == null || (position.From == null && position.To == null));
        }
        else if (from.Trim().Length > 0)
        {
            Assert.NotNull(position);
            Assert.Equal(int.Parse(from), position!.From);
            Assert.Equal(int.Parse(to), position.To);
        }
    }

    [Fact]
    public void Range_Invalid_OtherPartsStillApply()
    {
        var model = WithPosition(first: "1", from: "5", to: "2");

        var rule = OnlyPart<RemovePositionRule>(model);

        Assert.Equal(1, rule.First);
        Assert.Null(rule.From);
        Assert.Null(rule.To);
        Assert.True(model.RangeInvalid);
    }

    [Fact]
    public void Characters_PassedAsTyped()
    {
        var model = new RemovePanelModel { Characters = " " };

        Assert.Equal(" ", OnlyPart<RemoveTextRule>(model).Characters);
        Assert.Equal("ab.txt", RunOne(model, "a b.txt"));
    }

    [Fact]
    public void Text_PassedAsTyped() =>
        Assert.Equal(" x ", OnlyPart<RemoveTextRule>(new RemovePanelModel { Text = " x " }).Text);

    [Fact]
    public void EmptyText_NoPart() =>
        Assert.Equal(new[] { typeof(RemoveTidyRule) }, PartTypes(new RemovePanelModel { Characters = "", Text = "", Trim = true }));

    [Theory]
    [InlineData("", false, false, false)]
    [InlineData("Text", true, true, false)]
    [InlineData("Characters", false, true, false)]
    [InlineData("CropBefore", false, true, true)]
    [InlineData("CropAfter", false, true, true)]
    public void Applies_FollowTheTexts(string typed, bool wholeWord, bool matchCase, bool keepMarker)
    {
        var model = new RemovePanelModel();
        switch (typed)
        {
            case "Text": model.Text = " "; break;
            case "Characters": model.Characters = "x"; break;
            case "CropBefore": model.CropBefore = "x"; break;
            case "CropAfter": model.CropAfter = "x"; break;
        }

        Assert.Equal(wholeWord, model.WholeWordApplies);
        Assert.Equal(matchCase, model.MatchCaseApplies);
        Assert.Equal(keepMarker, model.KeepMarkerApplies);
    }

    [Fact]
    public void Applies_ChangeKeepsTheFlag()
    {
        var model = new RemovePanelModel { Text = "a", WholeWord = true };
        Assert.True(model.WholeWordApplies);

        model.Text = "";

        Assert.False(model.WholeWordApplies);
        Assert.True(model.WholeWord);
    }

    [Fact]
    public void Stage_IsRemove() => Assert.Equal(RuleStage.Remove, new RemovePanelModel().Stage);

    [Fact]
    public void EnabledWithDefaults_NullRule_Unchanged()
    {
        var model = new RemovePanelModel { Enabled = true };
        var host = new RulePanelHostModel([model]);

        Assert.Null(model.BuildRule());
        Assert.Empty(host.Rules);
        Assert.Equal(
            RenameStatus.Unchanged,
            new RenamePipeline(host.Rules).Run([new RenameItem("a.txt", @"C:\f")])[0].Status);
    }
}
