using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NameShift.Core;
using NameShift.Ui.Files;
using Xunit;
using static NameShift.Ui.Tests.FakeFileListSource;
using static NameShift.Ui.Tests.Files.TestWait;

namespace NameShift.Ui.Tests.Files;

/// <summary>NS-88 AC 5: the selection of the names given on the command line.</summary>
public sealed class SelectNamesTests
{
    private const string FolderA = @"C:\A";
    private readonly FakeFileListSource _source = new FakeFileListSource();

    private async Task<FileListModel> LoadedAsync(params RenameItem[] items)
    {
        _source.SetEntries(FolderA, items);
        var model = new FileListModel(_source);
        Assert.True(await Within(model.LoadAsync(FolderA)));
        return model;
    }

    [Fact]
    public async Task SelectNames_RootOnly_WithSubfolders()
    {
        _source.SetEntries(FolderA, File("a.jpg", folder: FolderA), File("a.jpg", folder: @"C:\A\sub"));
        var filters = new FilterPanelModel();
        using var model = new FileListModel(_source);
        using var binding = new FilterBinding(filters, model);
        filters.IncludeSubfolders = true;
        Assert.True(await Within(model.LoadAsync(FolderA)));
        Assert.Equal(2, model.Items.Count);

        IReadOnlyList<int> rows = model.SelectNames(new[] { "a.jpg" });

        Assert.Single(rows);
        Assert.Equal(FolderA, model.Items[rows[0]].FolderPath);
        Assert.Equal(rows, model.SelectedRows);
        Assert.Equal(1, model.Batch.Count);
        Assert.Equal(FolderA, model.Batch.Items[0].FolderPath);
    }

    [Fact]
    public async Task SelectNames_Selects_CaseInsensitive_InNameOrder_NoDuplicates()
    {
        using FileListModel model = await LoadedAsync(File("a", folder: FolderA), File("b", folder: FolderA), File("c", folder: FolderA), File("d", folder: FolderA));

        IReadOnlyList<int> rows = model.SelectNames(new[] { "C", "a", "c", "missing", "" });

        Assert.Equal(new[] { 2, 0 }, rows);
        Assert.Equal(new[] { 0, 2 }, model.SelectedRows);
    }

    [Fact]
    public async Task SelectNames_HiddenOrFiltered_NotSelected()
    {
        _source.SetEntries(FolderA, new[] { "h.txt" }, File("a.txt", folder: FolderA), File("b.txt", folder: FolderA), File("c.txt", folder: FolderA));
        using var model = new FileListModel(_source);
        Assert.True(await Within(model.LoadAsync(FolderA)));

        IReadOnlyList<int> rows = model.SelectNames(new[] { "h.txt", "a.txt" });

        Assert.Single(rows);
        Assert.Equal("a.txt", model.Items[rows[0]].Name);
        Assert.Equal(1, model.Batch.Count);
    }

    [Fact]
    public async Task SelectNames_NoneFound_SelectionUnchanged()
    {
        using FileListModel model = await LoadedAsync(File("a", folder: FolderA), File("b", folder: FolderA), File("c", folder: FolderA));
        model.SetSelection(new[] { 1 });

        IReadOnlyList<int> rows = model.SelectNames(new[] { "nothing", "nope" });

        Assert.Empty(rows);
        Assert.Equal(new[] { 1 }, model.SelectedRows);
        Assert.Equal(1, model.Batch.Count);
    }

    [Fact]
    public async Task SelectNames_Replaces()
    {
        using FileListModel model = await LoadedAsync(File("a", folder: FolderA), File("b", folder: FolderA), File("c", folder: FolderA));
        model.SetSelection(new[] { 0, 1 });

        model.SelectNames(new[] { "c" });

        Assert.Equal(new[] { 2 }, model.SelectedRows);
    }

    [Fact]
    public void SelectNames_NotLoaded_SelectsNothing()
    {
        using var model = new FileListModel(_source);

        Assert.Empty(model.SelectNames(new[] { "a" }));
    }

    [Fact]
    public async Task SelectNames_20000_Linear()
    {
        const int Count = 20000;
        RenameItem[] items = Enumerable.Range(0, Count).Select(i => File("file " + i + ".jpg", folder: FolderA)).ToArray();
        using FileListModel model = await LoadedAsync(items);

        IReadOnlyList<int> rows = model.SelectNames(items.Select(i => i.Name.ToUpperInvariant()).Reverse().ToList());

        Assert.Equal(Count, rows.Count);
        Assert.Equal(Count, rows.Distinct().Count());
        Assert.True(model.Batch.IsWholeList); // every row selected is the whole list
        Assert.Equal(Count, model.Batch.Count);
    }

    [Fact]
    public async Task Batch_IsTheSelectedRows()
    {
        using FileListModel model = await LoadedAsync(File("a", folder: FolderA), File("b", folder: FolderA), File("c", folder: FolderA), File("d", folder: FolderA));

        model.SelectNames(new[] { "b", "d" });

        Assert.False(model.Batch.IsWholeList);
        Assert.Equal(2, model.Batch.Count);
        Assert.Equal(new[] { "b", "d" }, model.Batch.Items.Select(i => i.Name));
    }
}
