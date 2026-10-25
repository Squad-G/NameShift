using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NameShift.Core;
using NameShift.Ui.Files;
using NameShift.Ui.Folders;
using NameShift.Ui.Tests.Files;
using Xunit;
using Xunit.Abstractions;
using static NameShift.Ui.Tests.FakeFileListSource;
using static NameShift.Ui.Tests.Files.TestWait;

namespace NameShift.Ui.Tests;

public sealed class FileListModelTests
{
    private readonly FakeFileListSource _source = new FakeFileListSource();
    private readonly FileListModel _model;
    private readonly ITestOutputHelper _output;
    private readonly FilterSeam _seam = new FilterSeam();
    private int _changes;
    private int _seamedChanges;
    private int _order;
    private int _selections;

    public FileListModelTests(ITestOutputHelper output)
    {
        _output = output;
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
    public async Task ExistingNamesByFolder_EveryEntry_NeverFiltered()
    {
        _source.SetEntries(@"C:\A", new[] { "secret.txt" }, File("b.txt"), File("a.txt"));

        await _model.LoadAsync(@"C:\A");

        Assert.Equal(new[] { "a.txt", "b.txt" }, _model.Items.Select(i => i.Name));
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> names = _model.ExistingNamesByFolder;
        Assert.Equal(new[] { @"C:\A" }, names.Keys);
        Assert.Equal(new[] { "a.txt", "b.txt", "secret.txt" }, names[@"C:\A"].OrderBy(n => n, StringComparer.Ordinal));

        Assert.True(await Within(_model.SetFilterAsync(new ItemFilter(mask: "a*"))));

        Assert.Equal(new[] { "a.txt" }, _model.Items.Select(i => i.Name));
        Assert.Same(names, _model.ExistingNamesByFolder);
    }

    [Fact]
    public async Task Load_Starting_ClearsExistingNames()
    {
        _source.SetEntries(@"C:\A", new[] { "secret.txt" }, File("a.txt"));
        await _model.LoadAsync(@"C:\A");
        var gate = _source.Gate(@"C:\B");

        Task<bool> loading = _model.LoadAsync(@"C:\B");

        Assert.Empty(_model.ExistingNamesByFolder);
        Assert.Empty(_model.AllItems);
        gate.Set();
        Assert.True(await loading);
    }

    [Fact]
    public async Task Load_Failed_HasNoExistingNames()
    {
        _source.SetListing(@"C:\A", FileListing.Failed(FolderProblem.NotFound));

        await _model.LoadAsync(@"C:\A");

        Assert.Empty(_model.ExistingNamesByFolder);
        Assert.Empty(_model.AllItems);
    }

    [Fact]
    public void Listing_Ok_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => FileListing.Ok(null!));
        IReadOnlyDictionary<string, IReadOnlyCollection<string>> names = FileListing.Ok(new[] { File("a.txt") }).ExistingNamesByFolder;
        Assert.Equal(new[] { @"C:\A" }, names.Keys);
        Assert.Equal(new[] { "a.txt" }, names[@"c:\a"]);
        Assert.Empty(FileListing.Ok(Array.Empty<RenameItem>()).ExistingNamesByFolder);
        Assert.Empty(FileListing.Failed(FolderProblem.NotFound).ExistingNamesByFolder);
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
        var names = _model.ExistingNamesByFolder;

        _model.SortBy(FileListColumn.Size);

        Assert.NotSame(oldItems, _model.Items);
        Assert.Same(names, _model.ExistingNamesByFolder);
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

    // NS-33: the filtered list

    /// <summary>A model whose filter goes through <see cref="_seam"/>, with every event counted.</summary>
    private FileListModel Seamed()
    {
        var model = new FileListModel(_source, _seam.Includes);
        model.Changed += (_, _) => _seamedChanges++;
        model.OrderChanged += (_, _) => _order++;
        model.SelectionChanged += (_, _) => _selections++;
        return model;
    }

    private int Events => _seamedChanges + _order + _selections;

    private void ResetEvents()
    {
        _seamedChanges = 0;
        _order = 0;
        _selections = 0;
    }

    private static RenameItem SystemFolder(string name) => new RenameItem(name, @"C:\A", ItemKind.Folder, ItemAttributes.System);

    private static RenameItem Sized(string name, long size) => File(name, size);

    // AC 6

    [Fact]
    public async Task Load_AllItems_HoldsHiddenAndSystem_ItemsDoNot()
    {
        _source.SetEntries(@"C:\A", File("a.txt"), Hidden("h.txt"), SystemFolder("s"), Folder("d"));

        await _model.LoadAsync(@"C:\A");

        Assert.Equal(new[] { "d", "s", "a.txt", "h.txt" }, NamesOf(_model.AllItems));
        Assert.Equal(new[] { "d", "a.txt" }, NamesOf(_model.Items));
        Assert.Same(_model.AllItems[0], _model.Items[0]);
        Assert.Same(_model.AllItems[2], _model.Items[1]);
        Assert.True(_model.IsFilterDefault);
    }

    [Fact]
    public async Task Load_DefaultFilter_ItemsAsBefore()
    {
        _source.SetEntries(@"C:\A", File("b.txt"), Folder("d"), File("a.txt"));

        await _model.LoadAsync(@"C:\A");

        Assert.Equal(new[] { "d", "a.txt", "b.txt" }, NamesOf(_model.Items));
        Assert.Equal(_model.AllItems, _model.Items);
        Assert.False(_model.IsFilterPending);
    }

    // AC 7

