using System.Threading.Tasks;
using NameShift.Ui.Files;
using Xunit;
using static NameShift.Ui.Tests.FakeFileListSource;

namespace NameShift.Ui.Tests.Files;

/// <summary>NS-78 AC 6: with the Sub-folders box on, a selection asked for by name (the command line or a drop)
/// still finds a file of the shown folder and never one of a sub-folder.</summary>
public sealed class SubfolderFindRowTests
{
    private const string FolderA = @"C:\A";

    [Fact]
    public async Task BoxOn_FindsRootFileOnly()
    {
        var source = new FakeFileListSource();
        source.SetEntries(FolderA, File("a.txt", folder: FolderA), File("b.txt", folder: @"C:\A\Sub"));
        var filters = new FilterPanelModel();
        using var files = new FileListModel(source);
        using var binding = new FilterBinding(filters, files);

        filters.IncludeSubfolders = true;
        Assert.True(await TestWait.Within(files.LoadAsync(FolderA)));

        Assert.True(source.LastIncludeSubfolders);
        Assert.Equal(2, files.Items.Count);
        Assert.True(files.FindRow("a.txt") >= 0);
        Assert.Equal(-1, files.FindRow("b.txt"));
    }
}
