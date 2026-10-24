using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NameShift.Core;
using NameShift.Ui.Files;
using NameShift.Ui.Folders;
using Xunit;
using static NameShift.Ui.Tests.FakeFileListSource;

namespace NameShift.Ui.Tests;

public sealed class FileListModelTests
{
    private readonly FakeFileListSource _source = new FakeFileListSource();
    private readonly FileListModel _model;
    private int _changes;

    public FileListModelTests()
    {
        _model = new FileListModel(_source);
        _model.Changed += (_, _) => _changes++;
    }

    [Fact]
    public async Task LoadAsync_ListsTheFolder_AndRaisesChanged()
    {
        _source.SetEntries(@"C:\A", File("a.txt"), Folder("d"));

        bool applied = await _model.LoadAsync(@"C:\A");

        Assert.True(applied);
        Assert.Equal(@"C:\A", _model.FolderPath);
        Assert.Equal(FileListState.Loaded, _model.State);
        Assert.Equal(new[] { "d", "a.txt" }, _model.Items.Select(i => i.Name));
        Assert.Null(_model.Message);
        Assert.Equal(2, _changes);
    }

    [Fact]
    public async Task LoadAsync_WhileLoading_ClearsOldRows_AndShowsLoading()
    {
        _source.SetEntries(@"C:\A", File("a.txt"));
        await _model.LoadAsync(@"C:\A");
        var gate = _source.Gate(@"C:\B");

        Task<bool> loading = _model.LoadAsync(@"C:\B");

        Assert.Empty(_model.Items);
        Assert.Equal(FileListState.Loading, _model.State);
        Assert.Equal(Strings.Loading, _model.Message);
        Assert.Equal(@"C:\B", _model.FolderPath);
        gate.Set();
        Assert.True(await loading);
    }

    [Fact]
    public async Task LoadAsync_SupersededLoad_IsDiscarded()
    {
        _source.SetEntries(@"C:\A", File("a.txt"));
        _source.SetEntries(@"C:\B", File("b.txt"));
        var gateA = _source.Gate(@"C:\A");
        var gateB = _source.Gate(@"C:\B");

        Task<bool> loadA = _model.LoadAsync(@"C:\A");
        Task<bool> loadB = _model.LoadAsync(@"C:\B");
        gateB.Set();
        Assert.True(await loadB);
        gateA.Set();

        Assert.False(await loadA);
        Assert.Equal(@"C:\B", _model.FolderPath);
        Assert.Equal(new[] { "b.txt" }, _model.Items.Select(i => i.Name));
    }

    [Fact]
    public async Task LoadAsync_SortsWhateverTheSourceOrder()
    {
        _source.SetEntries(@"C:\A", File("file10.txt"), Folder("b"), File("file2.txt"), Folder("a"));

        await _model.LoadAsync(@"C:\A");

        Assert.Equal(new[] { "a", "b", "file2.txt", "file10.txt" }, _model.Items.Select(i => i.Name));
    }

    [Theory]
    [InlineData(FolderProblem.AccessDenied)]
    [InlineData(FolderProblem.NotFound)]
    public async Task LoadAsync_Failed_ShowsTheProblem_WithNoRows(FolderProblem problem)
    {
        _source.SetListing(@"C:\A", FileListing.Failed(problem));

        await _model.LoadAsync(@"C:\A");

        Assert.Equal(FileListState.Failed, _model.State);
        Assert.Equal(problem, _model.Problem);
        Assert.Empty(_model.Items);
        Assert.Equal(FolderProblemText.For(problem), _model.Message);
        Assert.Equal(problem == FolderProblem.AccessDenied ? Strings.AccessDenied : Strings.FolderNotFound, _model.Message);
    }

    [Fact]
    public async Task LoadAsync_SourceThrows_IsUnavailable()
    {
        _source.SetThrows(@"C:\A", new InvalidOperationException("boom"));

        bool applied = await _model.LoadAsync(@"C:\A");

        Assert.True(applied);
        Assert.Equal(FolderProblem.Unavailable, _model.Problem);
        Assert.Equal(Strings.FolderUnavailable, _model.Message);
    }

