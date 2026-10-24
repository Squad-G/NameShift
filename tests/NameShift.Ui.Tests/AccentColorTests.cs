using Xunit;

namespace NameShift.Ui.Tests;

public sealed class AccentColorTests
{
    private const int White = unchecked((int)0xFFFFFFFF);
    private const int Black = unchecked((int)0xFF000000);
    private const int Highlight = unchecked((int)0xFF0078D7);

    [Fact]
    public void ContrastRatio_BlackOnWhite_Is21()
    {
        Assert.Equal(21.0, AccentColor.ContrastRatio(Black, White), 2);
        Assert.Equal(21.0, AccentColor.ContrastRatio(White, Black), 2);
    }

    [Theory]
    [InlineData(unchecked((int)0xFFFF0000), 4.00)]
    [InlineData(unchecked((int)0xFF0000FF), 8.59)]
    [InlineData(unchecked((int)0xFF00FF00), 1.37)]
    [InlineData(unchecked((int)0xFF767676), 4.54)]
    public void ContrastRatio_KnownWcagValuesAgainstWhite(int colour, double expected)
    {
        Assert.Equal(expected, AccentColor.ContrastRatio(colour, White), 2);
    }

    [Fact]
    public void MinimumContrast_Is45()
    {
        Assert.Equal(4.5, AccentColor.MinimumContrast);
    }

    [Fact]
    public void ContrastRatio_SameColour_Is1()
    {
        int grey = unchecked((int)0xFF808080);
        Assert.Equal(1.0, AccentColor.ContrastRatio(grey, grey), 2);
    }

    [Fact]
    public void ContrastRatio_IgnoresAlpha()
    {
        Assert.Equal(21.0, AccentColor.ContrastRatio(0x00000000, White), 2);
    }

    [Fact]
    public void Derive_Yellow_DarkenedTo45_KeepsHue()
    {
        int result = AccentColor.Derive(unchecked((int)0xFFFFD700), Highlight, false);

        double ratio = AccentColor.ContrastRatio(result, White);
        Assert.True(ratio >= AccentColor.MinimumContrast, "contrast " + ratio);
        Assert.True(ratio < 5.5, "stopped at the first passing step, contrast " + ratio);
        Assert.Equal(0xFF, (result >> 24) & 0xFF);
        Assert.Equal(0, result & 0xFF);
        double red = (result >> 16) & 0xFF;
        double green = (result >> 8) & 0xFF;
        Assert.Equal(0xD7 / (double)0xFF, green / red, 2);
    }

    [Fact]
    public void Derive_PassingAccent_Unchanged_AlphaForced()
    {
        Assert.Equal(unchecked((int)0xFF003399), AccentColor.Derive(unchecked((int)0x80003399), Highlight, false));
    }

    [Fact]
    public void Derive_HighContrast_ReturnsHighlight()
    {
        int highlight = unchecked((int)0xFF1AEBFF);
        Assert.Equal(highlight, AccentColor.Derive(unchecked((int)0xFFFFD700), highlight, true));
    }

    [Fact]
    public void Derive_AccentZero_ReturnsHighlight()
    {
        int highlight = unchecked((int)0xFF1AEBFF);
        Assert.Equal(highlight, AccentColor.Derive(0, highlight, false));
    }

    [Fact]
    public void Derive_White_EndsAndPasses()
    {
        int result = AccentColor.Derive(White, Highlight, false);

        Assert.True(AccentColor.ContrastRatio(result, White) >= AccentColor.MinimumContrast);
        Assert.Equal((result >> 16) & 0xFF, (result >> 8) & 0xFF);
        Assert.Equal((result >> 8) & 0xFF, result & 0xFF);
    }

    [Theory]
    [InlineData(unchecked((int)0xFFFFFFFF))]
    [InlineData(unchecked((int)0xFFC0C0C0))]
    [InlineData(unchecked((int)0xFF808080))]
    [InlineData(unchecked((int)0xFFFF0000))]
    [InlineData(unchecked((int)0xFF00FF00))]
    [InlineData(unchecked((int)0xFF0000FF))]
    [InlineData(unchecked((int)0xFFFFFF00))]
    [InlineData(unchecked((int)0xFF00FFFF))]
    [InlineData(unchecked((int)0xFFFF00FF))]
    [InlineData(unchecked((int)0xFF0078D7))]
    public void Derive_EveryGreyAndPrimary_Passes(int accent)
    {
        int result = AccentColor.Derive(accent, Highlight, false);

        Assert.True(AccentColor.ContrastRatio(result, White) >= AccentColor.MinimumContrast);
    }
}
