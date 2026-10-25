using NameShift.Ui.Files;
using Xunit;

namespace NameShift.Ui.Tests.Files;

/// <summary>NS-70 AC 2 and 11: the More toggle's mark and description, and the text boxes' accessible names.</summary>
public sealed class FilterRowTextTests
{
    [Fact]
    public void AllDefaults_NoMark()
    {
        var filters = new FilterPanelModel();

        Assert.False(FilterRowText.HasHiddenValues(filters));
        Assert.Equal(Strings.FilterMore, FilterRowText.MoreText(filters, lineShown: false));
        Assert.Equal("", FilterRowText.MoreDescription(filters, lineShown: false));
    }

    [Theory]
    [InlineData("pattern")]
    [InlineData("ignoreCase")]
    [InlineData("min")]
    [InlineData("max")]
    public void EachHiddenValue_Marks(string value)
    {
        var filters = new FilterPanelModel();
        switch (value)
        {
            case "pattern":
                filters.Pattern = "a";
                break;
            case "ignoreCase":
                filters.IgnoreCase = true;
                break;
            case "min":
                filters.MinLengthText = "1";
                break;
            default:
                filters.MaxLengthText = "9";
                break;
        }

        Assert.Equal(Strings.FilterMoreMarked, FilterRowText.MoreText(filters, lineShown: false));
        Assert.Equal(Strings.FilterMoreActive, FilterRowText.MoreDescription(filters, lineShown: false));
        Assert.Equal("Regex or length filter active", FilterRowText.MoreDescription(filters, lineShown: false));
    }

    [Fact]
    public void LineShown_NoMark()
    {
        var filters = new FilterPanelModel { Pattern = "a" };

        Assert.Equal(Strings.FilterMore, FilterRowText.MoreText(filters, lineShown: true));
        Assert.Equal("", FilterRowText.MoreDescription(filters, lineShown: true));
    }

    [Theory]
    [InlineData("mask")]
    [InlineData("filesOff")]
    [InlineData("foldersOff")]
    [InlineData("hiddenOn")]
    public void FirstLineValues_NoMark(string value)
    {
        var filters = new FilterPanelModel();
        switch (value)
        {
            case "mask":
                filters.Mask = "*.jpg";
                break;
            case "filesOff":
                filters.IncludeFiles = false;
                break;
            case "foldersOff":
                filters.IncludeFolders = false;
                break;
            default:
                filters.IncludeHidden = true;
                break;
        }

        Assert.Equal(Strings.FilterMore, FilterRowText.MoreText(filters, lineShown: false));
        Assert.Equal("", FilterRowText.MoreDescription(filters, lineShown: false));
    }

    [Fact]
    public void InvalidLength_StillMarks()
    {
        var filters = new FilterPanelModel { MinLengthText = "abc" };

        Assert.NotNull(filters.Error);
        Assert.Equal(Strings.FilterMoreMarked, FilterRowText.MoreText(filters, lineShown: false));
    }

    [Theory]
    [InlineData("Mas&k:", "Mask")]
    [InlineData("Regex:", "Regex")]
    [InlineData("Min length:", "Min length")]
    [InlineData("Max length:", "Max length")]
    public void FieldName_DropsMnemonicAndColon(string label, string expected)
    {
        Assert.Equal(expected, FilterRowText.FieldName(label));
    }

    [Fact]
    public void FieldName_OfTheRowLabels()
    {
        Assert.Equal("Mask", FilterRowText.FieldName(Strings.FilterMask));
        Assert.Equal("Regex", FilterRowText.FieldName(Strings.FilterRegex));
        Assert.Equal("Min length", FilterRowText.FieldName(Strings.FilterMinLength));
        Assert.Equal("Max length", FilterRowText.FieldName(Strings.FilterMaxLength));
    }
}
