using System;
using System.Collections.Generic;
using NameShift.Core;
using NameShift.Ui.Preview;
using Xunit;

namespace NameShift.Ui.Tests.Preview;

/// <summary>Compares two cells by text and kind (PreviewCell is a plain struct).</summary>
internal sealed class CellComparer : IEqualityComparer<PreviewCell>
{
    public static readonly CellComparer Instance = new CellComparer();

    public bool Equals(PreviewCell x, PreviewCell y) => x.Text == y.Text && x.Kind == y.Kind;

    public int GetHashCode(PreviewCell obj) => obj.Text.GetHashCode();
}

public sealed class PreviewTextTests
{
    private static readonly RenameItem Item = new RenameItem("a.txt", @"C:\A");

    [Fact]
    public void Changed_ShowsTheNewName_AsChanged()
    {
        var result = new RenameResult(Item, 0, "b.txt", RenameStatus.Changed, null);

        PreviewCell cell = PreviewText.For(result);

        Assert.Equal("b.txt", cell.Text);
        Assert.Equal(PreviewCellKind.Changed, cell.Kind);
    }

    [Fact]
    public void Unchanged_ShowsTheName_AsUnchanged()
    {
        var result = new RenameResult(Item, 0, "a.txt", RenameStatus.Unchanged, null);

        PreviewCell cell = PreviewText.For(result);

        Assert.Equal("a.txt", cell.Text);
        Assert.Equal(PreviewCellKind.Unchanged, cell.Kind);
    }

    [Fact]
    public void Error_ShowsTheMessage_WithThePrefix()
    {
        var error = new RenameError(RuleStage.Replace, "ReplaceRule", "no good");
        var result = new RenameResult(Item, 0, "a.txt", RenameStatus.Error, error);

        PreviewCell cell = PreviewText.For(result);

        Assert.Equal("Error: no good", cell.Text);
        Assert.Equal(PreviewCellKind.Error, cell.Kind);
    }

    [Fact]
    public void ErrorMessage_IsOneLine()
    {
        var error = new RenameError(RuleStage.Replace, "ReplaceRule", "a\r\nb\tc");
        var result = new RenameResult(Item, 0, "a.txt", RenameStatus.Error, error);

        Assert.Equal("Error: a  b c", PreviewText.For(result).Text);
    }

    [Fact]
    public void UnknownStatus_Throws()
    {
        var result = new RenameResult(Item, 0, "a.txt", (RenameStatus)99, null);

        Assert.Throws<ArgumentOutOfRangeException>(() => PreviewText.For(result));
    }

    [Fact]
    public void Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PreviewText.For(null!));
    }
}

public sealed class PreviewSnapshotTests
{
    [Fact]
    public void Ctor_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new PreviewSnapshot(null!, Array.Empty<RenameResult>()));
        Assert.Throws<ArgumentNullException>(() => new PreviewSnapshot(Array.Empty<RenameItem>(), null!));
    }

    [Fact]
    public void Ctor_CountMismatch_Throws()
    {
        var items = new[] { new RenameItem("a.txt", @"C:\A") };

        Assert.Throws<ArgumentException>(() => new PreviewSnapshot(items, Array.Empty<RenameResult>()));
    }

    [Fact]
    public void Empty_HasNoItemsAndNoResults()
    {
        Assert.Empty(PreviewSnapshot.Empty.Items);
        Assert.Empty(PreviewSnapshot.Empty.Results);
    }

    [Fact]
    public void Ctor_KeepsTheItemsReference()
    {
        var items = new[] { new RenameItem("a.txt", @"C:\A") };
        var results = new[] { new RenameResult(items[0], 0, "a.txt", RenameStatus.Unchanged, null) };

        var snapshot = new PreviewSnapshot(items, results);

        Assert.Same(items, snapshot.Items);
        Assert.Same(results, snapshot.Results);
    }
}
