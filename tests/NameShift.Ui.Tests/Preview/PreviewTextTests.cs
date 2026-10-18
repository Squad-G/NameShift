using System;
using System.Collections.Generic;
using System.Linq;
using NameShift.Core;
using NameShift.Ui.Files;
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
    private static ConflictReport NoConflicts(int count) =>
        new ConflictReport(Enumerable.Range(0, count).Select(_ => (IReadOnlyList<Conflict>)Array.Empty<Conflict>()).ToArray());

    [Fact]
    public void Ctor_Null_Throws()
    {
        // The cast picks the list constructor: a bare null would match both.
        Assert.Throws<ArgumentNullException>(() => new PreviewSnapshot((IReadOnlyList<RenameItem>)null!, Array.Empty<RenameResult>(), NoConflicts(0)));
        Assert.Throws<ArgumentNullException>(() => new PreviewSnapshot((FileBatch)null!, Array.Empty<RenameResult>(), NoConflicts(0)));
        Assert.Throws<ArgumentNullException>(() => new PreviewSnapshot(Array.Empty<RenameItem>(), null!, NoConflicts(0)));
        Assert.Throws<ArgumentNullException>(() => new PreviewSnapshot(Array.Empty<RenameItem>(), Array.Empty<RenameResult>(), null!));
    }

    [Fact]
    public void Ctor_CountMismatch_Throws()
    {
        var items = new[] { new RenameItem("a.txt", @"C:\A") };

        Assert.Throws<ArgumentException>(() => new PreviewSnapshot(items, Array.Empty<RenameResult>(), NoConflicts(0)));
    }

    [Fact]
    public void BatchCtor_CountMismatch_Throws()
    {
        FileBatch batch = FileBatch.WholeList(new[] { new RenameItem("a.txt", @"C:\A") });

        Assert.Throws<ArgumentException>(() => new PreviewSnapshot(batch, Array.Empty<RenameResult>(), NoConflicts(0)));
    }

    [Fact]
    public void BatchCtor_ItemsAreTheBatchItems()
    {
        FileBatch batch = FileBatch.WholeList(new[] { new RenameItem("a.txt", @"C:\A") });
        var results = new[] { new RenameResult(batch.Items[0], 0, "a.txt", RenameStatus.Unchanged, null) };

        var snapshot = new PreviewSnapshot(batch, results, NoConflicts(1));

        Assert.Same(batch, snapshot.Batch);
        Assert.Same(batch.Items, snapshot.Items);
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

        var snapshot = new PreviewSnapshot(items, results, NoConflicts(1));

        Assert.Same(items, snapshot.Items);
        Assert.Same(results, snapshot.Results);
    }

    [Fact]
    public void Snapshot_CountsConflictRowsAndErrors()
    {
        var items = new[] { new RenameItem("a.txt", @"C:\A"), new RenameItem("b.txt", @"C:\A"), new RenameItem("c.txt", @"C:\A") };
        var results = new[]
        {
            new RenameResult(items[0], 0, "x.txt", RenameStatus.Changed, null),
            new RenameResult(items[1], 1, "b.txt", RenameStatus.Error, new RenameError(RuleStage.Replace, "Replace", "boom")),
            new RenameResult(items[2], 2, "d.txt", RenameStatus.Changed, null),
        };
        var conflicts = new ConflictReport(new IReadOnlyList<Conflict>[]
        {
            new[] { new Conflict(ConflictCode.InvalidCharacter, "?"), new Conflict(ConflictCode.PathTooLong) },
            Array.Empty<Conflict>(),
            Array.Empty<Conflict>(),
        });

        var snapshot = new PreviewSnapshot(items, results, conflicts);

        Assert.Same(conflicts, snapshot.Conflicts);
        Assert.Equal(1, snapshot.ConflictCount);
        Assert.Equal(1, snapshot.ErrorCount);
        Assert.True(snapshot.HasConflicts);
    }

    [Fact]
    public void Snapshot_ConflictsCountMismatch_Throws()
    {
        var items = new[] { new RenameItem("a.txt", @"C:\A") };
        var results = new[] { new RenameResult(items[0], 0, "a.txt", RenameStatus.Unchanged, null) };

        Assert.Throws<ArgumentException>(() => new PreviewSnapshot(items, results, NoConflicts(2)));
    }

    [Fact]
    public void Snapshot_Empty_HasNoConflicts()
    {
        Assert.Equal(0, PreviewSnapshot.Empty.ConflictCount);
        Assert.Equal(0, PreviewSnapshot.Empty.ErrorCount);
        Assert.False(PreviewSnapshot.Empty.HasConflicts);
        Assert.Equal(0, PreviewSnapshot.Empty.Conflicts.Count);
    }
}
