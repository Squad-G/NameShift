using System;
using System.Globalization;
using System.Linq;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class AutoDateRuleTests
{
    private static readonly DateTime Mod = new DateTime(2024, 3, 9, 14, 5, 7);

    private static RenameItem Item(string name = "a.txt", ItemKind kind = ItemKind.File,
        DateTime? created = null, DateTime? modified = null,
        ItemAttributes attributes = ItemAttributes.None) =>
        new RenameItem(name, @"C:\Test", kind, attributes, 0, created ?? default, modified ?? Mod);

    private static RenameResult[] Run(AutoDateRule rule, params RenameItem[] items) =>
        new RenamePipeline([rule]).Run(items).ToArray();

    private static string RunName(AutoDateRule rule, RenameItem? item = null) =>
        Run(rule, item ?? Item())[0].NewName;

    [Fact]
    public void Prefix_Default() =>
        Assert.Equal("2024-03-09_a.txt", RunName(new AutoDateRule()));

    [Fact]
    public void Suffix_DateAndTime() =>
        Assert.Equal("a_20240309_140507.txt",
            RunName(new AutoDateRule(format: "yyyyMMdd_HHmmss", placement: DatePlacement.Suffix)));

    [Fact]
    public void Source_CreatedOrModified()
    {
        var item = Item(created: new DateTime(2020, 1, 2, 10, 0, 0), modified: Mod);

        Assert.Equal("2020-01-02_a.txt", RunName(new AutoDateRule(DateSource.Created), item));
        Assert.Equal("2024-03-09_a.txt", RunName(new AutoDateRule(DateSource.Modified), item));
    }

    [Theory]
    [InlineData("HH:mm", "14:05_a.txt")]
    [InlineData("yyyy/MM/dd", "2024/03/09_a.txt")]
    public void ForbiddenCharacter_AppliedAndFlagged(string format, string expected)
    {
        var result = Run(new AutoDateRule(format: format), Item())[0];

        Assert.Equal(expected, result.NewName);
        var conflicts = new InvalidNameCheck().Check([result])[0];
        Assert.Contains(conflicts, c => c.Code == ConflictCode.InvalidCharacter);
    }

    [Fact]
    public void InvariantCulture_UnderFrench()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            Assert.Equal("March 09_a.txt", RunName(new AutoDateRule(format: "MMMM dd")));
            Assert.Equal("Saturday_a.txt", RunName(new AutoDateRule(format: "dddd")));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void GregorianYear_UnderThai()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("th-TH");

            Assert.Equal("2024_a.txt", RunName(new AutoDateRule(format: "yyyy")));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void UtcKind_NotConverted()
    {
        var rule = new AutoDateRule(format: "HH-mm-ss");

        Assert.Equal("14-05-07_a.txt",
            RunName(rule, Item(modified: DateTime.SpecifyKind(Mod, DateTimeKind.Utc))));
        Assert.Equal("14-05-07_a.txt",
            RunName(rule, Item(modified: DateTime.SpecifyKind(Mod, DateTimeKind.Local))));
    }

    [Fact]
    public void NoDate_ItemErrorOthersFine()
    {
        var results = Run(new AutoDateRule(),
            Item("a.txt"), new RenameItem("b.txt", @"C:\Test"), Item("c.txt"));

        Assert.Equal("2024-03-09_a.txt", results[0].NewName);
        Assert.Equal("2024-03-09_c.txt", results[2].NewName);
        Assert.Equal(RenameStatus.Error, results[1].Status);
        Assert.Equal("The item has no date to use.", results[1].Error!.Message);
        Assert.Equal("b.txt", results[1].NewName);
    }

    [Fact]
    public void NoDate_Created_ItemError()
    {
        var result = Run(new AutoDateRule(DateSource.Created), Item())[0];

        Assert.Equal(RenameStatus.Error, result.Status);
        Assert.Equal("a.txt", result.NewName);
    }

    [Theory]
    [InlineData("'abc")]
    [InlineData(@"yyyy\")]
    [InlineData("K")]
    [InlineData("z")]
    [InlineData("zz")]
    [InlineData("zzz")]
    [InlineData("zzzz")]
    [InlineData("zzzzzzz")]
    [InlineData("yyyy-MM-dd zzz")]
    [InlineData("%z")]
    [InlineData("\"q")]
    public void Validate_Refused(string format)
    {
        var message = AutoDateRule.Validate(format);

        Assert.NotNull(message);
        Assert.False(string.IsNullOrWhiteSpace(message));
        var ex = Assert.Throws<ArgumentException>(() => new AutoDateRule(format: format));
        Assert.Equal("format", ex.ParamName);
    }

    [Theory]
    [InlineData("yyyy-MM-dd")]
    [InlineData("")]
    [InlineData("yyyy'z'")]
    [InlineData(@"yyyy\z")]
    [InlineData("yyyy\"z\"")]
    [InlineData("'K'")]
    [InlineData(@"'a\'z'")]
    public void Validate_Accepted(string format) =>
        Assert.Null(AutoDateRule.Validate(format));

    [Fact]
    public void FormatDate_Cases()
    {
        Assert.Equal("", AutoDateRule.FormatDate(Mod, ""));
        Assert.Equal("2024z", AutoDateRule.FormatDate(Mod, "yyyy'z'"));
        Assert.Throws<FormatException>(() => AutoDateRule.FormatDate(Mod, "zzz"));
        Assert.Throws<FormatException>(() => AutoDateRule.FormatDate(Mod, "K"));
    }

    [Fact]
    public void Nulls_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => AutoDateRule.Validate(null!));
        Assert.Throws<ArgumentNullException>(() => AutoDateRule.FormatDate(Mod, null!));
        Assert.Throws<ArgumentNullException>(() => new AutoDateRule(format: null!));
    }

    [Fact]
    public void EmptyFormat_Disabled()
    {
        var rule = new AutoDateRule(format: "");
        var parts = new NameParts("a", "txt");

        Assert.False(rule.Enabled);
        Assert.Equal(RenameStatus.Unchanged, Run(rule, Item())[0].Status);
        Assert.Equal(parts, rule.Apply(parts, new ItemContext(Item(), 0, 1)));
    }

    [Fact]
    public void EmptyQuotedFormat_Unchanged()
    {
        var result = Run(new AutoDateRule(format: "''"), Item())[0];

        Assert.Equal("a.txt", result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Theory]
    [InlineData("d", "9")]
    [InlineData("M", "3")]
    [InlineData("yyyy", "2024")]
    public void OneCharacterFormat_IsCustom(string format, string expected)
    {
        Assert.Equal(expected, AutoDateRule.FormatDate(Mod, format));
        Assert.Equal(expected + "_a.txt", RunName(new AutoDateRule(format: format)));
    }

    [Fact]
    public void EmptySeparator() =>
        Assert.Equal("20240309a.txt", RunName(new AutoDateRule(format: "yyyyMMdd", separator: "")));

    [Fact]
    public void NullSeparator_Throws()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new AutoDateRule(separator: null!));

        Assert.Equal("separator", ex.ParamName);
    }

    [Fact]
    public void UndefinedEnums_Throw()
    {
        var source = Assert.Throws<ArgumentOutOfRangeException>(() => new AutoDateRule((DateSource)9));
        var placement = Assert.Throws<ArgumentOutOfRangeException>(
            () => new AutoDateRule(placement: (DatePlacement)9));

        Assert.Equal("source", source.ParamName);
        Assert.Equal("placement", placement.ParamName);
    }

    [Fact]
    public void Dotfile_WholeNameIsBase() =>
        Assert.Equal("2024-03-09_.gitignore",
            RunName(new AutoDateRule(), Item(".gitignore", attributes: ItemAttributes.Hidden)));

    [Fact]
    public void Folder_SuffixNoExtension() =>
        Assert.Equal("Sub_2024-03-09",
            RunName(new AutoDateRule(placement: DatePlacement.Suffix), Item("Sub", ItemKind.Folder)));

    [Fact]
    public void Pipeline_StageOrder()
    {
        IRenameRule[] rules =
        [
            new NumberingRule(NumberingMode.Suffix, separator: "_"),
            new AutoDateRule(placement: DatePlacement.Suffix),
            new AddRule(suffix: "-x"),
        ];

        var result = new RenamePipeline(rules).Run([Item()])[0];

        Assert.Equal("a-x_2024-03-09_1.txt", result.NewName);
    }

    [Fact]
    public void Properties_ReturnTheValuesGiven()
    {
        var rule = new AutoDateRule(DateSource.Created, "yyyy", DatePlacement.Suffix, "-");

        Assert.Equal(DateSource.Created, rule.Source);
        Assert.Equal("yyyy", rule.Format);
        Assert.Equal(DatePlacement.Suffix, rule.Placement);
        Assert.Equal("-", rule.Separator);

        var defaults = new AutoDateRule();

        Assert.Equal(DateSource.Modified, defaults.Source);
        Assert.Equal("yyyy-MM-dd", defaults.Format);
        Assert.Equal(DatePlacement.Prefix, defaults.Placement);
        Assert.Equal("_", defaults.Separator);
        Assert.Equal(RuleStage.AutoDate, defaults.Stage);
    }
}
