using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NameShift.Core;
using NameShift.Disk;
using NameShift.Ui.Actions;
using NameShift.Ui.Files;
using NameShift.Ui.Preview;
using NameShift.Ui.Rules;
using NameShift.Ui.Tests.Files;
using NameShift.Ui.Tests.Preview;
using NameShift.Ui.Tests.Rules;
using Xunit;
using static NameShift.Ui.Tests.FakeFileListSource;

namespace NameShift.Ui.Tests.Actions;

public sealed class MainActionsTests
{
    private const string FolderA = @"C:\A";
    private const string FolderB = @"C:\B";

    private readonly FakeFileListSource _source = new FakeFileListSource();
    private readonly FakeDelay _delay = new FakeDelay();
    private readonly FakeBatchRenamer _renamer = new FakeBatchRenamer();
    private readonly FileListModel _files;
    private readonly List<string> _confirmations = new List<string>();
    private PreviewModel _preview = null!;
    private MainActions _actions = null!;
    private int _changes;

    public MainActionsTests()
    {
        _files = new FileListModel(_source);
        _source.SetEntries(FolderA, File("a.txt"), File("b.txt"), File("c.txt"));
        _source.SetEntries(FolderB, File("x.txt", folder: FolderB), File("y.txt", folder: FolderB));
    }

    private MainActions Build(params FakeRulePanelModel[] panels)
    {
        var host = new RulePanelHostModel(panels);
        _preview = new PreviewModel(host, _files, _delay.Delay);
        _actions = new MainActions(_preview, _files, host, _renamer);
        _actions.Changed += (_, _) => Interlocked.Increment(ref _changes);
        return _actions;
    }

    /// <summary>Builds the actions, loads the folder and waits for the preview.</summary>
    private async Task<MainActions> BuildLoaded(params FakeRulePanelModel[] panels) => await BuildLoadedIn(FolderA, panels);

    private async Task<MainActions> BuildLoadedIn(string folder, params FakeRulePanelModel[] panels)
    {
        MainActions actions = Build(panels);
        await _files.LoadAsync(folder);
        await _preview.Pending;
        return actions;
    }

    private static FakeRulePanelModel Enabled(FakeRulePanelModel panel, string text)
    {
        panel.Text = text;
        panel.Enabled = true;
        return panel;
    }

    /// <summary>Adds "x_" before every name: every row changes.</summary>
    private static FakeRulePanelModel Prefix(string text = "x_") => Enabled(FakeRulePanelModel.Add(), text);

    private static FakeRulePanelModel ReplacePanel(Func<string, IRenameRule?> build, string text = "")
    {
        var panel = new FakeRulePanelModel(RuleStage.Replace, build, "&Replace");
        panel.Text = text;
        panel.Enabled = true;
        return panel;
    }

    private bool Confirm(string text, bool answer)
    {
        _confirmations.Add(text);
        return answer;
    }

    private Task<ActionReport?> Rename(bool answer = true) => _actions.RenameAsync(t => Confirm(t, answer));

    private void SetBigFolder(int changed, int unchanged)
    {
        IEnumerable<RenameItem> items = Enumerable.Range(0, changed).Select(i => File($"fa{i:D4}.txt", folder: @"C:\Big"))
            .Concat(Enumerable.Range(0, unchanged).Select(i => File($"fb{i:D4}.txt", folder: @"C:\Big")));
        _source.SetEntries(@"C:\Big", items.ToArray());
    }

    /// <summary>Replaces "a" with "_": only the fa*.txt files change.</summary>
    private static FakeRulePanelModel ReplaceA() => Enabled(FakeRulePanelModel.Replace(), "a");

    // AC 1

    [Fact]
    public async Task NoChangedName_RenameDisabled()
    {
        MainActions actions = await BuildLoaded(FakeRulePanelModel.Add());

        Assert.Equal(FileListState.Loaded, _files.State);
        Assert.False(actions.CanRename);
        Assert.Equal("", actions.RenameHint);
    }

    // AC 2

    [Fact]
    public async Task Conflicts_RenameDisabled_HintSaysFixConflicts()
    {
        MainActions actions = await BuildLoaded(ReplacePanel(_ => new RenameToRule("same.txt")));

        Assert.True(_preview.Snapshot.HasConflicts);
        Assert.False(actions.CanRename);
        Assert.Equal("Fix the conflicts to rename.", actions.RenameHint);
    }

    [Fact]
    public async Task RuleError_RenameDisabled_HintSaysFixRuleErrors()
    {
        MainActions actions = await BuildLoaded(ReplacePanel(_ => new ThrowingRule(RuleStage.Replace, 1, "boom")), Prefix());

        Assert.Equal(1, _preview.Snapshot.ErrorCount);
        Assert.False(_preview.Snapshot.HasConflicts);
        Assert.False(actions.CanRename);
        Assert.Equal("Fix the rule errors to rename.", actions.RenameHint);
    }

    [Fact]
    public async Task ConflictsAndRuleErrors_HintNamesBoth()
    {
        _source.SetEntries(FolderA, File("a.txt"), File("b.txt"), File("c.txt"), File("d.txt"));
        var duplicate = new RenameToRule("same.txt");
        var failing = new ThrowingRule(RuleStage.Replace, 3, "boom");
        MainActions actions = await BuildLoaded(ReplacePanel(_ => new SplitRule(duplicate, failing, 3)));

        Assert.True(_preview.Snapshot.HasConflicts);
        Assert.Equal(1, _preview.Snapshot.ErrorCount);
        Assert.False(actions.CanRename);
        Assert.Equal("Fix the conflicts and rule errors to rename.", actions.RenameHint);
    }

    [Fact]
    public async Task ValidChanges_RenameEnabled_NoHint()
    {
        MainActions actions = await BuildLoaded(Prefix());

        Assert.Equal(3, _preview.Snapshot.ChangedCount);
        Assert.True(actions.CanRename);
        Assert.Equal("", actions.RenameHint);
    }

