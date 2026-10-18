using System.Collections.Generic;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class PathLengthCheckTests
{
    private static readonly Conflict TooLong = new(ConflictCode.PathTooLong);

    // A folder of exactly `length` chars, no trailing separator.
    private static string Folder(int length) => @"C:\" + new string('f', length - 3);

    // A name of exactly `length` chars.
    private static string Name(int length) => new string('x', length - 4) + ".txt";

    private static RenameResult Changed(string folder, string newName, string oldName = "old.txt") =>
        new(new RenameItem(oldName, folder), 0, newName, RenameStatus.Changed, null);

    private static RenameResult UnchangedLong() =>
        new(new RenameItem(Name(60), Folder(250)), 0, Name(60), RenameStatus.Unchanged, null);

    private static IReadOnlyList<Conflict> CheckOne(RenameResult r) => new PathLengthCheck().Check([r])[0];

    private static void AssertFlag(bool flagged, IReadOnlyList<Conflict> actual)
    {
        if (flagged)
        {
            Assert.Equal([TooLong], actual);
        }
        else
        {
            Assert.Empty(actual);
        }
    }

    [Fact]
    public void Check_FullPathOver259_FlagsPathTooLong() =>
        Assert.Equal([TooLong], CheckOne(Changed(Folder(200), Name(60))));

    [Theory]
    [InlineData(58, false)]
    [InlineData(59, true)]
    [InlineData(60, true)]
    public void Check_AroundTheLimit_FlagsOnlyOver259(int nameLength, bool flagged) =>
        AssertFlag(flagged, CheckOne(Changed(Folder(200), Name(nameLength))));

    [Fact]
    public void Check_FullPathOfExactly259_IsNotFlagged() =>
        Assert.Empty(CheckOne(Changed(Folder(200), Name(58))));

    [Theory]
    [InlineData('\\', 59, false)]
    [InlineData('\\', 60, true)]
    [InlineData('/', 59, false)]
    public void Check_FolderEndingWithASeparator_CountsNoExtraSeparator(char separator, int nameLength, bool flagged) =>
        AssertFlag(flagged, CheckOne(Changed(@"C:\" + new string('f', 196) + separator, Name(nameLength))));

    [Theory]
    [InlineData(256, false)]
    [InlineData(257, true)]
    public void Check_DriveRoot_CountsNoExtraSeparator(int nameLength, bool flagged) =>
        AssertFlag(flagged, CheckOne(Changed(@"C:\", Name(nameLength))));

    // An empty folder does not end with a separator, so one is counted: 0 + 1 + 259 = 260 (and no index error).
    [Fact]
    public void Check_EmptyFolder_CountsOneSeparator() =>
        Assert.Equal([TooLong], CheckOne(Changed(string.Empty, Name(259))));

    [Fact]
    public void Check_UnchangedOverLongPath_IsNotFlagged() =>
        Assert.Empty(CheckOne(UnchangedLong()));

    [Fact]
    public void Check_ErrorResult_IsNotFlagged() =>
        Assert.Empty(CheckOne(new RenameResult(
            new RenameItem(Name(60), Folder(250)), 0, Name(60), RenameStatus.Error, new RenameError(RuleStage.Name, "X", "boom"))));

    [Theory]
    [InlineData(8, false)]
    [InlineData(9, true)]
    public void Check_ShorterNameInOverLongFolder_FlagsOnlyIfStillOver259(int newNameLength, bool flagged) =>
        AssertFlag(flagged, CheckOne(Changed(Folder(250), Name(newNameLength), Name(40))));

    [Fact]
    public void Check_ReturnsOneListPerResultInOrder()
    {
        var actual = new PathLengthCheck().Check(
            [Changed(Folder(200), Name(60)), UnchangedLong(), Changed(Folder(200), Name(10))]);

        Assert.Equal(3, actual.Count);
        Assert.Equal([TooLong], actual[0]);
        Assert.Empty(actual[1]);
        Assert.Empty(actual[2]);
    }

    [Fact]
    public void Check_NullResults_Throws() =>
        Assert.Throws<System.ArgumentNullException>(() => new PathLengthCheck().Check(null!));
}
