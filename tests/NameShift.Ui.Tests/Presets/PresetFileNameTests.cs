using System;
using System.Globalization;
using System.IO;
using NameShift.Ui.Presets;
using Xunit;

namespace NameShift.Ui.Tests.Presets;

/// <summary>NS-36 AC 9: the safe file name of a preset.</summary>
public sealed class PresetFileNameTests
{
    [Fact]
    public void InvalidCharacters()
    {
        Assert.Equal("Photos_ 2024_raw_.json", PresetFileName.For("Photos: 2024/raw?", 1));
        Assert.Equal("a_b_c_d_e_f_g_h.json", PresetFileName.For("a<b>c\"d\\e|f*g\u0001h", 1));
        Assert.Equal("a_b.json", PresetFileName.For("a\tb", 1));
    }

    [Theory]
    [InlineData("CON", "CON_.json")]
    [InlineData("con", "con_.json")]
    [InlineData("Nul", "Nul_.json")]
    [InlineData("COM1", "COM1_.json")]
    [InlineData("LPT9", "LPT9_.json")]
    [InlineData("con.txt", "con_.txt.json")]
    [InlineData("aux.", "aux_.json")]
    [InlineData("CONSOLE", "CONSOLE.json")]
    [InlineData("COM10", "COM10.json")]
    [InlineData("acon", "acon.json")]
    public void Reserved(string name, string expected)
    {
        CultureInfo before = CultureInfo.CurrentCulture;
        try
        {
            foreach (string culture in new[] { "", "tr-TR" })
            {
                CultureInfo.CurrentCulture = culture.Length == 0 ? CultureInfo.InvariantCulture : new CultureInfo(culture);

                Assert.Equal(expected, PresetFileName.For(name, 1));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    [Fact]
    public void TrailingDotsAndSpaces()
    {
        Assert.Equal("a.json", PresetFileName.For("a. .", 1));
        Assert.Equal("a.json", PresetFileName.For("  a  ", 1));
        Assert.Equal("a.b.json", PresetFileName.For("a.b", 1));
    }

    [Fact]
    public void OnlyDots_IsPreset()
    {
        Assert.Equal("preset.json", PresetFileName.For("..", 1));
        Assert.Equal("preset.json", PresetFileName.For("   ", 1));
        Assert.Equal("preset (2).json", PresetFileName.For(". .", 2));
    }

    [Fact]
    public void Emoji_KeptWhole()
    {
        Assert.Equal("Photos \uD83D\uDCF7.json", PresetFileName.For("Photos \uD83D\uDCF7", 1));
    }

    [Fact]
    public void LoneSurrogate_Underscore()
    {
        Assert.Equal("a_b.json", PresetFileName.For("a" + '\uD800' + "b", 1));
        Assert.Equal("a_b.json", PresetFileName.For("a" + '\uDC00' + "b", 1));
        Assert.Equal("a_.json", PresetFileName.For("a" + '\uD83D', 1));
    }

    [Fact]
    public void Long_AtMost100_NoSplitPair()
    {
        string name = new string('x', 300);

        string first = PresetFileName.For(name, 1);
        string copy = PresetFileName.For(name, 999);

        Assert.True(first.Length <= 100, first);
        Assert.True(copy.Length <= 100, copy);
        Assert.EndsWith(" (999).json", copy);

        // A pair across the cut: the high half would be the last unit kept.
        string paired = new string('y', PresetFileName.MaxBaseLength - 1) + "\uD83D\uDCF7" + new string('z', 50);
        string cut = PresetFileName.For(paired, 1);
        Assert.Equal(new string('y', PresetFileName.MaxBaseLength - 1) + ".json", cut);
        Assert.False(char.IsHighSurrogate(cut[cut.Length - 6]));

        // A reserved stem on a long name still fits.
        string reserved = PresetFileName.For("con." + new string('w', 300), 999);
        Assert.StartsWith("con_.", reserved);
        Assert.True(reserved.Length <= 100, reserved);
    }

    [Fact]
    public void Copy_Suffix()
    {
        Assert.Equal("a.json", PresetFileName.For("a", 1));
        Assert.Equal("a (2).json", PresetFileName.For("a", 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => PresetFileName.For("a", 0));
    }

    [Theory]
    [InlineData(@"..\victim")]
    [InlineData(@"C:\x\victim")]
    [InlineData("a/b")]
    [InlineData(@"\\server\share\x")]
    [InlineData("..")]
    public void NeverASeparator(string name)
    {
        string folder = Path.Combine(Path.GetTempPath(), "presets");

        string result = PresetFileName.For(name, 1);

        Assert.DoesNotContain("\\", result);
        Assert.DoesNotContain("/", result);
        Assert.DoesNotContain(":", result);
        Assert.Equal(folder, Path.GetDirectoryName(Path.Combine(folder, result)));
    }
}
