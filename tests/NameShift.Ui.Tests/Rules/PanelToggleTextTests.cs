using System.Globalization;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Rules;

public sealed class PanelToggleTextTests
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    [Fact]
    public void Arrow()
    {
        Assert.Equal("▸", PanelToggleText.Arrow(collapsed: true));
        Assert.Equal("▾", PanelToggleText.Arrow(collapsed: false));
    }

    [Theory]
    [InlineData("&Replace")]
    [InlineData("Replace")]
    public void Name(string title)
    {
        Assert.Equal("Expand Replace", PanelToggleText.Name(title, collapsed: true, English));
        Assert.Equal("Collapse Replace", PanelToggleText.Name(title, collapsed: false, English));
    }

    [Fact]
    public void Name_DoubleAmpersand_KeepsOne()
    {
        Assert.Equal("Collapse Move & Copy", PanelToggleText.Name("&Move && Copy", collapsed: false, English));
    }
}
