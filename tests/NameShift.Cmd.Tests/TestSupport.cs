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
using NameShift.Ui.Presets;

namespace NameShift.Cmd.Tests;

/// <summary>A fresh temp folder per test: Files is the folder the command renames in, History a journal folder that
/// does not exist yet. Dispose clears read-only and hidden attributes, then deletes everything.</summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Root = Path.Combine(Path.GetTempPath(), "NameShift.Cmd.Tests", Guid.NewGuid().ToString("N"));
        Files = Path.Combine(Root, "files");
        History = Path.Combine(Root, "history");
        Directory.CreateDirectory(Files);
    }

    public string Root { get; }

    public string Files { get; }

    public string History { get; }

    /// <summary>A presets folder that does not exist yet (the first save creates it; Dispose removes it with Root).</summary>
    public string Presets => Path.Combine(Root, "presets");

    /// <summary>Creates a file in Files and returns its full path.</summary>
    public string File(string name, FileAttributes attributes = FileAttributes.Normal)
    {
        string path = Path.Combine(Files, name);
        System.IO.File.WriteAllText(path, "content of " + name);
        if (attributes != FileAttributes.Normal)
        {
            System.IO.File.SetAttributes(path, attributes);
        }

        return path;
    }

    /// <summary>The names of the files in Files, in ordinal order (exact case).</summary>
    public string[] FileNames() =>
        Directory.GetFiles(Files).Select(path => Path.GetFileName(path)!).OrderBy(name => name, StringComparer.Ordinal).ToArray();

    public void Dispose()
    {
        if (!Directory.Exists(Root))
        {
            return;
        }

        foreach (string path in Directory.GetFileSystemEntries(Root, "*", SearchOption.AllDirectories))
        {
            System.IO.File.SetAttributes(path, Directory.Exists(path) ? FileAttributes.Directory : FileAttributes.Normal);
        }

        Directory.Delete(Root, true);
    }
}

/// <summary>Runs a CommandRunner on two StringWriters, in en-US.</summary>
internal static class Cmd
{
    public static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    public static (int Code, string Output, string Error) Run(
        IBatchRenamer renamer,
        string currentDirectory,
        IReadOnlyList<string> args,
        IFileListSource? files = null,
        Func<ApplyOptions, IReadOnlyList<IRenameRule>>? buildRules = null,
        string? presetsFolder = null)
    {
        var output = new StringWriter(CultureInfo.InvariantCulture);
        var error = new StringWriter(CultureInfo.InvariantCulture);
        var runner = new CommandRunner(output, error, currentDirectory, renamer, files ?? new DiskFileListSource(), EnUs, "1.2.3",
            new PresetStore(presetsFolder ?? UnusedPresetsFolder()));
        if (buildRules != null)
        {
            runner.BuildRules = buildRules;
        }

        int code = runner.Run(args);
        return (code, output.ToString(), error.ToString());
    }

    /// <summary>A folder that no test creates and none holds presets: for tests that use no preset option.</summary>
    public static string UnusedPresetsFolder() =>
        Path.Combine(Path.GetTempPath(), "NameShift.Cmd.Tests", "unused-presets-" + Guid.NewGuid().ToString("N"));

    /// <summary>A store over <see cref="UnusedPresetsFolder"/>.</summary>
    public static PresetStore UnusedPresets() => new PresetStore(UnusedPresetsFolder());

    /// <summary>The real thing on a temp folder: the disk listing and a journaled renamer with the temp history root.</summary>
    public static (int Code, string Output, string Error) RunOnDisk(TempFolder temp, params string[] args)
    {
        var output = new StringWriter(CultureInfo.InvariantCulture);
        var error = new StringWriter(CultureInfo.InvariantCulture);
        int code = CommandRunner.ForDisk(output, error, temp.Files, temp.History, temp.Presets, "1.2.3").Run(args);
        return (code, output.ToString(), error.ToString());
    }

    /// <summary>The lines written, split on Environment.NewLine; the final line break gives no empty line.</summary>
    public static string[] Lines(string text)
    {
        if (text.Length == 0)
        {
            return Array.Empty<string>();
        }

        string[] lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        return lines[lines.Length - 1].Length == 0 ? lines.Take(lines.Length - 1).ToArray() : lines;
    }

    /// <summary>What a writer holds after WriteLine of each text.</summary>
    public static string Written(params string[] lines) => string.Concat(lines.Select(line => line + Environment.NewLine));
}

/// <summary>An in-memory renamer: records each Rename call and returns the scripted result, or throws. Touches no disk.</summary>
internal sealed class FakeBatchRenamer : IBatchRenamer
{
    public RenameRunResult Result { get; set; } = CmdResults.Succeeded(3);