    [Fact]
    public async Task SetFilter_Applied_NewItems_BatchRebuilt_SourceNotCalled()
    {
        _source.SetEntries(@"C:\A", File("a.txt"), File("b.jpg"), File("c.txt"));
        await _model.LoadAsync(@"C:\A");
        IReadOnlyList<RenameItem> before = _model.Items;
        int calls = _source.Calls;
        var filter = new ItemFilter(mask: "*.txt");

        Assert.True(await Within(_model.SetFilterAsync(filter)));

        Assert.NotSame(before, _model.Items);
        Assert.Equal(new[] { "a.txt", "c.txt" }, NamesOf(_model.Items));
        Assert.Same(filter, _model.Filter);
        Assert.False(_model.IsFilterPending);
        Assert.False(_model.IsFilterDefault);
        Assert.True(_model.Batch.IsWholeList);
        Assert.Same(_model.Items, _model.Batch.Items);
        Assert.Equal(calls, _source.Calls);
        Assert.Equal(3, _model.AllItems.Count);
    }

    [Fact]
    public async Task SetFilter_WhilePending_OldItemsWhole_NothingRaised()
    {
        FileListModel model = Seamed();
        _source.SetEntries(@"C:\A", File("a.txt"), File("b.jpg"), File("c.txt"));
        await model.LoadAsync(@"C:\A");
        ResetEvents();
        IReadOnlyList<RenameItem> before = model.Items;
        ItemFilter applied = model.Filter;
        var slow = new ItemFilter(mask: "*.txt");
        using var gate = new TestGate();
        _seam.Use(slow, item =>
        {
            gate.Block();
            return slow.Includes(item);
        });

        Task<bool> run = model.SetFilterAsync(slow);
        gate.WaitUntilReached();

        Assert.True(model.IsFilterPending);
        Assert.Same(before, model.Items);
        Assert.Same(applied, model.Filter);
        Assert.Equal(3, model.Items.Count);
        Assert.Equal(0, Events);
        gate.Release();
        Assert.True(await Within(run));
        Assert.Equal(1, _order);
        Assert.Equal(new[] { "a.txt", "c.txt" }, NamesOf(model.Items));
        Assert.False(model.IsFilterPending);
    }

    [Fact]
    public async Task SetFilter_KeepsSelectedItemsStillListed()
    {
        _source.SetEntries(@"C:\A", File("a.jpg"), File("a.txt"), File("c.jpg"), File("d.txt"));
        await _model.LoadAsync(@"C:\A");
        RenameItem aTxt = _model.Items[1];
        _model.SetSelection(new[] { 1, 2 });

        Assert.True(await Within(_model.SetFilterAsync(new ItemFilter(mask: "*.txt"))));

        Assert.Equal(new[] { "a.txt", "d.txt" }, NamesOf(_model.Items));
        Assert.False(_model.Batch.IsWholeList);
        Assert.Equal(new[] { aTxt }, _model.Batch.Items);
        Assert.Equal(new[] { 0 }, _model.SelectedRows);
        Assert.Equal(new[] { 0 }, _model.Batch.Rows);
        Assert.Same(_model.Items, _model.Batch.ListItems);
    }

    [Fact]
    public async Task SetFilter_SelectionChangedWhilePending_UsesCurrentSelection()
    {
        FileListModel model = Seamed();
        _source.SetEntries(@"C:\A", File("a.txt"), File("b.txt"), File("c.jpg"));
        await model.LoadAsync(@"C:\A");
        model.SetSelection(new[] { 0 });
        RenameItem b = model.Items[1];
        var slow = new ItemFilter(mask: "*.txt");
        using var gate = new TestGate();
        _seam.Use(slow, item =>
        {
            gate.Block();
            return slow.Includes(item);
        });

        Task<bool> run = model.SetFilterAsync(slow);
        gate.WaitUntilReached();
        model.SetSelection(new[] { 1 });
        gate.Release();

        Assert.True(await Within(run));
        Assert.Equal(new[] { b }, model.Batch.Items);
        Assert.Equal(new[] { 1 }, model.SelectedRows);
    }

    [Fact]
    public async Task SetFilter_NewlyShownItems_NotSelected()
    {
        _source.SetEntries(@"C:\A", File("a.txt"), File("b.txt"), Hidden("h.txt"));
        await _model.LoadAsync(@"C:\A");
        RenameItem a = _model.Items[0];
        _model.SetSelection(new[] { 0 });

        Assert.True(await Within(_model.SetFilterAsync(new ItemFilter(includeHidden: true))));

        Assert.Equal(new[] { "a.txt", "b.txt", "h.txt" }, NamesOf(_model.Items));
        Assert.Equal(new[] { a }, _model.Batch.Items);
        Assert.Equal(new[] { 0 }, _model.SelectedRows);
    }

    [Fact]
    public async Task SetFilter_EverySelectedRowStillListed_AllSelected()
    {
        _source.SetEntries(@"C:\A", File("a.txt"), File("b.jpg"), File("c.txt"));
        await _model.LoadAsync(@"C:\A");
        _model.SetSelection(new[] { 0, 2 });

        Assert.True(await Within(_model.SetFilterAsync(new ItemFilter(mask: "*.txt"))));

        Assert.True(_model.Batch.IsWholeList);
        Assert.Equal(new[] { 0, 1 }, _model.SelectedRows);
    }

    [Fact]
    public async Task SetFilter_AllSelectedHidden_WholeList()
    {
        _source.SetEntries(@"C:\A", File("a.txt"), File("b.jpg"), File("c.txt"));
        await _model.LoadAsync(@"C:\A");
        _model.SetSelection(new[] { 1 });

        Assert.True(await Within(_model.SetFilterAsync(new ItemFilter(mask: "*.txt"))));

        Assert.Empty(_model.SelectedRows);
        Assert.True(_model.Batch.IsWholeList);
        Assert.Same(_model.Items, _model.Batch.Items);
    }

