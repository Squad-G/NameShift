using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NameShift.Disk;
using NameShift.Ui.Actions;
using NameShift.Ui.Files;
using NameShift.Ui.Preview;
using NameShift.Ui.Rules;
using NameShift.Ui.Tests.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Actions;

/// <summary>The window's actions on a real disk: a fresh temp folder per test, with its own history folder. Nothing
/// outside it is touched.</summary>
public sealed class MainActionsDiskTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "NameShift.Ui.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _folder;
    private readonly string _history;
    private readonly FileListModel _files = new FileListModel(new DiskFileListSource());
    private readonly PreviewModel _preview;
    private readonly MainActions _actions;

    public MainActionsDiskTests()
    {
        _folder = Path.Combine(_temp, "files");
        _history = Path.Combine(_temp, "history");
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "a.txt"), "content of a");
        File.WriteAllText(Path.Combine(_folder, "b.txt"), "content of b");
        File.WriteAllText(Path.Combine(_folder, "c.txt"), "content of c");

        FakeRulePanelModel prefix = FakeRulePanelModel.Add();
        prefix.Text = "x_";
        prefix.Enabled = true;
        var host = new RulePanelHostModel(new IRulePanelModel[] { prefix });
        _preview = new PreviewModel(host, _files, (_, _) => Task.CompletedTask);
        _actions = new MainActions(_preview, _files, host, new JournaledBatchRenamer(new JournaledRenamer(_history)));
    }

    public void Dispose()
    {
        foreach (string path in Directory.GetFileSystemEntries(_temp, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        Directory.Delete(_temp, true);
    }

    private string[] NamesOnDisk() => Directory.GetFiles(_folder).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray()!;

    private string Content(string name) => File.ReadAllText(Path.Combine(_folder, name));

    private async Task LoadAsync()
    {
        await _files.LoadAsync(_folder);
        await _preview.Pending;
        await _actions.LoadJournalAsync();
    }

    [Fact]
    public async Task RenameThenRevert_RoundTripOnDisk()
    {
        await LoadAsync();
        Assert.True(_actions.CanRename);
        Assert.False(_actions.CanRevert);

        ActionReport? renamed = await _actions.RenameAsync(_ => throw new InvalidOperationException("Never asked for 3 files."));

        Assert.Equal("3 files renamed.", renamed!.Status);
        Assert.Null(renamed.Details);
        Assert.Equal(new[] { "x_a.txt", "x_b.txt", "x_c.txt" }, NamesOnDisk());
        Assert.Equal("content of a", Content("x_a.txt"));
        Assert.Equal("content of b", Content("x_b.txt"));
        Assert.Equal("content of c", Content("x_c.txt"));
        Assert.Equal(new[] { "x_a.txt", "x_b.txt", "x_c.txt" }, _files.Items.Select(i => i.Name).ToArray());
        Assert.True(File.Exists(Path.Combine(_history, "last-batch.json")));
        Assert.True(_actions.CanRevert);
        Assert.Equal("Undo the last rename: 3 names in " + _folder, _actions.RevertDescription);

        ActionReport? reverted = await _actions.RevertAsync();

        Assert.Equal("3 names restored.", reverted!.Status);
        Assert.Null(reverted.Details);
        Assert.Equal(new[] { "a.txt", "b.txt", "c.txt" }, NamesOnDisk());
        Assert.Equal("content of a", Content("a.txt"));
        Assert.Equal("content of b", Content("b.txt"));
        Assert.Equal("content of c", Content("c.txt"));
        Assert.Equal(new[] { "a.txt", "b.txt", "c.txt" }, _files.Items.Select(i => i.Name).ToArray());
        Assert.False(_actions.CanRevert);
        Assert.False(_actions.IsBusy);
    }

    [Fact]
    public async Task LockedFile_RollsBack_NamesAsBefore_NoHistoryWritten()
    {
        await LoadAsync();
        ActionReport? report;
        using (new FileStream(Path.Combine(_folder, "b.txt"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            report = await _actions.RenameAsync(_ => true);
        }

        Assert.StartsWith("Nothing was renamed. ", report!.Status);
        Assert.Contains("b.txt", report.Status);
        Assert.Equal("Nothing was renamed. b.txt is in use by another program: close it and try again.", report.Status);
        Assert.Null(report.Details);
        Assert.Equal(new[] { "a.txt", "b.txt", "c.txt" }, NamesOnDisk());
        Assert.Equal("content of a", Content("a.txt"));
        Assert.False(File.Exists(Path.Combine(_history, "last-batch.json")));
        Assert.False(_actions.CanRevert);
        Assert.Equal(new[] { "a.txt", "b.txt", "c.txt" }, _files.Items.Select(i => i.Name).ToArray());
    }
}