    [Fact]
    public async Task LoadAsync_EmptyFolder_SaysSo()
    {
        await _model.LoadAsync(@"C:\A");

        Assert.Equal(FileListState.Loaded, _model.State);
        Assert.Equal(Strings.FolderEmpty, _model.Message);
    }

    [Fact]
    public async Task FindRow_IgnoresCase()
    {
        _source.SetEntries(@"C:\A", File("a.jpg"), File("B.txt"));
        await _model.LoadAsync(@"C:\A");

        Assert.Equal(1, _model.FindRow("b.txt"));
        Assert.Equal(0, _model.FindRow("a.jpg"));
        Assert.Equal(-1, _model.FindRow("c.txt"));
        Assert.Equal(-1, _model.FindRow(null));
        Assert.Equal(-1, _model.FindRow(""));
    }

    [Fact]
    public async Task FindRow_BeforeLoad_Or_Failed()
    {
        Assert.Equal(-1, _model.FindRow("a.jpg"));

        _source.SetListing(@"C:\A", FileListing.Failed(FolderProblem.AccessDenied));
        await _model.LoadAsync(@"C:\A");

        Assert.Equal(FileListState.Failed, _model.State);
        Assert.Equal(-1, _model.FindRow("a.jpg"));
    }

    [Fact]
    public async Task FindRow_HiddenFile()
    {
        _source.SetEntries(@"C:\A", new[] { "h.txt" }, File("a.jpg"));
        await _model.LoadAsync(@"C:\A");

        Assert.Equal(-1, _model.FindRow("h.txt"));
        Assert.Equal(0, _model.FindRow("a.jpg"));
    }

    [Fact]
    public async Task FindRow_WhileLoading()
    {
        _source.SetEntries(@"C:\A", File("a.jpg"));
        await _model.LoadAsync(@"C:\A");
        _source.SetEntries(@"C:\B", File("a.jpg", folder: @"C:\B"));
        var gate = _source.Gate(@"C:\B");

        Task<bool> loading = _model.LoadAsync(@"C:\B");

        Assert.Equal(-1, _model.FindRow("a.jpg"));
        gate.Set();
        Assert.True(await loading);
        Assert.Equal(0, _model.FindRow("a.jpg"));
    }

    [Fact]
    public void BeforeAnyLoad_NothingIsShown()
    {
        Assert.Equal(FileListState.None, _model.State);
        Assert.Null(_model.Message);
        Assert.Empty(_model.Items);
    }

    [Fact]
    public async Task Items_GoStraightIntoThePipeline_RowIsIndex()
    {
        _source.SetEntries(@"C:\A", File("b.txt"), Folder("a"), File("c.txt"));
        await _model.LoadAsync(@"C:\A");

        var results = new RenamePipeline(Array.Empty<IRenameRule>()).Run(_model.Items);

        Assert.Equal(3, results.Count);
        for (int i = 0; i < results.Count; i++)
        {
            Assert.Equal(i, results[i].Index);
            Assert.Same(_model.Items[i], results[i].Item);
        }
    }