    [Fact]
    public async Task SetFilter_RaisesOnlyOrderChanged_Once()
    {
        FileListModel model = Seamed();
        _source.SetEntries(@"C:\A", File("a.txt"), File("b.jpg"), File("c.txt"));
        await model.LoadAsync(@"C:\A");
        model.SetSelection(new[] { 0 });
        ResetEvents();
        ItemFilter? seen = null;
        model.OrderChanged += (_, _) => seen = model.Filter;
        var filter = new ItemFilter(mask: "*.txt");

        Assert.True(await Within(model.SetFilterAsync(filter)));

        Assert.Equal(1, _order);
        Assert.Equal(0, _seamedChanges);
        Assert.Equal(0, _selections);
        Assert.Same(filter, seen);
    }

    [Fact]
    public async Task SetFilter_SameInstance_ReturnsFalse_RaisesNothing()
    {
        FileListModel model = Seamed();
        _source.SetEntries(@"C:\A", File("a.txt"), File("b.jpg"));
        await model.LoadAsync(@"C:\A");
        ResetEvents();
        int calls = _seam.Calls;

        Assert.False(await Within(model.SetFilterAsync(model.Filter)));
        Assert.Equal(calls, _seam.Calls);

        var filter = new ItemFilter(mask: "*.txt");
        Assert.True(await Within(model.SetFilterAsync(filter)));
        ResetEvents();
        calls = _seam.Calls;

        Assert.False(await Within(model.SetFilterAsync(filter)));

        Assert.Equal(0, Events);
        Assert.Equal(calls, _seam.Calls);
    }

    [Fact]
    public async Task SetFilter_NewerWins_OlderFinishesLast()
    {
        FileListModel model = Seamed();
        _source.SetEntries(@"C:\A", File("a.txt"), File("b.jpg"), File("c.txt"));
        await model.LoadAsync(@"C:\A");
        ResetEvents();
        var f1 = new ItemFilter(mask: "*.txt");
        var f2 = new ItemFilter(mask: "*.jpg");
        using var gate = new TestGate();
        _seam.Use(f1, item =>
        {
            gate.Block();
            return f1.Includes(item);
        });

        Task<bool> t1 = model.SetFilterAsync(f1);
        gate.WaitUntilReached();
        Task<bool> t2 = model.SetFilterAsync(f2);

        Assert.True(await Within(t2));
        IReadOnlyList<RenameItem> shown = model.Items;
        Assert.Equal(new[] { "b.jpg" }, NamesOf(shown));
        gate.Release();
        Assert.False(await Within(t1));
        Assert.Same(shown, model.Items);
        Assert.Same(f2, model.Filter);
        Assert.False(model.IsFilterPending);
        Assert.Equal(1, _order);
    }

    [Fact]
    public async Task SetFilter_NewerWins_OlderFinishesFirst()
    {
        FileListModel model = Seamed();
        _source.SetEntries(@"C:\A", File("a.txt"), File("b.jpg"), File("c.txt"));
        await model.LoadAsync(@"C:\A");
        ResetEvents();
        RenameItem last = model.AllItems[model.AllItems.Count - 1];
        var f1 = new ItemFilter(mask: "*.txt");
        var f2 = new ItemFilter(mask: "*.jpg");
        using var gate1 = new TestGate();
        using var gate2 = new TestGate();

        // f1 waits on its last item: no token check comes after it, so only the version can stop its result.
        _seam.Use(f1, item =>
        {
            if (ReferenceEquals(item, last))
            {
                gate1.Block();
            }

            return f1.Includes(item);
        });
        _seam.Use(f2, item =>
        {
            gate2.Block();
            return f2.Includes(item);
        });

        Task<bool> t1 = model.SetFilterAsync(f1);
        gate1.WaitUntilReached();
        Task<bool> t2 = model.SetFilterAsync(f2);
        gate2.WaitUntilReached();
        gate1.Release();

        Assert.False(await Within(t1));
        Assert.Equal(0, Events);
        Assert.Equal(3, model.Items.Count);
        Assert.True(model.IsFilterPending);
        gate2.Release();
        Assert.True(await Within(t2));
        Assert.Equal(new[] { "b.jpg" }, NamesOf(model.Items));
        Assert.Same(f2, model.Filter);
        Assert.Equal(1, _order);
    }

    [Fact]
    public async Task SetFilter_Cancelled_StopsTheLoop()
    {
        FileListModel model = Seamed();
        _source.SetEntries(@"C:\A", Enumerable.Range(0, 10000).Select(i => File("file" + i + ".txt")).ToArray());
        await model.LoadAsync(@"C:\A");
        var slow = new ItemFilter(mask: "*.txt");
        var f2 = new ItemFilter(mask: "*1*");
        int slowCalls = 0;
        using var gate = new TestGate();
        _seam.Use(slow, item =>
        {
            if (Interlocked.Increment(ref slowCalls) == 5)
            {
                gate.Block();
            }

            return true;
        });

        Task<bool> t1 = model.SetFilterAsync(slow);
        gate.WaitUntilReached();
        Task<bool> t2 = model.SetFilterAsync(f2);
        Assert.True(await Within(t2));
        gate.Release();

        Assert.False(await Within(t1));
        Assert.InRange(Volatile.Read(ref slowCalls), 5, 6);
        Assert.Same(f2, model.Filter);
        Assert.All(model.Items, item => Assert.Contains("1", item.Name));
    }

