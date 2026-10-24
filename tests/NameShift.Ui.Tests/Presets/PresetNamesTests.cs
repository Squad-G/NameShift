using NameShift.Ui.Presets;
using Xunit;

namespace NameShift.Ui.Tests.Presets;

/// <summary>NS-72 AC 2: the rules for a typed preset name.</summary>
public sealed class PresetNamesTests
{
    [Theory]
    [InlineData(null, PresetNameVerdict.Empty, "")]
    [InlineData("", PresetNameVerdict.Empty, "")]
    [InlineData("  ", PresetNameVerdict.Empty, "")]
    [InlineData("a", PresetNameVerdict.Ok, "a")]
    [InlineData(" a ", PresetNameVerdict.Ok, "a")]
    [InlineData("My  photos", PresetNameVerdict.Ok, "My  photos")]
    public void Check(string? text, PresetNameVerdict verdict, string name)
    {
        PresetNameCheck check = PresetNames.Check(text);

        Assert.Equal(verdict, check.Verdict);
        Assert.Equal(name, check.Name);
    }

    [Fact]
    public void Check_Length_100Ok_101TooLong()
    {
        Assert.Equal(PresetNameVerdict.Ok, PresetNames.Check(new string('x', 100)).Verdict);
        Assert.Equal(PresetNameVerdict.TooLong, PresetNames.Check(new string('x', 101)).Verdict);
        Assert.Equal(100, PresetNames.MaxLength);
    }

    [Fact]
    public void Check_LengthIsMeasuredAfterTrimming()
    {
        PresetNameCheck check = PresetNames.Check("  " + new string('x', 100) + "  ");

        Assert.Equal(PresetNameVerdict.Ok, check.Verdict);
        Assert.Equal(new string('x', 100), check.Name);
    }

    [Fact]
    public void Check_FileNameCharacters_Accepted()
    {
        const string text = "a: b / c? \"d\"";

        PresetNameCheck check = PresetNames.Check(text);

        Assert.Equal(PresetNameVerdict.Ok, check.Verdict);
        Assert.Equal(text, check.Name);
    }
}
