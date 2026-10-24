using System;
using System.Globalization;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class ExtensionRuleTests
{
    private static RenameResult RunOne(ExtensionRule rule, string name) =>
        new RenamePipeline([rule]).Run(Items.Files(name))[0];

    private static NameParts ApplyTo(ExtensionRule rule, string baseName, string extension) =>
        rule.Apply(new NameParts(baseName, extension), new ItemContext(Items.File(baseName + "." + extension), 0, 1));

    [Fact]
    public void Run_Lower_LowersTheExtension() =>
        Assert.Equal("a.jpg", RunOne(new ExtensionRule(ExtensionMode.Lower), "a.JPG").NewName);

    [Fact]
    public void Run_Upper_UppersTheExtension() =>
        Assert.Equal("a.JPG", RunOne(new ExtensionRule(ExtensionMode.Upper), "a.jpg").NewName);

    [Fact]
    public void Same_ChangesNothing_AndIsDisabled()
    {
        var rule = new ExtensionRule(ExtensionMode.Same);

        Assert.False(rule.Enabled);
        Assert.Equal("a.JPG", RunOne(rule, "a.JPG").NewName);
        Assert.Equal(new NameParts("a", "JPG"), ApplyTo(rule, "a", "JPG"));
    }

    [Fact]
    public void Run_Fixed_ReplacesTheExtension() =>
        Assert.Equal("a.png", RunOne(new ExtensionRule(ExtensionMode.Fixed, "png"), "a.jpg").NewName);

    [Theory]
    [InlineData(".png")]
    [InlineData(" .png ")]
    [InlineData("..png")]
    [InlineData("png.")]
    [InlineData("\tpng\t")]
    [InlineData(" . .png. . ")]
    [InlineData("\t.\u00a0png\r\n.")]
    public void Value_TrimsSpacesAndOuterDots(string typed)
    {
        var rule = new ExtensionRule(ExtensionMode.Fixed, typed);

        Assert.Equal("png", rule.Value);
        Assert.Equal("a.png", RunOne(rule, "a.jpg").NewName);
    }

    [Fact]
    public void Run_Fixed_KeepsAnInnerDot()
    {
        var rule = new ExtensionRule(ExtensionMode.Fixed, "tar.gz");

        Assert.Equal("tar.gz", rule.Value);
        Assert.Equal("a.tar.gz", RunOne(rule, "a.jpg").NewName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".")]
    [InlineData(" . . ")]
    [InlineData("...")]
    public void Fixed_OnlyDotsAndSpaces_IsEmpty_Disabled_Unchanged(string typed)
    {
        var rule = new ExtensionRule(ExtensionMode.Fixed, typed);

        Assert.Equal("", rule.Value);
        Assert.False(rule.Enabled);
        Assert.Equal(new NameParts("a", "jpg"), ApplyTo(rule, "a", "jpg"));
    }

    [Fact]
    public void Fixed_WithAValue_IsEnabled() =>
        Assert.True(new ExtensionRule(ExtensionMode.Fixed, "png").Enabled);

    [Theory]
    [InlineData("a.txt", "a.txt.bak")]
    [InlineData("Makefile", "Makefile.bak")]
    public void Run_Extra_AddsAfterTheWholeName(string name, string expected) =>
        Assert.Equal(expected, RunOne(new ExtensionRule(ExtensionMode.Extra, "bak"), name).NewName);

    [Fact]
    public void Extra_CleansLikeFixed()
    {
        var rule = new ExtensionRule(ExtensionMode.Extra, " .bak. ");

        Assert.Equal("bak", rule.Value);
        Assert.True(rule.Enabled);
        Assert.Equal("a.txt.bak", RunOne(rule, "a.txt").NewName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" . ")]
    public void Extra_Empty_Disabled_Unchanged(string typed)
    {
        var rule = new ExtensionRule(ExtensionMode.Extra, typed);

        Assert.False(rule.Enabled);
        Assert.Equal(new NameParts("a", "txt"), ApplyTo(rule, "a", "txt"));
    }

    [Theory]
    [InlineData("a.txt", "a")]
    [InlineData("a.b", "a")]
    [InlineData("a.tar.gz", "a.tar")]
    [InlineData("Makefile", "Makefile")]
    [InlineData(".gitignore", ".gitignore")]
    public void Run_Remove_DropsTheLastExtensionOnly(string name, string expected) =>
        Assert.Equal(expected, RunOne(new ExtensionRule(ExtensionMode.Remove), name).NewName);

    [Theory]
    [InlineData(ExtensionMode.Remove)]
    [InlineData(ExtensionMode.Lower)]
    [InlineData(ExtensionMode.Upper)]
    public void CaseAndRemoveModes_AreEnabled(ExtensionMode mode) =>
        Assert.True(new ExtensionRule(mode).Enabled);

    [Fact]
    public void Run_Fixed_OnANameWithoutExtension_AddsIt() =>
        Assert.Equal("Makefile.png", RunOne(new ExtensionRule(ExtensionMode.Fixed, "png"), "Makefile").NewName);

    [Theory]
    [InlineData(ExtensionMode.Fixed)]
    [InlineData(ExtensionMode.Extra)]
    public void Run_FixedOrExtra_OnADotfile_AddsAnExtension(ExtensionMode mode) =>
        Assert.Equal(".gitignore.png", RunOne(new ExtensionRule(mode, "png"), ".gitignore").NewName);

    [Theory]
    [InlineData(ExtensionMode.Same)]
    [InlineData(ExtensionMode.Lower)]
    [InlineData(ExtensionMode.Upper)]
    [InlineData(ExtensionMode.Fixed)]
    [InlineData(ExtensionMode.Extra)]
    [InlineData(ExtensionMode.Remove)]
    public void Run_Folder_NeverChanged(ExtensionMode mode)
    {
        var item = new RenameItem("v1.2", @"C:\Test", ItemKind.Folder);

        var result = new RenamePipeline([new ExtensionRule(mode, "png")]).Run([item])[0];

        Assert.Equal("v1.2", result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Fact]
    public void Apply_Folder_ReturnsTheNameUnchanged()
    {
        var item = new RenameItem("v1.2", @"C:\Test", ItemKind.Folder);
        var name = new NameParts("v1.2", "");

        Assert.Equal(name, new ExtensionRule(ExtensionMode.Extra, "png").Apply(name, new ItemContext(item, 0, 1)));
        Assert.Equal(name, new ExtensionRule(ExtensionMode.Fixed, "png").Apply(name, new ItemContext(item, 0, 1)));
    }

    [Fact]
    public void Run_Lower_KeepsTheBaseNameCase()
    {
        Assert.Equal("a.tar.gz", RunOne(new ExtensionRule(ExtensionMode.Lower), "a.tar.GZ").NewName);
        Assert.Equal("Photo.JPG", RunOne(new ExtensionRule(ExtensionMode.Upper), "Photo.jpg").NewName);
        Assert.Equal("Photo.jpg", RunOne(new ExtensionRule(ExtensionMode.Lower), "Photo.JPG").NewName);
    }

    [Fact]
    public void Run_CaseModes_OnANameWithoutExtension_ChangeNothing()
    {
        Assert.Equal("MakeFile", RunOne(new ExtensionRule(ExtensionMode.Lower), "MakeFile").NewName);
        Assert.Equal("MakeFile", RunOne(new ExtensionRule(ExtensionMode.Upper), "MakeFile").NewName);
    }

    [Fact]
    public void Run_Upper_InvariantWhateverTheCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            Assert.Equal("a.INI", RunOne(new ExtensionRule(ExtensionMode.Upper), "a.ini").NewName);
            Assert.Equal("a.ini", RunOne(new ExtensionRule(ExtensionMode.Lower), "a.INI").NewName);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Pipeline_ExtensionRunsLast()
    {
        var rules = new IRenameRule[] { new ExtensionRule(ExtensionMode.Fixed, "png"), new AddRule(suffix: "_x") };

        Assert.Equal("a_x.png", new RenamePipeline(rules).Run(Items.Files("a.jpg"))[0].NewName);
    }

    [Fact]
    public void Run_InvalidCharacterInValue_AppliedAsTyped_ThenFlagged()
    {
        var results = new RenamePipeline([new ExtensionRule(ExtensionMode.Fixed, "a.b?c")]).Run(Items.Files("x.jpg"));

        Assert.Equal("x.a.b?c", results[0].NewName);
        Assert.Null(results[0].Error);
        Assert.Contains(new InvalidNameCheck().Check(results)[0], c => c.Code == ConflictCode.InvalidCharacter);
    }

    [Fact]
    public void Constructor_UnknownMode_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExtensionRule((ExtensionMode)99));

    [Fact]
    public void Constructor_NullValue_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new ExtensionRule(ExtensionMode.Fixed, null!));

    [Fact]
    public void Stage_IsExtension11()
    {
        var stage = new ExtensionRule(ExtensionMode.Lower).Stage;

        Assert.Equal(RuleStage.Extension, stage);
        Assert.Equal(11, (int)stage);
    }

    [Fact]
    public void Mode_IsKept() =>
        Assert.Equal(ExtensionMode.Extra, new ExtensionRule(ExtensionMode.Extra, "bak").Mode);
}
