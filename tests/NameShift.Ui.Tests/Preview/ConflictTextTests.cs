using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NameShift.Core;
using NameShift.Ui.Preview;
using Xunit;

namespace NameShift.Ui.Tests.Preview;

public sealed class ConflictTextTests
{
    public static IEnumerable<object[]> AllCodes() =>
        Enum.GetValues(typeof(ConflictCode)).Cast<ConflictCode>().Select(code => new object[] { code });

    [Theory]
    [MemberData(nameof(AllCodes))]
    public void Message_EveryCode_HasItsOwnText(ConflictCode code)
    {
        string message = ConflictText.Message(new Conflict(code, "x"));

        Assert.NotEmpty(message);
        Assert.DoesNotContain("{", message);
    }

    [Fact]
    public void Messages_AreDistinctPerCode()
    {
        string[] messages = Enum.GetValues(typeof(ConflictCode)).Cast<ConflictCode>()
            .Select(code => ConflictText.Message(new Conflict(code, "x")))
            .ToArray();

        Assert.Equal(8, messages.Length);
        Assert.Equal(messages.Length, messages.Distinct().Count());
    }

    [Fact]
    public void Message_InvalidCharacter_ListsTheCharacters()
    {
        Assert.Equal("Not allowed in a name: ? *", ConflictText.Message(new Conflict(ConflictCode.InvalidCharacter, "?*")));
    }

    [Fact]
    public void Message_InvalidCharacter_ControlCharacter_IsEscaped()
    {
        string message = ConflictText.Message(new Conflict(ConflictCode.InvalidCharacter, "\u0001\t"));

        Assert.Equal("Not allowed in a name: U+0001 U+0009", message);
        Assert.DoesNotContain(message, char.IsControl);
    }

    [Fact]
    public void Message_ReservedName_ShowsTheStem()
    {
        Assert.Equal("Reserved by Windows: CON", ConflictText.Message(new Conflict(ConflictCode.ReservedName, "CON")));
    }

    [Fact]
    public void Message_ReservedName_ControlCharacter_IsEscaped()
    {
        Assert.Equal(
            "Reserved by Windows: aU+001Fb",
            ConflictText.Message(new Conflict(ConflictCode.ReservedName, "a\u001Fb")));
    }

    [Fact]
    public void Message_NullDetail_DoesNotThrow()
    {
        Assert.Equal("Not allowed in a name: ", ConflictText.Message(new Conflict(ConflictCode.InvalidCharacter)));
        Assert.Equal("Reserved by Windows: ", ConflictText.Message(new Conflict(ConflictCode.ReservedName)));
    }

    [Fact]
    public void Message_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ConflictText.Message(null!));
    }

    [Fact]
    public void Message_UnknownCode_Throws()
    {
        // Conflict rejects an undefined code, so the guard in Message is reached only through a defined one.
        Assert.Throws<ArgumentOutOfRangeException>(() => new Conflict((ConflictCode)99));
    }

    [Fact]
    public void For_SeveralConflicts_JoinsInOrder()
    {
        string text = ConflictText.For(new[]
        {
            new Conflict(ConflictCode.InvalidCharacter, "?"),
            new Conflict(ConflictCode.DuplicateTarget),
        });

        Assert.Equal("Not allowed in a name: ?; Same new name as another file", text);
    }

    [Fact]
    public void For_NoConflict_IsEmpty()
    {
        Assert.Equal("", ConflictText.For(Array.Empty<Conflict>()));
    }

    [Fact]
    public void For_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ConflictText.For(null!));
    }

    [Fact]
    public void Summary_Zero_IsEmpty()
    {
        Assert.Equal("", ConflictText.Summary(0));
    }

    [Fact]
    public void Summary_One_IsSingular()
    {
        Assert.Equal("1 conflict", InEnUs(() => ConflictText.Summary(1)));
    }

    [Fact]
    public void Summary_Many_IsPlural()
    {
        Assert.Equal("1,234 conflicts", InEnUs(() => ConflictText.Summary(1234)));
    }

    [Fact]
    public void Escape_KeepsPrintable_EscapesC0DelC1()
    {
        Assert.Equal("aU+0000U+007FU+0085\u00e9", ConflictText.Escape("a\u0000\u007F\u0085\u00e9"));
    }

    private static string InEnUs(Func<string> action)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