    [Fact]
    public async Task Load_DuringPendingFilter_AppliesFilterToNewFolder()
    {
        FileListModel model = Seamed();
        _source.SetEntries(@"C:\A", File("a.txt"), File("b.jpg"));
        _source.SetEntries(@"C:\B", File("x.txt", folder: @"C:\B"), File("y.jpg", folder: @"C:\B"));
        await model.LoadAsync(@"C:\A");
        RenameItem lastOfA = model.AllItems[model.AllItems.Count - 1];
        var slow = new ItemFilter(mask: "*.txt");
        using var gate = new TestGate();

        // Held on the last item of C:\A: no token check comes after it, so only the shared version stops its result.
        _seam.Use(slow, item =>
        {
            if (ReferenceEquals(item, lastOfA))
            {
                gate.Block();
            }

            return slow.Includes(item);
        });

        Task<bool> t1 = model.SetFilterAsync(slow);
        gate.WaitUntilReached();
        Task<bool> load = model.LoadAsync(@"C:\B");

        Assert.True(await Within(load));
        Assert.Equal(new[] { "x.txt" }, NamesOf(model.Items));
        Assert.Same(slow, model.Filter);
        Assert.False(model.IsFilterPending);
        gate.Release();
        Assert.False(await Within(t1));
        await Within(model.PendingFilter);
        Assert.Equal(new[] { "x.txt" }, NamesOf(model.Items));
        Assert.All(model.Items, item => Assert.Equal(@"C:\B", item.FolderPath));
        Assert.Equal(@"C:\B", model.FolderPath);
    }

    [Fact]
    public async Task SetFilter_WhileLoading_AppliedAfterLoad()
    {
        FileListModel model = Seamed();
        _source.SetEntries(@"C:\A", File("a.txt"), File("b.jpg"));
        ManualResetEventSlim gate = _source.Gate(@"C:\A");
        try
        {
            Task<bool> load = model.LoadAsync(@"C:\A");
            var filter = new ItemFilter(mask: "*.txt");

            Assert.False(await Within(model.SetFilterAsync(filter)));
            Assert.Equal(1, _seamedChanges);
            Assert.Equal(0, _order);
            gate.Set();
            Assert.True(await Within(load));
            Assert.Equal(2, _seamedChanges);
            await Within(model.PendingFilter);

            Assert.Equal(new[] { "a.txt" }, NamesOf(model.Items));
            Assert.Same(filter, model.Filter);
            Assert.Equal(1, _order);
            Assert.False(model.IsFilterPending);
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task SetFilter_BeforeAnyLoad_UsedByTheLoad()
    {
        FileListModel model = Seamed();
        _source.SetEntries(@"C:\A", File("a.txt"), File("b.jpg"));
        var filter = new ItemFilter(mask: "*.txt");

        Assert.False(await Within(model.SetFilterAsync(filter)));
        Assert.Equal(0, Events);
        Assert.Same(filter, model.Filter);
        Assert.Equal(0, _source.Calls);

        Assert.True(await Within(model.LoadAsync(@"C:\A")));

        Assert.Equal(new[] { "a.txt" }, NamesOf(model.Items));
        Assert.Same(filter, model.Filter);
        Assert.Equal(0, _order);
        await Within(model.PendingFilter);
        Assert.Equal(0, _order);
    }

    [Fact]
    public async Task SetFilter_AfterFailedLoad_UsedByTheNextLoad()
    {
        _source.SetListing(@"C:\A", FileListing.Failed(FolderProblem.NotFound));
        await _model.LoadAsync(@"C:\A");
        var filter = new ItemFilter(mask: "*.txt");

        Assert.False(await Within(_model.SetFilterAsync(filter)));
        Assert.Same(filter, _model.Filter);

        _source.SetEntries(@"C:\B", File("x.txt", folder: @"C:\B"), File("y.jpg", folder: @"C:\B"));
        await _model.LoadAsync(@"C:\B");
        Assert.Equal(new[] { "x.txt" }, NamesOf(_model.Items));
    }

    [Fact]
    public async Task SortBy_WhilePending_ResultInCurrentOrder()
    {
        FileListModel model = Seamed();
        _source.SetEntries(@"C:\A", Folder("d"), Sized("a.txt", 30), Sized("b.txt", 10), Sized("c.txt", 20), Sized("e.jpg", 5));
        await model.LoadAsync(@"C:\A");
        var slow = new ItemFilter(mask: "*.txt");
        using var gate = new TestGate();
        _seam.Use(slow, item =>
        {
            gate.Block();
            return slow.Includes(item);
        });

        Task<bool> run = model.SetFilterAsync(slow);
        gate.WaitUntilReached();
        model.SortBy(FileListColumn.Size);
        Assert.Equal(new[] { "d", "e.jpg", "b.txt", "c.txt", "a.txt" }, NamesOf(model.Items));
        gate.Release();

        Assert.True(await Within(run));
        Assert.Equal(new[] { "b.txt", "c.txt", "a.txt" }, NamesOf(model.Items));
        Assert.Equal(new FileListSort(FileListColumn.Size, false), model.Sort);
    }

    [Fact]
    public async Task SortBy_KeepsFilter_AndSortsHiddenOnes()
    {
        _source.SetEntries(@"C:\A", Sized("a.txt", 30), Sized("b.jpg", 10), Sized("c.txt", 20), Sized("d.jpg", 40));
        await _model.LoadAsync(@"C:\A");
        Assert.True(await Within(_model.SetFilterAsync(new ItemFilter(mask: "*.txt"))));
        int calls = _source.Calls;

        _model.SortBy(FileListColumn.Size);
        Assert.Equal(new[] { "c.txt", "a.txt" }, NamesOf(_model.Items));
        Assert.Equal(new[] { "b.jpg", "c.txt", "a.txt", "d.jpg" }, NamesOf(_model.AllItems));

        Assert.True(await Within(_model.SetFilterAsync(new ItemFilter())));
        Assert.Equal(new[] { "b.jpg", "c.txt", "a.txt", "d.jpg" }, NamesOf(_model.Items));
        Assert.Equal(calls, _source.Calls);
    }

    [Fact]
    public async Task SortBy_FilterHidesEverything_StillSorts()
    {
        _source.SetEntries(@"C:\A", Sized("a.txt", 30), Sized("b.txt", 10));
        await _model.LoadAsync(@"C:\A");
        Assert.True(await Within(_model.SetFilterAsync(new ItemFilter(mask: "*.jpg"))));
        int order = 0;
        _model.OrderChanged += (_, _) => order++;

        _model.SortBy(FileListColumn.Size);

        Assert.Equal(1, order);
        Assert.Empty(_model.Items);
        Assert.Equal(new[] { "b.txt", "a.txt" }, NamesOf(_model.AllItems));
    }

    [Fact]
    public async Task SetFilter_FilterThrows_NothingChanges()
    {
        FileListModel model = Seamed();
        _source.SetEntries(@"C:\A", File("a.txt"), File("b.jpg"));
        await model.LoadAsync(@"C:\A");
        ResetEvents();
        IReadOnlyList<RenameItem> before = model.Items;
        ItemFilter applied = model.Filter;
        var bad = new ItemFilter(mask: "*.txt");
        _seam.Use(bad, _ => throw new InvalidOperationException("A filter bug."));

        Assert.False(await Within(model.SetFilterAsync(bad)));

        Assert.False(model.IsFilterPending);
        Assert.Same(before, model.Items);
        Assert.Same(applied, model.Filter);
        Assert.Equal(0, Events);
        int calls = _seam.Calls;
        Assert.False(await Within(model.SetFilterAsync(bad)));
        Assert.True(_seam.Calls > calls);
    }

    [Fact]
    public async Task SetFilter_Null_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _model.SetFilterAsync(null!));
    }

