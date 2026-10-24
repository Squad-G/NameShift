using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class DuplicateTargetCheckTests
{
    private const string Folder = @"C:\Test";
    private static readonly Conflict Dup = new(ConflictCode.DuplicateTarget);
    private static readonly Conflict Exists = new(ConflictCode.TargetExists);
    private const string NfcCafe = "caf\u00e9.txt";
    private const string NfdCafe = "cafe\u0301.txt";
    private const string NfcCafeUpper = "CAF\u00c9.txt";
    private const string NfdCafeUpper = "CAFE\u0301.txt";

    private static RenameResult R(int index, string name, string newName, string folder = Folder) =>
        new(new RenameItem(name, folder), index, newName,
            string.Equals(name, newName, StringComparison.Ordinal) ? RenameStatus.Unchanged : RenameStatus.Changed, null);

    private static DuplicateTargetCheck With(params string[] existing) =>
        new(new Dictionary<string, IReadOnlyCollection<string>> { [Folder] = existing });

    private static void AssertLists(IReadOnlyList<IReadOnlyList<Conflict>> actual, params Conflict[][] expected)
    {
        Assert.Equal(expected.Length, actual.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i]);
        }
    }

    [Fact]
    public void Check_TwoItemsToTheSameName_FlagsBothDuplicateTarget() =>
        AssertLists(
            With("a.txt", "b.txt").Check([R(0, "a.txt", "x.txt"), R(1, "b.txt", "x.txt")]),
            [Dup], [Dup]);

    [Theory]
    [InlineData("x.txt", "X.TXT")]
    [InlineData("été.txt", "ÉTÉ.txt")]
    public void Check_SameNameInAnotherCase_FlagsBothDuplicateTarget(string first, string second) =>
        AssertLists(
            With("a.txt", "b.txt").Check([R(0, "a.txt", first), R(1, "b.txt", second)]),
            [Dup], [Dup]);

    [Theory]
    [InlineData("x.txt", "x.txt")]
    [InlineData("X.TXT", "x.txt")]
    [InlineData("desktop.ini", "desktop.ini")]
    public void Check_NameOfAnEntryOutsideTheBatch_FlagsTargetExists(string listed, string target) =>
        AssertLists(With("a.txt", listed).Check([R(0, "a.txt", target)]), [[Exists]]);

    [Fact]
    public void Check_NameOfAFolderOutsideTheBatch_FlagsTargetExists() =>
        AssertLists(With("a.txt", "x").Check([R(0, "a.txt", "x")]), [[Exists]]);

    [Fact]
    public void Check_TwoItemsToAnExistingName_FlagsBothCodes() =>
        AssertLists(
            With("a.txt", "b.txt", "x.txt").Check([R(0, "a.txt", "x.txt"), R(1, "b.txt", "x.txt")]),
            [Dup, Exists], [Dup, Exists]);

    [Fact]
    public void Check_Chain_IsNotFlagged() =>
        AssertLists(
            With("a.txt", "b.txt").Check([R(0, "a.txt", "b.txt"), R(1, "b.txt", "c.txt")]),
            [], []);

    [Fact]
    public void Check_LongerChain_IsNotFlagged() =>
        AssertLists(
            With("a", "b", "c").Check([R(0, "a", "b"), R(1, "b", "c"), R(2, "c", "d")]),
            [], [], []);

    [Fact]
    public void Check_Swap_IsNotFlagged() =>
        AssertLists(
            With("a.txt", "b.txt").Check([R(0, "a.txt", "b.txt"), R(1, "b.txt", "a.txt")]),
            [], []);

    [Fact]
    public void Check_SwapInAnotherCase_IsNotFlagged() =>
        AssertLists(
            With("a.txt", "b.txt").Check([R(0, "a.txt", "B.TXT"), R(1, "b.txt", "A.TXT")]),
            [], []);

    [Fact]
    public void Check_CaseOnlyChange_IsNotFlagged() =>
        AssertLists(With("photo.jpg").Check([R(0, "photo.jpg", "Photo.jpg")]), [[]]);

    [Fact]
    public void Check_SameNameInTwoFolders_IsNotFlagged()
    {
        var check = new DuplicateTargetCheck(new Dictionary<string, IReadOnlyCollection<string>>
        {
            [@"C:\One"] = new[] { "a.txt" },
            [@"C:\Two"] = new[] { "b.txt" },
        });

        AssertLists(
            check.Check([R(0, "a.txt", "x.txt", @"C:\One"), R(1, "b.txt", "x.txt", @"C:\Two")]),
            [], []);
    }

    [Fact]
    public void Check_ExistingNameInAnotherFolder_IsNotFlagged()
    {
        var check = new DuplicateTargetCheck(new Dictionary<string, IReadOnlyCollection<string>>
        {
            [@"C:\One"] = new[] { "a.txt" },
            [@"C:\Two"] = new[] { "x.txt" },
        });

        AssertLists(check.Check([R(0, "a.txt", "x.txt", @"C:\One")]), [[]]);
    }

    [Fact]
    public void Check_ChangedItemOntoAnUnchangedItem_FlagsOnlyTheChangedOne() =>
        AssertLists(
            With("a.txt", "b.txt").Check([R(0, "a.txt", "a.txt"), R(1, "b.txt", "A.TXT")]),
            [], [Dup]);

    [Fact]
    public void Check_ChangedItemOntoAnErrorItem_FlagsOnlyTheChangedOne()
    {
        var error = new RenameResult(
            new RenameItem("a.txt", Folder), 0, "a.txt", RenameStatus.Error,
            new RenameError(RuleStage.Name, "X", "boom"));

        AssertLists(With("a.txt", "b.txt").Check([error, R(1, "b.txt", "a.txt")]), [], [Dup]);
    }

    [Fact]
    public void Check_FolderPathsDifferingOnlyInCase_AreOneFolder()
    {
        var check = new DuplicateTargetCheck(new Dictionary<string, IReadOnlyCollection<string>>
        {
            [@"C:\TEST"] = new[] { "a.txt", "b.txt" },
        });

        AssertLists(
            check.Check([R(0, "a.txt", "x.txt", @"C:\Test"), R(1, "b.txt", "x.txt", @"c:\test")]),
            [Dup], [Dup]);
    }

    [Fact]
    public void Check_FolderKeysDifferingOnlyInCase_AreMerged()
    {
        var check = new DuplicateTargetCheck(new Dictionary<string, IReadOnlyCollection<string>>
        {
            [@"C:\Test"] = new[] { "a.txt" },
            [@"c:\test"] = new[] { "x.txt" },
        });

        AssertLists(check.Check([R(0, "a.txt", "x.txt")]), [[Exists]]);
    }

    [Fact]
    public void Check_FolderWithoutAList_Throws()
    {
        var check = new DuplicateTargetCheck(new Dictionary<string, IReadOnlyCollection<string>>());

        Assert.Throws<InvalidOperationException>(() => check.Check([R(0, "a.txt", "x.txt")]));
    }

    [Fact]
    public void Check_ReturnsOneListPerResultInOrder() =>
        AssertLists(
            With("a", "b", "c", "x").Check([R(0, "a", "x"), R(1, "b", "y"), R(2, "c", "c")]),
            [Exists], [], []);

    [Fact]
    public void Check_WithThePipeline_FlagsBothItems()
    {
        var pipeline = new RenamePipeline([new DelegateRule(RuleStage.Name, (n, _) => n.WithBaseName("x"))]);
        var results = pipeline.Run(Items.Files("a.txt", "b.txt"));
        var check = new DuplicateTargetCheck(new Dictionary<string, IReadOnlyCollection<string>>
        {
            [@"C:\Test"] = new[] { "a.txt", "b.txt" },
        });

        var report = ConflictChecker.Run(results, [check]);

        Assert.Equal([Dup], report.ForItem(0));
        Assert.Equal([Dup], report.ForItem(1));
    }

    [Fact]
    public void Constructor_RejectsNull() =>
        Assert.Throws<ArgumentNullException>(() => new DuplicateTargetCheck(null!));

    [Fact]
    public void Check_RejectsNull() =>
        Assert.Throws<ArgumentNullException>(() => With().Check(null!));

    [Fact]
    public void Forms_AreDifferentStringsWithOneNfc()
    {
        Assert.NotEqual(NfcCafe, NfdCafe);
        Assert.Equal(NfcCafe, NfdCafe.Normalize(NormalizationForm.FormC));
    }

    [Fact]
    public void Check_TwoFormsOfOneName_FlagsBothDuplicateTarget() =>
        AssertLists(
            With("a.txt", "b.txt").Check([R(0, "a.txt", NfcCafe), R(1, "b.txt", NfdCafe)]),
            [Dup], [Dup]);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Check_OtherFormOfAnExistingEntry_FlagsTargetExists(bool entryIsNfc)
    {
        var entry = entryIsNfc ? NfcCafe : NfdCafe;
        var target = entryIsNfc ? NfdCafe : NfcCafe;

        AssertLists(With("a.txt", entry).Check([R(0, "a.txt", target)]), [[Exists]]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Check_FormsAndCaseDiffer_FlagsDuplicate(int pair)
    {
        var first = pair == 0 ? NfcCafeUpper : NfdCafeUpper;
        var second = pair == 0 ? NfdCafe : NfcCafe;

        AssertLists(
            With("a.txt", "b.txt").Check([R(0, "a.txt", first), R(1, "b.txt", second)]),
            [Dup], [Dup]);
    }

    [Fact]
    public void Check_FormsAndCaseDiffer_ExistingEntry_FlagsTargetExists() =>
        AssertLists(With("a.txt", NfdCafeUpper).Check([R(0, "a.txt", NfcCafe)]), [[Exists]]);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Check_ChangeToTheOtherFormOnly_IsNotFlagged(bool fromNfc)
    {
        var from = fromNfc ? NfcCafe : NfdCafe;
        var to = fromNfc ? NfdCafe : NfcCafe;

        AssertLists(With(from).Check([R(0, from, to)]), [[]]);
    }

    [Fact]
    public void Check_ChainThroughTheOtherForm_IsNotFlagged() =>
        AssertLists(
            With("a.txt", NfdCafe).Check([R(0, "a.txt", NfcCafe), R(1, NfdCafe, "z.txt")]),
            [], []);

    [Fact]
    public void Check_LoneSurrogate_DoesNotThrow_ComparedAsIs()
    {
        var check = With("a.txt", "b.txt");

        AssertLists(
            check.Check([R(0, "a.txt", "x\uD800.txt"), R(1, "b.txt", "x\uD800.txt")]),
            [Dup], [Dup]);
        AssertLists(
            check.Check([R(0, "a.txt", "x\uD800.txt"), R(1, "b.txt", "X\uD800.txt")]),
            [Dup], [Dup]);
    }

    [Fact]
    public void Check_LoneSurrogate_ExistingEntry()
    {
        var check = With("a.txt", "x\uDC00.txt");

        AssertLists(check.Check([R(0, "a.txt", "X\uDC00.txt")]), [[Exists]]);
        AssertLists(check.Check([R(0, "a.txt", "x\uDC01.txt")]), [[]]);
    }

    [Fact]
    public void Check_CompatibilityForms_AreDifferent() =>
        AssertLists(
            With("a.txt", "b.txt").Check([R(0, "a.txt", "\uFB01le.txt"), R(1, "b.txt", "file.txt")]),
            [], []);

    [Fact]
    public void Check_TenThousandNfdNames_UnderOneSecond()
    {
        const int Count = 10000;
        var existing = new string[Count * 2];
        var results = new RenameResult[Count];
        for (var i = 0; i < Count; i++)
        {
            existing[i] = "a" + i + ".txt";
            existing[Count + i] = "\u00e9x" + i;
            results[i] = R(i, "a" + i + ".txt", "e\u0301" + i + ".txt");
        }

        var check = With(existing);
        var watch = Stopwatch.StartNew();
        var found = check.Check(results);
        watch.Stop();

        Assert.All(found, list => Assert.Empty(list));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), $"Took {watch.Elapsed}.");
    }
}
