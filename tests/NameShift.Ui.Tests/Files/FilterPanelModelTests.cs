using System;
using System.Globalization;
using NameShift.Core;
using NameShift.Ui.Files;
using Xunit;

namespace NameShift.Ui.Tests.Files;

public sealed class FilterPanelModelTests
{
    private readonly FilterPanelModel _model = new FilterPanelModel();
    private int _changes;

    public FilterPanelModelTests()
    {
        _model.Changed += (_, _) => _changes++;
    }

    /// <summary>Sets the property to a value other than its default, or to its default again.</summary>
    private static void Set(FilterPanelModel model, string property, bool toDefault)
    {
        switch (property)
        {
            case "Mask":
                model.Mask = toDefault ? "" : "*.txt";
                break;
            case "Pattern":
                model.Pattern = toDefault ? "" : "a";
                break;
            case "IgnoreCase":
                model.IgnoreCase = !toDefault;
                break;
            case "IncludeFiles":
                model.IncludeFiles = toDefault;
                break;
            case "IncludeFolders":
                model.IncludeFolders = toDefault;
                break;
            case "IncludeHidden":
                model.IncludeHidden = !toDefault;
                break;
            case "MinLengthText":
                model.MinLengthText = toDefault ? "" : "2";
                break;
            case "MaxLengthText":
                model.MaxLengthText = toDefault ? "" : "9";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(property), property, "Unknown property.");
        }
    }

    // AC 1

    [Fact]
    public void New_IsDefault_AndFilterBehavesAsDefault()
    {
        Assert.Equal("", _model.Mask);
        Assert.Equal("", _model.Pattern);
        Assert.False(_model.IgnoreCase);
        Assert.True(_model.IncludeFiles);
        Assert.True(_model.IncludeFolders);
        Assert.False(_model.IncludeHidden);
        Assert.Equal("", _model.MinLengthText);
        Assert.Equal("", _model.MaxLengthText);
        Assert.True(_model.IsDefault);
        Assert.Null(_model.Error);
        Assert.Null(_model.Filter.MinLength);
        Assert.Null(_model.Filter.MaxLength);

        var items = new[]
        {
            new RenameItem("a.txt", @"C:\A", ItemKind.File),
            new RenameItem("d", @"C:\A", ItemKind.Folder),
            new RenameItem("h.txt", @"C:\A", ItemKind.File, ItemAttributes.Hidden),
            new RenameItem("s", @"C:\A", ItemKind.Folder, ItemAttributes.System),
            new RenameItem("a.b.c", @"C:\A", ItemKind.File),
        };
        Assert.Equal(new ItemFilter().Apply(items), _model.Filter.Apply(items));
        Assert.Equal(new[] { items[0], items[1], items[4] }, _model.Filter.Apply(items));
    }

    // AC 2

    [Theory]
    [InlineData("Mask")]
    [InlineData("Pattern")]
    [InlineData("IgnoreCase")]
    [InlineData("IncludeFiles")]
    [InlineData("IncludeFolders")]
    [InlineData("IncludeHidden")]
    [InlineData("MinLengthText")]
    [InlineData("MaxLengthText")]
    public void EachProperty_Changed_RaisesOnce_NotDefault(string property)
    {
        Set(_model, property, toDefault: false);

        Assert.True(_changes == 1, property);
        Assert.False(_model.IsDefault, property);
    }

    [Theory]
    [InlineData("Mask")]
    [InlineData("Pattern")]
    [InlineData("IgnoreCase")]
    [InlineData("IncludeFiles")]
    [InlineData("IncludeFolders")]
    [InlineData("IncludeHidden")]
    [InlineData("MinLengthText")]
    [InlineData("MaxLengthText")]
    public void EachProperty_SameValue_RaisesNothing(string property)
    {
        Set(_model, property, toDefault: true);

        Assert.True(_changes == 0, property);
        Assert.True(_model.IsDefault, property);
    }

    [Fact]
    public void SameValueAgain_RaisesNothing()
    {
        _model.Mask = "*.txt";
        _model.Mask = "*.txt";
        _model.IncludeHidden = true;
        _model.IncludeHidden = true;

        Assert.Equal(2, _changes);
    }

    [Fact]
    public void NullText_IsEmpty()
    {
        _model.Mask = null!;
        _model.Pattern = null!;
        _model.MinLengthText = null!;
        _model.MaxLengthText = null!;

        Assert.Equal(0, _changes);
        Assert.Equal("", _model.Mask);
        Assert.Equal("", _model.Pattern);
        Assert.Equal("", _model.MinLengthText);
        Assert.Equal("", _model.MaxLengthText);
        Assert.True(_model.IsDefault);
    }

    [Fact]
    public void WhiteSpaceMask_IsNotDefault()
    {
        _model.Mask = " ";

        Assert.False(_model.IsDefault);
        Assert.Equal(" ", _model.Mask);
    }

    [Fact]
    public void Reset_RestoresDefaults_RaisesOnce()
    {
        _model.Mask = "*.txt";
        _model.Pattern = "(";
        _model.IgnoreCase = true;
        _model.IncludeFiles = false;
        _model.IncludeFolders = false;
        _model.IncludeHidden = true;
        _model.MinLengthText = "x";
        _model.MaxLengthText = "3";
        _changes = 0;

        _model.Reset();

        Assert.Equal(1, _changes);
        Assert.True(_model.IsDefault);
        Assert.Null(_model.Error);
        Assert.Equal("", _model.Mask);
        Assert.Equal("", _model.Pattern);
        Assert.False(_model.IgnoreCase);
        Assert.True(_model.IncludeFiles);
        Assert.True(_model.IncludeFolders);
        Assert.False(_model.IncludeHidden);
        Assert.Equal("", _model.MinLengthText);
        Assert.Equal("", _model.MaxLengthText);
        Assert.Equal("", _model.Filter.Mask);
        Assert.Null(_model.Filter.MaxLength);
    }

    [Fact]
    public void Reset_WhenDefault_RaisesNothing()
    {
        ItemFilter before = _model.Filter;

        _model.Reset();

        Assert.Equal(0, _changes);
        Assert.Same(before, _model.Filter);
    }

    // AC 3

    [Fact]
    public void Lengths_Parsed()
    {
        _model.MinLengthText = "5";
        _model.MaxLengthText = "8";

        Assert.Equal(5, _model.Filter.MinLength);
        Assert.Equal(8, _model.Filter.MaxLength);
        Assert.Null(_model.Error);

        _model.MinLengthText = " 7 ";
        Assert.Equal(7, _model.Filter.MinLength);
        Assert.Equal(" 7 ", _model.MinLengthText);

        _model.MinLengthText = "0";
        Assert.Equal(0, _model.Filter.MinLength);

        _model.MinLengthText = "007";
        Assert.Equal(7, _model.Filter.MinLength);

        _model.MinLengthText = "   ";
        Assert.Null(_model.Filter.MinLength);
        Assert.Null(_model.Error);
    }

    [Theory]
    [InlineData("abc", true)]
    [InlineData("-1", true)]
    [InlineData("1.5", true)]
    [InlineData("+5", true)]
    [InlineData("99999999999", true)]
    [InlineData("1,5", true)]
    [InlineData("\u0665", true)]
    [InlineData("5 5", true)]
    [InlineData("abc", false)]
    [InlineData("-1", false)]
    [InlineData("1.5", false)]
    [InlineData("+5", false)]
    [InlineData("99999999999", false)]
    [InlineData("1,5", false)]
    [InlineData("\u0665", false)]
    [InlineData("5 5", false)]
    public void InvalidLength_Ignored_WithError(string text, bool min)
    {
        CultureInfo culture = CultureInfo.CurrentCulture;
        try
        {
            // A culture with a decimal comma: "1,5" must still be invalid.
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            if (min)
            {
                _model.MinLengthText = text;
            }
            else
            {
                _model.MaxLengthText = text;
            }

            Assert.Null(min ? _model.Filter.MinLength : _model.Filter.MaxLength);
            Assert.Equal(Strings.FilterLengthInvalid, _model.Error);
            Assert.Equal(text, min ? _model.MinLengthText : _model.MaxLengthText);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void BothLengthsInvalid_OneMessage()
    {
        _model.MinLengthText = "x";
        _model.MaxLengthText = "-2";

        Assert.Equal(Strings.FilterLengthInvalid, _model.Error);
        Assert.Null(_model.Filter.MinLength);
        Assert.Null(_model.Filter.MaxLength);
    }

    [Fact]
    public void InvalidLength_ThenFixed_ErrorCleared()
    {
        _model.MinLengthText = "x";
        _model.MinLengthText = "3";

        Assert.Null(_model.Error);
        Assert.Equal(3, _model.Filter.MinLength);
    }

    // AC 4

    [Fact]
    public void InvalidPattern_ErrorIsTheFilters()
    {
        _model.Pattern = "(";

        Assert.NotNull(_model.Error);
        Assert.Equal(_model.Filter.Error, _model.Error);
    }

    [Fact]
    public void MinAboveMax_ErrorIsRangeText()
    {
        _model.MinLengthText = "9";
        _model.MaxLengthText = "3";

        Assert.Equal(ItemFilter.LengthRangeError, _model.Error);
    }

    [Fact]
    public void SeveralProblems_JoinedBySpace()
    {
        _model.Pattern = "(";
        _model.MinLengthText = "x";

        Assert.NotNull(_model.Filter.Error);
        Assert.Equal(_model.Filter.Error + " " + Strings.FilterLengthInvalid, _model.Error);
    }

    [Fact]
    public void PatternAndRange_BothFromTheFilter()
    {
        _model.Pattern = "(";
        _model.MinLengthText = "9";
        _model.MaxLengthText = "3";

        Assert.Equal(_model.Filter.Error, _model.Error);
        Assert.EndsWith(" " + ItemFilter.LengthRangeError, _model.Error);
    }

    // AC 5

    [Fact]
    public void Change_BuildsANewFilter_ReadyBeforeChanged()
    {
        ItemFilter before = _model.Filter;
        ItemFilter? seen = null;
        _model.Changed += (_, _) => seen = _model.Filter;

        _model.Mask = "*.jpg";

        Assert.NotNull(seen);
        Assert.NotSame(before, seen);
        Assert.Equal("*.jpg", seen!.Mask);
        Assert.Same(seen, _model.Filter);

        _model.Mask = "*.jpg";
        Assert.Same(seen, _model.Filter);
    }

    [Fact]
    public void Filter_CarriesEverySetting()
    {
        _model.Mask = "*.txt";
        _model.Pattern = "^a";
        _model.IgnoreCase = true;
        _model.IncludeFiles = false;
        _model.IncludeFolders = false;
        _model.IncludeHidden = true;
        _model.MinLengthText = "1";
        _model.MaxLengthText = "4";

        ItemFilter filter = _model.Filter;
        Assert.Equal("*.txt", filter.Mask);
        Assert.Equal("^a", filter.Pattern);
        Assert.True(filter.IgnoreCase);
        Assert.False(filter.IncludeFiles);
        Assert.False(filter.IncludeFolders);
        Assert.True(filter.IncludeHidden);
        Assert.Equal(1, filter.MinLength);
        Assert.Equal(4, filter.MaxLength);
    }

    [Fact]
    public void Strings_Texts()
    {
        Assert.Equal("No item matches the filters.", Strings.NoItemMatchesFilter);
        Assert.Equal("Enter a whole number for the length.", Strings.FilterLengthInvalid);
    }
}