    [Fact]
    public async Task Load_KeepsEveryEntryName_IncludingHidden()
    {
        _source.SetEntries(@"C:\A", new[] { "secret.txt" }, File("b.txt"), File("a.txt"));

        await _model.LoadAsync(@"C:\A");

        Assert.Equal(new[] { "a.txt", "b.txt" }, _model.Items.Select(i => i.Name));
        Assert.Equal(new[] { "a.txt", "b.txt", "secret.txt" }, _model.ExistingNames.OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Load_Starting_ClearsExistingNames()
    {
        _source.SetEntries(@"C:\A", new[] { "secret.txt" }, File("a.txt"));
        await _model.LoadAsync(@"C:\A");
        var gate = _source.Gate(@"C:\B");

        Task<bool> loading = _model.LoadAsync(@"C:\B");

        Assert.Empty(_model.ExistingNames);
        gate.Set();
        Assert.True(await loading);
    }

    [Fact]
    public async Task Load_Failed_HasNoExistingNames()
    {
        _source.SetListing(@"C:\A", FileListing.Failed(FolderProblem.NotFound));

        await _model.LoadAsync(@"C:\A");

        Assert.Empty(_model.ExistingNames);
    }

    [Fact]
    public void Listing_Ok_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => FileListing.Ok(null!));
        Assert.Throws<ArgumentNullException>(() => FileListing.Ok(null!, Array.Empty<string>()));
        Assert.Throws<ArgumentNullException>(() => FileListing.Ok(Array.Empty<RenameItem>(), null!));
        Assert.Equal(new[] { "a.txt" }, FileListing.Ok(new[] { File("a.txt") }).ExistingNames);
    }

    private async Task LoadThree()
    {
        _source.SetEntries(@"C:\A", File("a1.txt"), File("a2.txt"), File("b.txt"));
        await _model.LoadAsync(@"C:\A");
    }

    [Fact]
    public async Task Load_BatchIsTheWholeList()
    {
        await LoadThree();

        Assert.True(_model.Batch.IsWholeList);
        Assert.Same(_model.Items, _model.Batch.Items);
        Assert.Equal(3, _model.Batch.Count);
    }

    [Fact]
    public async Task SetSelection_AnyOrderAndDuplicates_GivesListOrder()
    {
        await LoadThree();

        _model.SetSelection(new[] { 2, 0, 2 });

        Assert.False(_model.Batch.IsWholeList);
        Assert.Equal(new[] { 0, 2 }, _model.Batch.Rows);
        Assert.Equal(new[] { "a1.txt", "b.txt" }, _model.Batch.Items.Select(i => i.Name));
        Assert.Same(_model.Items, _model.Batch.ListItems);
        Assert.True(_model.Batch.TryGetIndex(2, out int index));
        Assert.Equal(1, index);
        Assert.False(_model.Batch.TryGetIndex(1, out _));
    }

    [Fact]
    public async Task SetSelection_RaisesSelectionChanged_NotChanged()
    {
        await LoadThree();
        int selections = 0;
        _model.SelectionChanged += (_, _) => selections++;
        int changes = _changes;

        _model.SetSelection(new[] { 1 });

        Assert.Equal(1, selections);
        Assert.Equal(changes, _changes);
    }

    [Fact]
    public async Task SetSelection_EveryRow_IsTheWholeList()
    {
        await LoadThree();
        int selections = 0;
        _model.SelectionChanged += (_, _) => selections++;

        _model.SetSelection(new[] { 0, 1, 2 });

        Assert.True(_model.Batch.IsWholeList);
        Assert.Equal(0, selections);

        _model.SetSelection(new[] { 1 });
        _model.SetSelection(new[] { 2, 1, 0 });

        Assert.True(_model.Batch.IsWholeList);
        Assert.Equal(2, selections);
    }

    [Fact]
    public async Task SetSelection_Same_RaisesNothing()
    {
        await LoadThree();
        int selections = 0;
        _model.SelectionChanged += (_, _) => selections++;

        _model.SetSelection(Array.Empty<int>());
        _model.SetSelection(new[] { 0 });
        _model.SetSelection(new[] { 0, 0 });

        Assert.Equal(1, selections);
    }

    [Fact]
    public async Task SetSelection_DifferentRowsOfTheSameSize_RaisesAgain()
    {
        await LoadThree();
        int selections = 0;
        _model.SelectionChanged += (_, _) => selections++;

        _model.SetSelection(new[] { 0 });
        _model.SetSelection(new[] { 1 });

        Assert.Equal(2, selections);
    }

