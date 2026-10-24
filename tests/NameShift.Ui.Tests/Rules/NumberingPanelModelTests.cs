using System;
using System.Collections.Generic;
using System.Globalization;
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

    [Fact]
    public void Systems_InEnumOrder_WithTexts()
    {
        Assert.Equal(
            new[] { NumeralSystem.Decimal, NumeralSystem.Hexadecimal, NumeralSystem.Letters, NumeralSystem.Roman },
            NumberingPanelModel.Systems.ToArray());
        for (int i = 0; i < NumberingPanelModel.Systems.Count; i++)
        {
            Assert.Equal(i, NumberingPanelModel.IndexOf(NumberingPanelModel.Systems[i]));
        }

        Assert.Equal(-1, NumberingPanelModel.IndexOf((NumeralSystem)99));
        Assert.Equal(
            new[] { "Numbers (1, 2, 3)", "Hexadecimal (0A, 0B)", "Letters (a, b, c)", "Roman numerals (I, II, III)" },
            NumberingPanelModel.Systems.Select(NumeralSystemText.For).ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => NumeralSystemText.For((NumeralSystem)99));
    }

    [Fact]
    public void Cases_CapitalFirst_WithTexts()
    {
        Assert.Equal(new[] { true, false }, NumberingPanelModel.Cases.ToArray());
        Assert.Equal(0, NumberingPanelModel.IndexOfCase(true));
        Assert.Equal(1, NumberingPanelModel.IndexOfCase(false));
        Assert.Equal("Capital letters", NumberingCaseText.For(true));
        Assert.Equal("Small letters", NumberingCaseText.For(false));
    }

    [Fact]
    public void NewDefaults()
    {
        var model = new NumberingPanelModel();

        Assert.Equal(NumeralSystem.Decimal, model.Numerals);
        Assert.True(model.UpperCase);
        Assert.True(model.PadApplies);
        Assert.False(model.CaseApplies);
    }

    [Fact]
    public void Numerals_Undefined_Throws()
    {
        var model = new NumberingPanelModel { Numerals = NumeralSystem.Hexadecimal, StartText = "FF" };
        int raised = 0;
        model.Changed += (_, _) => raised++;

        Assert.Throws<ArgumentOutOfRangeException>(() => model.Numerals = (NumeralSystem)9);

        Assert.Equal(NumeralSystem.Hexadecimal, model.Numerals);
        Assert.Equal("FF", model.StartText);
        Assert.True(model.UpperCase);
        Assert.Equal(0, raised);
    }

    [Theory]
    [InlineData(NumeralSystem.Hexadecimal, true)]
    [InlineData(NumeralSystem.Roman, true)]
    [InlineData(NumeralSystem.Letters, false)]
    public void Numerals_PicksTheUsualCase(NumeralSystem numerals, bool expected)
    {
        var model = new NumberingPanelModel { UpperCase = false };

        model.Numerals = numerals;

        Assert.Equal(expected, model.UpperCase);
    }

    [Fact]
    public void Numerals_LettersThenDecimal_UpperCase()
    {
        var model = new NumberingPanelModel { Numerals = NumeralSystem.Letters };
        Assert.False(model.UpperCase);

        model.Numerals = NumeralSystem.Decimal;

        Assert.True(model.UpperCase);
    }

    [Fact]
    public void UpperCase_KeptUntilTheSystemChanges()
    {
        var model = new NumberingPanelModel { Numerals = NumeralSystem.Hexadecimal };

        model.UpperCase = false;
        Assert.False(model.UpperCase);

        model.Numerals = NumeralSystem.Roman;
        Assert.True(model.UpperCase);
    }

    [Fact]
    public void Numerals_Change_RaisesChangedOnce()
    {
        var model = new NumberingPanelModel { StartText = "10", UpperCase = false };
        int raised = 0;
        bool? upperInHandler = null;
        string? startInHandler = null;
        model.Changed += (_, _) =>
        {
            raised++;
            upperInHandler = model.UpperCase;
            startInHandler = model.StartText;
        };

        model.Numerals = NumeralSystem.Hexadecimal;

        Assert.Equal(1, raised);
        Assert.True(upperInHandler);
        Assert.Equal("A", startInHandler);
    }

    [Theory]
    [InlineData(NumeralSystem.Decimal, "10", NumeralSystem.Hexadecimal, "A")]
    [InlineData(NumeralSystem.Decimal, "10", NumeralSystem.Letters, "j")]
    [InlineData(NumeralSystem.Decimal, "10", NumeralSystem.Roman, "X")]
    [InlineData(NumeralSystem.Decimal, "255", NumeralSystem.Hexadecimal, "FF")]
    [InlineData(NumeralSystem.Hexadecimal, "A", NumeralSystem.Decimal, "10")]
    [InlineData(NumeralSystem.Letters, "ab", NumeralSystem.Decimal, "28")]
    [InlineData(NumeralSystem.Roman, "IV", NumeralSystem.Hexadecimal, "4")]
    [InlineData(NumeralSystem.Decimal, "+5", NumeralSystem.Hexadecimal, "5")]
    public void Numerals_RewritesTheStart(NumeralSystem from, string text, NumeralSystem to, string expected)
    {
        var model = new NumberingPanelModel { Numerals = from, StartText = text };

        model.Numerals = to;

        Assert.Equal(expected, model.StartText);
    }

    [Fact]
    public void Numerals_RoundTrip_255()
    {
        var model = new NumberingPanelModel { StartText = "255" };

        model.Numerals = NumeralSystem.Hexadecimal;
        Assert.Equal("FF", model.StartText);

        model.Numerals = NumeralSystem.Decimal;
        Assert.Equal("255", model.StartText);
    }

    [Theory]
    [InlineData("0", NumeralSystem.Letters)]
    [InlineData("4000", NumeralSystem.Roman)]
    [InlineData("abc", NumeralSystem.Hexadecimal)]
    public void Numerals_KeepsUnwritableStart(string text, NumeralSystem to)
    {
        var model = new NumberingPanelModel { StartText = text };

        model.Numerals = to;

        Assert.Equal(text, model.StartText);
        Assert.Equal(to, model.Numerals);
    }

    [Fact]
    public void Numerals_SameSystem_NothingRaised()
    {
        var model = new NumberingPanelModel();
        int raised = 0;
        model.Changed += (_, _) => raised++;

        model.Numerals = NumeralSystem.Decimal;

        Assert.Equal(0, raised);
        Assert.Equal("1", model.StartText);
    }

    [Theory]
    [InlineData("+5", 5)]
    [InlineData(" 7 ", 7)]
    [InlineData("0", 0)]
    [InlineData("-1", null)]
    [InlineData("1.5", null)]
    [InlineData("", null)]
    public void Start_Decimal_AsBefore(string text, int? expected)
    {
        Assert.Equal(expected, new NumberingPanelModel { StartText = text }.Start);
    }

    [Theory]
    [InlineData("FF", 255)]
    [InlineData("ff", 255)]
    [InlineData("0x10", null)]
    [InlineData("G", null)]
    [InlineData("80000000", null)]
    [InlineData("", null)]
    public void Start_Hexadecimal(string text, int? expected)
    {
        Assert.Equal(expected, new NumberingPanelModel { Numerals = NumeralSystem.Hexadecimal, StartText = text }.Start);
    }

    [Theory]
    [InlineData("a", 1)]
    [InlineData("ab", 28)]
    [InlineData("0", null)]
    [InlineData("a1", null)]
    [InlineData("", null)]
    public void Start_Letters(string text, int? expected)
    {
        Assert.Equal(expected, new NumberingPanelModel { Numerals = NumeralSystem.Letters, StartText = text }.Start);
    }

    [Theory]
    [InlineData("IV", 4)]
    [InlineData("iv", 4)]
    [InlineData("IIII", null)]
    [InlineData("0", null)]
    [InlineData("MMMM", null)]
    [InlineData("", null)]
    public void Start_Roman(string text, int? expected)
    {
        Assert.Equal(expected, new NumberingPanelModel { Numerals = NumeralSystem.Roman, StartText = text }.Start);
    }

    [Fact]
    public void StartError_BySystem()
    {
        Assert.Equal(
            "Type a hexadecimal number from 0 to 7FFFFFFF (digits 0-9 and A-F). Until then numbering is skipped.",
            Strings.NumberingStartInvalidHexadecimal);
        Assert.Equal(
            "Type letters from a (a, b, ... z, aa, ab ...). Until then numbering is skipped.",
            Strings.NumberingStartInvalidLetters);
        Assert.Equal(
            "Type a Roman numeral from I to MMMCMXCIX (1 to 3999). Until then numbering is skipped.",
            Strings.NumberingStartInvalidRoman);
        Assert.Equal("Type a whole number from 0. Until then numbering is skipped.", Strings.NumberingStartInvalid);

        Assert.Equal(Strings.NumberingStartInvalid, BadStart(NumeralSystem.Decimal).StartError);
        Assert.Equal(Strings.NumberingStartInvalidHexadecimal, BadStart(NumeralSystem.Hexadecimal).StartError);
        Assert.Equal(Strings.NumberingStartInvalidLetters, BadStart(NumeralSystem.Letters).StartError);
        Assert.Equal(Strings.NumberingStartInvalidRoman, BadStart(NumeralSystem.Roman).StartError);
    }

    [Fact]
    public void StartError_EmptyWhenUsable()
    {
        Assert.Equal("", new NumberingPanelModel { StartText = "5" }.StartError);
        Assert.Equal("", new NumberingPanelModel { Numerals = NumeralSystem.Hexadecimal, StartText = "FF" }.StartError);
        Assert.Equal("", new NumberingPanelModel { Numerals = NumeralSystem.Letters, StartText = "b" }.StartError);
        Assert.Equal("", new NumberingPanelModel { Numerals = NumeralSystem.Roman, StartText = "IV" }.StartError);
    }

    [Theory]
    [InlineData(NumeralSystem.Decimal, true, false)]
    [InlineData(NumeralSystem.Hexadecimal, true, true)]
    [InlineData(NumeralSystem.Letters, false, true)]
    [InlineData(NumeralSystem.Roman, false, true)]
    public void PadAndCaseApply_BySystem(NumeralSystem numerals, bool padApplies, bool caseApplies)
    {
        var model = new NumberingPanelModel { Numerals = numerals };

        Assert.Equal(padApplies, model.PadApplies);
        Assert.Equal(caseApplies, model.CaseApplies);
    }

    [Theory]
    [InlineData(NumeralSystem.Letters, "a")]
    [InlineData(NumeralSystem.Roman, "I")]
    public void Pad_IgnoredForLettersAndRoman(NumeralSystem numerals, string start)
    {
        var model = new NumberingPanelModel { Numerals = numerals, StartText = start, PadText = "abc" };

        Assert.Equal("", model.PadError);
        var rule = Assert.IsType<NumberingRule>(model.BuildRule());
        Assert.Equal(0, rule.Pad);
        Assert.Equal("abc", model.PadText);

        var hex = new NumberingPanelModel { Numerals = NumeralSystem.Hexadecimal, StartText = "A", PadText = "abc" };

        Assert.NotEqual("", hex.PadError);
        Assert.Null(hex.BuildRule());
    }

    [Fact]
    public void BuildRule_MapsNumeralsAndCase()
    {
        var model = new NumberingPanelModel { Numerals = NumeralSystem.Roman, UpperCase = false, StartText = "IV" };

        var rule = Assert.IsType<NumberingRule>(model.BuildRule());

        Assert.Equal(NumeralSystem.Roman, rule.Numerals);
        Assert.False(rule.UpperCase);
        Assert.Equal(4, rule.Start);
    }

    [Theory]
    [InlineData(NumeralSystem.Hexadecimal, true, "A", "2", NumberingMode.Prefix, "_", "0A_a.txt", "0B_b.txt")]
    [InlineData(NumeralSystem.Hexadecimal, false, "A", "2", NumberingMode.Prefix, "_", "0a_a.txt", "0b_b.txt")]
    [InlineData(NumeralSystem.Letters, true, "a", "0", NumberingMode.Prefix, "_", "A_a.txt", "B_b.txt")]
    [InlineData(NumeralSystem.Letters, false, "a", "4", NumberingMode.Prefix, "_", "a_a.txt", "b_b.txt")]
    [InlineData(NumeralSystem.Roman, true, "IV", "0", NumberingMode.Prefix, "_", "IV_a.txt", "V_b.txt")]
    [InlineData(NumeralSystem.Roman, false, "IV", "0", NumberingMode.Prefix, "_", "iv_a.txt", "v_b.txt")]
    [InlineData(NumeralSystem.Decimal, true, "1", "0", NumberingMode.Prefix, "_", "1_a.txt", "2_b.txt")]
    public void Pipeline_Examples(
        NumeralSystem numerals, bool upperCase, string start, string pad, NumberingMode mode, string separator,
        string first, string second)
    {
        var model = new NumberingPanelModel
        {
            Enabled = true, Numerals = numerals, UpperCase = upperCase, Mode = mode, StartText = start, PadText = pad,
            Separator = separator,
        };

        Assert.Equal(new[] { first, second }, Run(model, "a.txt", "b.txt").Select(r => r.NewName).ToArray());
    }

    [Fact]
    public void Pipeline_DecimalDefaults_NumberAfterName()
    {
        var model = new NumberingPanelModel { Enabled = true };

        Assert.Equal(new[] { "a_1.txt", "b_2.txt" }, Run(model, "a.txt", "b.txt").Select(r => r.NewName).ToArray());
    }

    [Fact]
    public void Pipeline_LettersSuffix_CarriesPastZ()
    {
        var model = new NumberingPanelModel
        {
            Enabled = true, Numerals = NumeralSystem.Letters, Mode = NumberingMode.Suffix, Separator = "-",
            StartText = "z",
        };

        Assert.Equal(
            new[] { "x-z.txt", "y-aa.txt", "z-ab.txt" },
            Run(model, "x.txt", "y.txt", "z.txt").Select(r => r.NewName).ToArray());
    }

    [Fact]
    public void Pipeline_RomanPastTheLimit_IsAnItemError()
    {
        var model = new NumberingPanelModel
        {
            Enabled = true, Numerals = NumeralSystem.Roman, Mode = NumberingMode.Prefix, StartText = "MMMCMXCIX",
        };

        var results = Run(model, "a.txt", "b.txt");

        Assert.Equal("MMMCMXCIX_a.txt", results[0].NewName);
        Assert.Equal("b.txt", results[1].NewName);
        Assert.Equal(RenameStatus.Error, results[1].Status);
    }

    [Theory]
    [InlineData(NumeralSystem.Roman, "0")]
    [InlineData(NumeralSystem.Hexadecimal, "G")]
    [InlineData(NumeralSystem.Letters, "")]
    public void BuildRule_UnusableStart_NullNoThrow(NumeralSystem numerals, string text)
    {
        var model = new NumberingPanelModel { Numerals = numerals, StartText = text };

        IRenameRule? rule = null;
        Assert.Null(Record.Exception(() => rule = model.BuildRule()));
        Assert.Null(rule);
    }

    [Fact]
    public void BuildRule_NeverThrows_EverySystem()
    {
        string?[] texts = { null, "", "   ", "abc", "0", "-5", "+3", "256", "99999999999999999999", "1.5", "\u0663", "\0" };

        foreach (NumeralSystem numerals in NumberingPanelModel.Systems)
        {
            foreach (string? text in texts)
            {
                foreach (NumberingMode mode in NumberingPanelModel.Modes)
                {
                    var model = new NumberingPanelModel
                    {
                        Numerals = numerals, Mode = mode, StartText = text!, StepText = text!, PadText = text!,
                        Separator = text!, InsertPositionText = text!,
                    };

                    Assert.Null(Record.Exception(() => model.BuildRule()));
                }
            }
        }
    }

    [Fact]
    public void IsDefault_NeedsDecimalAndUpper()
    {
        var hex = new NumberingPanelModel { Numerals = NumeralSystem.Hexadecimal };
        Assert.False(hex.IsDefault);

        Assert.False(new NumberingPanelModel { UpperCase = false }.IsDefault);

        hex.Numerals = NumeralSystem.Decimal;
        Assert.True(hex.IsDefault);
    }

    [Fact]
    public void Reset_RestoresSystemAndCase_ChangedOnce()
    {
        var model = new NumberingPanelModel { Enabled = true, Numerals = NumeralSystem.Roman };
        model.UpperCase = false;
        model.StartText = "IV";
        model.PadText = "3";
        int raised = 0;
        model.Changed += (_, _) => raised++;

        model.Reset();

        Assert.Equal(1, raised);
        Assert.Equal(NumeralSystem.Decimal, model.Numerals);
        Assert.True(model.UpperCase);
        Assert.Equal("1", model.StartText);
        Assert.Equal("0", model.PadText);
        Assert.False(model.Enabled);
        Assert.True(model.IsDefault);
    }

    [Fact]
    public void TurkishCulture_LettersAndRoman()
    {
        CultureInfo saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            var letters = new NumberingPanelModel { StartText = "9" };
            letters.Numerals = NumeralSystem.Letters;
            Assert.Equal("i", letters.StartText);
            Assert.NotEqual("\u0131", letters.StartText);

            var upper = new NumberingPanelModel { Numerals = NumeralSystem.Letters, StartText = "I" };
            Assert.Equal(9, upper.Start);

            var roman = new NumberingPanelModel { Numerals = NumeralSystem.Roman, StartText = "iv" };
            Assert.Equal(4, roman.Start);
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    private static NumberingPanelModel BadStart(NumeralSystem numerals) =>
        new NumberingPanelModel { Numerals = numerals, StartText = "?" };

    private static IReadOnlyList<RenameResult> Run(NumberingPanelModel model, params string[] names)
    {
        var host = new RulePanelHostModel(new IRulePanelModel[] { model });
        return new RenamePipeline(host.Rules).Run(names.Select(n => new RenameItem(n, @"C:\f")).ToArray());
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
