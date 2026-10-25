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

/// <summary>A refused undo retry that cannot delete pending.json (real temp folders, a temp history root): the first undo
/// exits 3 and says the history was not cleaned, the next one says the history is unusable and does not revert.</summary>
public sealed class CommandRunnerUndoJournalIssueTests : IDisposable
{
    private const string HookMessage = "The process cannot access the file because it is being used by another process.";

    private readonly TempFolder _temp = new TempFolder();
    private FileStream? _lock;

    public void Dispose()
    {
        try
        {
            _lock?.Dispose();
            _lock = null;
        }
        finally
        {
            _temp.Dispose();
        }
    }

    [Fact]
    public void RefusedRetry_PendingNotDeleted_Exit3_ThenHistoryUnusable()
    {
        _temp.File("a1.txt");
        var apply = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--replace", "1", "2");
        Assert.True(apply.Code == 0, "The setup apply failed: " + apply.Code + " " + apply.Error);
        Assert.Equal(new[] { "a2.txt" }, _temp.FileNames());

        _lock = new FileStream(Path.Combine(_temp.Files, "a2.txt"), FileMode.Open, FileAccess.Read, FileShare.None);

        var hooked = new FailingPendingDelete();
        var first = new CountingRenamer(new JournaledBatchRenamer(new JournaledRenamer(
            _temp.History,
            new DiskFileSystem(),
            () => Guid.NewGuid().ToString("N").Substring(0, 8),
            hooked,
            () => DateTimeOffset.UtcNow,
            () => Guid.NewGuid().ToString("N"))));
        var output = new StringWriter(CultureInfo.InvariantCulture);
        var error = new StringWriter(CultureInfo.InvariantCulture);
        var runner = new CommandRunner(output, error, _temp.Files, first, new DiskFileListSource(), Cmd.EnUs, "1.2.3", Cmd.UnusedPresets());

        int code = runner.Run(new[] { "undo" });

        string text = error.ToString();
        string detail = "exit " + code + ", error: " + text;
        Assert.True(code == 3, detail);
        Assert.True(output.ToString().Length == 0, detail);
        string[] lines = Cmd.Lines(text);
        Assert.True(lines.Length > 0 && lines[0].StartsWith(UiStrings.RevertNone, StringComparison.Ordinal), detail);
        Assert.True(lines[0].Contains(HookMessage), detail);
        Assert.True(File.Exists(Path.Combine(_temp.History, UndoJournal.PendingFileName)), detail);
        Assert.True(hooked.DeleteAttempts >= 1, detail);
        Assert.Equal(new[] { "a2.txt" }, _temp.FileNames());

        _lock.Dispose();
        _lock = null;

        var second = new CountingRenamer(new JournaledBatchRenamer(new JournaledRenamer(_temp.History)));
        var output2 = new StringWriter(CultureInfo.InvariantCulture);
        var error2 = new StringWriter(CultureInfo.InvariantCulture);
        var runner2 = new CommandRunner(output2, error2, _temp.Files, second, new DiskFileListSource(), Cmd.EnUs, "1.2.3", Cmd.UnusedPresets());

        int code2 = runner2.Run(new[] { "undo" });

        Assert.Equal(3, code2);
        Assert.Equal("", output2.ToString());
        Assert.Equal(Cmd.Written(Strings.UndoHistoryUnusable), error2.ToString());
        Assert.Equal(0, second.RevertCalls);
        Assert.Equal(new[] { "a2.txt" }, _temp.FileNames());
    }

    /// <summary>The real journal files, except that deleting pending.json fails.</summary>
    private sealed class FailingPendingDelete : IJournalFiles
    {
        private readonly DiskJournalFiles _inner = new DiskJournalFiles();

        public int DeleteAttempts { get; private set; }

        public bool Exists(string path) => _inner.Exists(path);

        public byte[] ReadAll(string path) => _inner.ReadAll(path);

        public void WriteAtomic(string path, byte[] content) => _inner.WriteAtomic(path, content);

        public void Delete(string path)
        {
            if (string.Equals(Path.GetFileName(path), UndoJournal.PendingFileName, StringComparison.OrdinalIgnoreCase))
            {
                DeleteAttempts++;
                throw new IOException(HookMessage);
            }

            _inner.Delete(path);
        }
    }

    /// <summary>Forwards everything and counts the Revert calls.</summary>
    private sealed class CountingRenamer : IBatchRenamer
    {
        private readonly IBatchRenamer _inner;

        public CountingRenamer(IBatchRenamer inner) => _inner = inner;

        public int RevertCalls { get; private set; }

        public LastBatch LoadLastBatch() => _inner.LoadLastBatch();

        public RenameRunResult Rename(IReadOnlyList<RenameResult> results, ConflictReport conflicts) => _inner.Rename(results, conflicts);

        public RevertResult Revert()
        {
            RevertCalls++;
            return _inner.Revert();
        }
    }
}
