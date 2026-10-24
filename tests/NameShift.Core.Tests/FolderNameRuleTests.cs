using System;
using Xunit;

namespace NameShift.Core.Tests;

public sealed class FolderNameRuleTests
{
    private const string Paris = @"C:\Photos\Paris";

    private static RenameResult Run(FolderNameRule rule, string name, string folder, ItemKind kind = ItemKind.File) =>
        new RenamePipeline([rule]).Run([new RenameItem(name, folder, kind)])[0];

    [Fact]
    public void Prefix_OneLevel() =>
        Assert.Equal("Paris_a.jpg", Run(new FolderNameRule(), "a.jpg", Paris).NewName);

    [Fact]
    public void Defaults()
    {
        var rule = new FolderNameRule();

        Assert.Equal(FolderNamePlacement.Prefix, rule.Placement);
        Assert.Equal(1, rule.Levels);
        Assert.Equal("_", rule.Separator);
        Assert.Equal(RuleStage.FolderName, rule.Stage);
        Assert.True(rule.Enabled);
    }

    [Fact]
    public void Suffix_TwoLevels_OuterFirst() =>
        Assert.Equal("a-Photos-Paris.jpg",
            Run(new FolderNameRule(FolderNamePlacement.Suffix, 2, "-"), "a.jpg", Paris).NewName);

    [Fact]
    public void Prefix_TwoLevels_OuterFirst() =>
        Assert.Equal("Photos-Paris-a.jpg",
            Run(new FolderNameRule(FolderNamePlacement.Prefix, 2, "-"), "a.jpg", Paris).NewName);

    [Fact]
    public void ForwardSlashesAndTrailingSeparator() =>
        Assert.Equal("Photos-Paris-a.jpg",
            Run(new FolderNameRule(FolderNamePlacement.Prefix, 2, "-"), "a.jpg", "C:/Photos//Paris/").NewName);

    [Fact]
    public void CaseKept() =>
        Assert.Equal("photos_PARIS_a.jpg",
            Run(new FolderNameRule(FolderNamePlacement.Prefix, 2, "_"), "a.jpg", @"C:\photos\PARIS").NewName);

    [Fact]
    public void MoreLevelsThanFolders_UsesAll() =>
        Assert.Equal("Photos_Paris_a.jpg",
            Run(new FolderNameRule(FolderNamePlacement.Prefix, 5, "_"), "a.jpg", Paris).NewName);

