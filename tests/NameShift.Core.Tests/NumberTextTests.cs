using System;
using System.Globalization;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class NumberTextTests
{
    private const string LettersText = "The number cannot be written in letters (from 1).";
    private const string RomanText = "The number cannot be written in Roman numerals (1 to 3999).";

    [Theory]
    [InlineData(10, 2, true, "0A")]
    [InlineData(16, 0, true, "10")]
    [InlineData(255, 0, true, "FF")]
    [InlineData(255, 0, false, "ff")]
    [InlineData(int.MaxValue, 0, true, "7FFFFFFF")]
    [InlineData(10, 5, true, "0000A")]
    public void Format_Hex(long value, int pad, bool upper, string expected) =>
        Assert.Equal(expected, NumberText.Format(value, NumeralSystem.Hexadecimal, pad, upper));

    [Fact]
    public void Format_PadNeverCuts()
    {
        Assert.Equal("1000", NumberText.Format(4096, NumeralSystem.Hexadecimal, 1, true));
        Assert.Equal("12345", NumberText.Format(12345, NumeralSystem.Decimal, 2, false));
    }

    [Theory]
    [InlineData(1, false, "a")]
    [InlineData(2, false, "b")]
    [InlineData(26, false, "z")]
    [InlineData(27, false, "aa")]
    [InlineData(28, false, "ab")]
    [InlineData(52, false, "az")]
    [InlineData(53, false, "ba")]
    [InlineData(702, false, "zz")]
    [InlineData(703, false, "aaa")]
    [InlineData(27, true, "AA")]
    public void Format_Letters(long value, bool upper, string expected) =>
        Assert.Equal(expected, NumberText.Format(value, NumeralSystem.Letters, 0, upper));

    [Theory]
    [InlineData(1, true, "I")]
    [InlineData(4, true, "IV")]
    [InlineData(9, true, "IX")]
    [InlineData(14, true, "XIV")]
    [InlineData(40, true, "XL")]
    [InlineData(90, true, "XC")]
    [InlineData(400, true, "CD")]
    [InlineData(1994, true, "MCMXCIV")]
    [InlineData(3999, true, "MMMCMXCIX")]
    [InlineData(4, false, "iv")]
    public void Format_Roman(long value, bool upper, string expected) =>
        Assert.Equal(expected, NumberText.Format(value, NumeralSystem.Roman, 0, upper));

    [Fact]
    public void Format_PadIgnored_InLettersAndRoman()
    {
        Assert.Equal("a", NumberText.Format(1, NumeralSystem.Letters, 3, false));
        Assert.Equal("I", NumberText.Format(1, NumeralSystem.Roman, 3, true));
    }

    [Fact]
    public void Format_NoLettersForm_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => NumberText.Format(0, NumeralSystem.Letters, 0, false));

        Assert.Equal(LettersText, ex.Message);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(4000L)]
    [InlineData(long.MaxValue)]
    public void Format_NoRomanForm_Throws(long value)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => NumberText.Format(value, NumeralSystem.Roman, 0, true));

        Assert.Equal(RomanText, ex.Message);
    }

    [Fact]
    public void Format_InvalidArguments_Throw()
    {
        foreach (var system in new[]
                 { NumeralSystem.Decimal, NumeralSystem.Hexadecimal, NumeralSystem.Letters, NumeralSystem.Roman })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => NumberText.Format(-1, system, 0, true));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => NumberText.Format(1, (NumeralSystem)9, 0, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => NumberText.Format(1, NumeralSystem.Decimal, -1, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => NumberText.Format(1, NumeralSystem.Decimal, 256, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => NumberText.Format(1, NumeralSystem.Roman, 256, true));
    }

    [Fact]
    public void Format_Zero()
    {
        Assert.Equal("0", NumberText.Format(0, NumeralSystem.Decimal, 0, false));
        Assert.Equal("0", NumberText.Format(0, NumeralSystem.Hexadecimal, 0, true));
        Assert.Equal("00", NumberText.Format(0, NumeralSystem.Hexadecimal, 2, true));
    }

    [Fact]
    public void Format_LongMaxValue()
    {
        Assert.Equal("9223372036854775807", NumberText.Format(long.MaxValue, NumeralSystem.Decimal, 0, false));
        Assert.Equal("7FFFFFFFFFFFFFFF", NumberText.Format(long.MaxValue, NumeralSystem.Hexadecimal, 0, true));

        var letters = NumberText.Format(long.MaxValue, NumeralSystem.Letters, 0, false);

        Assert.NotEmpty(letters);
        Assert.Equal(letters.ToLowerInvariant(), letters);
        Assert.NotEqual(letters, NumberText.Format(long.MaxValue - 1, NumeralSystem.Letters, 0, false));
    }

    [Fact]
    public void Format_Pad255_Accepted()
    {
        var text = NumberText.Format(7, NumeralSystem.Decimal, 255, false);

        Assert.Equal(255, text.Length);
        Assert.EndsWith("7", text);
    }

    [Theory]
    [InlineData("12", 12)]
    [InlineData("007", 7)]
    [InlineData("0", 0)]
    [InlineData(" 12 ", 12)]
    public void TryParse_Decimal(string text, int expected)
    {
        Assert.True(NumberText.TryParse(text, NumeralSystem.Decimal, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1.5")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1 2")]
    [InlineData("2147483648")]
    public void TryParse_Decimal_Fails(string text)
    {
        Assert.False(NumberText.TryParse(text, NumeralSystem.Decimal, out var value));
        Assert.Equal(0, value);
    }

    [Theory]
    [InlineData("A", 10)]
    [InlineData("a", 10)]
    [InlineData("FF", 255)]
    [InlineData("10", 16)]
    [InlineData("000A", 10)]
    [InlineData("7FFFFFFF", int.MaxValue)]
    public void TryParse_Hex(string text, int expected)
    {
        Assert.True(NumberText.TryParse(text, NumeralSystem.Hexadecimal, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("0x10")]
    [InlineData("G")]
    [InlineData("-A")]
    [InlineData("")]
    [InlineData("80000000")]
    public void TryParse_Hex_Fails(string text)
    {
        Assert.False(NumberText.TryParse(text, NumeralSystem.Hexadecimal, out var value));
        Assert.Equal(0, value);
    }

    [Theory]
    [InlineData("a", 1)]
    [InlineData("Z", 26)]
    [InlineData("aa", 27)]
    [InlineData("Ab", 28)]
    [InlineData("zz", 702)]
    public void TryParse_Letters(string text, int expected)
    {
        Assert.True(NumberText.TryParse(text, NumeralSystem.Letters, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("a1")]
    [InlineData("\u00E9")]
    [InlineData("")]
    [InlineData("zzzzzzzzzzzz")]
    public void TryParse_Letters_Fails(string text)
    {
        Assert.False(NumberText.TryParse(text, NumeralSystem.Letters, out var value));
        Assert.Equal(0, value);
    }

    [Theory]
    [InlineData("IV", 4)]
    [InlineData("iv", 4)]
    [InlineData("MCMXCIV", 1994)]
    [InlineData("MMMCMXCIX", 3999)]
    public void TryParse_Roman(string text, int expected)
    {
        Assert.True(NumberText.TryParse(text, NumeralSystem.Roman, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("IIII")]
    [InlineData("IC")]
    [InlineData("VX")]
    [InlineData("MMMM")]
    [InlineData("IVX")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("\u0131")]
    public void TryParse_Roman_Fails(string text)
    {
        Assert.False(NumberText.TryParse(text, NumeralSystem.Roman, out var value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void RoundTrip_AllSystems_BothCases()
    {
        foreach (var upper in new[] { false, true })
        {
            for (var v = 1; v <= 3999; v++)
            {
                AssertRoundTrip(v, NumeralSystem.Roman, upper);
            }

            for (var v = 1; v <= 3000; v++)
            {
                AssertRoundTrip(v, NumeralSystem.Letters, upper);
            }

            for (var v = 0; v <= 3000; v++)
            {
                AssertRoundTrip(v, NumeralSystem.Hexadecimal, upper);
                AssertRoundTrip(v, NumeralSystem.Decimal, upper);
            }

            AssertRoundTrip(int.MaxValue, NumeralSystem.Hexadecimal, upper);
            AssertRoundTrip(int.MaxValue, NumeralSystem.Decimal, upper);
            AssertRoundTrip(int.MaxValue, NumeralSystem.Letters, upper);
        }
    }

    [Fact]
    public void TryParse_UndefinedSystem_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NumberText.TryParse("1", (NumeralSystem)9, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => NumberText.TryParse(null, (NumeralSystem)9, out _));
    }

    [Fact]
    public void TryParse_LongLeadingZeros()
    {
        Assert.True(NumberText.TryParse("0000000000007", NumeralSystem.Decimal, out var dec));
        Assert.Equal(7, dec);
        Assert.True(NumberText.TryParse("00000000000A", NumeralSystem.Hexadecimal, out var hex));
        Assert.Equal(10, hex);
    }

    [Theory]
    [InlineData("\u0663", NumeralSystem.Decimal)]
    [InlineData("\uFF13", NumeralSystem.Decimal)]
    [InlineData("\u0663", NumeralSystem.Hexadecimal)]
    [InlineData("\uFF13", NumeralSystem.Hexadecimal)]
    public void TryParse_NonAsciiDigits_Fail(string text, NumeralSystem system)
    {
        Assert.False(NumberText.TryParse(text, system, out var value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryParse_RomanZeroAndFourThousand_Fail()
    {
        Assert.False(NumberText.TryParse("0", NumeralSystem.Roman, out _));
        Assert.False(NumberText.TryParse("MMMM", NumeralSystem.Roman, out _));
    }

    [Theory]
    [InlineData(NumeralSystem.Decimal)]
    [InlineData(NumeralSystem.Hexadecimal)]
    [InlineData(NumeralSystem.Letters)]
    [InlineData(NumeralSystem.Roman)]
    public void TryParse_Null_Fails(NumeralSystem system)
    {
        Assert.False(NumberText.TryParse(null, system, out var value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void TryParse_VeryLongText_Fails()
    {
        Assert.False(NumberText.TryParse(new string('a', 10000), NumeralSystem.Letters, out var letters));
        Assert.Equal(0, letters);
        Assert.False(NumberText.TryParse(new string('1', 10000), NumeralSystem.Decimal, out var digits));
        Assert.Equal(0, digits);
        Assert.False(NumberText.TryParse(new string('I', 10000), NumeralSystem.Roman, out var roman));
        Assert.Equal(0, roman);
    }

    [Fact]
    public void FormatAndParse_TurkishCulture()
    {
        var saved = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            Assert.Equal("iv", NumberText.Format(4, NumeralSystem.Roman, 0, false));
            Assert.Equal("i", NumberText.Format(9, NumeralSystem.Letters, 0, false));
            Assert.Equal("I", NumberText.Format(9, NumeralSystem.Letters, 0, true));
            Assert.True(NumberText.TryParse("iv", NumeralSystem.Roman, out var roman));
            Assert.Equal(4, roman);
            Assert.True(NumberText.TryParse("I", NumeralSystem.Letters, out var letters));
            Assert.Equal(9, letters);
        }
        finally
        {
            CultureInfo.CurrentCulture = saved;
        }
    }

    private static void AssertRoundTrip(int value, NumeralSystem system, bool upper)
    {
        var text = NumberText.Format(value, system, 0, upper);

        Assert.True(NumberText.TryParse(text, system, out var result),
            "Not read back: " + value + " in " + system + " (" + text + ").");
        Assert.True(result == value, "Round trip failed: " + value + " in " + system + " gave " + result + ".");
    }
}