    public Exception? Throws { get; set; }

    public int RenameCalls { get; private set; }

    public IReadOnlyList<RenameResult>? Results { get; private set; }

    public ConflictReport? Conflicts { get; private set; }

    /// <summary>What LoadLastBatch returns; null (the default) makes it throw, so an apply that reads the journal fails.</summary>
    public LastBatch? LastBatch { get; set; }

    /// <summary>What Revert returns; null (the default) makes it throw, so an apply that reverts fails.</summary>
    public RevertResult? RevertResult { get; set; }

    /// <summary>Thrown by Revert when set.</summary>
    public Exception? RevertThrows { get; set; }

    public int LoadCalls { get; private set; }

    public int RevertCalls { get; private set; }

    /// <summary>What CheckRevert returns.</summary>
    public RevertCheck CheckResult { get; set; } = RevertCheck.Empty;

    /// <summary>The includeModified of the last Revert call, or null before any.</summary>
    public bool? LastIncludeModified { get; private set; }

    public LastBatch LoadLastBatch()
    {
        LoadCalls++;
        return LastBatch ?? throw new NotSupportedException("The command line never reads the journal.");
    }

    public RenameRunResult Rename(IReadOnlyList<RenameResult> results, ConflictReport conflicts)
    {
        RenameCalls++;
        Results = results;
        Conflicts = conflicts;
        if (Throws != null)
        {
            throw Throws;
        }

        return Result;
    }

    public RevertCheck CheckRevert() => CheckResult;

    public RevertResult Revert(bool includeModified = false)
    {
        RevertCalls++;
        LastIncludeModified = includeModified;
        if (RevertThrows != null)
        {
            throw RevertThrows;
        }

        return RevertResult ?? throw new NotSupportedException("The command line never reverts.");
    }
}

/// <summary>Returns one fixed listing for every folder, and records how it was asked.</summary>
internal sealed class FakeFileListSource : IFileListSource
{
    private readonly FileListing _listing;

    public FakeFileListSource(FileListing listing)
    {
        _listing = listing;
    }

    /// <summary>The includeSubfolders of every call, in order.</summary>
    public List<bool> IncludeSubfolders { get; } = new List<bool>();

    public FileListing ListEntries(string folderPath, bool includeSubfolders, CancellationToken cancellationToken)
    {
        IncludeSubfolders.Add(includeSubfolders);
        return _listing;
    }
}

/// <summary>Real Disk result objects, built through their validating internal constructors (a copy of the Ui tests'
/// DiskResults builders): an impossible combination throws here.</summary>
internal static class CmdResults
{
    public const int SharingViolation = unchecked((int)0x80070020);

    public static CompletedRename Done(string oldPath, string newPath, ItemKind kind = ItemKind.File) =>
        new CompletedRename(oldPath, newPath, kind);

    /// <summary>n renames in C:\A: f0.txt to x_f0.txt, and so on.</summary>
    public static CompletedRename[] Renames(int count, string folder = @"C:\A") =>
        Enumerable.Range(0, count)
            .Select(i => Done(Path.Combine(folder, $"f{i}.txt"), Path.Combine(folder, $"x_f{i}.txt")))
            .ToArray();

    public static ExecutionFailure Failure(FailureReason reason, string? itemPath = null, string? blockingPath = null, string? systemMessage = null) =>
        new ExecutionFailure(reason, itemPath, blockingPath, systemMessage, systemMessage == null ? null : SharingViolation);

    public static StrandedItem Stranded(string currentPath, string originalPath, string? systemMessage = "Access is denied.") =>
        new StrandedItem(currentPath, originalPath, ItemKind.File, systemMessage);

    public static JournalWriteFailure Journal(JournalWritePhase phase, string message = "The disk is full.") =>
        new JournalWriteFailure(phase, new IOException(message));

    public static RenameRunResult Succeeded(int count, JournalWriteFailure? journalIssue = null) =>
        new RenameRunResult(new ExecutionResult(ExecutionOutcome.Succeeded, Renames(count)), journalIssue);

    public static RenameRunResult Failed(ExecutionOutcome outcome, ExecutionFailure failure, JournalWriteFailure? journalIssue = null) =>
        new RenameRunResult(new ExecutionResult(outcome, failure: failure), journalIssue);

    public static RenameRunResult NeedsAttention(ExecutionFailure failure, JournalWriteFailure? journalIssue, params StrandedItem[] stranded) =>
        new RenameRunResult(new ExecutionResult(ExecutionOutcome.NeedsAttention, failure: failure, needsAttention: stranded), journalIssue);

