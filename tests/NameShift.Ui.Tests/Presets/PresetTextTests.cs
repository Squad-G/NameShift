using System;
using System.Globalization;
using NameShift.Ui.Presets;
using Xunit;

namespace NameShift.Ui.Tests.Presets;

/// <summary>NS-72 AC 4, 5, 8, 9 and 12: the menu and status texts.</summary>
public sealed class PresetTextTests
{
    [Theory]
    [InlineData("Fish & Chips", "Fish && Chips")]
    [InlineData("&&", "&&&&")]
    [InlineData("Plain", "Plain")]
    public void MenuText_DoublesAmpersand(string name, string expected) =>
        Assert.Equal(expected, PresetText.MenuText(name));

    [Fact]
    public void StatusTexts_ShowNameAsTyped()
    {
        Assert.Equal("Preset \"Fish & Chips\" saved.", PresetText.Saved("Fish & Chips"));
        Assert.Equal("Preset \"Fish & Chips\" loaded.", PresetText.Loaded("Fish & Chips"));
        Assert.Equal("Preset \"Fish & Chips\" deleted.", PresetText.Deleted("Fish & Chips"));
        Assert.Equal("The preset \"Fish & Chips\" was not found.", PresetText.NotFound("Fish & Chips"));
    }

    [Fact]
    public void Unreadable()
    {
        Assert.Null(PresetText.Unreadable(0));
        Assert.Equal("1 preset could not be read.", PresetText.Unreadable(1));
        Assert.Equal("3 presets could not be read.", PresetText.Unreadable(3));
    }

    [Fact]
    public void Questions()
    {
        Assert.Equal("Replace the preset \"X\"?", PresetText.ReplaceQuestion("X"));
        Assert.Equal("Delete the preset \"X\"?", PresetText.DeleteQuestion("X"));
    }

    [Fact]
    public void Failures()
    {
        Assert.Equal("The preset could not be saved: Disk full.", PresetText.SaveFailed("Disk full."));
        Assert.Equal("The preset could not be deleted: Access is denied.", PresetText.DeleteFailed("Access is denied."));
    }

    [Fact]
    public void Texts_SameUnderTurkishCulture()
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo turkish = new CultureInfo("tr-TR");
            CultureInfo.CurrentCulture = turkish;
            CultureInfo.CurrentUICulture = turkish;

            Assert.Equal("Fish && Chips", PresetText.MenuText("Fish & Chips"));
            Assert.Equal("Preset \"IiI\" saved.", PresetText.Saved("IiI"));
            Assert.Equal("Preset \"IiI\" loaded.", PresetText.Loaded("IiI"));
            Assert.Equal("Preset \"IiI\" deleted.", PresetText.Deleted("IiI"));
            Assert.Equal("The preset \"IiI\" was not found.", PresetText.NotFound("IiI"));
            Assert.Equal("1 preset could not be read.", PresetText.Unreadable(1));
            Assert.Equal("3 presets could not be read.", PresetText.Unreadable(3));
            Assert.Equal("Replace the preset \"IiI\"?", PresetText.ReplaceQuestion("IiI"));
            Assert.Equal("Delete the preset \"IiI\"?", PresetText.DeleteQuestion("IiI"));
            Assert.Equal("The preset could not be saved: Disk full.", PresetText.SaveFailed("Disk full."));
            Assert.Equal("The preset could not be deleted: Access is denied.", PresetText.DeleteFailed("Access is denied."));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }
}
