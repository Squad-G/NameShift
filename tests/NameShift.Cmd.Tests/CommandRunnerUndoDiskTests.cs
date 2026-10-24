using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using NameShift.Core;
using NameShift.Disk;
using NameShift.Ui.Actions;
using NameShift.Ui.Files;
using NameShift.Ui.Preview;
using Xunit;
using UiStrings = NameShift.Ui.Strings;

namespace NameShift.Cmd.Tests;

/// <summary>undo on real temp folders with a temp history root (never the real journal): the journal the command line
/// writes is the one undo reads, and it is the window's journal.</summary>
public sealed class CommandRunnerUndoDiskTests : IDisposable
{
    private readonly TempFolder _temp = new TempFolder();

    public void Dispose() => _temp.Dispose();

    private string PendingPath => Path.Combine(_temp.History, "pending.json");

    private string LastBatchPath => Path.Combine(_temp.History, "last-batch.json");

    /// <summary>Name, size, content hash stand-in (the bytes) and last write time of every file under a folder.</summary>
    private static string[] Snapshot(string folder) =>
        Directory.Exists(folder)
            ? Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => path + "|" + Convert.ToBase64String(File.ReadAllBytes(path)) + "|" + File.GetLastWriteTimeUtc(path).Ticks)
                .ToArray()
            : Array.Empty<string>();

    private void ApplyLower()
    {
        var applied = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--case", "lower");
        Assert.Equal(0, applied.Code);
    }

    [Fact]
    public void ApplyThenUndo_RoundTrip()
    {
        _temp.File("IMG_001.JPG");
        _temp.File("IMG_002.JPG");
        ApplyLower();
        Assert.Equal(new[] { "img_001.JPG", "img_002.JPG" }, _temp.FileNames());

        var undo = Cmd.RunOnDisk(_temp, "undo");

        Assert.Equal(0, undo.Code);
        Assert.Equal(Cmd.Written("2 names restored."), undo.Output);
        Assert.Equal("", undo.Error);
        Assert.Equal(new[] { "IMG_001.JPG", "IMG_002.JPG" }, _temp.FileNames());

        var again = Cmd.RunOnDisk(_temp, "undo");

        Assert.Equal(0, again.Code);
        Assert.Equal(Cmd.Written(UiStrings.NothingToRevert), again.Output);
        Assert.Equal("There is nothing to revert." + Environment.NewLine, again.Output);
        Assert.Equal("", again.Error);
        Assert.Equal(new[] { "IMG_001.JPG", "IMG_002.JPG" }, _temp.FileNames());
    }

    [Fact]
    public void WindowBatch_UndoneByTheCommandLine()
    {
        _temp.File("IMG_001.JPG");
        _temp.File("IMG_002.JPG");
        FileListing listing = new DiskFileListSource().ListEntries(_temp.Files);
        RenameItem[] items = FileListOrder.Sort(listing.Items).Where(new ItemFilter(includeFolders: false).Includes).ToArray();
        RenamePlan plan = RenamePlanner.Plan(
            new IRenameRule[] { new CaseRule(CaseMode.Lower) }, items, listing.ExistingNamesByFolder, CancellationToken.None);
        var window = new JournaledBatchRenamer(new JournaledRenamer(_temp.History));
        RenameRunResult done = window.Rename(plan.Results, plan.Conflicts);
        Assert.Equal(ExecutionOutcome.Succeeded, done.Execution!.Outcome);
        Assert.Equal(new[] { "img_001.JPG", "img_002.JPG" }, _temp.FileNames());

        var undo = Cmd.RunOnDisk(_temp, "undo");

        Assert.Equal(0, undo.Code);
        Assert.Equal(Cmd.Written("2 names restored."), undo.Output);
        Assert.Equal(new[] { "IMG_001.JPG", "IMG_002.JPG" }, _temp.FileNames());
    }

    [Fact]
    public void CorruptLastBatch_Exit3_Untouched()
    {
        _temp.File("a.txt");
        Directory.CreateDirectory(_temp.History);
        File.WriteAllText(LastBatchPath, "this is not json");
        string[] before = Snapshot(_temp.Root);

        var undo = Cmd.RunOnDisk(_temp, "undo");

        Assert.Equal(3, undo.Code);
        Assert.Equal(Cmd.Written(Strings.UndoHistoryUnusable), undo.Error);
        Assert.Equal("", undo.Output);
        Assert.Equal(before, Snapshot(_temp.Root));
        Assert.False(File.Exists(PendingPath));
    }

    [Fact]
    public void StalePending_Exit3_Untouched()
    {
        _temp.File("IMG_001.JPG");
        ApplyLower();
        File.WriteAllText(PendingPath, "{ \"id\": \"another-id\" }");
        string[] before = Snapshot(_temp.Root);

        var undo = Cmd.RunOnDisk(_temp, "undo");

        Assert.Equal(3, undo.Code);
        Assert.Equal(Cmd.Written(Strings.UndoHistoryUnusable), undo.Error);
        Assert.Equal("", undo.Output);
        Assert.Equal(before, Snapshot(_temp.Root));
        Assert.Equal(new[] { "img_001.JPG" }, _temp.FileNames());
    }

    [Fact]
    public void OldNameTaken_Partly_Exit4_ThenRetry()
    {
        _temp.File("a1.txt");
        _temp.File("b1.txt");
        Assert.Equal(0, Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--replace", "1", "2").Code);
        Assert.Equal(new[] { "a2.txt", "b2.txt" }, _temp.FileNames());
        string blocker = Path.Combine(_temp.Files, "a1.txt");
        File.WriteAllText(blocker, "the new a1");

        var first = Cmd.RunOnDisk(_temp, "undo");

        Assert.Equal(4, first.Code);
        Assert.Equal("", first.Output);
        string[] lines = Cmd.Lines(first.Error);
        Assert.True(lines.Length >= 2, first.Error);
        Assert.StartsWith("1 of 2", lines[0], StringComparison.Ordinal);
        Assert.Equal(new[] { "a1.txt", "a2.txt", "b1.txt" }, _temp.FileNames());
        Assert.Equal("the new a1", File.ReadAllText(blocker));
        Assert.Equal("content of a1.txt", File.ReadAllText(Path.Combine(_temp.Files, "a2.txt")));

        File.Delete(blocker);
        var retry = Cmd.RunOnDisk(_temp, "undo");

        Assert.Equal(0, retry.Code);
        Assert.Equal(Cmd.Written("1 name restored."), retry.Output);
        Assert.Equal("", retry.Error);
        Assert.Equal(new[] { "a1.txt", "b1.txt" }, _temp.FileNames());
        Assert.Equal("content of a1.txt", File.ReadAllText(blocker));
    }

    [Fact]
    public void AllTaken_NothingReverted_Exit3()
    {
        _temp.File("a1.txt");
        _temp.File("b1.txt");
        Assert.Equal(0, Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--replace", "1", "2").Code);
        File.WriteAllText(Path.Combine(_temp.Files, "a1.txt"), "new a1");
        File.WriteAllText(Path.Combine(_temp.Files, "b1.txt"), "new b1");

        var undo = Cmd.RunOnDisk(_temp, "undo");

        Assert.Equal(3, undo.Code);
        Assert.Equal("", undo.Output);
        Assert.Equal(UiStrings.RevertNone, Cmd.Lines(undo.Error)[0]);
        Assert.True(Cmd.Lines(undo.Error).Length >= 2, undo.Error);
        Assert.Equal(new[] { "a1.txt", "a2.txt", "b1.txt", "b2.txt" }, _temp.FileNames());
        Assert.Equal("new a1", File.ReadAllText(Path.Combine(_temp.Files, "a1.txt")));
        Assert.Equal("new b1", File.ReadAllText(Path.Combine(_temp.Files, "b1.txt")));
    }

    [Fact]
    public void DryRun_TouchesNothing()
    {
        _temp.File("IMG_001.JPG");
        _temp.File("IMG_002.JPG");
        ApplyLower();
        string[] before = Snapshot(_temp.Root);

        var dry = Cmd.RunOnDisk(_temp, "undo", "--dry-run");

        Assert.Equal(0, dry.Code);
        Assert.Equal(Cmd.Written("img_001.JPG -> IMG_001.JPG", "img_002.JPG -> IMG_002.JPG"), dry.Output);
        Assert.Equal("", dry.Error);
        Assert.Equal(before, Snapshot(_temp.Root));
        Assert.False(File.Exists(PendingPath));
        Assert.Equal(new[] { "img_001.JPG", "img_002.JPG" }, _temp.FileNames());
    }

    [Fact]
    public void Undo_FromAnUnrelatedCurrentDirectory()
    {
        _temp.File("IMG_001.JPG");
        ApplyLower();
        string other = Path.Combine(_temp.Root, "unrelated");
        Directory.CreateDirectory(other);
        var output = new StringWriter(CultureInfo.InvariantCulture);
        var error = new StringWriter(CultureInfo.InvariantCulture);

        int code = CommandRunner.ForDisk(output, error, other, _temp.History, "1.2.3").Run(new[] { "undo" });

        Assert.Equal(0, code);
        Assert.Equal("", error.ToString());
        Assert.Equal(new[] { "IMG_001.JPG" }, _temp.FileNames());
    }

    [Fact]
    public void ShellUndo_ThenWindowRevert_NothingToRevert()
    {
        _temp.File("IMG_001.JPG");
        ApplyLower();
        var window = new JournaledBatchRenamer(new JournaledRenamer(_temp.History));
        Assert.Equal(LastBatchState.Revertable, window.LoadLastBatch().State);

        Assert.Equal(0, Cmd.RunOnDisk(_temp, "undo").Code);

        Assert.Equal(RevertOutcome.NothingToRevert, window.Revert().Outcome);
        Assert.Equal(new[] { "IMG_001.JPG" }, _temp.FileNames());
    }
}