    public static RenameRunResult NotStarted(string message) =>
        new RenameRunResult(null, Journal(JournalWritePhase.BeforeStart, message));

    /// <summary>A journal state; Revertable and Reverted carry a time, Revertable the folders and the renames.</summary>
    public static LastBatch Batch(LastBatchState state, params CompletedRename[] renames)
    {
        bool revertable = state == LastBatchState.Revertable;
        DateTimeOffset? time = revertable || state == LastBatchState.Reverted ? new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero) : (DateTimeOffset?)null;
        string[] folders = revertable
            ? renames.Select(r => Path.GetDirectoryName(r.OldPath)!).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            : Array.Empty<string>();
        return new LastBatch(state, time, folders, renames);
    }

    public static SkippedRevert Skipped(string oldPath, string newPath) =>
        new SkippedRevert(Done(oldPath, newPath), RevertSkipReason.ItemMissing);

    /// <summary>n skipped entries: C:\A\x_g0.txt back to g0.txt, and so on.</summary>
    public static SkippedRevert[] SkippedEntries(int count) =>
        Enumerable.Range(0, count)
            .Select(i => Skipped(Path.Combine(@"C:\A", $"g{i}.txt"), Path.Combine(@"C:\A", $"x_g{i}.txt")))
            .ToArray();

    public static RevertResult NothingToRevert() => new RevertResult(RevertOutcome.NothingToRevert);

    public static RevertResult Reverted(int count, bool afterFinishIssue = false) =>
        new RevertResult(
            RevertOutcome.Reverted,
            Renames(count),
            journalIssue: afterFinishIssue ? Journal(JournalWritePhase.AfterFinish) : null);

    public static RevertResult PartlyReverted(int reverted, int skipped, bool afterFinishIssue = false) =>
        new RevertResult(
            RevertOutcome.PartlyReverted,
            Renames(reverted),
            SkippedEntries(skipped),
            journalIssue: afterFinishIssue ? Journal(JournalWritePhase.AfterFinish) : null);

    public static RevertResult NothingReverted(int skipped, bool afterFinishIssue = false) =>
        new RevertResult(
            RevertOutcome.NothingReverted,
            notReverted: SkippedEntries(skipped),
            journalIssue: afterFinishIssue ? Journal(JournalWritePhase.AfterFinish) : null);

    public static RevertResult RevertNotStarted(string message) =>
        new RevertResult(RevertOutcome.NotStarted, journalIssue: Journal(JournalWritePhase.BeforeStart, message));

    public static RevertResult RevertNeedsAttention() =>
        new RevertResult(
            RevertOutcome.NeedsAttention,
            failure: Failure(FailureReason.RenameFailed, @"C:\A\f0.txt", systemMessage: "Access is denied."),
            needsAttention: new[] { Stranded(@"C:\A\tmp.txt", @"C:\A\f0.txt") });
}

/// <summary>Fails for one item index and passes the others.</summary>
internal sealed class ThrowingRule : IRenameRule
{
    private readonly int _failingIndex;
    private readonly string _message;

    public ThrowingRule(RuleStage stage, int failingIndex, string message)
    {
        Stage = stage;
        _failingIndex = failingIndex;
        _message = message;
    }

    public RuleStage Stage { get; }

    public bool Enabled => true;

    public NameParts Apply(NameParts name, ItemContext context)
    {
        if (context.Index == _failingIndex)
        {
            throw new InvalidOperationException(_message);
        }

        return name;
    }
}

/// <summary>Gives the item at one index a fixed full name and leaves the others.</summary>
internal sealed class RenameToRule : IRenameRule
{
    private readonly int _index;
    private readonly string _fullName;

    public RenameToRule(int index, string fullName)
    {
        _index = index;
        _fullName = fullName;
    }

    public RuleStage Stage => RuleStage.Replace;

    public bool Enabled => true;

    public NameParts Apply(NameParts name, ItemContext context) =>
        context.Index == _index ? NameParts.Split(_fullName, context.Item.Kind) : name;
}

/// <summary>A preset folder that is empty and whose every write fails like a full disk.</summary>
internal sealed class FailingPresetFiles : IPresetFiles
{
    public bool FolderExists(string folder) => false;

    public IReadOnlyList<string> ListJsonFiles(string folder) => Array.Empty<string>();

    public bool EntryExists(string path) => false;

    public long FileLength(string path) => 0;

    public byte[] ReadAll(string path) => throw new IOException("No file.");

    public void WriteAtomic(string path, byte[] content) => throw new IOException("Disk full.");

    public void Delete(string path) => throw new NotSupportedException();
}
