using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using NameShift.Core;
using NameShift.Disk;
using NameShift.Ui;
using NameShift.Ui.Actions;
using NameShift.Ui.Files;
using NameShift.Ui.Preview;

namespace NameShift.Cmd;

/// <summary>Runs one command line: lists the folder like the window, plans with <see cref="RenamePlanner"/>, and
/// renames only through the <see cref="IBatchRenamer"/> (the journal), in the window's words. Never reads input and
/// never prompts.</summary>
public sealed class CommandRunner
{
    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly string _currentDirectory;
    private readonly IBatchRenamer _renamer;
    private readonly IFileListSource _files;
    private readonly CultureInfo _culture;

    /// <param name="output">Results (exit code 0).</param>
    /// <param name="error">Errors and every other result.</param>
    /// <param name="currentDirectory">The folder a relative folder argument is joined to.</param>
    /// <param name="renamer">The only way to rename: runs the plan and records it for Revert.</param>
    /// <param name="files">Lists the folder.</param>
    /// <param name="culture">Formats the counts of the window's sentences.</param>
    public CommandRunner(TextWriter output, TextWriter error, string currentDirectory, IBatchRenamer renamer,
        IFileListSource files, CultureInfo culture)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _error = error ?? throw new ArgumentNullException(nameof(error));
        _currentDirectory = currentDirectory ?? throw new ArgumentNullException(nameof(currentDirectory));
        _renamer = renamer ?? throw new ArgumentNullException(nameof(renamer));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _culture = culture ?? throw new ArgumentNullException(nameof(culture));
    }

    /// <summary>For tests only: replaces <see cref="ApplyOptions.BuildRules"/> (a rule error cannot be produced by
    /// --replace or --case).</summary>
    internal Func<ApplyOptions, IReadOnlyList<IRenameRule>> BuildRules { get; set; } = o => o.BuildRules();

    /// <summary>The real thing: the disk's listing, the journaled renamer with its history in
    /// <paramref name="historyRoot"/>, and the current culture.</summary>
    public static CommandRunner ForDisk(TextWriter output, TextWriter error, string currentDirectory, string historyRoot) =>
        new CommandRunner(
            output,
            error,
            currentDirectory,
            new JournaledBatchRenamer(new JournaledRenamer(historyRoot)),
            new DiskFileListSource(),
            CultureInfo.CurrentCulture);

    /// <summary>Runs one command line; returns the exit code (an <see cref="ExitCode"/>), always 0 to 4.</summary>
    public int Run(IReadOnlyList<string> args)
    {
        if (args == null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        try
        {
            return RunCommand(args);
        }
        catch (Exception)
        {
            // The one deliberate catch-all (a renamer bug, a closed output pipe...): the disk state is unknown, so the
            // user must look, and a script still gets one of the documented exit codes.
            try
            {
                _error.WriteLine(NameShift.Ui.Strings.ActionUnexpected);
            }
            catch (Exception)
            {
                // The error stream is gone too: the exit code is all that is left.
            }

            return (int)ExitCode.NeedsAttention;
        }
    }

    private int RunCommand(IReadOnlyList<string> args)
    {
        if (!CommandLineParser.TryParse(args, out ApplyOptions? options, out string parseError))
        {
            return Fail(parseError, ExitCode.InputError);
        }

        if (!TryFolder(options!.FolderText, out string folder, out string folderError))
        {
            return Fail(folderError, ExitCode.InputError);
        }

        FileListing listing = _files.ListEntries(folder);
        if (listing.Problem != null)
        {
            return Fail(Format(Strings.CannotList, folder, FolderProblemText.For(listing.Problem.Value)), ExitCode.InputError);
        }

        // The window's default order, then files only (hidden and system out); every entry name still counts.
        var filter = new ItemFilter(includeFolders: false);
        RenameItem[] items = FileListOrder.Sort(listing.Items).Where(filter.Includes).ToArray();
        var existingNames = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [folder] = listing.ExistingNames.ToArray(),
        };

        RenamePlan plan = RenamePlanner.Plan(BuildRules(options), items, existingNames, CancellationToken.None);
        switch (plan.Blocker)
        {
            case RenameBlocker.Conflicts:
            case RenameBlocker.RuleErrors:
            case RenameBlocker.ConflictsAndRuleErrors:
                return Blocked(items, plan);
            case RenameBlocker.NothingToRename:
                _output.WriteLine(Strings.NothingToRename);
                return (int)ExitCode.Success;
            case RenameBlocker.None:
                return options.DryRun ? DryRun(plan) : Rename(plan);
            default:
                throw new InvalidOperationException("Unknown blocker: " + plan.Blocker);
        }
    }

    /// <summary>Every rule error and conflict, item by item, then the window's hint. Nothing is renamed.</summary>
    private int Blocked(IReadOnlyList<RenameItem> items, RenamePlan plan)
    {
        for (int i = 0; i < items.Count; i++)
        {
            RenameResult result = plan.Results[i];
            if (result.Status == RenameStatus.Error)
            {
                _error.WriteLine(Format(Strings.ItemLine, items[i].Name, PreviewText.For(result).Text));
            }

            IReadOnlyList<Conflict> conflicts = plan.Conflicts.ForItem(i);
            if (conflicts.Count > 0)
            {
                _error.WriteLine(Format(Strings.ItemLine, items[i].Name, ConflictText.For(conflicts)));
            }
        }

        _error.WriteLine(ActionText.Hint(new PreviewSnapshot(items, plan.Results, plan.Conflicts)));
        return (int)ExitCode.Blocked;
    }

    /// <summary>The preview: "old -> new" for each changed file, in list order. Nothing is renamed.</summary>
    private int DryRun(RenamePlan plan)
    {
        foreach (RenameResult result in plan.Results)
        {
            if (result.Status == RenameStatus.Changed)
            {
                _output.WriteLine(Format(Strings.DryRunLine, result.Item.Name, result.NewName));
            }
        }

        return (int)ExitCode.Success;
    }

    /// <summary>Renames the plan's own results and conflicts through the journal, and reports in the window's words.</summary>
    private int Rename(RenamePlan plan)
    {
        // An exception here reaches Run's catch-all: exit 4, the disk state is unknown.
        RenameRunResult result = _renamer.Rename(plan.Results, plan.Conflicts);
        ActionReport report = ActionText.ForRename(result, _culture);
        ExitCode code = ExitCodes.For(result);
        TextWriter writer = code == ExitCode.Success ? _output : _error;
        writer.WriteLine(report.Status);
        if (report.Details != null)
        {
            writer.WriteLine(report.Details);
        }

        return (int)code;
    }

    /// <summary>The folder as a full, normalised path that exists and is a folder; else the one-line error.</summary>
    private bool TryFolder(string text, out string folder, out string error)
    {
        folder = "";
        error = "";
        if (text.IndexOf('"') >= 0)
        {
            error = Format(Strings.QuoteInFolder, text);
            return false;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            // The parser already refuses it; never let an empty text become the current directory.
            error = Strings.NoFolder;
            return false;
        }

        // The window's path rules (PathInput), variables kept as typed; a path they refuse as not fully qualified is
        // joined to the current directory and read again.
        if (PathInput.TryNormalize(text, out string path, expandVariables: false) != PathInputResult.Ok)
        {
            string joined;
            try
            {
                joined = Path.Combine(_currentDirectory, text.Trim());
            }
            catch (ArgumentException)
            {
                // .NET Framework rejects characters such as < in Path.Combine.
                error = Format(Strings.InvalidFolder, text);
                return false;
            }

            if (PathInput.TryNormalize(joined, out path, expandVariables: false) != PathInputResult.Ok)
            {
                error = Format(Strings.InvalidFolder, text);
                return false;
            }
        }

        if (File.Exists(path))
        {
            error = Format(Strings.FolderIsFile, path);
            return false;
        }

        if (!Directory.Exists(path))
        {
            error = Format(Strings.FolderNotFound, path);
            return false;
        }

        folder = path;
        return true;
    }

    private int Fail(string message, ExitCode code)
    {
        _error.WriteLine(message);
        return (int)code;
    }

    private static string Format(string format, params object[] values) =>
        string.Format(CultureInfo.InvariantCulture, format, values);
}
