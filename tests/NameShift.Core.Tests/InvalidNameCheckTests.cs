using System;
using System.Collections.Generic;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class InvalidNameCheckTests
{
    private static RenameResult Changed(string newName) =>
        new(Items.File("old.txt"), 0, newName, RenameStatus.Changed, null);

    private static IReadOnlyList<Conflict> CheckOne(RenameResult result) =>
        new InvalidNameCheck().Check([result])[0];

    private static IReadOnlyList<Conflict> CheckName(string name) => CheckOne(Changed(name));

    [Theory]
    [InlineData("a\\b", "\\")]
    [InlineData("a/b", "/")]
    [InlineData("a:b", ":")]
    [InlineData("a*b", "*")]
    [InlineData("a?b", "?")]
    [InlineData("a\"b", "\"")]
    [InlineData("a<b", "<")]
    [InlineData("a>b", ">")]
    [InlineData("a|b", "|")]
    public void Check_ForbiddenCharacter_FlagsInvalidCharacter(string name, string detail) =>
        Assert.Equal([new Conflict(ConflictCode.InvalidCharacter, detail)], CheckName(name));

    [Theory]
    [InlineData("a\u0000b", "\u0000")]
    [InlineData("a\tb", "\t")]
    [InlineData("a\u001Fb", "\u001F")]
    public void Check_ControlCharacter_FlagsInvalidCharacter(string name, string detail) =>
        Assert.Equal([new Conflict(ConflictCode.InvalidCharacter, detail)], CheckName(name));

    [Fact]
    public void Check_SeveralBadCharacters_NamesEachOnceInOrder() =>
        Assert.Equal([new Conflict(ConflictCode.InvalidCharacter, "?*")], CheckName("a?b*c?.txt"));

    [Fact]
    public void Check_DeleteCharacter_IsAllowed() => Assert.Empty(CheckName("a\u007Fb.txt"));

    [Theory]
    [InlineData("nul.txt", "nul")]
    [InlineData("CON.txt", "CON")]
    [InlineData("nul.tar.gz", "nul")]
    [InlineData("con.backup.txt", "con")]
    [InlineData("Com1", "Com1")]
    public void Check_DeviceNameBeforeTheFirstDot_FlagsReservedName(string name, string detail) =>
        Assert.Equal([new Conflict(ConflictCode.ReservedName, detail)], CheckName(name));

    [Theory]
    [InlineData("PRN.txt", "PRN")]
    [InlineData("aux", "aux")]
    [InlineData("COM9.log", "COM9")]
    [InlineData("lpt1.txt", "lpt1")]
    [InlineData("LPT9", "LPT9")]
    public void Check_EveryDeviceName_FlagsReservedName(string name, string detail) =>
        Assert.Equal([new Conflict(ConflictCode.ReservedName, detail)], CheckName(name));

    [Theory]
    [InlineData("COM¹.txt", "COM¹")]
    [InlineData("com²", "com²")]
    [InlineData("LPT³.txt", "LPT³")]
    public void Check_SuperscriptDeviceName_FlagsReservedName(string name, string detail) =>
        Assert.Equal([new Conflict(ConflictCode.ReservedName, detail)], CheckName(name));

    [Fact]
    public void Check_DeviceNameWithTrailingSpaces_FlagsReservedName() =>
        Assert.Equal([new Conflict(ConflictCode.ReservedName, "CON")], CheckName("CON .txt"));

    [Theory]
    [InlineData("file.")]
    [InlineData("file ")]
    [InlineData("a.txt.")]
    public void Check_TrailingDotOrSpace_FlagsIt(string name) =>
        Assert.Equal([new Conflict(ConflictCode.TrailingDotOrSpace)], CheckName(name));

    [Fact]
    public void Check_DeviceNameAndTrailingDot_FlagsBothInOrder() =>
        Assert.Equal(
            [new Conflict(ConflictCode.ReservedName, "con"), new Conflict(ConflictCode.TrailingDotOrSpace)],
            CheckName("con.txt."));

    [Fact]
    public void Check_DeviceNameAndTrailingSpace_FlagsBoth() =>
        Assert.Equal(
            [new Conflict(ConflictCode.ReservedName, "nul"), new Conflict(ConflictCode.TrailingDotOrSpace)],
            CheckName("nul "));

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("...")]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData(". .")]
    public void Check_EmptyOrOnlyDotsAndSpaces_FlagsOnlyEmptyName(string name) =>
        Assert.Equal([new Conflict(ConflictCode.EmptyName)], CheckName(name));

    [Theory]
    [InlineData("con-x.txt")]
    [InlineData("COM10.txt")]
    [InlineData("xcon.txt")]
    [InlineData("COM0.txt")]
    [InlineData("LPT0")]
    [InlineData("COM⁴.txt")]
    [InlineData("CONIN$.txt")]
    public void Check_NamesThatOnlyLookReserved_AreNotFlagged(string name) => Assert.Empty(CheckName(name));

    [Theory]
    [InlineData("photo.jpg")]
    [InlineData(".gitignore")]
    [InlineData(".txt")]
    [InlineData("a..b.txt")]
    [InlineData(" a.txt")]
    [InlineData("Été 2024.jpg")]
    public void Check_OrdinaryNames_AreNotFlagged(string name) => Assert.Empty(CheckName(name));

    [Theory]
    [InlineData("CON")]
    [InlineData("a.")]
    public void Check_UnchangedInvalidName_IsNotFlagged(string name) =>
        Assert.Empty(CheckOne(new RenameResult(Items.File(name), 0, name, RenameStatus.Unchanged, null)));

    [Fact]
    public void Check_ErrorResult_IsNotFlagged() =>
        Assert.Empty(CheckOne(new RenameResult(
            Items.File("nul.txt"),
            0,
            "nul.txt",
            RenameStatus.Error,
            new RenameError(RuleStage.Name, "X", "boom"))));

    [Theory]
    [InlineData(256, "")]
    [InlineData(252, ".txt")]
    public void Check_NameOver255_FlagsNameTooLong(int baseLength, string extension) =>
        Assert.Equal(
            [new Conflict(ConflictCode.NameTooLong)],
            CheckName(new string('a', baseLength) + extension));

    [Theory]
    [InlineData(255, "")]
    [InlineData(251, ".txt")]
    public void Check_NameOf255_IsNotFlagged(int baseLength, string extension) =>
        Assert.Empty(CheckName(new string('a', baseLength) + extension));

    [Fact]
    public void Check_ReturnsOneListPerResultInOrder()
    {
        var lists = new InvalidNameCheck().Check([Changed("ok.txt"), Changed("a?"), Changed("fine")]);

        Assert.Equal(3, lists.Count);
        Assert.Empty(lists[0]);
        Assert.Equal([new Conflict(ConflictCode.InvalidCharacter, "?")], lists[1]);
        Assert.Empty(lists[2]);
    }

    [Fact]
    public void Check_RejectsNull() => Assert.Throws<ArgumentNullException>(() => new InvalidNameCheck().Check(null!));

    [Fact]
    public void CheckName_RejectsNull() => Assert.Throws<ArgumentNullException>(() => InvalidNameCheck.CheckName(null!));

    [Fact]
    public void CheckName_Direct_MatchesCheck() =>
        Assert.Equal([new Conflict(ConflictCode.ReservedName, "nul")], InvalidNameCheck.CheckName("nul.txt"));
}