    [Fact]
    public async Task Dispose_PendingFilter_NeverApplies()
    {
        FileListModel model = Seamed();
        _source.SetEntries(@"C:\A", File("a.txt"), File("b.jpg"), File("c.txt"));
        await model.LoadAsync(@"C:\A");
        ResetEvents();
        IReadOnlyList<RenameItem> before = model.Items;
        RenameItem last = model.AllItems[model.AllItems.Count - 1];
        var slow = new ItemFilter(mask: "*.txt");
        using var gate = new TestGate();

        // Held on the last item: no token check comes after it, so only Dispose's guard stops its result.
        _seam.Use(slow, item =>
        {
            if (ReferenceEquals(item, last))
            {
                gate.Block();
            }

            return slow.Includes(item);
        });

        Task<bool> run = model.SetFilterAsync(slow);
        gate.WaitUntilReached();
        model.Dispose();
        gate.Release();

        Assert.False(await Within(run));
        Assert.Equal(0, Events);
        Assert.Same(before, model.Items);
    }

    [Fact]
    public async Task Dispose_PendingLoad_NeverApplies()
    {
        FileListModel model = Seamed();
        _source.SetEntries(@"C:\A", File("a.txt"));
        ManualResetEventSlim gate = _source.Gate(@"C:\A");
        try
        {
            Task<bool> load = model.LoadAsync(@"C:\A");
            Assert.Equal(1, _seamedChanges);

            model.Dispose();
            gate.Set();

            Assert.False(await Within(load));
            Assert.Equal(1, _seamedChanges);
            Assert.Empty(model.Items);
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task Dispose_ThenCalls_DoNothing()
    {
        FileListModel model = Seamed();
        _source.SetEntries(@"C:\A", File("a.txt"), File("b.jpg"));
        await model.LoadAsync(@"C:\A");
        ResetEvents();
        int sourceCalls = _source.Calls;
        int seamCalls = _seam.Calls;

        model.Dispose();

        Assert.False(await Within(model.SetFilterAsync(new ItemFilter(mask: "*.txt"))));
        Assert.False(await Within(model.LoadAsync(@"C:\A")));
        model.Dispose();
        Assert.Equal(sourceCalls, _source.Calls);
        Assert.Equal(seamCalls, _seam.Calls);
        Assert.Equal(0, Events);
        Assert.Equal(2, model.Items.Count);
    }

    /// <summary>A model with 10,000 items in C:\A and one in C:\B, and a filter run held on its 5th item: the run
    /// still has 9,995 items to go, so only a cancellation stops it early.</summary>
    private async Task<(FileListModel Model, Task<bool> Run, Func<int> Calls)> HeldOnItemFive(TestGate gate)
    {
        FileListModel model = Seamed();
        _source.SetEntries(@"C:\A", Enumerable.Range(0, 10000).Select(i => File("file" + i + ".txt")).ToArray());
        _source.SetEntries(@"C:\B", File("x.txt", folder: @"C:\B"));
        Assert.True(await Within(model.LoadAsync(@"C:\A")));
        var slow = new ItemFilter(mask: "*.txt");
        int calls = 0;
        _seam.Use(slow, item =>
        {
            if (Interlocked.Increment(ref calls) == 5)
            {
                gate.Block();
            }

            return true;
        });

        Task<bool> run = model.SetFilterAsync(slow);
        gate.WaitUntilReached();
        return (model, run, () => Volatile.Read(ref calls));
    }

    [Fact]
    public async Task Load_StopsAPendingFilterLoop()
    {
        using var gate = new TestGate();
        (FileListModel model, Task<bool> run, Func<int> calls) = await HeldOnItemFive(gate);

        Assert.True(await Within(model.LoadAsync(@"C:\B")));
        gate.Release();

        Assert.False(await Within(run));

        // 5 calls before the load, at most one more after the release, and one for C:\B's only item.
        Assert.InRange(calls(), 5, 7);
        Assert.Equal(new[] { "x.txt" }, NamesOf(model.Items));
    }

    [Fact]
    public async Task Dispose_StopsAPendingFilterLoop()
    {
        using var gate = new TestGate();
        (FileListModel model, Task<bool> run, Func<int> calls) = await HeldOnItemFive(gate);
        ResetEvents();

        model.Dispose();
        gate.Release();

        Assert.False(await Within(run));
        Assert.InRange(calls(), 5, 6);
        Assert.Equal(0, Events);
    }

    [Fact]
    public async Task LoadAsync_SupersededEmptyLoad_IsDiscarded()
    {
        // An empty listing reaches no token check: only the version stops it.
        _source.SetEntries(@"C:\A");
        _source.SetEntries(@"C:\B", File("b.txt", folder: @"C:\B"));
        ManualResetEventSlim gateA = _source.Gate(@"C:\A");
        try
        {
            Task<bool> loadA = _model.LoadAsync(@"C:\A");
            Assert.True(SpinWait.SpinUntil(() => _source.Calls >= 1, TestWait.Timeout), "The first listing never started.");

            Assert.True(await Within(_model.LoadAsync(@"C:\B")));
            gateA.Set();

            Assert.False(await Within(loadA));
            Assert.Equal(@"C:\B", _model.FolderPath);
            Assert.Equal(new[] { "b.txt" }, NamesOf(_model.Items));
        }
        finally
        {
            gateA.Set();
        }
    }

    [Fact]
    public async Task Dispose_InTheLoadsChangedHandler_NoFollowUpRun()
    {
        FileListModel model = Seamed();
        _source.SetEntries(@"C:\A", File("a.txt"), File("b.jpg"));
        ManualResetEventSlim gate = _source.Gate(@"C:\A");
        try
        {
            Task<bool> load = model.LoadAsync(@"C:\A");
            var filter = new ItemFilter(mask: "*.txt");
            Assert.False(await Within(model.SetFilterAsync(filter)));
            model.Changed += (_, _) =>
            {
                if (model.State == FileListState.Loaded)
                {
                    model.Dispose();
                }
            };
            int calls = _seam.Calls;

            gate.Set();
            Assert.True(await Within(load));
            await Within(model.PendingFilter);

            Assert.Equal(0, _order);
            Assert.NotSame(filter, model.Filter);
            Assert.Equal(new[] { "a.txt", "b.jpg" }, NamesOf(model.Items));

            // Only the load's own pass over its two items: no filter run after Dispose.
            Assert.Equal(calls + 2, _seam.Calls);
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task SetFilter_WhileFailingLoad_IsKept()
    {
        _source.SetListing(@"C:\A", FileListing.Failed(FolderProblem.AccessDenied));
        ManualResetEventSlim gate = _source.Gate(@"C:\A");
        try
        {
            Task<bool> load = _model.LoadAsync(@"C:\A");
            var filter = new ItemFilter(mask: "*.txt");
            Assert.False(await Within(_model.SetFilterAsync(filter)));

            gate.Set();
            Assert.True(await Within(load));

            Assert.Equal(FileListState.Failed, _model.State);
            Assert.Same(filter, _model.Filter);
        }
        finally
        {
            gate.Set();
        }
    }

    // AC 8

    [Fact]
    public async Task Message_FilterHidesAll_NoItemMatches()
    {
        _source.SetEntries(@"C:\A", File("a.txt"), File("b.txt"));
        await _model.LoadAsync(@"C:\A");

        Assert.True(await Within(_model.SetFilterAsync(new ItemFilter(mask: "*.jpg"))));

        Assert.Empty(_model.Items);
        Assert.False(_model.IsFilterDefault);
        Assert.Equal(Strings.NoItemMatchesFilter, _model.Message);
        Assert.Equal("No item matches the filters.", _model.Message);
    }

    [Fact]
    public async Task Message_OnlyHiddenFiles_DefaultFilter_FolderEmpty()
    {
        _source.SetEntries(@"C:\A", new[] { "h.txt", "i.txt" });

        await _model.LoadAsync(@"C:\A");

        Assert.Empty(_model.Items);
        Assert.Equal(2, _model.AllItems.Count);
        Assert.True(_model.IsFilterDefault);
        Assert.Equal(Strings.FolderEmpty, _model.Message);
    }

    [Fact]
    public async Task Message_NoEntry_FolderEmpty()
    {
        Assert.False(await Within(_model.SetFilterAsync(new ItemFilter(mask: "*.jpg"))));

        await _model.LoadAsync(@"C:\A");

        Assert.False(_model.IsFilterDefault);
        Assert.Equal(Strings.FolderEmpty, _model.Message);
    }

    // AC 10

    [Fact]
    public async Task SetFilter_TenThousandItems_UnderOneSecond()
    {
        _source.SetEntries(@"C:\A", Enumerable.Range(0, 10000).Select(i => File("file" + i + (i % 2 == 0 ? ".txt" : ".jpg"))).ToArray());
        await _model.LoadAsync(@"C:\A");
        _model.SetSelection(Enumerable.Range(0, 100).ToArray());
        int expected = _model.AllItems.Count(i => i.Name.EndsWith(".txt", StringComparison.Ordinal) && i.Name.IndexOf('5') >= 0);

        var watch = Stopwatch.StartNew();
        bool applied = await Within(_model.SetFilterAsync(new ItemFilter(mask: "*5*.txt")));
        watch.Stop();

        Assert.True(applied);
        Assert.Equal(expected, _model.Items.Count);
        _output.WriteLine(string.Format(CultureInfo.InvariantCulture, "Re-filter of 10,000 items: {0:N1} ms.", watch.Elapsed.TotalMilliseconds));
        Assert.True(
            watch.Elapsed < TimeSpan.FromSeconds(1),
            string.Format(CultureInfo.InvariantCulture, "Re-filter took {0:N0} ms; budget 1,000 ms.", watch.Elapsed.TotalMilliseconds));
    }

    // NS-74: sub-folders and the walk's token

    private void WaitForCalls(int calls)
    {
        Assert.True(SpinWait.SpinUntil(() => _source.Calls >= calls, TestWait.Timeout), "The source was not called in time.");
    }

    [Fact]
    public async Task Load_PassesTheLoadToken()
    {
        _source.SetEntries(@"C:\A", File("a.txt"));

        Assert.True(await Within(_model.LoadAsync(@"C:\A")));

        Assert.True(_source.LastToken.CanBeCanceled);
        Assert.False(_source.LastToken.IsCancellationRequested);
        Assert.False(_source.LastIncludeSubfolders);
    }

    [Fact]
    public async Task Load_SupersededByNewLoad_TokenCancelled()
    {
        _source.BlockUntilCancelled(@"C:\A");
        _source.SetEntries(@"C:\B", File("b.txt", folder: @"C:\B"));
        Task<bool> first = _model.LoadAsync(@"C:\A");
        WaitForCalls(1);
        CancellationToken firstToken = _source.LastToken;

        Task<bool> second = _model.LoadAsync(@"C:\B");

        Assert.True(firstToken.IsCancellationRequested);
        Assert.False(await Within(first));
        Assert.True(await Within(second));
        Assert.False(_source.LastToken.IsCancellationRequested);
    }

    [Fact]
    public async Task Load_SourceThrowsCanceledWithoutCancel_IsFailed()
    {
        _source.SetThrows(@"C:\A", new OperationCanceledException());

        Assert.True(await Within(_model.LoadAsync(@"C:\A")));

        Assert.Equal(FileListState.Failed, _model.State);
        Assert.Equal(FolderProblem.Unavailable, _model.Problem);
        Assert.Equal(Strings.FolderUnavailable, _model.Message);
    }

    [Fact]
    public async Task SkippedFolders_FromTheListing()
    {
        Assert.Equal(0, _model.SkippedFolders);
        _source.SetListing(@"C:\A", FileListing.Ok(new[] { File("a.txt") }, 3));
        _source.SetListing(@"C:\B", FileListing.Ok(new[] { File("b.txt", folder: @"C:\B") }, 2));
        _source.SetListing(@"C:\C", FileListing.Failed(FolderProblem.AccessDenied));
        Assert.True(await Within(_model.LoadAsync(@"C:\A")));
        Assert.Equal(3, _model.SkippedFolders);
        ManualResetEventSlim gate = _source.Gate(@"C:\B");
        try
        {
            Task<bool> loading = _model.LoadAsync(@"C:\B");
            Assert.Equal(0, _model.SkippedFolders);
            gate.Set();
            Assert.True(await Within(loading));
            Assert.Equal(2, _model.SkippedFolders);
        }
        finally
        {
            gate.Set();
        }

        Assert.True(await Within(_model.LoadAsync(@"C:\C")));
        Assert.Equal(0, _model.SkippedFolders);
    }

    [Fact]
    public void Listing_SkippedFolders_DefaultZero_NegativeThrows()
    {
        Assert.Equal(0, FileListing.Ok(Array.Empty<RenameItem>()).SkippedFolders);
        Assert.Equal(0, FileListing.Failed(FolderProblem.NotFound).SkippedFolders);
        Assert.Equal(4, FileListing.Ok(Array.Empty<RenameItem>(), 4).SkippedFolders);
        Assert.Throws<ArgumentOutOfRangeException>(() => FileListing.Ok(Array.Empty<RenameItem>(), -1));
    }

    [Fact]
    public async Task SetIncludeSubfolders_ReloadsSameFolder()
    {
        await LoadSized();
        _model.SortBy(FileListColumn.Size);
        _model.SetSelection(new[] { 1 });
        var states = new List<FileListState>();
        _model.Changed += (_, _) => states.Add(_model.State);

        bool applied = await Within(_model.SetIncludeSubfoldersAsync(true));

        Assert.True(applied);
        Assert.True(_model.IncludeSubfolders);
        Assert.Equal(new[] { FileListState.Loading, FileListState.Loaded }, states);
        Assert.Equal(2, _source.Calls);
        Assert.True(_source.LastIncludeSubfolders);
        Assert.Equal(@"C:\A", _model.FolderPath);
        Assert.Equal(new FileListSort(FileListColumn.Size, false), _model.Sort);
        Assert.True(_model.IsSortChosen);
        Assert.Empty(_model.SelectedRows);
    }

    [Fact]
    public async Task SetIncludeSubfolders_SameValue_False_NoLoad()
    {
        await LoadSized();

        Assert.False(await Within(_model.SetIncludeSubfoldersAsync(false)));
        Assert.Equal(1, _source.Calls);
        Assert.True(await Within(_model.SetIncludeSubfoldersAsync(true)));
        Assert.False(await Within(_model.SetIncludeSubfoldersAsync(true)));
        Assert.Equal(2, _source.Calls);
    }

    [Fact]
    public async Task SetIncludeSubfolders_NoFolder_OnlyStored()
    {
        Assert.False(await Within(_model.SetIncludeSubfoldersAsync(true)));
        Assert.True(_model.IncludeSubfolders);
        Assert.Equal(0, _source.Calls);
        Assert.Equal(0, _changes);

        Assert.True(await Within(_model.LoadAsync(@"C:\A")));

        Assert.True(_source.LastIncludeSubfolders);
    }

    [Fact]
    public async Task SetIncludeSubfolders_AfterDispose_False_NotStored()
    {
        await LoadSized();
        _model.Dispose();

        Assert.False(await Within(_model.SetIncludeSubfoldersAsync(true)));

        Assert.False(_model.IncludeSubfolders);
        Assert.Equal(1, _source.Calls);
    }

    [Fact]
    public async Task Reload_KeepsIncludeSubfolders()
    {
        await LoadSized();
        Assert.True(await Within(_model.SetIncludeSubfoldersAsync(true)));

        Assert.True(await Within(_model.LoadAsync(@"C:\B")));
        Assert.True(_source.LastIncludeSubfolders);
        Assert.True(await Within(_model.LoadAsync(@"C:\B")));
        Assert.True(_source.LastIncludeSubfolders);
        Assert.Equal(4, _source.Calls);
    }

    [Fact]
    public async Task FindRow_OnlyTheShownFolder()
    {
        // The shown folder's item is spelled in another case: the folder match ignores case.
        _source.SetEntries(@"C:\A", File("a.txt", folder: @"C:\A\x"), File("a.txt", folder: @"c:\a"), File("b.txt", folder: @"C:\A\x"));
        await _model.LoadAsync(@"C:\A");
        Assert.Equal(@"C:\A\x", _model.Items[0].FolderPath);

        int row = _model.FindRow("A.TXT");

        Assert.Equal(@"c:\a", _model.Items[row].FolderPath);
        Assert.Equal("a.txt", _model.Items[row].Name);
        Assert.Equal(-1, _model.FindRow("b.txt"));
    }

    [Fact]
    public async Task Dispose_CancelsTheWalk()
    {
        _source.BlockUntilCancelled(@"C:\A");
        Task<bool> load = _model.LoadAsync(@"C:\A");
        WaitForCalls(1);
        CancellationToken token = _source.LastToken;
        int changes = _changes;

        _model.Dispose();

        Assert.True(token.IsCancellationRequested);
        Assert.False(await Within(load));
        Assert.Equal(changes, _changes);
        Assert.Equal(FileListState.Loading, _model.State);
        Assert.Empty(_model.AllItems);
    }

    [Fact]
    public async Task SetIncludeSubfolders_CancelsTheWalk()
    {
        _source.SetEntries(@"C:\A", File("a.txt"));
        _source.BlockUntilCancelled(@"C:\A");
        Task<bool> first = _model.LoadAsync(@"C:\A");
        WaitForCalls(1);
        CancellationToken token = _source.LastToken;
        Assert.False(_source.LastIncludeSubfolders);

        Task<bool> reload = _model.SetIncludeSubfoldersAsync(true);

        Assert.True(token.IsCancellationRequested);
        Assert.False(await Within(first));
        Assert.True(await Within(reload));
        Assert.True(_source.LastIncludeSubfolders);
        Assert.Equal(new[] { "a.txt" }, NamesOf(_model.Items));
    }

    [Fact]
    public async Task BlockedUntilCancelled_EndsSuperseded()
    {
        var states = new List<FileListState>();
        _model.Changed += (_, _) => states.Add(_model.State);
        _source.BlockUntilCancelled(@"C:\A");
        _source.SetEntries(@"C:\B", File("b.txt", folder: @"C:\B"));
        Task<bool> first = _model.LoadAsync(@"C:\A");
        WaitForCalls(1);

        Task<bool> second = _model.LoadAsync(@"C:\B");

        Assert.False(await Within(first));
        Assert.True(await Within(second));
        Assert.DoesNotContain(FileListState.Failed, states);
        Assert.Equal(FileListState.Loaded, _model.State);
        Assert.Null(_model.Problem);
        Assert.Equal(@"C:\B", _model.FolderPath);
        Assert.Equal(new[] { "b.txt" }, NamesOf(_model.Items));
    }

    [Fact]
    public async Task SortThenFilter_KeepsGroupOrder()
    {
        _source.SetEntries(
            @"C:\A",
            File("a.txt", 1),
            File("c.txt", 9, @"C:\A\x"),
            File("b.txt", 5),
            File("e.jpg", 7),
            File("d.txt", 3, @"C:\A\x"));
        await _model.LoadAsync(@"C:\A");
        _model.SortBy(FileListColumn.Size);
        _model.SortBy(FileListColumn.Size);
        Assert.Equal(new[] { "e.jpg", "b.txt", "a.txt", "c.txt", "d.txt" }, NamesOf(_model.Items));

        Assert.True(await Within(_model.SetFilterAsync(new ItemFilter(mask: "*.txt"))));

        Assert.Equal(new[] { "b.txt", "a.txt", "c.txt", "d.txt" }, NamesOf(_model.Items));
        Assert.Equal(new[] { @"C:\A", @"C:\A", @"C:\A\x", @"C:\A\x" }, _model.Items.Select(i => i.FolderPath));
    }
}
