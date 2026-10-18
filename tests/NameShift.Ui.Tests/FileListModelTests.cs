using System;
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
}
