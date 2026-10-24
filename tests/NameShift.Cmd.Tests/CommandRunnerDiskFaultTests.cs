using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NameShift.Core;
using NameShift.Disk;
using NameShift.Ui.Actions;
using NameShift.Ui.Files;
using Xunit;
using UiStrings = NameShift.Ui.Strings;

namespace NameShift.Cmd.Tests;

/// <summary>The reachable disk faults, on real temp folders with a temp history root: a file that vanished after the
/// listing, a file in use, and a history folder that cannot be written.</summary>
public sealed class CommandRunnerDiskFaultTests : IDisposable
{
    private readonly TempFolder _temp = new TempFolder();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void VanishedFile_Exit3()
    {
        _temp.File("IMG_001.JPG");
        string vanishing = _temp.File("IMG_002.JPG");
        var renamer = new DeletingRenamer(vanishing, new JournaledBatchRenamer(new JournaledRenamer(_temp.History)));
        var output = new StringWriter(CultureInfo.InvariantCulture);
        var error = new StringWriter(CultureInfo.InvariantCulture);
        var runner = new CommandRunner(output, error, _temp.Files, renamer, new DiskFileListSource(), Cmd.EnUs);

        int code = runner.Run(new[] { "apply", _temp.Files, "--case", "lower" });

        Assert.Equal(3, code);
        Assert.Equal("", output.ToString());
        Assert.Equal(Cmd.Written("Nothing was renamed. IMG_002.JPG is no longer there."), error.ToString());
        Assert.Equal(Cmd.Written(ActionText.ForRename(renamer.Result!, Cmd.EnUs).Status), error.ToString());
        Assert.Equal(new[] { "IMG_001.JPG" }, _temp.FileNames());
    }

    [Fact]
    public void LockedFile_Exit3_SaysInUse()
    {
        _temp.File("IMG_001.JPG");
        _temp.File("IMG_002.JPG");
        (int Code, string Output, string Error) run;
        using (new FileStream(Path.Combine(_temp.Files, "IMG_001.JPG"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--case", "lower");
        }

        Assert.Equal(3, run.Code);
        Assert.Equal("", run.Output);
        Assert.Contains(string.Format(CultureInfo.InvariantCulture, UiStrings.ItemInUse, "IMG_001.JPG"), run.Error, StringComparison.Ordinal);
        Assert.StartsWith(UiStrings.NothingRenamed, run.Error, StringComparison.Ordinal);
        Assert.Single(Cmd.Lines(run.Error));
        Assert.Equal(new[] { "IMG_001.JPG", "IMG_002.JPG" }, _temp.FileNames());
    }

    [Fact]
    public void UnwritableHistory_Exit3_NothingRenamed()
    {
        _temp.File("IMG_001.JPG");
        _temp.File("IMG_002.JPG");
        string blocker = Path.Combine(_temp.Root, "a-file");
        File.WriteAllText(blocker, "not a folder");
        var output = new StringWriter(CultureInfo.InvariantCulture);
        var error = new StringWriter(CultureInfo.InvariantCulture);

        int code = CommandRunner.ForDisk(output, error, _temp.Files, Path.Combine(blocker, "history"))
            .Run(new[] { "apply", _temp.Files, "--case", "lower" });

        Assert.Equal(3, code);
        Assert.Equal("", output.ToString());
        Assert.StartsWith(string.Format(CultureInfo.InvariantCulture, UiStrings.RenameNoHistory, ""), error.ToString(), StringComparison.Ordinal);
        Assert.Equal(new[] { "IMG_001.JPG", "IMG_002.JPG" }, _temp.FileNames());
    }

    /// <summary>Deletes one file (as another program would, after the listing), then runs the real journaled renamer.</summary>
    private sealed class DeletingRenamer : IBatchRenamer
    {
        private readonly string _path;
        private readonly IBatchRenamer _inner;

        public DeletingRenamer(string path, IBatchRenamer inner)
        {
            _path = path;
            _inner = inner;
        }

        public RenameRunResult? Result { get; private set; }

        public LastBatch LoadLastBatch() => throw new NotSupportedException();

        public RenameRunResult Rename(IReadOnlyList<RenameResult> results, ConflictReport conflicts)
        {
            File.Delete(_path);
            Result = _inner.Rename(results, conflicts);
            return Result;
        }

        public RevertResult Revert() => throw new NotSupportedException();
    }
}