    [Fact]
    public async Task RuleChangedDuringDebounce_RenameDisabledUntilPreviewApplied()
    {
        FakeRulePanelModel panel = Prefix();
        MainActions actions = await BuildLoaded(panel);
        Assert.True(actions.CanRename);
        int before = _changes;

        panel.Text = "y_";

        Assert.False(actions.CanRename);
        Assert.True(_changes > before);
        _delay.ReleaseAll();
        await _preview.Pending;
        Assert.True(actions.CanRename);
        Assert.Equal("y_a.txt", _preview.Snapshot.Results[0].NewName);
    }

    [Fact]
    public async Task RunInFlight_RenameDisabled()
    {
        var gate = new ManualResetEventSlim(false);
        try
        {
            FakeRulePanelModel hold = ReplacePanel(t => t == "hold" ? new GateRule(RuleStage.Replace, gate) : null);
            MainActions actions = await BuildLoaded(hold, Prefix());
            Assert.True(actions.CanRename);

            hold.Text = "hold";
            _delay.ReleaseAll();

            Assert.False(_preview.Pending.IsCompleted);
            Assert.False(actions.CanRename);
            gate.Set();
            await _preview.Pending;
            Assert.True(actions.CanRename);
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task SelectionChanged_RenameDisabledUntilPreviewApplied()
    {
        MainActions actions = await BuildLoaded(Prefix());
        int before = _changes;

        _files.SetSelection(new[] { 0 });

        Assert.False(actions.CanRename);
        Assert.True(_changes > before);
        _delay.ReleaseAll();
        await _preview.Pending;
        Assert.True(actions.CanRename);
        Assert.Equal(1, _preview.Snapshot.ChangedCount);
    }

    [Fact]
    public async Task FolderLoading_RenameDisabled()
    {
        MainActions actions = await BuildLoaded(Prefix());
        ManualResetEventSlim gate = _source.Gate(FolderB);
        try
        {
            Task load = _files.LoadAsync(FolderB);

            Assert.Equal(FileListState.Loading, _files.State);
            Assert.False(actions.CanRename);
            Assert.Equal("", actions.RenameHint);
            gate.Set();
            await load;
            await _preview.Pending;
            Assert.True(actions.CanRename);
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task PreviewFailed_RenameDisabled()
    {
        FakeRulePanelModel panel = ReplacePanel(t => t == "bad" ? new BrokenRule(RuleStage.Replace) : null);
        MainActions actions = await BuildLoaded(panel, Prefix());
        Assert.True(actions.CanRename);

        panel.Text = "bad";
        _delay.ReleaseAll();
        await _preview.Pending;

        Assert.True(_preview.HasFailed);
        Assert.False(actions.CanRename);
    }

    // AC 3

    [Fact]
    public async Task Rename_PassesTheSnapshotsOwnResultsAndConflicts()
    {
        await BuildLoaded(Prefix());
        PreviewSnapshot snapshot = _preview.Snapshot;

        await Rename();

        Assert.Equal(1, _renamer.RenameCalls);
        Assert.Same(snapshot.Results, _renamer.Results);
        Assert.Same(snapshot.Conflicts, _renamer.Conflicts);
    }

    [Fact]
    public async Task Rename_Succeeded_ReloadsList_Says3FilesRenamed()
    {
        await BuildLoaded(Prefix());
        _renamer.During = () => _source.SetEntries(FolderA, File("x_a.txt"), File("x_b.txt"), File("x_c.txt"));

        ActionReport? report = await Rename();

        Assert.NotNull(report);
        Assert.Equal("3 files renamed.", report!.Status);
        Assert.Null(report.Details);
        Assert.Equal(FileListState.Loaded, _files.State);
        Assert.Equal(new[] { "x_a.txt", "x_b.txt", "x_c.txt" }, _files.Items.Select(i => i.Name).ToArray());
        Assert.Empty(_confirmations);
    }

    [Fact]
    public async Task Rename_KeepsTheRules_ClearsTheSelection()
    {
        FakeRulePanelModel panel = Prefix();
        MainActions actions = await BuildLoaded(panel);
        _files.SetSelection(new[] { 1 });
        _delay.ReleaseAll();
        await _preview.Pending;
        _renamer.During = () => _source.SetEntries(FolderA, File("a.txt"), File("x_b.txt"), File("c.txt"));

        await Rename();
        await _preview.Pending;

        Assert.True(panel.Enabled);
        Assert.Equal("x_", panel.Text);
        Assert.True(_files.Batch.IsWholeList);
        Assert.Contains(_preview.Snapshot.Results, r => r.Item.Name == "x_b.txt" && r.NewName == "x_x_b.txt");
        Assert.False(actions.CanRename);
        Assert.Equal("Change a rule or the selection to rename again.", actions.RenameHint);
    }

    // AC 4

    [Fact]
    public async Task Rename_RolledBack_SaysWhichFileAndWhy_AndReloads()
    {
        await BuildLoaded(Prefix());
        _renamer.RenameResult = DiskResults.Failed(
            ExecutionOutcome.RolledBack,
            DiskResults.Failure(FailureReason.RenameFailed, @"C:\A\b.txt", systemMessage: "The process cannot access the file. "));
        _renamer.During = () => _source.SetEntries(FolderA, File("a.txt"), File("b.txt"), File("c.txt"), File("d.txt"));

        ActionReport? report = await Rename();

        Assert.Equal("Nothing was renamed. b.txt could not be renamed: The process cannot access the file.", report!.Status);
        Assert.Null(report.Details);
        Assert.Equal(4, _files.Items.Count);
    }

    // AC 5

    [Theory]
    [InlineData(LastBatchState.None)]
    [InlineData(LastBatchState.Reverted)]
    [InlineData(LastBatchState.Interrupted)]
    [InlineData(LastBatchState.Unreadable)]
    public async Task Revert_DisabledFor(LastBatchState state)
    {
        MainActions actions = await BuildLoaded(Prefix());
        _renamer.Last = DiskResults.Last(state);

        await actions.LoadJournalAsync();

        Assert.False(actions.CanRevert);
        Assert.Equal("", actions.RevertDescription);
        Assert.Null(await actions.RevertAsync());
        Assert.Equal(0, _renamer.RevertCalls);
    }

    [Fact]
    public async Task Revert_Revertable_Enabled_DescriptionNamesCountAndFolder()
    {
        MainActions actions = await BuildLoaded(Prefix());
        _renamer.Last = DiskResults.Last(LastBatchState.Revertable, 12, @"D:\Photos");
        int before = _changes;

        await actions.LoadJournalAsync();

        Assert.True(actions.CanRevert);
        Assert.Equal(@"Undo the last rename: 12 names in D:\Photos", actions.RevertDescription);
        Assert.True(_changes > before);
    }

    [Fact]
    public async Task JournalNotReadYet_RevertDisabled()
    {
        MainActions actions = await BuildLoaded(Prefix());
        _renamer.Last = DiskResults.Last(LastBatchState.Revertable);
        var gate = new ManualResetEventSlim(false);
        try
        {
            Assert.False(actions.CanRevert);

            _renamer.Load = () =>
            {
                gate.Wait();
                return DiskResults.Last(LastBatchState.Revertable);
            };
            Task read = actions.LoadJournalAsync();

            Assert.False(actions.CanRevert);
            gate.Set();
            await read;
            Assert.True(actions.CanRevert);
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task JournalReadThrows_RevertStaysDisabled()
    {
        MainActions actions = await BuildLoaded(Prefix());
        _renamer.Load = () => throw new InvalidOperationException("broken");

        await actions.LoadJournalAsync();

        Assert.True(actions.PendingJournalRead.IsCompleted);
        Assert.False(actions.CanRevert);
    }

    [Fact]
    public async Task AfterRename_JournalReadAgain_RevertEnabled()
    {
        MainActions actions = await BuildLoaded(Prefix());
        await actions.LoadJournalAsync();
        Assert.False(actions.CanRevert);
        _renamer.During = () => _renamer.Last = DiskResults.Last(LastBatchState.Revertable);

        await Rename();

        Assert.Equal(2, _renamer.LoadCalls);
        Assert.True(actions.CanRevert);
    }

    [Fact]
    public async Task Revert_Reverted_ReloadsList_DisablesRevert_SaysNamesRestored()
    {
        _source.SetEntries(FolderA, File("x_a.txt"), File("x_b.txt"), File("x_c.txt"));
        MainActions actions = await BuildLoaded(Prefix());
        _renamer.Last = DiskResults.Last(LastBatchState.Revertable);
        await actions.LoadJournalAsync();
        _renamer.During = () =>
        {
            _renamer.Last = DiskResults.Last(LastBatchState.Reverted);
            _source.SetEntries(FolderA, File("a.txt"), File("b.txt"), File("c.txt"));
        };

        ActionReport? report = await actions.RevertAsync();

        Assert.Equal("3 names restored.", report!.Status);
        Assert.Null(report.Details);
        Assert.Equal(1, _renamer.RevertCalls);
        Assert.False(actions.CanRevert);
        Assert.Equal(new[] { "a.txt", "b.txt", "c.txt" }, _files.Items.Select(i => i.Name).ToArray());
    }

    [Fact]
    public async Task Revert_PartlyReverted_StaysEnabled_DetailsListTheSkipped()
    {
        MainActions actions = await BuildLoaded(Prefix());
        _renamer.Last = DiskResults.Last(LastBatchState.Revertable);
        await actions.LoadJournalAsync();
        CompletedRename[] entries = DiskResults.Renames(3);
        _renamer.RevertResult = DiskResults.Revert(
            RevertOutcome.PartlyReverted,
            entries.Take(2).ToArray(),
            new[] { DiskResults.Skip(entries[2], RevertSkipReason.NameTaken, @"C:\A\f2.txt") });
        _renamer.During = () => _renamer.Last = DiskResults.Last(LastBatchState.Revertable, 1);

        ActionReport? report = await actions.RevertAsync();

        Assert.Equal("2 of 3 names restored. The others could not be restored; Revert stays available for them.", report!.Status);
        Assert.Equal(
            "Not restored:" + Environment.NewLine + @"C:\A\x_f2.txt: f2.txt now has its old name.",
            report.Details);
        Assert.True(actions.CanRevert);
        Assert.Equal(@"Undo the last rename: 1 name in C:\A", actions.RevertDescription);
    }

    [Fact]
    public async Task Revert_WithNoFolderShown_RunsWithoutReload()
    {
        MainActions actions = Build(Prefix());
        _renamer.Last = DiskResults.Last(LastBatchState.Revertable);
        await actions.LoadJournalAsync();

        ActionReport? report = await actions.RevertAsync();

        Assert.Equal("3 names restored.", report!.Status);
        Assert.Null(_files.FolderPath);
        Assert.False(actions.IsBusy);
    }

    [Fact]
    public async Task SlowStartupJournalRead_DoesNotOverwriteTheReadAfterRename()
    {
        MainActions actions = await BuildLoaded(Prefix());
        var gate = new ManualResetEventSlim(false);
        try
        {
            int reads = 0;
            _renamer.Load = () =>
            {
                if (Interlocked.Increment(ref reads) == 1)
                {
                    gate.Wait();
                    return DiskResults.Last(LastBatchState.None);
                }

                return DiskResults.Last(LastBatchState.Revertable);
            };
            Task startup = actions.LoadJournalAsync();

            await Rename();
            Assert.True(actions.CanRevert);
            gate.Set();
            await startup;

            Assert.True(actions.CanRevert);
        }
        finally
        {
            gate.Set();
        }
    }

    // AC 6

    [Fact]
    public async Task Reset_ResetsEveryPanel_FolderAndSelectionStay()
    {
        FakeRulePanelModel replace = Enabled(FakeRulePanelModel.Replace(), "a");
        FakeRulePanelModel add = Prefix();
        MainActions actions = await BuildLoaded(replace, add);
        _files.SetSelection(new[] { 0, 2 });
        FileBatch batch = _files.Batch;
        Assert.True(actions.CanReset);

        actions.Reset();

        foreach (FakeRulePanelModel panel in new[] { replace, add })
        {
            Assert.False(panel.Enabled);
            Assert.True(panel.IsDefault);
        }

        Assert.Equal(FolderA, _files.FolderPath);
        Assert.Same(batch, _files.Batch);
        Assert.False(actions.CanReset);
    }

    [Fact]
    public async Task Reset_NeverCallsTheRenamer()
    {
        MainActions actions = await BuildLoaded(Prefix());

        actions.Reset();

        Assert.Equal(0, _renamer.LoadCalls);
        Assert.Equal(0, _renamer.RenameCalls);
        Assert.Equal(0, _renamer.RevertCalls);
    }

    [Fact]
    public async Task Reset_NothingToReset_Disabled()
    {
        FakeRulePanelModel panel = FakeRulePanelModel.Add();
        MainActions actions = await BuildLoaded(panel);
        int calls = _delay.Calls.Count;

        Assert.False(actions.CanReset);
        actions.Reset();

        Assert.Equal(calls, _delay.Calls.Count);
    }

    // AC 7

    [Fact]
    public async Task Rename_500Changed_DoesNotAsk()
    {
        SetBigFolder(500, 0);
        await BuildLoadedIn(@"C:\Big", ReplaceA());

        await Rename();

        Assert.Empty(_confirmations);
        Assert.Equal(1, _renamer.RenameCalls);
    }

    [Fact]
    public async Task Rename_501Changed_AsksOnce_NoRenamesNothing()
    {
        SetBigFolder(501, 0);
        await BuildLoadedIn(@"C:\Big", ReplaceA());

        ActionReport? report = await Rename(answer: false);

        Assert.Equal(new[] { "Rename 501 files?" }, _confirmations);
        Assert.Null(report);
        Assert.Equal(0, _renamer.RenameCalls);
        Assert.False(_actions.IsBusy);
        Assert.True(_actions.CanRename);
    }

    [Fact]
    public async Task Rename_501Changed_Yes_Runs()
    {
        SetBigFolder(501, 0);
        await BuildLoadedIn(@"C:\Big", ReplaceA());
        _renamer.RenameResult = DiskResults.Succeeded(501);

        ActionReport? report = await Rename(answer: true);

        Assert.Single(_confirmations);
        Assert.Equal(1, _renamer.RenameCalls);
        Assert.Equal(501, _renamer.Results!.Count);
        Assert.NotNull(report);
    }

    [Fact]
    public async Task Rename_500ChangedAnd10Unchanged_DoesNotAsk()
    {
        SetBigFolder(500, 10);
        await BuildLoadedIn(@"C:\Big", ReplaceA());

        await Rename();

        Assert.Equal(500, _renamer.Results!.Count(r => r.Status == RenameStatus.Changed));
        Assert.Equal(510, _renamer.Results!.Count);
        Assert.Empty(_confirmations);
    }

    [Fact]
    public async Task PreviewChangedDuringConfirmation_DoesNotRun()
    {
        SetBigFolder(501, 0);
        FakeRulePanelModel panel = ReplaceA();
        MainActions actions = await BuildLoadedIn(@"C:\Big", panel);

        ActionReport? report = await actions.RenameAsync(_ =>
        {
            panel.Text = "fa";
            return true;
        });

        Assert.Equal("The new names changed. Check them and click Rename again.", report!.Status);
        Assert.Null(report.Details);
        Assert.Equal(0, _renamer.RenameCalls);
        Assert.False(actions.IsBusy);
    }

    [Fact]
    public async Task PreviewChangedAndAppliedDuringConfirmation_DoesNotRun()
    {
        SetBigFolder(501, 0);
        FakeRulePanelModel panel = ReplaceA();
        MainActions actions = await BuildLoadedIn(@"C:\Big", panel);
        PreviewSnapshot before = _preview.Snapshot;

        ActionReport? report = await actions.RenameAsync(_ =>
        {
            // The new preview is applied before the dialog closes: Rename would be allowed again, on another snapshot.
            panel.Text = "fa";
            _delay.ReleaseAll();
            _preview.Pending.GetAwaiter().GetResult();
            Assert.True(actions.CanRename);
            Assert.NotSame(before, _preview.Snapshot);
            return true;
        });

        Assert.Equal("The new names changed. Check them and click Rename again.", report!.Status);
        Assert.Equal(0, _renamer.RenameCalls);
        Assert.False(actions.IsBusy);
    }

    [Fact]
    public async Task Rename_NotAllowed_ReturnsNull_NeverAsks()
    {
        MainActions actions = await BuildLoaded(FakeRulePanelModel.Add());

        ActionReport? report = await actions.RenameAsync(t => Confirm(t, true));

        Assert.Null(report);
        Assert.Empty(_confirmations);
        Assert.Equal(0, _renamer.RenameCalls);
    }

    // AC 8

    [Fact]
    public async Task WhileRenaming_BusyEverythingDisabled_SecondCallIgnored()
    {
        MainActions actions = await BuildLoaded(Prefix());
        _renamer.Last = DiskResults.Last(LastBatchState.Revertable);
        var gate = new ManualResetEventSlim(false);
        _renamer.Gate = gate;
        try
        {
            int before = _changes;
            Task<ActionReport?> running = Rename();

            Assert.True(_changes > before);
            Assert.True(actions.IsBusy);
            Assert.False(actions.CanRename);
            Assert.False(actions.CanRevert);
            Assert.False(actions.CanReset);
            Assert.Equal("", actions.RenameHint);
            Assert.Equal("Renaming 3 files...", actions.BusyText);
            Assert.Null(await Rename());
            Assert.Null(await actions.RevertAsync());
            actions.Reset();

            gate.Set();
            await running;
            await _preview.Pending;

            Assert.Equal(1, _renamer.RenameCalls);
            Assert.Equal(0, _renamer.RevertCalls);
            Assert.False(actions.IsBusy);
            Assert.Equal("", actions.BusyText);
            Assert.False(actions.CanRename);
            Assert.True(actions.CanRevert);
            Assert.True(actions.CanReset);
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task WhileReverting_BusyEverythingDisabled()
    {
        MainActions actions = await BuildLoaded(Prefix());
        _renamer.Last = DiskResults.Last(LastBatchState.Revertable);
        await actions.LoadJournalAsync();
        var gate = new ManualResetEventSlim(false);
        _renamer.Gate = gate;
        try
        {
            Task<ActionReport?> running = actions.RevertAsync();

            Assert.True(actions.IsBusy);
            Assert.False(actions.CanRename);
            Assert.False(actions.CanRevert);
            Assert.False(actions.CanReset);
            Assert.Equal("Reverting...", actions.BusyText);
            Assert.Null(await actions.RevertAsync());

            gate.Set();
            await running;

            Assert.Equal(1, _renamer.RevertCalls);
            Assert.False(actions.IsBusy);
            Assert.Equal("", actions.BusyText);
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task BusyUntilTheReloadIsApplied()
    {
        MainActions actions = await BuildLoaded(Prefix());
        ManualResetEventSlim gate = _source.Gate(FolderA);
        try
        {
            Task<ActionReport?> running = Rename();

            Assert.True(SpinWait.SpinUntil(() => _renamer.LoadCalls == 1 && _files.State == FileListState.Loading, TimeSpan.FromSeconds(10)));
            Assert.True(actions.IsBusy);
            Assert.False(running.IsCompleted);

            gate.Set();
            await running;

            Assert.False(actions.IsBusy);
            Assert.Equal(FileListState.Loaded, _files.State);
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task RenamerThrows_SaysSomethingWentWrong_ReloadsAndUnlocks()
    {
        MainActions actions = await BuildLoaded(Prefix());
        _renamer.Throws = true;
        _renamer.Last = DiskResults.Last(LastBatchState.Interrupted);
        _renamer.During = () => _source.SetEntries(FolderA, File("x_a.txt"), File("b.txt"), File("c.txt"));

        ActionReport? report = await Rename();

        Assert.Equal("Something went wrong. Check the files in the folder before you continue.", report!.Status);
        Assert.Null(report.Details);
        Assert.Equal(1, _renamer.LoadCalls);
        Assert.False(actions.CanRevert);
        Assert.False(actions.IsBusy);
        Assert.Contains(_files.Items, i => i.Name == "x_a.txt");
    }

    [Fact]
    public void Ctor_NullArguments_Throw()
    {
        var host = new RulePanelHostModel(Array.Empty<IRulePanelModel>());
        var preview = new PreviewModel(host, _files);

        Assert.Throws<ArgumentNullException>(() => new MainActions(null!, _files, host, _renamer));
        Assert.Throws<ArgumentNullException>(() => new MainActions(preview, null!, host, _renamer));
        Assert.Throws<ArgumentNullException>(() => new MainActions(preview, _files, null!, _renamer));
        Assert.Throws<ArgumentNullException>(() => new MainActions(preview, _files, host, null!));
        var actions = new MainActions(preview, _files, host, _renamer);
        Assert.Throws<ArgumentNullException>(() => { _ = actions.RenameAsync(null!); });
    }

    [Fact]
    public void ConfirmAbove_Is500()
    {
        Assert.Equal(500, MainActions.ConfirmAbove);
    }

    /// <summary>One rule for some items and another from a given index: a conflict and a rule error in one run.</summary>
    private sealed class SplitRule : IRenameRule
    {
        private readonly IRenameRule _first;
        private readonly IRenameRule _second;
        private readonly int _from;

        public SplitRule(IRenameRule first, IRenameRule second, int from)
        {
            _first = first;
            _second = second;
            _from = from;
        }

        public RuleStage Stage => RuleStage.Replace;

        public bool Enabled => true;

        public NameParts Apply(NameParts name, ItemContext context) =>
            context.Index < _from ? _first.Apply(name, context) : _second.Apply(name, context);
    }

    // NS-56: FolderContentsChanged

    private List<(IReadOnlyList<string> Folders, bool Busy, string[] Items)> RecordEvents(MainActions actions)
    {
        var events = new List<(IReadOnlyList<string>, bool, string[])>();
        actions.FolderContentsChanged += (_, e) => events.Add((e.Folders, actions.IsBusy, _files.Items.Select(i => i.Name).ToArray()));
        return events;
    }

    private async Task<MainActions> RevertReady(string batchFolder, string shownFolder)
    {
        MainActions actions = await BuildLoadedIn(shownFolder, Prefix());
        _renamer.Last = DiskResults.Last(LastBatchState.Revertable, 3, batchFolder);
        await actions.LoadJournalAsync();
        return actions;
    }

    [Fact]
    public async Task Rename_Ran_RaisesOnce_ShownFolder_AfterReload_WhileBusy()
    {
        MainActions actions = await BuildLoaded(Prefix());
        var events = RecordEvents(actions);
        _renamer.During = () => _source.SetEntries(FolderA, File("x_a.txt"), File("x_b.txt"), File("x_c.txt"));

        await Rename();

        var e = Assert.Single(events);
        Assert.Equal(new[] { FolderA }, e.Folders);
        Assert.True(e.Busy);
        Assert.Equal(new[] { "x_a.txt", "x_b.txt", "x_c.txt" }, e.Items);
        Assert.False(actions.IsBusy);
    }

    [Fact]
    public async Task Rename_RolledBack_StillRaises()
    {
        MainActions actions = await BuildLoaded(Prefix());
        var events = RecordEvents(actions);
        _renamer.RenameResult = DiskResults.Failed(
            ExecutionOutcome.RolledBack,
            DiskResults.Failure(FailureReason.RenameFailed, @"C:\A\b.txt", systemMessage: "Busy."));

        await Rename();

        Assert.Single(events);
    }

    [Fact]
    public async Task RenamerThrows_StillRaises()
    {
        MainActions actions = await BuildLoaded(Prefix());
        var events = RecordEvents(actions);
        _renamer.Throws = true;
        _renamer.Last = DiskResults.Last(LastBatchState.Interrupted);

        await Rename();

        Assert.Single(events);
        Assert.False(actions.IsBusy);
    }

    [Fact]
    public async Task Rename_NotAllowed_DoesNotRaise()
    {
        MainActions actions = await BuildLoaded(FakeRulePanelModel.Add());
        var events = RecordEvents(actions);

        await Rename();

        Assert.Empty(events);
    }

    [Fact]
    public async Task Rename_501Changed_No_DoesNotRaise()
    {
        SetBigFolder(501, 0);
        MainActions actions = await BuildLoadedIn(@"C:\Big", ReplaceA());
        var events = RecordEvents(actions);

        await Rename(false);

        Assert.Empty(events);
    }

    [Fact]
    public async Task PreviewChangedDuringConfirmation_DoesNotRaise()
    {
        SetBigFolder(501, 0);
        FakeRulePanelModel panel = ReplaceA();
        MainActions actions = await BuildLoadedIn(@"C:\Big", panel);
        var events = RecordEvents(actions);

        await actions.RenameAsync(_ =>
        {
            panel.Text = "fa";
            return true;
        });

        Assert.Empty(events);
    }

    [Fact]
    public async Task Revert_BatchInAWhileBShown_RaisesAAndB()
    {
        MainActions actions = await RevertReady(FolderA, FolderB);
        var events = RecordEvents(actions);
        _renamer.During = () => _renamer.Last = DiskResults.Last(LastBatchState.Reverted);

        await actions.RevertAsync();

        var e = Assert.Single(events);
        Assert.Equal(new[] { FolderA, FolderB }, e.Folders);
        Assert.True(e.Busy);
    }

    [Fact]
    public async Task Revert_BatchInShownFolder_NoDuplicate()
    {
        MainActions actions = await RevertReady(@"c:\a", FolderA);
        var events = RecordEvents(actions);

        await actions.RevertAsync();

        Assert.Equal(new[] { @"c:\a" }, Assert.Single(events).Folders);
    }

    [Fact]
    public async Task Revert_PartlyReverted_StillRaises()
    {
        MainActions actions = await RevertReady(FolderA, FolderA);
        var events = RecordEvents(actions);
        CompletedRename[] entries = DiskResults.Renames(3);
        _renamer.RevertResult = DiskResults.Revert(
            RevertOutcome.PartlyReverted,
            entries.Take(2).ToArray(),
            new[] { DiskResults.Skip(entries[2], RevertSkipReason.NameTaken, @"C:\A\f2.txt") });

        await actions.RevertAsync();

        Assert.Single(events);
    }

    [Fact]
    public async Task Revert_WithNoFolderShown_RaisesBatchFolderOnly()
    {
        MainActions actions = Build(Prefix());
        _renamer.Last = DiskResults.Last(LastBatchState.Revertable);
        await actions.LoadJournalAsync();
        var events = RecordEvents(actions);

        await actions.RevertAsync();

        Assert.Equal(new[] { FolderA }, Assert.Single(events).Folders);
    }

    [Fact]
    public async Task Revert_NotAllowed_DoesNotRaise()
    {
        MainActions actions = await BuildLoaded(Prefix());
        var events = RecordEvents(actions);

        await actions.RevertAsync();

        Assert.Empty(events);
    }

    // NS-57: Rename stays off until something changes

    private const string LockText = "Change a rule or the selection to rename again.";

    private void ReloadAsRenamed() =>
        _renamer.During = () => _source.SetEntries(FolderA, File("x_a.txt"), File("x_b.txt"), File("x_c.txt"));

    /// <summary>A Rename that renamed 3 files, the reload and the new preview applied: the lock is set.</summary>
    private async Task<(MainActions Actions, FakeRulePanelModel Panel)> RenamedAndLocked()
    {
        FakeRulePanelModel panel = Prefix();
        MainActions actions = await BuildLoaded(panel);
        ReloadAsRenamed();
        await Rename();
        _delay.ReleaseAll();
        await _preview.Pending;
        return (actions, panel);
    }

    private async Task ApplyPreview()
    {
        _delay.ReleaseAll();
        await _preview.Pending;
    }

    // AC 1

    [Fact]
    public async Task Rename_RenamedFiles_LocksRename_EvenWithChangedPreview()
    {
        (MainActions actions, FakeRulePanelModel panel) = await RenamedAndLocked();

        Assert.True(_preview.IsCurrent);
        Assert.True(_preview.Snapshot.ChangedCount > 0);
        Assert.False(_preview.Snapshot.HasConflicts);
        Assert.False(actions.CanRename);
        Assert.Equal("x_", panel.Text);
        Assert.True(panel.Enabled);
    }

    // AC 2

    [Fact]
    public async Task Rename_RolledBack_DoesNotLock()
    {
        MainActions actions = await BuildLoaded(Prefix());
        _renamer.RenameResult = DiskResults.Failed(
            ExecutionOutcome.RolledBack,
            DiskResults.Failure(FailureReason.RenameFailed, @"C:\A\b.txt", systemMessage: "Busy."));

        await Rename();
        await ApplyPreview();

        Assert.True(actions.CanRename);
        Assert.Equal("", actions.RenameHint);
    }

    [Fact]
    public async Task Rename_NotStarted_DoesNotLock()
    {
        MainActions actions = await BuildLoaded(Prefix());
        _renamer.RenameResult = DiskResults.NotStarted("The disk is full.");

        await Rename();
        await ApplyPreview();

        Assert.True(actions.CanRename);
    }

    // AC 3

    [Fact]
    public async Task RenamerThrows_LocksRename()
    {
        MainActions actions = await BuildLoaded(Prefix());
        _renamer.Throws = true;
        _renamer.Last = DiskResults.Last(LastBatchState.Interrupted);

        ActionReport? report = await Rename();
        await ApplyPreview();

        Assert.Equal("Something went wrong. Check the files in the folder before you continue.", report!.Status);
        Assert.False(actions.CanRename);
        Assert.Equal(LockText, actions.RenameHint);
    }

    [Fact]
    public async Task Rename_NeedsAttention_LocksRename()
    {
        MainActions actions = await BuildLoaded(Prefix());
        _renamer.RenameResult = DiskResults.NeedsAttention(
            DiskResults.Failure(FailureReason.RenameFailed, @"C:\A.txt", systemMessage: "Busy."),
            DiskResults.Stranded(@"C:\A\~ns-1234.tmp", @"C:\A.txt"));

        await Rename();
        await ApplyPreview();

        Assert.False(actions.CanRename);
        Assert.Equal(LockText, actions.RenameHint);
    }

    // AC 4

    [Fact]
    public async Task Locked_HintSaysChangeARule()
    {
        (MainActions actions, _) = await RenamedAndLocked();

        Assert.Equal(LockText, actions.RenameHint);
    }

    [Fact]
    public async Task Locked_ConflictHintWins()
    {
        var rule = new CollidingRule();
        MainActions actions = await BuildLoaded(ReplacePanel(_ => rule, "on"));
        Assert.True(actions.CanRename);
        _renamer.During = () => rule.Collide = true;

        await Rename();
        await ApplyPreview();

        Assert.True(_preview.Snapshot.HasConflicts);
        Assert.False(actions.CanRename);
        Assert.Equal("Fix the conflicts to rename.", actions.RenameHint);
    }

    [Fact]
    public async Task Locked_WhileBusy_HintEmpty()
    {
        (MainActions actions, _) = await RenamedAndLocked();
        _renamer.Last = DiskResults.Last(LastBatchState.Revertable);
        await actions.LoadJournalAsync();
        var gate = new ManualResetEventSlim(false);
        _renamer.Gate = gate;
        try
        {
            Task<ActionReport?> running = actions.RevertAsync();

            Assert.True(actions.IsBusy);
            Assert.Equal("", actions.RenameHint);
            gate.Set();
            await running;
        }
        finally
        {
            gate.Set();
        }
    }

    // AC 5

    [Fact]
    public async Task Locked_RuleEdit_Unlocks()
    {
        (MainActions actions, FakeRulePanelModel panel) = await RenamedAndLocked();

        panel.Text = "y_";
        await ApplyPreview();

        Assert.True(actions.CanRename);
        Assert.Equal("", actions.RenameHint);
    }

    [Fact]
    public async Task Locked_RuleDisabled_Unlocks()
    {
        (MainActions actions, FakeRulePanelModel panel) = await RenamedAndLocked();

        panel.Enabled = false;
        await ApplyPreview();

        Assert.Equal("", actions.RenameHint);
    }

    [Fact]
    public async Task Locked_ResetAll_Unlocks()
    {
        (MainActions actions, _) = await RenamedAndLocked();

        actions.Reset();
        await ApplyPreview();

        Assert.Equal("", actions.RenameHint);
    }

    [Fact]
    public async Task Locked_SelectionChange_Unlocks()
    {
        (MainActions actions, _) = await RenamedAndLocked();

        _files.SetSelection(new[] { 0 });
        await ApplyPreview();

        Assert.True(actions.CanRename);
        Assert.Equal("", actions.RenameHint);
    }

    [Fact]
    public async Task Locked_LoadOtherFolder_Unlocks()
    {
        (MainActions actions, _) = await RenamedAndLocked();

        await _files.LoadAsync(FolderB);
        await ApplyPreview();

        Assert.True(actions.CanRename);
        Assert.Equal("", actions.RenameHint);
    }

    [Fact]
    public async Task Locked_FilterApplied_Unlocks()
    {
        (MainActions actions, _) = await RenamedAndLocked();
        Assert.False(actions.CanRename);

        Assert.True(await TestWait.Within(_files.SetFilterAsync(new ItemFilter(mask: "*"))));
        await ApplyPreview();

        Assert.True(actions.CanRename);
        Assert.Equal("", actions.RenameHint);
    }

    [Fact]
    public async Task Locked_ReloadSameFolder_Unlocks()
    {
        (MainActions actions, _) = await RenamedAndLocked();

        await _files.LoadAsync(FolderA);
        await ApplyPreview();

        Assert.True(actions.CanRename);
    }

    // AC 6

    [Fact]
    public async Task OwnReload_DoesNotUnlock_EvenAfterPreviewRecomputes()
    {
        FakeRulePanelModel panel = Prefix();
        MainActions actions = await BuildLoaded(panel);
        ReloadAsRenamed();

        await Rename();

        Assert.False(actions.IsBusy);
        Assert.False(actions.CanRename);
        await ApplyPreview();
        Assert.False(actions.CanRename);
        Assert.Equal(LockText, actions.RenameHint);
    }

    [Fact]
    public async Task FolderChangedDuringRename_NoLock()
    {
        MainActions actions = await BuildLoaded(Prefix());
        ManualResetEventSlim gate = _source.Gate(FolderA);
        Task? other = null;
        void OnChanged(object? sender, EventArgs e)
        {
            if (actions.IsBusy && _files.State == FileListState.Loading && _files.FolderPath == FolderA)
            {
                _files.Changed -= OnChanged;
                other = _files.LoadAsync(FolderB);
            }
        }

        try
        {
            _files.Changed += OnChanged;
            Task<ActionReport?> running = Rename();
            Assert.True(SpinWait.SpinUntil(() => other != null, TimeSpan.FromSeconds(10)));
            await other!;
            gate.Set();
            await running;

            // Checked before anything else can raise an event: the lock must not be set for the other folder.
            Assert.Equal(FolderB, _files.FolderPath);
            Assert.Equal(FileListState.Loaded, _files.State);
            Assert.Equal("", actions.RenameHint);
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task SelectionChangedByFolderContentsHandler_WhileBusy_KeepsLock()
    {
        MainActions actions = await BuildLoaded(Prefix());
        ReloadAsRenamed();
        actions.FolderContentsChanged += (_, _) => _files.SetSelection(new[] { 0 });

        await Rename();
        await ApplyPreview();

        Assert.False(actions.CanRename);
        Assert.Equal(LockText, actions.RenameHint);
    }

    [Fact]
    public async Task SortByFolderContentsHandler_WhileBusy_KeepsLock()
    {
        MainActions actions = await BuildLoaded(Prefix());
        ReloadAsRenamed();
        actions.FolderContentsChanged += (_, _) => _files.SortBy(FileListColumn.Name);

        await Rename();
        await ApplyPreview();

        Assert.False(actions.CanRename);
        Assert.Equal(LockText, actions.RenameHint);
    }

    // AC 7

    [Fact]
    public async Task Revert_Ran_ClearsLock()
    {
        FakeRulePanelModel panel = Prefix();
        MainActions actions = await BuildLoaded(panel);
        _renamer.During = () =>
        {
            _source.SetEntries(FolderA, File("x_a.txt"), File("x_b.txt"), File("x_c.txt"));
            _renamer.Last = DiskResults.Last(LastBatchState.Revertable);
        };
        await Rename();
        await ApplyPreview();
        Assert.False(actions.CanRename);
        Assert.True(actions.CanRevert);

        await actions.RevertAsync();
        await ApplyPreview();

        Assert.Equal(1, _renamer.RevertCalls);
        Assert.True(actions.CanRename);
        Assert.Equal("", actions.RenameHint);
    }

    [Fact]
    public async Task Revert_NotAllowed_LockUntouched()
    {
        (MainActions actions, _) = await RenamedAndLocked();
        _renamer.Last = DiskResults.Last(LastBatchState.None);
        await actions.LoadJournalAsync();

        Assert.Null(await actions.RevertAsync());

        Assert.False(actions.CanRename);
        Assert.Equal(LockText, actions.RenameHint);
    }

    // AC 8

    [Fact]
    public async Task Locked_RenameAsync_ReturnsNull_NothingRuns()
    {
        (MainActions _, _) = await RenamedAndLocked();

        Assert.Null(await Rename());

        Assert.Equal(1, _renamer.RenameCalls);
        Assert.Empty(_confirmations);
    }

    // AC 9

    [Fact]
    public async Task TwoRenames_WithRuleEditBetween_SecondRunsAndLocksAgain()
    {
        (MainActions actions, FakeRulePanelModel panel) = await RenamedAndLocked();
        panel.Text = "y_";
        await ApplyPreview();
        Assert.True(actions.CanRename);

        ActionReport? report = await Rename();
        await ApplyPreview();

        Assert.NotNull(report);
        Assert.Equal(2, _renamer.RenameCalls);
        Assert.False(actions.CanRename);
        Assert.Equal(LockText, actions.RenameHint);
    }

    // AC 10

    [Fact]
    public async Task Lock_DoesNotAffectResetRevertBusy_AndRaisesChanged()
    {
        FakeRulePanelModel panel = Prefix();
        MainActions actions = await BuildLoaded(panel);
        _renamer.During = () =>
        {
            _source.SetEntries(FolderA, File("x_a.txt"), File("x_b.txt"), File("x_c.txt"));
            _renamer.Last = DiskResults.Last(LastBatchState.Revertable);
        };
        int beforeRename = _changes;
        await Rename();
        await ApplyPreview();

        Assert.True(_changes > beforeRename);
        Assert.False(actions.CanRename);
        Assert.True(actions.CanReset);
        Assert.True(actions.CanRevert);
        Assert.False(actions.IsBusy);

        int beforeEdit = _changes;
        panel.Text = "y_";

        Assert.True(_changes > beforeEdit);
        Assert.False(actions.IsBusy);
    }

    /// <summary>Adds "x_" before the name while Collide is false; gives every item the same name when true.</summary>
    private sealed class CollidingRule : IRenameRule
    {
        public volatile bool Collide;

        public RuleStage Stage => RuleStage.Replace;

        public bool Enabled => true;

        public NameParts Apply(NameParts name, ItemContext context) =>
            NameParts.Split(Collide ? "same.txt" : "x_" + context.Item.Name, context.Item.Kind);
    }

    [Fact]
    public async Task Locked_Sort_Unlocks()
    {
        (MainActions actions, _) = await RenamedAndLocked();
        Assert.False(actions.CanRename);

        _files.SortBy(FileListColumn.Name);
        await ApplyPreview();

        Assert.True(actions.CanRename);
        Assert.Equal("", actions.RenameHint);
    }

    [Fact]
    public async Task Locked_NoSort_StaysLocked()
    {
        (MainActions actions, _) = await RenamedAndLocked();

        await ApplyPreview();

        Assert.False(actions.CanRename);
        Assert.Equal(LockText, actions.RenameHint);
    }
}
