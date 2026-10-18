using System;
using System.Globalization;
using System.Linq;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class CaseRuleTests
{
    private static RenameResult RunOne(CaseRule rule, string name) =>
        new RenamePipeline([rule]).Run(Items.Files(name))[0];

    private static CaseRule Title(string? exceptions = null) =>
        new(CaseMode.Title, CaseRule.ParseExceptions(exceptions));

    [Fact]
    public void Run_Lower_LeavesTheExtensionAlone() =>
        Assert.Equal("img_001.JPG", RunOne(new CaseRule(CaseMode.Lower), "IMG_001.JPG").NewName);

    [Fact]
    public void Run_Upper_LeavesTheExtensionAlone() =>
        Assert.Equal("MY FILE.txt", RunOne(new CaseRule(CaseMode.Upper), "my file.txt").NewName);

    [Fact]
    public void Run_Title_CapitalisesEveryWord() =>
        Assert.Equal("The Lord Of The Rings.txt", RunOne(Title(), "the lord of the rings.txt").NewName);

    [Fact]
    public void Run_Title_LowersTheRestOfEachWord() =>
        Assert.Equal("The Lord.txt", RunOne(Title(), "tHE LORD.txt").NewName);

    [Fact]
    public void Run_Title_SplitsWordsAtSeparators() =>
        Assert.Equal("My_Holiday-Photo (2).jpg", RunOne(Title(), "my_holiday-photo (2).jpg").NewName);

    [Fact]
    public void Run_Title_KeepsApostrophesInsideAWord() =>
        Assert.Equal("Don't Stop.txt", RunOne(Title(), "don't stop.txt").NewName);

    [Fact]
    public void Run_Title_DoesNotCapitaliseAfterALeadingDigit() =>
        Assert.Equal("3rd Place.txt", RunOne(Title(), "3rd place.txt").NewName);

    [Fact]
    public void Run_Title_KeepsCombiningMarksInsideAWord() =>
        Assert.Equal("E\u0301cole Normale.txt", RunOne(Title(), "e\u0301cole normale.txt").NewName);

    [Fact]
    public void Run_Title_AFolderNameIsCasedWhole()
    {
        var folder = new RenameItem("my.old photos", @"C:\Test", ItemKind.Folder);

        var result = new RenamePipeline([Title()]).Run([folder])[0];

        Assert.Equal("My.Old Photos", result.NewName);
    }

    [Fact]
    public void Run_Sentence_CapitalisesTheNameAndAfterAPeriodAndSpace() =>
        Assert.Equal(
            "Hello world. Second sentence.txt",
            RunOne(new CaseRule(CaseMode.Sentence), "HELLO world. second SENTENCE.txt").NewName);

    [Fact]
    public void Run_Sentence_APeriodWithoutASpaceStartsNoSentence() =>
        Assert.Equal("V1.final draft.txt", RunOne(new CaseRule(CaseMode.Sentence), "v1.final draft.txt").NewName);

    [Fact]
    public void Run_Sentence_OnlyAPeriodEndsASentence() =>
        Assert.Equal("Wow! really? yes.txt", RunOne(new CaseRule(CaseMode.Sentence), "wow! really? yes.txt").NewName);

    [Fact]
    public void Run_Sentence_SkipsLeadingPunctuationButNotDigits() =>
        Assert.Equal(
            "(Draft) notes. 2nd try.txt",
            RunOne(new CaseRule(CaseMode.Sentence), "(draft) notes. 2nd TRY.txt").NewName);

    [Fact]
    public void Run_Sentence_IgnoresTheExceptions() =>
        Assert.Equal(
            "Usa trip.txt",
            RunOne(new CaseRule(CaseMode.Sentence, CaseRule.ParseExceptions("USA")), "usa trip.txt").NewName);

    [Fact]
    public void Run_Same_LeavesTheNameUnchanged()
    {
        var result = RunOne(new CaseRule(CaseMode.Same), "MiXeD Name.TXT");

        Assert.Equal("MiXeD Name.TXT", result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Theory]
    [InlineData(CaseMode.Same, false)]
    [InlineData(CaseMode.Lower, true)]
    [InlineData(CaseMode.Upper, true)]
    [InlineData(CaseMode.Title, true)]
    [InlineData(CaseMode.Sentence, true)]
    public void Enabled_IsFalseOnlyForSame(CaseMode mode, bool expected) =>
        Assert.Equal(expected, new CaseRule(mode).Enabled);

    [Fact]
    public void Apply_Same_ReturnsTheNameUnchanged()
    {
        var context = new ItemContext(Items.File("a.txt"), 0, 1);

        var result = new CaseRule(CaseMode.Same).Apply(new NameParts("MiXeD", "TXT"), context);

        Assert.Equal(new NameParts("MiXeD", "TXT"), result);
    }

    [Fact]
    public void Run_Title_WritesExceptionsAsListed() =>
        Assert.Equal(
            "The Lord of the USA.txt",
            RunOne(Title("of, the, USA"), "the lord of the usa.txt").NewName);

    [Fact]
    public void Run_Title_MatchesExceptionsIgnoringCase() =>
        Assert.Equal(
            "The Lord of the USA.txt",
            RunOne(Title("of, the, USA"), "THE LORD OF THE USA.txt").NewName);

    [Fact]
    public void Run_Title_AnAllLowercaseExceptionAsFirstWordIsCapitalised() =>
        Assert.Equal("Of Mice.txt", RunOne(Title("of"), "of mice.txt").NewName);

    [Theory]
    [InlineData(CaseMode.Lower, "The USA Trip.txt", "the usa trip.txt")]
    [InlineData(CaseMode.Upper, "the iphone trip.txt", "THE IPHONE TRIP.txt")]
    public void Run_LowerAndUpper_IgnoreTheExceptions(CaseMode mode, string name, string expected) =>
        Assert.Equal(
            expected,
            RunOne(new CaseRule(mode, CaseRule.ParseExceptions("the, USA, iPhone")), name).NewName);

    [Fact]
    public void Run_Title_AnExceptionWithAnUpperCaseLetterIsWrittenAsListedEvenFirst() =>
        Assert.Equal("iPhone Photos.txt", RunOne(Title("iPhone"), "iphone photos.txt").NewName);

    [Fact]
    public void Run_Title_AnExceptionMatchesWholeWordsOnly() =>
        Assert.Equal("The Theory.txt", RunOne(Title("the"), "the theory.txt").NewName);

    [Fact]
    public void Run_Title_TheFirstListedFormWinsOnDuplicates() =>
        Assert.Equal("A usa B.txt", RunOne(Title("usa, USA"), "a usa b.txt").NewName);

    [Fact]
    public void Run_Title_AnEntryWithASeparatorNeverMatches() =>
        Assert.Equal("New York.txt", RunOne(Title("new york"), "new york.txt").NewName);

    [Fact]
    public void ParseExceptions_SplitsTrimsAndDropsBlanks()
    {
        Assert.Equal(["of", "the", "USA"], CaseRule.ParseExceptions("of, the, USA"));
        Assert.Equal(["a", "b"], CaseRule.ParseExceptions(" , a,,b "));
        Assert.Empty(CaseRule.ParseExceptions(null));
        Assert.Empty(CaseRule.ParseExceptions("  "));
    }

    [Fact]
    public void Constructor_NormalisesTheExceptions() =>
        Assert.Equal(["of", "USA"], new CaseRule(CaseMode.Title, [" of ", "", null!, "USA"]).Exceptions.ToArray());

    [Fact]
    public void Run_Lower_HandlesAccentedLetters() =>
        Assert.Equal("école.txt", RunOne(new CaseRule(CaseMode.Lower), "ÉCOLE.txt").NewName);

    [Theory]
    [InlineData(CaseMode.Lower, "TITLE.txt", "title.txt")]
    [InlineData(CaseMode.Upper, "istanbul.txt", "ISTANBUL.txt")]
    [InlineData(CaseMode.Title, "istanbul city.txt", "Istanbul City.txt")]
    public void Run_CasesTheSameWhateverTheCulture(CaseMode mode, string name, string expected)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            Assert.Equal(expected, RunOne(new CaseRule(mode), name).NewName);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Constructor_SetsStageAndDefaults()
    {
        var rule = new CaseRule(CaseMode.Lower);

        Assert.Equal(RuleStage.Case, rule.Stage);
        Assert.Empty(rule.Exceptions);
    }

    [Fact]
    public void Constructor_RejectsAnUndefinedMode() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new CaseRule((CaseMode)99));

    [Fact]
    public void Run_Lower_ANameWithoutExtension() =>
        Assert.Equal("readme", RunOne(new CaseRule(CaseMode.Lower), "README").NewName);
}
