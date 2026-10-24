using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class ItemFilterTests
{
    private static RenameItem File(string name, ItemAttributes a = ItemAttributes.None) =>
        new(name, @"C:\x", ItemKind.File, a);

    private static RenameItem Folder(string name, ItemAttributes a = ItemAttributes.None) =>
        new(name, @"C:\x", ItemKind.Folder, a);

    private static string[] Names(ItemFilter f, params RenameItem[] items) =>
        f.Apply(items).Select(i => i.Name).ToArray();

    private static void WithCulture(string name, Action action)
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(name);
            CultureInfo.CurrentUICulture = new CultureInfo(name);
            action();
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }

    [Fact]
    public void New_Defaults()
    {
        var f = new ItemFilter();

        Assert.Equal("", f.Mask);
        Assert.Equal("", f.Pattern);
        Assert.False(f.IgnoreCase);
        Assert.True(f.IncludeFiles);
        Assert.True(f.IncludeFolders);
        Assert.False(f.IncludeHidden);
        Assert.Null(f.MinLength);
        Assert.Null(f.MaxLength);
        Assert.Null(f.Error);
        Assert.True(f.Includes(File("a.txt")));
        Assert.True(f.Includes(Folder("a")));
        Assert.True(f.Includes(File("a.txt", ItemAttributes.ReadOnly)));
        Assert.False(f.Includes(File("a.txt", ItemAttributes.Hidden)));
        Assert.False(f.Includes(File("a.txt", ItemAttributes.System)));
    }

    [Fact]
    public void Ctor_NullMaskOrPattern_Throws()
    {
        Assert.Equal("mask", Assert.Throws<ArgumentNullException>(() => new ItemFilter(mask: null!)).ParamName);
        Assert.Equal("pattern", Assert.Throws<ArgumentNullException>(() => new ItemFilter(pattern: null!)).ParamName);
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(null, -1)]
    public void Ctor_NegativeLength_Throws(int? min, int? max) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ItemFilter(minLength: min, maxLength: max));

    [Fact]
    public void Includes_Null_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new ItemFilter().Includes(null!));

    [Fact]
    public void Apply_Null_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new ItemFilter().Apply(null!));

    [Fact]
    public void Apply_NullItem_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new ItemFilter().Apply([File("a"), null!]));

    [Fact]
    public void Apply_SameInstancesSameOrder()
    {
        RenameItem c = File("c.txt");
        RenameItem a = File("a.txt");
        RenameItem b = File("b.gif");
        RenameItem[] input = [c, a, b];

        IReadOnlyList<RenameItem> result = new ItemFilter("*.txt").Apply(input);

        Assert.Equal(2, result.Count);
        Assert.Same(c, result[0]);
        Assert.Same(a, result[1]);
        Assert.NotSame(input, result);

        IReadOnlyList<RenameItem> all = new ItemFilter().Apply(input);
        Assert.Equal(3, all.Count);
        Assert.NotSame(input, all);
    }

    [Theory]
    [InlineData("a.jpg", true)]
    [InlineData("B.PNG", true)]
    [InlineData(".jpg", true)]
    [InlineData("a.jpg.bak", false)]
    [InlineData("jpg", false)]
    [InlineData("a.gif", false)]
    public void Mask_Extensions(string name, bool expected) =>
        Assert.Equal(expected, new ItemFilter("*.jpg;*.png").Includes(File(name)));

    [Theory]
    [InlineData("*.jpg ; *.png")]
    [InlineData(";*.jpg;;*.png")]
    [InlineData("  *.jpg;*.png  ")]
    public void Mask_SpacesAndEmptyParts_Ignored(string mask)
    {
        var f = new ItemFilter(mask);

        Assert.True(f.Includes(File("a.jpg")));
        Assert.True(f.Includes(File("a.png")));
        Assert.False(f.Includes(File("a.gif")));
    }

    [Fact]
    public void Mask_IgnoresCase_Invariant() =>
        WithCulture("tr-TR", () =>
        {
            Assert.True(new ItemFilter("*.JPG").Includes(File("a.jpg")));
            Assert.True(new ItemFilter("*.JPG").Includes(File("I.jpg")));
            Assert.True(new ItemFilter("\u00e9*").Includes(File("\u00c9t\u00e9.txt")));
        });

    [Theory]
    [InlineData("IMG_001.jpg", true)]
    [InlineData("IMG_01.jpg", false)]
    [InlineData("IMG_0001.jpg", false)]
    public void Mask_QuestionMark_ExactlyOne(string name, bool expected) =>
        Assert.Equal(expected, new ItemFilter("IMG_???.jpg").Includes(File(name)));

    [Theory]
    [InlineData("a*", "a", true)]
    [InlineData("a*", "a.txt", true)]
    [InlineData("a*", "ba", false)]
    [InlineData("*a*b*", "xaxbx", true)]
    [InlineData("*a*b*", "xbxax", false)]
    public void Mask_Star_AnyTextEmptyIncluded(string mask, string name, bool expected) =>
        Assert.Equal(expected, new ItemFilter(mask).Includes(File(name)));

    [Theory]
    [InlineData("a(1).txt", "a(1).txt", true)]
    [InlineData("a(1).txt", "A(1).TXT", true)]
    [InlineData("a(1).txt", "a1.txt", false)]
    [InlineData("a(1).txt", "a(1)xtxt", false)]
    [InlineData("[ab].txt", "[ab].txt", true)]
    [InlineData("[ab].txt", "a.txt", false)]
    [InlineData("a+$.txt", "a+$.txt", true)]
    [InlineData("a+$.txt", "aa.txt", false)]
    public void Mask_OtherCharactersLiteral(string mask, string name, bool expected) =>
        Assert.Equal(expected, new ItemFilter(mask).Includes(File(name)));

    [Theory]
    [InlineData("*.*")]
    [InlineData("*")]
    [InlineData("x.y;*.*")]
    public void Mask_StarDotStar_And_Star_MatchAll(string mask)
    {
        var f = new ItemFilter(mask);

        Assert.True(f.Includes(File("a")));
        Assert.True(f.Includes(File("abc")));
        Assert.True(f.Includes(File("a.b")));
        Assert.True(f.Includes(Folder("Trip 2024")));
    }

    [Theory]
    [InlineData("a", false)]
    [InlineData("abc", false)]
    [InlineData("a.", true)]
    [InlineData("abc.txt", true)]
    public void Mask_AStarDotStar_IsLiteralDot(string name, bool expected) =>
        Assert.Equal(expected, new ItemFilter("a*.*").Includes(File(name)));

    [Theory]
    [InlineData("   ")]
    [InlineData(";;")]
    [InlineData(" ; ")]
    public void Mask_OnlySpacesOrSemicolons_NoCondition(string mask)
    {
        var f = new ItemFilter(mask);

        Assert.True(f.Includes(File("a.gif")));
        Assert.True(f.Includes(Folder("x")));
    }

    [Theory]
    [InlineData("readme.txt", true)]
    [InlineData("README.TXT", true)]
    [InlineData("readme.txt.bak", false)]
    [InlineData("xreadme.txt", false)]
    public void Mask_NoWildcard_OnlyThatName(string name, bool expected) =>
        Assert.Equal(expected, new ItemFilter("readme.txt").Includes(File(name)));

    [Fact]
    public void Mask_AppliesToFolders()
    {
        Assert.True(new ItemFilter("Trip*").Includes(Folder("Trip 2024")));
        Assert.False(new ItemFilter("*.jpg").Includes(Folder("Trip 2024")));
    }

    [Fact]
    public void Pattern_SearchOnBaseName()
    {
        var f = new ItemFilter(pattern: @"^IMG_\d+");

        Assert.True(f.Includes(File("IMG_042.jpg")));
        Assert.False(f.Includes(File("img_1.jpg")));
        Assert.False(f.Includes(File("a_IMG_1.jpg")));

        var ci = new ItemFilter(pattern: @"^IMG_\d+", ignoreCase: true);

        Assert.True(ci.Includes(File("IMG_042.jpg")));
        Assert.True(ci.Includes(File("img_1.jpg")));
        Assert.False(ci.Includes(File("a_IMG_1.jpg")));

        Assert.True(new ItemFilter(pattern: "42").Includes(File("IMG_042.jpg")));
    }

    [Fact]
    public void Pattern_CultureInvariant() =>
        WithCulture("tr-TR", () =>
            Assert.True(new ItemFilter(pattern: "i", ignoreCase: true).Includes(File("I.txt"))));

    [Fact]
    public void Pattern_ExtensionNotSearched()
    {
        var f = new ItemFilter(pattern: "jpg");

        Assert.False(f.Includes(File("a.jpg")));
        Assert.True(f.Includes(Folder("a.jpg")));
        Assert.True(f.Includes(File(".jpg")));
    }

    [Theory]
    [InlineData("(")]
    [InlineData("[a-")]
    [InlineData("*a")]
    public void Pattern_Invalid_ErrorAndPartIgnored(string pattern)
    {
        var f = new ItemFilter(pattern: pattern);

        Assert.False(string.IsNullOrEmpty(f.Error));
        Assert.True(f.Includes(File("a.txt")));
        Assert.False(f.Includes(File("a.txt", ItemAttributes.Hidden)));
        Assert.False(new ItemFilter("*.gif", pattern).Includes(File("a.txt")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("^IMG")]
    public void Pattern_ValidOrEmpty_NoError(string pattern) =>
        Assert.Null(new ItemFilter(pattern: pattern).Error);

    [Fact]
    public void Pattern_Timeout_ItemLeftOut_NoThrow()
    {
        var f = new ItemFilter(pattern: "(a+)+$");

        var stopwatch = Stopwatch.StartNew();
        string[] names = Names(f, File("aaaa.txt"), File(new string('a', 40) + "b.txt"), File("b.txt"));
        stopwatch.Stop();

        Assert.Equal(["aaaa.txt"], names);
        Assert.Null(f.Error);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Pattern_Timeout_LeavesOutEvenAMatch()
    {
        const string Pattern = "(a+)+$|b";
        string baseName = new string('a', 40) + "b";
        var probe = new Regex(Pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        Assert.Throws<RegexMatchTimeoutException>(() => probe.IsMatch(baseName));

        Assert.False(new ItemFilter(pattern: Pattern).Includes(File(baseName + ".txt")));
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, false, false)]
    public void Kinds(bool files, bool folders, bool fileIn, bool folderIn)
    {
        var f = new ItemFilter(includeFiles: files, includeFolders: folders);

        Assert.Equal(fileIn, f.Includes(File("a")));
        Assert.Equal(folderIn, f.Includes(Folder("a")));
    }

    [Theory]
    [InlineData(ItemAttributes.Hidden)]
    [InlineData(ItemAttributes.System)]
    [InlineData(ItemAttributes.Hidden | ItemAttributes.System)]
    public void Hidden_HiddenOrSystemOut_UnlessIncluded(ItemAttributes attributes)
    {
        Assert.False(new ItemFilter().Includes(File("a.txt", attributes)));
        Assert.False(new ItemFilter().Includes(Folder("a", attributes)));
        Assert.True(new ItemFilter(includeHidden: true).Includes(File("a.txt", attributes)));
        Assert.True(new ItemFilter(includeHidden: true).Includes(Folder("a", attributes)));
    }

    [Fact]
    public void Hidden_ReadOnlyAndDotNameIgnored()
    {
        var f = new ItemFilter();

        Assert.True(f.Includes(File("a.txt", ItemAttributes.ReadOnly)));
        Assert.True(f.Includes(File(".gitignore")));
    }

    [Theory]
    [InlineData("abcd.txt", false)]
    [InlineData("abcde.txt", true)]
    [InlineData("abcdefgh.txt", true)]
    [InlineData("abcdefghi.txt", false)]
    public void Length_Range(string name, bool expected) =>
        Assert.Equal(expected, new ItemFilter(minLength: 5, maxLength: 8).Includes(File(name)));

    [Fact]
    public void Length_OneSidedOpen()
    {
        var min = new ItemFilter(minLength: 5);
        Assert.True(min.Includes(File("abcdefghijklmnop.txt")));
        Assert.False(min.Includes(File("abcd.txt")));

        var max = new ItemFilter(maxLength: 3);
        Assert.True(max.Includes(File("a.txt")));
        Assert.False(max.Includes(File("abcd.txt")));
    }

    [Fact]
    public void Length_WholeNameForFolderAndDotfile()
    {
        var ten = new ItemFilter(minLength: 10, maxLength: 10);
        Assert.True(ten.Includes(File(".gitignore")));
        Assert.True(ten.Includes(Folder("abcdefghij")));

        Assert.True(new ItemFilter(maxLength: 3).Includes(Folder("a.b")));
        Assert.False(new ItemFilter(minLength: 4).Includes(Folder("a.b")));
    }

    [Fact]
    public void Length_CountsUtf16Units()
    {
        string emoji = "\uD83D\uDE00";

        Assert.False(new ItemFilter(maxLength: 1).Includes(File(emoji + ".txt")));
        Assert.True(new ItemFilter(minLength: 2, maxLength: 2).Includes(File(emoji + ".txt")));
    }

    [Fact]
    public void Length_MinZero_All()
    {
        var f = new ItemFilter(minLength: 0);

        Assert.True(f.Includes(File("a.txt")));
        Assert.True(f.Includes(File(".x")));
    }

    [Fact]
    public void Length_MinOverMax_ErrorAndIgnored()
    {
        var f = new ItemFilter(minLength: 9, maxLength: 2);

        Assert.Equal(ItemFilter.LengthRangeError, f.Error);
        Assert.Equal("The minimum length is larger than the maximum length.", f.Error);
        Assert.True(f.Includes(File("a.txt")));
        Assert.True(f.Includes(File("abcdefghijk.txt")));
        Assert.False(f.Includes(File("a.txt", ItemAttributes.Hidden)));
        Assert.Equal(9, f.MinLength);
        Assert.Equal(2, f.MaxLength);
    }

    [Fact]
    public void Length_MinEqualsMax_Exact()
    {
        var f = new ItemFilter(minLength: 3, maxLength: 3);

        Assert.True(f.Includes(File("abc.txt")));
        Assert.False(f.Includes(File("ab.txt")));
        Assert.False(f.Includes(File("abcd.txt")));
    }

    [Fact]
    public void Errors_BothJoined_PatternFirst()
    {
        var f = new ItemFilter(pattern: "(", minLength: 9, maxLength: 2, includeFolders: false);
        string error = f.Error!;

        Assert.EndsWith(" " + ItemFilter.LengthRangeError, error, StringComparison.Ordinal);
        Assert.True(error.Length > ItemFilter.LengthRangeError.Length + 1);
        Assert.False(error.StartsWith(ItemFilter.LengthRangeError, StringComparison.Ordinal));
        Assert.Equal(RegexRule.Validate("(") + " " + ItemFilter.LengthRangeError, error);
        Assert.True(f.Includes(File("a.txt")));
        Assert.False(f.Includes(Folder("a")));
    }

    [Fact]
    public void Together_AllMustPass()
    {
        var f = new ItemFilter("*.jpg", "^IMG", minLength: 6, includeFolders: false);

        Assert.False(f.Includes(Folder("IMG_001.jpg")));
        Assert.True(f.Includes(File("IMG_01.jpg")));
        Assert.False(f.Includes(File("IMG_1.jpg")));
        Assert.False(f.Includes(File("DSC_0001.jpg")));
    }

    [Fact]
    public void Properties_AsGiven()
    {
        var f = new ItemFilter(" *.jpg ; ", " ^a ", true, false, false, true, 1, 7);

        Assert.Equal(" *.jpg ; ", f.Mask);
        Assert.Equal(" ^a ", f.Pattern);
        Assert.True(f.IgnoreCase);
        Assert.False(f.IncludeFiles);
        Assert.False(f.IncludeFolders);
        Assert.True(f.IncludeHidden);
        Assert.Equal(1, f.MinLength);
        Assert.Equal(7, f.MaxLength);
    }

    [Fact]
    public void ExcludedFile_StillCausesTargetExists()
    {
        RenameItem[] items = [File("a.png"), File("a.jpg")];

        IReadOnlyList<RenameItem> included = new ItemFilter("*.png").Apply(items);

        Assert.Equal(["a.png"], included.Select(i => i.Name).ToArray());

        var results = new RenamePipeline([new ExtensionRule(ExtensionMode.Fixed, "jpg")]).Run(included);
        var check = new DuplicateTargetCheck(new Dictionary<string, IReadOnlyCollection<string>>
        {
            [@"C:\x"] = new[] { "a.png", "a.jpg" },
        });

        ConflictReport report = ConflictChecker.Run(results, [check]);

        Assert.Equal([new Conflict(ConflictCode.TargetExists)], report.ForItem(0));
    }
}