    [Theory]
    [InlineData(@"C:\")]
    [InlineData("C:")]
    public void DriveRoot_Unchanged(string folder)
    {
        var result = Run(new FolderNameRule(), "a.jpg", folder);

        Assert.Equal("a.jpg", result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Fact]
    public void Drive_NeverUsed()
    {
        var rule = new FolderNameRule(FolderNamePlacement.Prefix, 3, "_");

        Assert.Equal("a.jpg", Run(rule, "a.jpg", @"C:\").NewName);
        Assert.Equal("x_a.jpg", Run(rule, "a.jpg", @"C:\x").NewName);
    }

    [Fact]
    public void Drive_LowerCase_NeverUsed() =>
        Assert.Equal("x_a.jpg",
            Run(new FolderNameRule(FolderNamePlacement.Prefix, 3, "_"), "a.jpg", @"c:\x").NewName);

    [Fact]
    public void Network_ServerAndShareNeverUsed() =>
        Assert.Equal("Docs_Trip_a.jpg",
            Run(new FolderNameRule(FolderNamePlacement.Prefix, 5, "_"), "a.jpg", @"\\server\share\Docs\Trip").NewName);

    [Theory]
    [InlineData(@"\\server\share")]
    [InlineData("//server/share")]
    [InlineData(@"\\server")]
    public void Network_ShareItself_Unchanged(string folder)
    {
        var result = Run(new FolderNameRule(FolderNamePlacement.Prefix, 5, "_"), "a.jpg", folder);

        Assert.Equal("a.jpg", result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void LevelsBelowOne_DisabledUnchanged(int levels)
    {
        var rule = new FolderNameRule(FolderNamePlacement.Prefix, levels);
        var item = new RenameItem("a.jpg", Paris);
        var parts = new NameParts("a", "jpg");

        Assert.False(rule.Enabled);
        Assert.Equal(RenameStatus.Unchanged, Run(rule, "a.jpg", Paris).Status);
        Assert.Equal(parts, rule.Apply(parts, new ItemContext(item, 0, 1)));
    }

    [Fact]
    public void LevelsMax_ClampedNoError()
    {
        var result = Run(new FolderNameRule(FolderNamePlacement.Prefix, int.MaxValue), "a.jpg", Paris);

        Assert.Equal("Photos_Paris_a.jpg", result.NewName);
        Assert.Null(result.Error);
    }

    [Fact]
    public void EmptySeparator_JoinsWithNothing() =>
        Assert.Equal("Parisa.jpg",
            Run(new FolderNameRule(FolderNamePlacement.Prefix, 1, ""), "a.jpg", Paris).NewName);

    [Fact]
    public void NullSeparator_Throws()
    {
        var e = Assert.Throws<ArgumentNullException>(() => new FolderNameRule(FolderNamePlacement.Prefix, 1, null!));

        Assert.Equal("separator", e.ParamName);
    }

    [Fact]
    public void UndefinedPlacement_Throws()
    {
        var e = Assert.Throws<ArgumentOutOfRangeException>(() => new FolderNameRule((FolderNamePlacement)99));

        Assert.Equal("placement", e.ParamName);
    }

    [Fact]
    public void FolderItem_NoExtension() =>
        Assert.Equal("Sub_Paris",
            Run(new FolderNameRule(FolderNamePlacement.Suffix, 1, "_"), "Sub", Paris, ItemKind.Folder).NewName);

    [Fact]
    public void FolderItem_WithDotInName_WholeNameIsBase() =>
        Assert.Equal("v1.2_Paris",
            Run(new FolderNameRule(FolderNamePlacement.Suffix), "v1.2", Paris, ItemKind.Folder).NewName);

    [Fact]
    public void Dotfile_WholeNameIsBase()
    {
        var item = new RenameItem(".gitignore", Paris, ItemKind.File, ItemAttributes.Hidden);

        var result = new RenamePipeline([new FolderNameRule()]).Run([item])[0];

        Assert.Equal("Paris_.gitignore", result.NewName);
    }

    [Fact]
    public void Suffix_BeforeExtension_OnlyLastDot() =>
        Assert.Equal("a.tar_Paris.gz",
            Run(new FolderNameRule(FolderNamePlacement.Suffix), "a.tar.gz", Paris).NewName);

    [Fact]
    public void Pipeline_NumberingRunsFirst()
    {
        var pipeline = new RenamePipeline([
            new FolderNameRule(FolderNamePlacement.Suffix),
            new NumberingRule(NumberingMode.Suffix, separator: "_"),
        ]);

        Assert.Equal("a_1_Paris.jpg", pipeline.Run([new RenameItem("a.jpg", Paris)])[0].NewName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(@"\")]
    [InlineData("/")]
    [InlineData(@"C:\\")]
    [InlineData(@"\\\\")]
    [InlineData("//")]
    [InlineData(@"\/\/")]
    public void OddPaths_NoFoldersNoThrow(string folder)
    {
        var result = Run(new FolderNameRule(FolderNamePlacement.Prefix, 3, "_"), "a.jpg", folder);

        Assert.Equal("a.jpg", result.NewName);
        Assert.Equal(RenameStatus.Unchanged, result.Status);
        Assert.Null(result.Error);
    }

    [Fact]
    public void LongFolderName_AppliedAndFlaggedByPathLength()
    {
        var folder = @"C:\" + new string('p', 250);

        var result = Run(new FolderNameRule(), "a.jpg", folder);

        Assert.Equal(RenameStatus.Changed, result.Status);
        Assert.Equal(new string('p', 250) + "_a.jpg", result.NewName);
        Assert.Equal([new Conflict(ConflictCode.PathTooLong)], new PathLengthCheck().Check([result])[0]);
    }

    [Fact]
    public void Properties_ReturnTheValuesGiven()
    {
        var rule = new FolderNameRule(FolderNamePlacement.Suffix, 3, " - ");

        Assert.Equal(FolderNamePlacement.Suffix, rule.Placement);
        Assert.Equal(3, rule.Levels);
        Assert.Equal(" - ", rule.Separator);
    }
}