    [Fact]
    public async Task Load_ResetsTheSelection()
    {
        await LoadThree();
        _source.SetEntries(@"C:\B", File("x.txt", folder: @"C:\B"), File("y.txt", folder: @"C:\B"));
        _model.SetSelection(new[] { 1 });

        await _model.LoadAsync(@"C:\B");

        Assert.True(_model.Batch.IsWholeList);
        Assert.Same(_model.Items, _model.Batch.Items);
        Assert.Equal(2, _model.Batch.Count);

        _model.SetSelection(new[] { 1 });
        await _model.LoadAsync(@"C:\B");

        Assert.True(_model.Batch.IsWholeList);
    }

    [Fact]
    public async Task LoadAsync_WhileLoading_TheBatchIsEmpty()
    {
        await LoadThree();
        _model.SetSelection(new[] { 1 });
        var gate = _source.Gate(@"C:\B");

        Task<bool> loading = _model.LoadAsync(@"C:\B");

        Assert.Equal(0, _model.Batch.Count);
        gate.Set();
        await loading;
    }

    [Fact]
    public async Task SetSelection_OutOfRangeOrNull_Throws()
    {
        await LoadThree();
        _model.SetSelection(new[] { 1 });
        FileBatch before = _model.Batch;

        Assert.Throws<ArgumentOutOfRangeException>(() => _model.SetSelection(new[] { -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => _model.SetSelection(new[] { 3 }));
        Assert.Throws<ArgumentNullException>(() => _model.SetSelection(null!));
        Assert.Same(before, _model.Batch);

        var gate = _source.Gate(@"C:\B");
        Task<bool> loading = _model.LoadAsync(@"C:\B");
        Assert.Throws<ArgumentOutOfRangeException>(() => _model.SetSelection(new[] { 0 }));
        gate.Set();
        await loading;
    }

    private async Task LoadSized()
    {
        _source.SetEntries(@"C:\A", Folder("d"), File("a.txt", 30), File("b.txt", 10), File("c.txt", 20), File("e.txt", 40));
        await _model.LoadAsync(@"C:\A");
    }

    private static string[] NamesOf(IEnumerable<RenameItem> items) => items.Select(i => i.Name).ToArray();

    [Fact]
    public async Task SortBy_NewColumnAscending_SameColumnToggles()
    {
        await LoadSized();
        Assert.False(_model.IsSortChosen);

        _model.SortBy(FileListColumn.Size);
        Assert.Equal(new FileListSort(FileListColumn.Size, false), _model.Sort);
        Assert.True(_model.IsSortChosen);
        Assert.Equal(new[] { "d", "b.txt", "c.txt", "a.txt", "e.txt" }, NamesOf(_model.Items));

        _model.SortBy(FileListColumn.Size);
        Assert.Equal(new FileListSort(FileListColumn.Size, true), _model.Sort);
        Assert.Equal(new[] { "d", "e.txt", "a.txt", "c.txt", "b.txt" }, NamesOf(_model.Items));

        _model.SortBy(FileListColumn.Size);
        Assert.Equal(new FileListSort(FileListColumn.Size, false), _model.Sort);

        _model.SortBy(FileListColumn.Modified);
        Assert.Equal(new FileListSort(FileListColumn.Modified, false), _model.Sort);
    }

    [Fact]
    public async Task SortBy_NameFromDefault_IsDescending()
    {
        await LoadSized();

        _model.SortBy(FileListColumn.Name);

        Assert.Equal(new FileListSort(FileListColumn.Name, true), _model.Sort);
        Assert.Equal(new[] { "d", "e.txt", "c.txt", "b.txt", "a.txt" }, NamesOf(_model.Items));
    }

    [Fact]
    public async Task SortBy_NewItemsInstance_ExistingNamesAndFolderUnchanged()
    {
        await LoadSized();
        var oldItems = _model.Items;
        var names = _model.ExistingNames;

        _model.SortBy(FileListColumn.Size);

        Assert.NotSame(oldItems, _model.Items);
        Assert.Same(names, _model.ExistingNames);
        Assert.Equal(@"C:\A", _model.FolderPath);
        Assert.Equal(FileListState.Loaded, _model.State);
    }

    [Fact]
    public async Task SortBy_KeepsSelectedItems()
    {
        await LoadSized();
        RenameItem first = _model.Items[0];
        RenameItem third = _model.Items[2];
        _model.SetSelection(new[] { 0, 2 });

        _model.SortBy(FileListColumn.Size);

        int rowFirst = _model.Items.ToList().IndexOf(first);
        int rowThird = _model.Items.ToList().IndexOf(third);
        Assert.True(rowFirst < rowThird);
        Assert.False(_model.Batch.IsWholeList);
        Assert.Equal(new[] { first, third }, _model.Batch.Items);
        Assert.Equal(new[] { rowFirst, rowThird }, _model.Batch.Rows);
        Assert.Equal(new[] { rowFirst, rowThird }, _model.SelectedRows);
        Assert.Same(_model.Items, _model.Batch.ListItems);
    }

    [Fact]
    public async Task SortBy_SelectionMovesWithItsItems()
    {
        await LoadSized();
        RenameItem a = _model.Items[1];
        RenameItem e = _model.Items[4];
        _model.SetSelection(new[] { 1, 4 });

        _model.SortBy(FileListColumn.Name);

        Assert.Equal(new[] { 1, 4 }, _model.SelectedRows);
        Assert.Equal(new[] { e, a }, _model.Batch.Items);
        Assert.Equal(new[] { 1, 4 }, _model.Batch.Rows);
        Assert.Same(e, _model.Items[1]);
        Assert.Same(a, _model.Items[4]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SortBy_NoneOrAllSelected_WholeListInNewOrder(bool all)
    {
        await LoadSized();
        if (all)
        {
            _model.SetSelection(new[] { 0, 1, 2, 3, 4 });
        }

        _model.SortBy(FileListColumn.Size);

        Assert.True(_model.Batch.IsWholeList);
        Assert.Same(_model.Items, _model.Batch.Items);
        Assert.Equal(all ? new[] { 0, 1, 2, 3, 4 } : new int[0], _model.SelectedRows);
    }

    [Fact]
    public async Task SortBy_RaisesOnlyOrderChanged_Once()
    {
        await LoadSized();
        int order = 0;
        int selection = 0;
        _model.OrderChanged += (_, _) => order++;
        _model.SelectionChanged += (_, _) => selection++;
        _model.SetSelection(new[] { 1 });
        selection = 0;
        int changes = _changes;

        _model.SortBy(FileListColumn.Size);

        Assert.Equal(1, order);
        Assert.Equal(changes, _changes);
        Assert.Equal(0, selection);
    }

    [Fact]
    public async Task SortBy_OrderChanged_SeesTheNewState()
    {
        await LoadSized();
        FileListSort? seen = null;
        bool? chosen = null;
        string? second = null;
        _model.OrderChanged += (_, _) =>
        {
            seen = _model.Sort;
            chosen = _model.IsSortChosen;
            second = _model.Items[1].Name;
        };

        _model.SortBy(FileListColumn.Size);

        Assert.Equal(new FileListSort(FileListColumn.Size, false), seen);
        Assert.True(chosen);
        Assert.Equal("b.txt", second);
    }

    [Fact]
    public async Task SortBy_SameOrder_StillRaisesOrderChanged()
    {
        _source.SetEntries(@"C:\A", File("a.txt", 5), File("b.txt", 5));
        await _model.LoadAsync(@"C:\A");
        int order = 0;
        _model.OrderChanged += (_, _) => order++;

        _model.SortBy(FileListColumn.Size);

        Assert.Equal(1, order);
        Assert.Equal(new[] { "a.txt", "b.txt" }, NamesOf(_model.Items));
    }

    [Fact]
    public async Task SelectedRows_TracksSetSelection()
    {
        await LoadSized();
        Assert.Empty(_model.SelectedRows);

        _model.SetSelection(new[] { 2, 0, 2 });
        Assert.Equal(new[] { 0, 2 }, _model.SelectedRows);

        _model.SetSelection(new[] { 4, 3, 2, 1, 0 });
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, _model.SelectedRows);

        _model.SetSelection(new int[0]);
        Assert.Empty(_model.SelectedRows);

        int selection = 0;
        _model.SelectionChanged += (_, _) => selection++;
        _model.SetSelection(new[] { 0, 1, 2, 3, 4 });
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, _model.SelectedRows);
        Assert.Equal(0, selection);
    }

    [Fact]
    public async Task SelectedRows_EmptyAfterEveryLoad()
    {
        await LoadSized();
        _model.SetSelection(new[] { 1 });

        await _model.LoadAsync(@"C:\A");

        Assert.Empty(_model.SelectedRows);
    }

    [Fact]
    public async Task Load_OtherFolder_ResetsSort()
    {
        await LoadSized();
        _source.SetEntries(@"C:\B", File("z.txt", 1, @"C:\B"), File("y.txt", 9, @"C:\B"));
        _model.SortBy(FileListColumn.Size);
        _model.SortBy(FileListColumn.Size);

        await _model.LoadAsync(@"C:\B");

        Assert.Equal(FileListSort.Default, _model.Sort);
        Assert.False(_model.IsSortChosen);
        Assert.Equal(new[] { "y.txt", "z.txt" }, NamesOf(_model.Items));
    }

    [Theory]
    [InlineData(@"C:\A")]
    [InlineData(@"c:\a\")]
    public async Task Load_SameFolder_KeepsSort(string reload)
    {
        await LoadSized();
        _source.SetEntries(reload, Folder("d"), File("a.txt", 30), File("b.txt", 10), File("c.txt", 20), File("e.txt", 40));
        _model.SortBy(FileListColumn.Size);
        _model.SortBy(FileListColumn.Size);
        _model.SetSelection(new[] { 1 });

        await _model.LoadAsync(reload);

        Assert.Equal(new[] { "d", "e.txt", "a.txt", "c.txt", "b.txt" }, NamesOf(_model.Items));
        Assert.Equal(new FileListSort(FileListColumn.Size, true), _model.Sort);
        Assert.True(_model.IsSortChosen);
        Assert.Empty(_model.SelectedRows);
    }

    [Theory]
    [InlineData("before")]
    [InlineData("loading")]
    [InlineData("failed")]
    [InlineData("empty")]
    public async Task SortBy_NotLoadedOrEmpty_DoesNothing(string situation)
    {
        System.Threading.ManualResetEventSlim? gate = null;
        Task<bool>? loading = null;
        try
        {
            switch (situation)
            {
                case "loading":
                    gate = _source.Gate(@"C:\A");
                    loading = _model.LoadAsync(@"C:\A");
                    break;
                case "failed":
                    _source.SetListing(@"C:\A", FileListing.Failed(FolderProblem.NotFound));
                    await _model.LoadAsync(@"C:\A");
                    break;
                case "empty":
                    await _model.LoadAsync(@"C:\A");
                    break;
            }

            int events = 0;
            _model.OrderChanged += (_, _) => events++;
            _model.Changed += (_, _) => events++;
            _model.SelectionChanged += (_, _) => events++;
            FileListState state = _model.State;

            _model.SortBy(FileListColumn.Size);

            Assert.Equal(FileListSort.Default, _model.Sort);
            Assert.False(_model.IsSortChosen);
            Assert.Equal(state, _model.State);
            Assert.Equal(0, events);
        }
        finally
        {
            gate?.Set();
            if (loading != null)
            {
                await loading;
            }
        }
    }

    [Fact]
    public async Task SortBy_UndefinedColumn_Throws()
    {
        await LoadSized();

        Assert.Throws<ArgumentOutOfRangeException>(() => _model.SortBy((FileListColumn)9));
    }
}
