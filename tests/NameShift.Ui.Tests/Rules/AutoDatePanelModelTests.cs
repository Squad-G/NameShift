using System;
using System.Globalization;
using System.Linq;
using NameShift.Core;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Rules;

public sealed class AutoDatePanelModelTests
{
    [Fact]
    public void Stage_IsAutoDate()
    {
        Assert.Equal(RuleStage.AutoDate, new AutoDatePanelModel().Stage);
    }

    [Fact]
    public void Defaults()
    {
        var model = new AutoDatePanelModel();

        Assert.Equal(DateSource.Modified, model.Source);
        Assert.Equal("yyyy-MM-dd", model.Format);
        Assert.Equal(DatePlacement.Prefix, model.Placement);
        Assert.Equal("_", model.Separator);
        Assert.True(model.IsDefault);
        Assert.False(model.Enabled);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EachSetting_MakesItNonDefault_AndResetRestores(int setting)
    {
        var model = new AutoDatePanelModel();

        switch (setting)
        {
            case 0:
                model.Source = DateSource.Created;
                break;
            case 1:
                model.Format = "yyyy";
                break;
            case 2:
                model.Placement = DatePlacement.Suffix;
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
    public void Text_NotTrimmed_NullBecomesEmpty()
    {
        var model = new AutoDatePanelModel { Format = " yyyy ", Separator = " _ " };

        Assert.Equal(" yyyy ", model.Format);
        Assert.Equal(" _ ", model.Separator);

        model.Format = null!;
        model.Separator = null!;

        Assert.Equal("", model.Format);
        Assert.Equal("", model.Separator);
    }

    [Fact]
    public void Lists_InEnumOrder_WithTexts()
    {
        Assert.Equal(new[] { DateSource.Modified, DateSource.Created }, AutoDatePanelModel.Sources.ToArray());
        Assert.Equal(new[] { DatePlacement.Prefix, DatePlacement.Suffix }, AutoDatePanelModel.Placements.ToArray());
        for (int i = 0; i < AutoDatePanelModel.Sources.Count; i++)
        {
            Assert.Equal(i, AutoDatePanelModel.IndexOf(AutoDatePanelModel.Sources[i]));
        }

        for (int i = 0; i < AutoDatePanelModel.Placements.Count; i++)
        {
            Assert.Equal(i, AutoDatePanelModel.IndexOf(AutoDatePanelModel.Placements[i]));
        }

        Assert.Equal(-1, AutoDatePanelModel.IndexOf((DateSource)9));
        Assert.Equal(-1, AutoDatePanelModel.IndexOf((DatePlacement)9));
        Assert.Equal("Modified date", DateSourceText.For(DateSource.Modified));
        Assert.Equal("Created date", DateSourceText.For(DateSource.Created));
        Assert.Equal("Before the name", DatePlacementText.For(DatePlacement.Prefix));
        Assert.Equal("After the name", DatePlacementText.For(DatePlacement.Suffix));
    }

    [Fact]
    public void UndefinedEnum_Throws()
    {
        var model = new AutoDatePanelModel();

        Assert.Throws<ArgumentOutOfRangeException>(() => model.Source = (DateSource)9);
        Assert.Throws<ArgumentOutOfRangeException>(() => model.Placement = (DatePlacement)9);
        Assert.Throws<ArgumentOutOfRangeException>(() => DateSourceText.For((DateSource)9));
        Assert.Throws<ArgumentOutOfRangeException>(() => DatePlacementText.For((DatePlacement)9));
    }

    [Fact]
    public void BuildRule_MapsSource()
    {
        var rule = Assert.IsType<AutoDateRule>(new AutoDatePanelModel { Source = DateSource.Created }.BuildRule());

        Assert.Equal(DateSource.Created, rule.Source);
        Assert.Equal(RuleStage.AutoDate, rule.Stage);
    }

    [Fact]
    public void BuildRule_MapsFormat()
    {
        var rule = Assert.IsType<AutoDateRule>(new AutoDatePanelModel { Format = "yyyyMMdd" }.BuildRule());

        Assert.Equal("yyyyMMdd", rule.Format);
        Assert.Equal(RuleStage.AutoDate, rule.Stage);
    }

    [Fact]
    public void BuildRule_MapsPlacement()
    {
        var rule = Assert.IsType<AutoDateRule>(new AutoDatePanelModel { Placement = DatePlacement.Suffix }.BuildRule());

        Assert.Equal(DatePlacement.Suffix, rule.Placement);
        Assert.Equal(RuleStage.AutoDate, rule.Stage);
    }

    [Fact]
    public void BuildRule_MapsSeparator()
    {
        var rule = Assert.IsType<AutoDateRule>(new AutoDatePanelModel { Separator = " - " }.BuildRule());

        Assert.Equal(" - ", rule.Separator);
        Assert.Equal(RuleStage.AutoDate, rule.Stage);
    }

    [Fact]
    public void BuildRule_EmptyFormat_Null()
    {
        Assert.Null(new AutoDatePanelModel { Format = "" }.BuildRule());
    }

    [Theory]
    [InlineData("'abc")]
    [InlineData("yyyy\\")]
    [InlineData("K")]
    [InlineData("zz")]
    public void BuildRule_InvalidFormat_NullNoThrow(string format)
    {
        var model = new AutoDatePanelModel { Format = format };
        IRenameRule? rule = null;

        var exception = Record.Exception(() => rule = model.BuildRule());

        Assert.Null(exception);
        Assert.Null(rule);
    }

    [Fact]
    public void FormatError_Live()
    {
        var model = new AutoDatePanelModel();
        Assert.Equal("", model.FormatError);

        model.Format = "";
        Assert.Equal("", model.FormatError);

        model.Format = "'abc";
        Assert.NotEqual("", model.FormatError);
        Assert.Contains(AutoDateRule.Validate("'abc")!, model.FormatError);

        model.Format = "yyyy";
        Assert.Equal("", model.FormatError);
    }

    [Fact]
    public void FormatError_UsesTheStringsFrame()
    {
        var parts = Strings.AutoDateFormatInvalid.Split(new[] { "{0}" }, StringSplitOptions.None);
        Assert.Equal(2, parts.Length);

        string error = new AutoDatePanelModel { Format = "z" }.FormatError;

        Assert.StartsWith(parts[0], error, StringComparison.Ordinal);
        Assert.EndsWith(parts[1], error, StringComparison.Ordinal);
        Assert.Contains(AutoDateRule.Validate("z")!, error);
    }

    [Fact]
    public void Strings_FormatInvalid_Wording()
    {
        Assert.Equal("This format is not valid. {0} Until then the date is skipped.", Strings.AutoDateFormatInvalid);
    }

    [Fact]
    public void Format_EachChange_RaisesChangedOnce()
    {
        var model = new AutoDatePanelModel();
        int raised = 0;
        model.Changed += (_, _) => raised++;

        model.Format = "'abc";
        Assert.Equal(1, raised);

        model.Format = "yyyy";
        Assert.Equal(2, raised);

        model.Format = "yyyy";
        Assert.Equal(2, raised);
    }

    [Theory]
    [InlineData("yyyy-MM-dd", "2024-03-09")]
    [InlineData("yyyyMMdd_HHmmss", "20240309_140507")]
    [InlineData("MMMM", "March")]
    [InlineData("d", "9")]
    public void Example_ForFormat(string format, string expected)
    {
        Assert.Equal(expected, new AutoDatePanelModel { Format = format }.Example);
    }

    [Fact]
    public void Example_MMMM_IsInvariant_UnderFrench()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            Assert.Equal("March", new AutoDatePanelModel { Format = "MMMM" }.Example);
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("'abc")]
    [InlineData("K")]
    public void Example_EmptyOrInvalid_IsEmpty(string format)
    {
        var model = new AutoDatePanelModel { Format = format };

        Assert.Equal("", model.Example);
        Assert.Equal("", model.ExampleText);
    }

    [Fact]
    public void ExampleText_UsesTheStringsFrame()
    {
        Assert.Equal("Example: {0}", Strings.AutoDateExample);
        Assert.Equal(
            string.Format(CultureInfo.CurrentCulture, Strings.AutoDateExample, "2024-03-09"),
            new AutoDatePanelModel().ExampleText);
    }

    [Fact]
    public void SampleDate_IsFixed()
    {
        Assert.Equal(new DateTime(2024, 3, 9, 14, 5, 7), AutoDatePanelModel.SampleDate);
    }

    [Fact]
    public void CommonFormats_ListedInOrder()
    {
        Assert.Equal(
            new[]
            {
                "yyyy-MM-dd", "yyyyMMdd", "yyyy-MM-dd_HH-mm-ss", "yyyyMMdd_HHmmss", "dd-MM-yyyy", "yyyy-MM", "yyyy",
            },
            AutoDatePanelModel.CommonFormats.ToArray());
    }

    [Fact]
    public void CommonFormats_AllValid_SafeAndDistinct()
    {
        var forbidden = "<>:\"/\\|?*".ToCharArray().Concat(Enumerable.Range(0, 32).Select(i => (char)i)).ToArray();
        var examples = new System.Collections.Generic.List<string>();

        foreach (string format in AutoDatePanelModel.CommonFormats)
        {
            Assert.Null(AutoDateRule.Validate(format));
            string example = AutoDateRule.FormatDate(AutoDatePanelModel.SampleDate, format);
            Assert.Equal(-1, example.IndexOfAny(forbidden));
            examples.Add(example);
        }

        Assert.Equal(7, AutoDatePanelModel.CommonFormats.Count);
        Assert.Equal(7, examples.Distinct().Count());
    }

    [Fact]
    public void Reset_RaisesChangedOnce_AllDefaults()
    {
        var model = new AutoDatePanelModel
        {
            Enabled = true, Source = DateSource.Created, Format = "yyyy", Placement = DatePlacement.Suffix,
            Separator = "-",
        };
        int raised = 0;
        model.Changed += (_, _) => raised++;

        model.Reset();

        Assert.Equal(1, raised);
        Assert.Equal(DateSource.Modified, model.Source);
        Assert.Equal("yyyy-MM-dd", model.Format);
        Assert.Equal(DatePlacement.Prefix, model.Placement);
        Assert.Equal("_", model.Separator);
        Assert.False(model.Enabled);
    }

    [Fact]
    public void EnabledDefaults_BuildRule_AndIsDefault()
    {
        var model = new AutoDatePanelModel { Enabled = true };

        var rule = Assert.IsType<AutoDateRule>(model.BuildRule());

        Assert.Equal(DateSource.Modified, rule.Source);
        Assert.Equal("yyyy-MM-dd", rule.Format);
        Assert.Equal(DatePlacement.Prefix, rule.Placement);
        Assert.Equal("_", rule.Separator);
        Assert.True(model.IsDefault);
    }

    [Fact]
    public void DateSourceText_Taken()
    {
        Assert.Equal("Photo date", DateSourceText.For(DateSource.Taken));
        Assert.Equal(Strings.AutoDateSourceTaken, DateSourceText.For(DateSource.Taken));
    }

    [Fact]
    public void Sources_StayModifiedAndCreated()
    {
        Assert.Equal(new[] { DateSource.Modified, DateSource.Created }, AutoDatePanelModel.Sources.ToArray());
        Assert.Equal(-1, AutoDatePanelModel.IndexOf(DateSource.Taken));
    }

    [Fact]
    public void Source_Taken_Throws()
    {
        var model = new AutoDatePanelModel();

        Assert.Throws<ArgumentOutOfRangeException>(() => model.Source = DateSource.Taken);
        Assert.Equal(DateSource.Modified, model.Source);
    }

    [Theory]
    [InlineData("Taken")]
    [InlineData("2")]
    public void LoadSettings_Taken_KeepsModified(string saved)
    {
        var model = new AutoDatePanelModel();

        model.LoadSettings(new System.Collections.Generic.Dictionary<string, string> { ["Source"] = saved });

        Assert.Equal(DateSource.Modified, model.Source);
    }
}
