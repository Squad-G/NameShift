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
using NameShift.Ui.Presets;
using NameShift.Ui.Preview;
using NameShift.Ui.Rules;

namespace NameShift.Cmd;

/// <summary>Runs one command line: lists the folder like the window, plans with <see cref="RenamePlanner"/>, and
/// renames and reverts only through the <see cref="IBatchRenamer"/> (the journal), in the window's words. Never reads
/// input and never prompts. <c>--quiet</c> hides success output only: every error is written as without it.</summary>
public sealed class CommandRunner
{
    private readonly TextWriter _output;
    private readonly TextWriter _error;
    private readonly string _currentDirectory;
    private readonly IBatchRenamer _renamer;
    private readonly IFileListSource _files;
    private readonly CultureInfo _culture;
    private readonly string _version;
    private readonly PresetStore _presets;
    private bool _renamerCalled;
    private bool _undoing;

    /// <param name="output">Results (exit code 0).</param>
    /// <param name="error">Errors and every other result.</param>
    /// <param name="currentDirectory">The folder a relative folder argument is joined to.</param>
    /// <param name="renamer">The only way to rename: runs the plan and records it for Revert.</param>
    /// <param name="files">Lists the folder.</param>
    /// <param name="culture">Formats the counts of the window's sentences.</param>
    /// <param name="version">The text <c>--version</c> prints after <c>NameShift</c>.</param>
    /// <param name="presets">The saved presets <c>--preset</c> reads and <c>--save-preset</c> writes.</param>
    public CommandRunner(TextWriter output, TextWriter error, string currentDirectory, IBatchRenamer renamer,
        IFileListSource files, CultureInfo culture, string version, PresetStore presets)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _error = error ?? throw new ArgumentNullException(nameof(error));
        _currentDirectory = currentDirectory ?? throw new ArgumentNullException(nameof(currentDirectory));
        _renamer = renamer ?? throw new ArgumentNullException(nameof(renamer));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _culture = culture ?? throw new ArgumentNullException(nameof(culture));
        _version = version ?? throw new ArgumentNullException(nameof(version));
        _presets = presets ?? throw new ArgumentNullException(nameof(presets));
    }

    /// <summary>For tests only: when set, replaces the rules of the panels (a rule error cannot be produced by
    /// --replace or --case).</summary>
    internal Func<ApplyOptions, IReadOnlyList<IRenameRule>>? BuildRules { get; set; }

    /// <summary>The real thing: the disk's listing, the journaled renamer with its history in
    /// <paramref name="historyRoot"/>, the presets in <paramref name="presetsFolder"/> (nothing is read until a preset
    /// option is used), and the current culture. <paramref name="version"/> is what <c>--version</c> prints.</summary>
    public static CommandRunner ForDisk(
        TextWriter output, TextWriter error, string currentDirectory, string historyRoot, string presetsFolder,
        string version) =>
        new CommandRunner(
            output,
            error,
            currentDirectory,
            new JournaledBatchRenamer(new JournaledRenamer(historyRoot)),
            new DiskFileListSource(),
            CultureInfo.CurrentCulture,
            version,
            new PresetStore(presetsFolder));

    /// <summary>Runs one command line; returns the exit code (an <see cref="ExitCode"/>), always 0 to 4.</summary>
    public int Run(IReadOnlyList<string> args)
    {
        if (args == null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        _renamerCalled = false;
        _undoing = false;
        try
        {
            return RunCommand(args);
        }
        catch (Exception)
        {
            // The one deliberate catch-all (a renamer bug, a closed output pipe...). Before the renamer was called
            // nothing can have been renamed (exit 3); from the call on the disk state is unknown, so the user must
            // look (exit 4). A script still gets one of the documented exit codes, and the exception text is never
            // written.
            bool renamerCalled = _renamerCalled;
            try
            {
                _error.WriteLine(renamerCalled
                    ? NameShift.Ui.Strings.ActionUnexpected
                    : (_undoing ? Strings.UnexpectedBeforeUndo : Strings.UnexpectedBeforeRename));
            }
            catch (Exception)
            {
                // The error stream is gone too: the exit code is all that is left.
            }

            return (int)(renamerCalled ? ExitCode.NeedsAttention : ExitCode.NotRenamed);
        }
    }

    private int RunCommand(IReadOnlyList<string> args)
    {
        ParsedCommand command = CommandLineParser.Parse(args);
        switch (command.Kind)
        {
            case CommandKind.Help:
                return WriteLines(HelpText.Main());
            case CommandKind.ApplyHelp:
                return WriteLines(HelpText.Apply());
            case CommandKind.UndoHelp:
                return WriteLines(HelpText.Undo());
            case CommandKind.Version:
                _output.WriteLine(Format(Strings.VersionLine, _version));
                return (int)ExitCode.Success;
            case CommandKind.Error:
                return Fail(command.Error, ExitCode.InputError);
            case CommandKind.Apply:
                return RunApply(command.Options!);
            case CommandKind.Undo:
                return RunUndo(command.UndoDryRun);
            default:
                throw new InvalidOperationException("Unknown command kind: " + command.Kind);
        }
    }

    /// <summary>Help text to the output stream: exit 0. Never lists, plans or renames.</summary>
    private int WriteLines(IReadOnlyList<string> lines)
    {
        foreach (string line in lines)
        {
            _output.WriteLine(line);
        }

        return (int)ExitCode.Success;
    }

    private int RunApply(ApplyOptions options)
    {
        if (!TryFolder(options.FolderText, out string folder, out string folderError))
        {
            return Fail(folderError, ExitCode.InputError);
        }

        // The window's own panels: the preset first, then each rule option over its panel, whole.
        var host = new RulePanelHostModel(RulePanelModels.CreateAll());
        if (options.PresetName != null)
        {
            PresetLoadResult loaded = _presets.Load(options.PresetName);
            if (loaded.Preset == null)
            {
                IReadOnlyList<PresetEntry> saved = _presets.List().Presets;
                string known = saved.Count == 0
                    ? Strings.NoSavedPresets
                    : Format(Strings.SavedPresets, string.Join(", ", saved.Select(entry => entry.Name)));
                return Fail(Format(Strings.PresetNotFound, options.PresetName) + " " + known, ExitCode.InputError);
            }

            host.ApplyPreset(loaded.Preset);
            if (host.Rules.Count == 0)
            {
                return Fail(Format(Strings.PresetHasNoRules, options.PresetName), ExitCode.InputError);
            }
        }

        options.ApplyTo(host);

        if (options.SaveName != null)
        {
            PresetSaveResult saveResult = _presets.Save(Preset.FromPanels(options.SaveName, host.Panels), options.Overwrite);
            switch (saveResult.Status)
            {
                case PresetSaveStatus.Saved:
                    if (options.Output != OutputLevel.Quiet)
                    {
                        _output.WriteLine(Format(Strings.PresetSaved, options.SaveName));
                    }

                    break;
                case PresetSaveStatus.Exists:
                    return Fail(Format(Strings.PresetExists, options.SaveName), ExitCode.InputError);
                case PresetSaveStatus.Failed:
                    return Fail(Format(Strings.PresetSaveFailed, saveResult.SystemMessage ?? ""), ExitCode.NotRenamed);
                default:
                    throw new InvalidOperationException("The preset name was refused: " + saveResult.Status);
            }
        }

        FileListing listing = _files.ListEntries(folder, false, CancellationToken.None);
        if (listing.Problem != null)
        {
            return Fail(Format(Strings.CannotList, folder, FolderProblemText.For(listing.Problem.Value)), ExitCode.InputError);
        }

        // The window's default order, then files only (hidden and system out by the filter's default); every entry
        // name still counts. No UI thread here, so the filter runs in place.
        var filter = new ItemFilter(includeFolders: false);
        RenameItem[] items = FileListOrder.Sort(listing.Items).Where(filter.Includes).ToArray();

        RenamePlan plan = RenamePlanner.Plan(BuildRules?.Invoke(options) ?? host.Rules, items, listing.ExistingNamesByFolder, CancellationToken.None);
        switch (plan.Blocker)
        {
            case RenameBlocker.Conflicts:
            case RenameBlocker.RuleErrors:
            case RenameBlocker.ConflictsAndRuleErrors:
                return Blocked(items, plan);
            case RenameBlocker.NothingToRename:
                if (options.Output == OutputLevel.Verbose)
                {
                    WriteUnchanged(plan);
                }

                if (options.Output != OutputLevel.Quiet)
                {
                    _output.WriteLine(Strings.NothingToRename);
                }

                return (int)ExitCode.Success;
            case RenameBlocker.None:
                return options.DryRun ? DryRun(plan, options.Output) : Rename(plan, options.Output);
            default:
                throw new InvalidOperationException("Unknown blocker: " + plan.Blocker);
        }
    }

    /// <summary>Takes back the last batch through the renamer, in the window's words. The journal is read first: Revert
    /// alone answers "nothing to revert" for every state that cannot be reverted. Never lists a folder.</summary>
    private int RunUndo(bool dryRun)
    {
        _undoing = true;
        LastBatch last = _renamer.LoadLastBatch();
        switch (last.State)
        {
            case LastBatchState.None:
            case LastBatchState.Reverted:
                _output.WriteLine(NameShift.Ui.Strings.NothingToRevert);
                return (int)ExitCode.Success;
            case LastBatchState.Interrupted:
            case LastBatchState.Unreadable:
                _error.WriteLine(Strings.UndoHistoryUnusable);
                return (int)ExitCode.NotRenamed;
            case LastBatchState.Revertable:
                if (dryRun)
                {
                    // The check reads the disk and changes nothing; every entry prints in journal order.
                    RevertCheck check = _renamer.CheckRevert();
                    foreach (CompletedRename rename in last.Renames)
                    {
                        SkippedRevert? skip = check.Skipped.FirstOrDefault(s => s.Entry.NewPath == rename.NewPath && s.Entry.OldPath == rename.OldPath);
                        string name = Path.GetFileName(rename.NewPath);
                        _output.WriteLine(skip != null
                            ? Format(Strings.DryRunSkippedLine, name, RevertCheckText.Reason(skip.Reason))
                            : Format(Strings.DryRunLine, name, Path.GetFileName(rename.OldPath)));
                    }

                    return (int)ExitCode.Success;
                }

                // From here on the disk state is unknown: an exception reaches Run's catch-all as exit 4.
                _renamerCalled = true;
                RevertResult result = _renamer.Revert();
                ActionReport report = ActionText.ForRevert(result, _culture);
                ExitCode code = ExitCodes.For(result);
                TextWriter writer = code == ExitCode.Success ? _output : _error;
                writer.WriteLine(report.Status);
                if (report.Details != null)
                {
                    writer.WriteLine(report.Details);
                }

                return (int)code;
            default:
                throw new InvalidOperationException("Unknown journal state: " + last.State);
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

    /// <summary>"old -> new" for each changed file, in list order.</summary>
    private void WriteChanged(RenamePlan plan)
    {
        foreach (RenameResult result in plan.Results)
        {
            if (result.Status == RenameStatus.Changed)
            {
                _output.WriteLine(Format(Strings.DryRunLine, result.Item.Name, result.NewName));
            }
        }
    }

    /// <summary>"name: unchanged" for each file the rules leave as it is, in list order. The plan's items are the CLI's
    /// files only (hidden, system and folders are filtered out before planning).</summary>
    private void WriteUnchanged(RenamePlan plan)
    {
        foreach (RenameResult result in plan.Results)
        {
            if (result.Status == RenameStatus.Unchanged)
            {
                _output.WriteLine(Format(Strings.UnchangedLine, result.Item.Name));
            }
        }
    }

    /// <summary>The preview: "old -> new" for each changed file, in list order; with <see cref="OutputLevel.Verbose"/>
    /// then the unchanged files. Nothing is renamed. The parser never builds Quiet with a dry run; if it happens, this
    /// writes as Normal.</summary>
    private int DryRun(RenamePlan plan, OutputLevel level)
    {
        WriteChanged(plan);
        if (level == OutputLevel.Verbose)
        {
            WriteUnchanged(plan);
        }

        return (int)ExitCode.Success;
    }

    /// <summary>Renames the plan's own results and conflicts through the journal, and reports in the window's words.
    /// On exit 0: Normal writes the summary, Verbose first lists the renamed files (from the plan, in list order) and
    /// the unchanged files, Quiet writes nothing. Anything else goes to the error stream at every level.</summary>
    private int Rename(RenamePlan plan, OutputLevel level)
    {
        // From here on the disk state is unknown: an exception reaches Run's catch-all as exit 4.
        _renamerCalled = true;
        RenameRunResult result = _renamer.Rename(plan.Results, plan.Conflicts);
        ActionReport report = ActionText.ForRename(result, _culture);
        ExitCode code = ExitCodes.For(result);
        if (code != ExitCode.Success)
        {
            _error.WriteLine(report.Status);
            if (report.Details != null)
            {
                _error.WriteLine(report.Details);
            }

            return (int)code;
        }

        if (level == OutputLevel.Quiet)
        {
            return (int)code;
        }

        if (level == OutputLevel.Verbose)
        {
            WriteChanged(plan);
            WriteUnchanged(plan);
        }

        _output.WriteLine(report.Status);
        if (report.Details != null)
        {
            _output.WriteLine(report.Details);
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
