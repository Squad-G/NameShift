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
        Assert.Equal("Windows does not allow these characters in a name: ? *", ConflictText.Message(new Conflict(ConflictCode.InvalidCharacter, "?*")));
    }

    [Fact]
    public void Message_InvalidCharacter_ControlCharacter_IsEscaped()
    {
        string message = ConflictText.Message(new Conflict(ConflictCode.InvalidCharacter, "\u0001\t"));

        Assert.Equal("Windows does not allow these characters in a name: U+0001 U+0009", message);
        Assert.DoesNotContain(message, char.IsControl);
    }

    [Fact]
    public void Message_ReservedName_ShowsTheStem()
    {
        Assert.Equal("Windows reserves the name CON: change it", ConflictText.Message(new Conflict(ConflictCode.ReservedName, "CON")));
    }

    [Fact]
    public void Message_ReservedName_ControlCharacter_IsEscaped()
    {
        Assert.Equal(
            "Windows reserves the name aU+001Fb: change it",
            ConflictText.Message(new Conflict(ConflictCode.ReservedName, "a\u001Fb")));
    }

    [Fact]
    public void Message_InvalidCharacter_OneControlCharacter_UsesThisCharacter()
    {
        Assert.Equal(
            "Windows does not allow this character in a name: U+0009",
            ConflictText.Message(new Conflict(ConflictCode.InvalidCharacter, "	")));
    }

    [Theory]
    [InlineData(ConflictCode.InvalidCharacter, null, "Windows does not allow these characters in a name: ")]
    [InlineData(ConflictCode.InvalidCharacter, "", "Windows does not allow these characters in a name: ")]
    [InlineData(ConflictCode.ReservedName, null, "Windows reserves this name: change it")]
    [InlineData(ConflictCode.ReservedName, "", "Windows reserves this name: change it")]
    public void Message_NoDetail_UsesTheNoDetailTexts(ConflictCode code, string? detail, string expected)
    {
        Assert.Equal(expected, ConflictText.Message(new Conflict(code, detail)));
    }

    [Theory]
    [InlineData(ConflictCode.InvalidCharacter, "?", "Windows does not allow this character in a name: ?")]
    [InlineData(ConflictCode.InvalidCharacter, "?*", "Windows does not allow these characters in a name: ? *")]
    [InlineData(ConflictCode.InvalidCharacter, null, "Windows does not allow these characters in a name: ")]
    [InlineData(ConflictCode.ReservedName, "CON", "Windows reserves the name CON: change it")]
    [InlineData(ConflictCode.ReservedName, null, "Windows reserves this name: change it")]
    [InlineData(ConflictCode.TrailingDotOrSpace, null, "A name cannot end with a dot or a space: remove it")]
    [InlineData(ConflictCode.EmptyName, null, "The new name is empty: keep some text in the name")]
    [InlineData(ConflictCode.NameTooLong, null, "The name is longer than 255 characters: shorten it")]
    [InlineData(ConflictCode.DuplicateTarget, null, "Another item gets the same new name: change one of them")]
    [InlineData(ConflictCode.TargetExists, null, "An item with this name already exists in the folder: choose another name")]
    [InlineData(ConflictCode.PathTooLong, null, "The full path would be longer than 259 characters: shorten the name")]
    public void Message_EachCode_ExactText(ConflictCode code, string? detail, string expected)
    {
        Assert.Equal(expected, ConflictText.Message(new Conflict(code, detail)));
    }

    [Fact]
    public void Message_NameTooLong_QuotesTheCheckLimit()
    {
        string limit = InvalidNameCheck.MaxNameLength.ToString(CultureInfo.InvariantCulture);

        Assert.Equal("255", limit);
        Assert.Contains(limit, ConflictText.Message(new Conflict(ConflictCode.NameTooLong)));
    }

    [Fact]
    public void Message_PathTooLong_QuotesTheCheckLimit()
    {
        string limit = PathLengthCheck.MaxPathLength.ToString(CultureInfo.InvariantCulture);

        Assert.Equal("259", limit);
        Assert.Contains(limit, ConflictText.Message(new Conflict(ConflictCode.PathTooLong)));
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("ar-SA")]
    [InlineData("tr-TR")]
    public void Message_Limits_SameInAnyCulture(string culture)
    {
        string[] messages = InCulture(
            culture,
            () => new[]
            {
                ConflictText.Message(new Conflict(ConflictCode.NameTooLong)),
                ConflictText.Message(new Conflict(ConflictCode.PathTooLong)),
            });

        Assert.Equal("The name is longer than 255 characters: shorten it", messages[0]);
        Assert.Equal("The full path would be longer than 259 characters: shorten the name", messages[1]);
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

        Assert.Equal("Windows does not allow this character in a name: ?; Another item gets the same new name: change one of them", text);
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

    private static string InEnUs(Func<string> action) => InCulture("en-US", action);

    private static T InCulture<T>(string name, Func<T> action)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(name);
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
