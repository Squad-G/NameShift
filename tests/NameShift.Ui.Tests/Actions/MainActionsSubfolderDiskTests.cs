using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NameShift.Core;
using NameShift.Disk;
using NameShift.Ui.Actions;
using NameShift.Ui.Files;
using NameShift.Ui.Preview;
using NameShift.Ui.Rules;
using NameShift.Ui.Tests.Preview;
using NameShift.Ui.Tests.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Actions;

/// <summary>NS-74 AC 15: a Rename across sub-folders on a real disk: a fresh temp tree per test, with its own history
/// folder. Nothing outside it is touched.</summary>
public sealed class MainActionsSubfolderDiskTests : IDisposable
{
    private readonly string _temp = Path.Combine(Path.GetTempPath(), "NameShift.Ui.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _root;
    private readonly string _history;
    private readonly FileListModel _files = new FileListModel(new DiskFileListSource());
    private PreviewModel _preview = null!;
    private MainActions _actions = null!;

    public MainActionsSubfolderDiskTests()
    {
        _root = Path.Combine(_temp, "root");
        _history = Path.Combine(_temp, "history");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        _files.Dispose();
        foreach (string path in Directory.GetFileSystemEntries(_temp, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        Directory.Delete(_temp, true);
    }

    private string Write(string relative, string content, FileAttributes attributes = FileAttributes.Normal)
    {
        string path = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        File.SetAttributes(path, attributes);
        return path;
    }

    /// <summary>Every item renamed by the rule (those whose name starts with <paramref name="onlyStart"/>, when
    /// given) to <paramref name="newName"/>; the root loaded with sub-folders, the preview and the journal read.</summary>
    private async Task LoadWithRule(string newName, string? onlyStart)
    {
        var panel = new FakeRulePanelModel(RuleStage.Replace, _ => new RenameToRule(newName, onlyStart), "&Replace");
        panel.Text = "on";
        panel.Enabled = true;
        var host = new RulePanelHostModel(new IRulePanelModel[] { panel });
        _preview = new PreviewModel(host, _files, (_, _) => Task.CompletedTask);
        _actions = new MainActions(_preview, _files, host, new FilterPanelModel(), new JournaledBatchRenamer(new JournaledRenamer(_history)));

        Assert.False(await _files.SetIncludeSubfoldersAsync(true));
        Assert.True(await _files.LoadAsync(_root));
        await _preview.Pending;
        await _actions.LoadJournalAsync();
    }

    private int IndexOf(string folder, string name)
    {
        string path = Path.Combine(_root, folder);
        int index = -1;
        for (int i = 0; i < _preview.Snapshot.Items.Count; i++)
        {
            RenameItem item = _preview.Snapshot.Items[i];
            if (item.Name == name && string.Equals(item.FolderPath, path, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
            }
        }

        Assert.True(index >= 0, "No item " + name + " in " + folder + ".");
        return index;
    }

    [Fact]
    public async Task TwoFolders_RenamedInPlace_ThenReverted()
    {
        string xa = Write(@"x\a.txt", "content of x");
        string ya = Write(@"y\a.txt", "content of y");
        string xb = Path.Combine(_root, "x", "b.txt");
        string yb = Path.Combine(_root, "y", "b.txt");
        await LoadWithRule("b.txt", "a");
        Assert.False(_preview.Snapshot.HasConflicts);
        Assert.Equal(2, _preview.Snapshot.ChangedCount);
        Assert.True(_actions.CanRename);

        ActionReport? renamed = await _actions.RenameAsync(_ => throw new InvalidOperationException("Never asked for 2 files."));

        Assert.Equal("2 files renamed.", renamed!.Status);
        Assert.False(File.Exists(xa));
        Assert.False(File.Exists(ya));
        Assert.Equal("content of x", File.ReadAllText(xb));
        Assert.Equal("content of y", File.ReadAllText(yb));
        LastBatch last = new JournaledRenamer(_history).LoadLastBatch();
        Assert.Equal(LastBatchState.Revertable, last.State);
        Assert.Contains(Path.Combine(_root, "x"), last.Folders, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine(_root, "y"), last.Folders, StringComparer.OrdinalIgnoreCase);
        Assert.True(_files.IncludeSubfolders);
        Assert.Equal(2, _files.Items.Count(item => item.Name == "b.txt"));
        Assert.True(_actions.CanRevert);

        ActionReport? reverted = await _actions.RevertAsync();

        Assert.Equal("2 names restored.", reverted!.Status);
        Assert.Equal("content of x", File.ReadAllText(xa));
        Assert.Equal("content of y", File.ReadAllText(ya));
        Assert.False(File.Exists(xb));
        Assert.False(File.Exists(yb));
    }

    [Fact]
    public async Task SameFolder_TwoTargets_Conflict()
    {
        Write(@"x\a.txt", "a");
        Write(@"x\c.txt", "c");
        await LoadWithRule("b.txt", null);

        Assert.True(_preview.Snapshot.HasConflicts);
        Assert.NotEmpty(_preview.Snapshot.Conflicts.ForItem(IndexOf("x", "a.txt")));
        Assert.NotEmpty(_preview.Snapshot.Conflicts.ForItem(IndexOf("x", "c.txt")));
        Assert.False(_actions.CanRename);
    }

    [Fact]
    public async Task TargetExists_EvenHiddenOrFilteredOut()
    {
        Write(@"x\a.txt", "a");
        Write(@"x\b.txt", "hidden b", FileAttributes.Hidden);
        await LoadWithRule("b.txt", "a");

        Assert.DoesNotContain(_files.Items, item => item.Name == "b.txt");
        Assert.Contains(
            _preview.Snapshot.Conflicts.ForItem(IndexOf("x", "a.txt")),
            conflict => conflict.Code == ConflictCode.TargetExists);
        Assert.False(_actions.CanRename);
    }
}
