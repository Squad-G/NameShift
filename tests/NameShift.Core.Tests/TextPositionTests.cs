using System;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class TextPositionTests
{
    [Theory]
    [InlineData("4", 4)]
    [InlineData(" 4 ", 4)]
    [InlineData("1", 1)]
    public void Parse_ReadsAWholeNumber(string text, int expected) =>
        Assert.Equal(expected, TextPosition.Parse(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("0")]
    [InlineData("-3")]
    [InlineData("abc")]
    [InlineData("4.5")]
    [InlineData("99999999999")]
    public void Parse_ReturnsNullWhenThePartMustBeIgnored(string? text) =>
        Assert.Null(TextPosition.Parse(text));

    [Theory]
    [InlineData(4, 6, 3)]
    [InlineData(1, 6, 0)]
    [InlineData(10, 2, 2)]
    public void ToInsertIndex_FromTheStart(int position, int length, int expected) =>
        Assert.Equal(expected, TextPosition.ToInsertIndex(position, length, false));

    [Theory]
    [InlineData(2, 6, 4)]
    [InlineData(1, 6, 5)]
    [InlineData(10, 2, 0)]
    public void ToInsertIndex_FromTheEnd(int position, int length, int expected) =>
        Assert.Equal(expected, TextPosition.ToInsertIndex(position, length, true));

    [Fact]
    public void ToInsertIndex_RejectsAPositionBelow1() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => TextPosition.ToInsertIndex(0, 3, false));

    [Fact]
    public void ToInsertIndex_RejectsANegativeLength() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => TextPosition.ToInsertIndex(1, -1, false));
}
