using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NameShift.Core;
using NameShift.Disk;
using NameShift.Ui.Actions;
using NameShift.Ui.Files;

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
        Func<ApplyOptions, IReadOnlyList<IRenameRule>>? buildRules = null)
    {
        var output = new StringWriter(CultureInfo.InvariantCulture);
        var error = new StringWriter(CultureInfo.InvariantCulture);
        var runner = new CommandRunner(output, error, currentDirectory, renamer, files ?? new DiskFileListSource(), EnUs);
        if (buildRules != null)
        {
            runner.BuildRules = buildRules;
        }

        int code = runner.Run(args);
        return (code, output.ToString(), error.ToString());
    }

    /// <summary>The real thing on a temp folder: the disk listing and a journaled renamer with the temp history root.</summary>
    public static (int Code, string Output, string Error) RunOnDisk(TempFolder temp, params string[] args)
    {
        var output = new StringWriter(CultureInfo.InvariantCulture);
        var error = new StringWriter(CultureInfo.InvariantCulture);
        int code = CommandRunner.ForDisk(output, error, temp.Files, temp.History).Run(args);
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

    public LastBatch LoadLastBatch() => throw new NotSupportedException("The command line never reads the journal.");

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

    public RevertResult Revert() => throw new NotSupportedException("The command line never reverts.");
}

/// <summary>Returns one fixed listing for every folder.</summary>
internal sealed class FakeFileListSource : IFileListSource
{
    private readonly FileListing _listing;

    public FakeFileListSource(FileListing listing)
    {
        _listing = listing;
    }

    public FileListing ListEntries(string folderPath) => _listing;
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
