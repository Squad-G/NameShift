using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NameShift.Core;
using NameShift.Ui.Files;
using NameShift.Ui.Preview;
using NameShift.Ui.Rules;
using NameShift.Ui.Tests.Files;
using NameShift.Ui.Tests.Rules;
using Xunit;
using static NameShift.Ui.Tests.FakeFileListSource;
using static NameShift.Ui.Tests.Files.TestWait;

namespace NameShift.Ui.Tests.Preview;

public sealed class PreviewModelTests
{
    private readonly FakeFileListSource _source = new FakeFileListSource();
    private readonly FakeDelay _delay = new FakeDelay();
    private readonly FileListModel _files;
    private int _changes;

    public PreviewModelTests()
    {
        _files = new FileListModel(_source);
        _source.SetEntries(@"C:\A", File("a1.txt"), File("a2.txt"), File("b.txt"));
        _source.SetEntries(@"C:\B", File("x.txt", folder: @"C:\B"), File("y.txt", folder: @"C:\B"));
    }

    private PreviewModel Build(FakeRulePanelModel panel)
    {
        var host = new RulePanelHostModel(new IRulePanelModel[] { panel });
        var preview = new PreviewModel(host, _files, _delay.Delay);
        preview.Changed += (_, _) => Interlocked.Increment(ref _changes);
        return preview;
    }

    private static FakeRulePanelModel ReplacePanel(string text)
    {
        FakeRulePanelModel panel = FakeRulePanelModel.Replace();
        panel.Text = text;
        return panel;
    }

    private static FakeRulePanelModel PanelOf(Func<string, IRenameRule?> build, string text = "", RuleStage stage = RuleStage.Replace)
    {
        var panel = new FakeRulePanelModel(stage, build, "&Replace");
        panel.Text = text;
        return panel;
    }

    [Fact]
    public async Task RulesChanged_AfterTheDelay_ComputesEveryRow()
    {
        FakeRulePanelModel panel = ReplacePanel("a");
        PreviewModel preview = Build(panel);
        await _files.LoadAsync(@"C:\A");
        await preview.Pending;
        int before = _changes;

        panel.Enabled = true;
        _delay.ReleaseAll();
        await preview.Pending;

        Assert.Equal(new PreviewCell("_1.txt", PreviewCellKind.Changed), preview.CellForRow(0), CellComparer.Instance);
        Assert.Equal(new PreviewCell("_2.txt", PreviewCellKind.Changed), preview.CellForRow(1), CellComparer.Instance);
        Assert.Equal(new PreviewCell("b.txt", PreviewCellKind.Unchanged), preview.CellForRow(2), CellComparer.Instance);
        Assert.Equal(before + 1, _changes);
    }

    [Fact]
    public async Task FolderLoaded_ComputesAtOnce_WithoutTheDelay()
    {
        FakeRulePanelModel panel = ReplacePanel("a");
        PreviewModel preview = Build(panel);
        panel.Enabled = true;

        await _files.LoadAsync(@"C:\A");
        await preview.Pending;

        Assert.Empty(_delay.Calls);
        Assert.Equal("_1.txt", preview.CellForRow(0).Text);
        Assert.Equal("b.txt", preview.CellForRow(2).Text);
    }

    [Fact]
    public async Task RulesChanged_WaitsTheDebounceDelay_BeforeRunning()
    {
        var counter = new ApplyCounter();
        FakeRulePanelModel panel = PanelOf(t => new CountingRule(RuleStage.Replace, t, counter), "x");
        PreviewModel preview = Build(panel);
        await _files.LoadAsync(@"C:\A");
        await preview.Pending;
        PreviewSnapshot before = preview.Snapshot;

        panel.Enabled = true;

        Assert.Equal(new[] { TimeSpan.FromMilliseconds(150) }, _delay.Calls);
        Assert.Equal(0, counter.Count);
        Assert.Same(before, preview.Snapshot);
        _delay.ReleaseAll();
        await preview.Pending;
        Assert.Equal(3, counter.Count);
    }

    [Fact]
    public async Task RapidChanges_RunOnce_WithTheLatestSettings()
    {
        var counter = new ApplyCounter();
        FakeRulePanelModel panel = PanelOf(t => new CountingRule(RuleStage.Replace, t, counter));
        PreviewModel preview = Build(panel);
        await _files.LoadAsync(@"C:\A");
        await preview.Pending;

        panel.Enabled = true;
        panel.Text = "a";
        panel.Text = "ab";
        panel.Text = "abc";
        _delay.ReleaseLast();
        await preview.Pending;

        Assert.Equal(3, _delay.Cancelled);
        Assert.Equal(new[] { "abc", "abc", "abc" }, counter.Texts);
    }

    [Fact]
    public void RulesChanged_WithNoFolderLoaded_DoesNothing()
    {
        FakeRulePanelModel panel = ReplacePanel("a");
        PreviewModel preview = Build(panel);

        panel.Enabled = true;

        Assert.Empty(_delay.Calls);
        Assert.Same(PreviewSnapshot.Empty, preview.Snapshot);
        Assert.Equal(0, _changes);
    }

    [Fact]
    public async Task Compute_RunsOffTheCallingThread()
    {
        var gate = new ManualResetEventSlim(false);
        try
        {
            FakeRulePanelModel panel = PanelOf(_ => new GateRule(RuleStage.Replace, gate));
            PreviewModel preview = Build(panel);
            panel.Enabled = true;

            await _files.LoadAsync(@"C:\A");

            Assert.False(preview.Pending.IsCompleted);
            Assert.Equal(PreviewCellKind.None, preview.CellForRow(0).Kind);
            gate.Set();
            await preview.Pending;
            Assert.Equal(PreviewCellKind.Unchanged, preview.CellForRow(0).Kind);
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task NewerChange_CancelsAndDiscardsTheOlderRun()
    {
        var gate = new ManualResetEventSlim(false);
        try
        {
            var counter = new ApplyCounter();
            FakeRulePanelModel panel = PanelOf(t =>
                t == "go" ? new CountingRule(RuleStage.Replace, t, counter) : new GateRule(RuleStage.Replace, gate, "b.txt"));
            PreviewModel preview = Build(panel);
            panel.Enabled = true;
            await _files.LoadAsync(@"C:\A");
            Task firstRun = preview.Pending;

            panel.Text = "go";
            _delay.ReleaseAll();
            await preview.Pending;
            PreviewSnapshot applied = preview.Snapshot;
            int changes = _changes;
            Assert.Equal(3, applied.Results.Count);

            gate.Set();
            await firstRun;

            Assert.Same(applied, preview.Snapshot);
            Assert.Equal(changes, _changes);
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task WhileRecomputing_TheOldPreviewStays()
    {
        FakeRulePanelModel panel = ReplacePanel("a");
        PreviewModel preview = Build(panel);
        panel.Enabled = true;
        await _files.LoadAsync(@"C:\A");
        await preview.Pending;
        Assert.Equal("_1.txt", preview.CellForRow(0).Text);

        panel.Text = "ab";

        Assert.False(preview.Pending.IsCompleted);
        Assert.Equal("_1.txt", preview.CellForRow(0).Text);
        _delay.ReleaseAll();
        await preview.Pending;
    }

    [Fact]
    public async Task FolderLoading_ClearsThePreview()
    {
        FakeRulePanelModel panel = ReplacePanel("a");
        PreviewModel preview = Build(panel);
        panel.Enabled = true;
        await _files.LoadAsync(@"C:\A");
        await preview.Pending;
        int before = _changes;
        ManualResetEventSlim gate = _source.Gate(@"C:\B");
        Task<bool> loading = _files.LoadAsync(@"C:\B");
        try
        {
            Assert.Same(PreviewSnapshot.Empty, preview.Snapshot);
            Assert.Equal(PreviewCellKind.None, preview.CellForRow(0).Kind);
            Assert.True(_changes > before);
        }
        finally
        {
            gate.Set();
            await loading;
            await preview.Pending;
        }
    }

    [Fact]
    public async Task FolderChange_RecomputesForTheNewList()
    {
        FakeRulePanelModel panel = ReplacePanel("x");
        PreviewModel preview = Build(panel);
        panel.Enabled = true;
        await _files.LoadAsync(@"C:\A");
        await preview.Pending;

        await _files.LoadAsync(@"C:\B");
        await preview.Pending;

        Assert.Equal("_.txt", preview.CellForRow(0).Text);
        Assert.Equal("y.txt", preview.CellForRow(1).Text);
    }

    [Fact]
    public async Task FolderChangeDuringARun_DiscardsTheOldFolder()
    {
        var gate = new ManualResetEventSlim(false);
        try
        {
            FakeRulePanelModel panel = PanelOf(_ => new GateRule(RuleStage.Replace, gate, "b.txt"));
            PreviewModel preview = Build(panel);
            panel.Enabled = true;
            await _files.LoadAsync(@"C:\A");
            Task runA = preview.Pending;

            await _files.LoadAsync(@"C:\B");
            await preview.Pending;
            PreviewSnapshot applied = preview.Snapshot;
            Assert.Equal(2, applied.Results.Count);

            gate.Set();
            await runA;

            Assert.Same(applied, preview.Snapshot);
            Assert.Equal("x.txt", preview.CellForRow(0).Text);
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task CellForRow_OutOfRangeOrCleared_IsNone()
    {
        var gate = new ManualResetEventSlim(false);
        try
        {
            FakeRulePanelModel panel = PanelOf(_ => new GateRule(RuleStage.Replace, gate, "x.txt"));
            PreviewModel preview = Build(panel);
            panel.Enabled = true;
            await _files.LoadAsync(@"C:\A");
            await preview.Pending;
            Assert.Equal(PreviewCellKind.Unchanged, preview.CellForRow(0).Kind);
            Assert.Equal(PreviewCellKind.None, preview.CellForRow(-1).Kind);
            Assert.Equal(PreviewCellKind.None, preview.CellForRow(3).Kind);

            await _files.LoadAsync(@"C:\B");

            Assert.False(preview.Pending.IsCompleted);
            Assert.Equal(PreviewCellKind.None, preview.CellForRow(0).Kind);
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task FolderLoadStarting_CancelsAPendingDebounce()
    {
        FakeRulePanelModel panel = ReplacePanel("a");
        PreviewModel preview = Build(panel);
        await _files.LoadAsync(@"C:\A");
        await preview.Pending;
        panel.Enabled = true;
        Assert.Equal(0, _delay.Cancelled);
        ManualResetEventSlim gate = _source.Gate(@"C:\B");
        Task<bool> loading = _files.LoadAsync(@"C:\B");
        try
        {
            Assert.Equal(1, _delay.Cancelled);
        }
        finally
        {
            gate.Set();
            await loading;
            await preview.Pending;
        }
    }

    [Fact]
    public async Task RuleError_ShowsTheErrorText()
    {
        FakeRulePanelModel panel = PanelOf(_ => new ThrowingRule(RuleStage.Replace, 1, "boom"));
        PreviewModel preview = Build(panel);
        panel.Enabled = true;

        await _files.LoadAsync(@"C:\A");
        await preview.Pending;

        Assert.Equal(PreviewCellKind.Unchanged, preview.CellForRow(0).Kind);
        Assert.Equal(new PreviewCell("Error: boom", PreviewCellKind.Error), preview.CellForRow(1), CellComparer.Instance);
        Assert.Equal(PreviewCellKind.Unchanged, preview.CellForRow(2).Kind);
    }

    [Fact]
    public async Task UnexpectedFailure_ClearsAndSetsHasFailed()
    {
        FakeRulePanelModel panel = PanelOf(t => t == "bad" ? new BrokenRule(RuleStage.Replace) : new ReplaceRule("a", "_"));
        PreviewModel preview = Build(panel);
        panel.Enabled = true;
        await _files.LoadAsync(@"C:\A");
        await preview.Pending;
        int before = _changes;

        panel.Text = "bad";
        _delay.ReleaseAll();
        await preview.Pending;

        Assert.Same(PreviewSnapshot.Empty, preview.Snapshot);
        Assert.True(preview.HasFailed);
        Assert.Equal(before + 1, _changes);

        panel.Text = "ok";
        _delay.ReleaseAll();
        await preview.Pending;

        Assert.False(preview.HasFailed);
        Assert.Equal(3, preview.Snapshot.Results.Count);
    }

    [Fact]
    public void Ctor_NullArguments_Throw()
    {
        var host = new RulePanelHostModel(new IRulePanelModel[] { FakeRulePanelModel.Replace() });

        Assert.Throws<ArgumentNullException>(() => new PreviewModel(null!, _files, _delay.Delay));
        Assert.Throws<ArgumentNullException>(() => new PreviewModel(host, null!, _delay.Delay));
        Assert.Throws<ArgumentNullException>(() => new PreviewModel(host, _files, null!));
    }

    [Fact]
    public void DebounceDelay_Is150Milliseconds()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(150), PreviewModel.DebounceDelay);
        Assert.Equal(PreviewCellKind.None, PreviewCell.None.Kind);
        Assert.Equal("", PreviewCell.None.Text);
        Assert.NotNull(new PreviewModel(
            new RulePanelHostModel(Enumerable.Empty<IRulePanelModel>()),
            _files));
    }

    private static FakeRulePanelModel EnabledPanelOf(Func<string, IRenameRule?> build)
    {
        FakeRulePanelModel panel = PanelOf(build);
        panel.Enabled = true;
        return panel;
    }

    private static string Message(ConflictCode code) => ConflictText.Message(new Conflict(code));

    [Fact]
    public async Task Duplicates_MarkBothRows_AndCount()
    {
        PreviewModel preview = Build(EnabledPanelOf(_ => new RenameToRule("x.txt", "a")));

        await _files.LoadAsync(@"C:\A");
        await preview.Pending;

        foreach (int row in new[] { 0, 1 })
        {
            StatusCell cell = preview.StatusForRow(row);
            Assert.True(cell.IsConflict);
            Assert.Equal(Message(ConflictCode.DuplicateTarget), cell.Text);
        }

        Assert.False(preview.StatusForRow(2).IsConflict);
        Assert.Equal("", preview.StatusForRow(2).Text);
        Assert.Equal("2 conflicts", preview.ConflictSummary);
    }

    [Fact]
    public async Task HiddenEntry_IsTargetExists()
    {
        _source.SetEntries(@"C:\H", new[] { "_.txt" }, File("a.txt", folder: @"C:\H"));
        PreviewModel preview = Build(EnabledPanelOf(_ => new ReplaceRule("a", "_")));

        await _files.LoadAsync(@"C:\H");
        await preview.Pending;

        StatusCell cell = preview.StatusForRow(0);
        Assert.True(cell.IsConflict);
        Assert.Equal(Message(ConflictCode.TargetExists), cell.Text);
        Assert.Equal("1 conflict", preview.ConflictSummary);
    }

    [Fact]
    public async Task NameOutsideTheBatch_IsTargetExists()
    {
        _source.SetEntries(@"C:\H", new[] { "c.txt" }, File("a.txt", folder: @"C:\H"), File("b.txt", folder: @"C:\H"));
        PreviewModel preview = Build(EnabledPanelOf(_ => new ReplaceRule("a", "c")));

        await _files.LoadAsync(@"C:\H");
        await preview.Pending;

        Assert.Equal(Message(ConflictCode.TargetExists), preview.StatusForRow(0).Text);
    }

    [Fact]
    public async Task InvalidCharacter_InStatus_IsEscaped()
    {
        PreviewModel preview = Build(EnabledPanelOf(_ => new ReplaceRule("b", "\u0001")));

        await _files.LoadAsync(@"C:\A");
        await preview.Pending;

        StatusCell cell = preview.StatusForRow(2);
        Assert.True(cell.IsConflict);
        Assert.Contains("U+0001", cell.Text);
        Assert.DoesNotContain(cell.Text, char.IsControl);
    }

    [Fact]
    public async Task PathTooLong_InStatus()
    {
        string folder = @"C:\" + new string('f', 247);
        _source.SetEntries(folder, File("a.txt", folder: folder));
        PreviewModel preview = Build(EnabledPanelOf(_ => new ReplaceRule("a", new string('x', 16))));

        await _files.LoadAsync(folder);
        await preview.Pending;

        Assert.Equal(Message(ConflictCode.PathTooLong), preview.StatusForRow(0).Text);
    }

    [Fact]
    public async Task NoConflicts_SummaryIsEmpty()
    {
        PreviewModel preview = Build(EnabledPanelOf(_ => new ReplaceRule("a", "_")));

        await _files.LoadAsync(@"C:\A");
        await preview.Pending;

        Assert.Equal("", preview.ConflictSummary);
        for (int row = 0; row < 3; row++)
        {
            Assert.False(preview.StatusForRow(row).IsConflict);
            Assert.Equal("", preview.StatusForRow(row).Text);
        }
    }

    [Fact]
    public async Task StatusForRow_OutOfRangeOrCleared_IsNone()
    {
        PreviewModel preview = Build(EnabledPanelOf(_ => new RenameToRule("x.txt", "a")));
        await _files.LoadAsync(@"C:\A");
        await preview.Pending;
        Assert.True(preview.StatusForRow(0).IsConflict);
        Assert.False(preview.StatusForRow(-1).IsConflict);
        Assert.False(preview.StatusForRow(3).IsConflict);

        ManualResetEventSlim gate = _source.Gate(@"C:\B");
        Task<bool> loading = _files.LoadAsync(@"C:\B");
        try
        {
            Assert.Equal("", preview.StatusForRow(0).Text);
            Assert.False(preview.StatusForRow(0).IsConflict);
            Assert.Equal("", preview.ConflictSummary);
        }
        finally
        {
            gate.Set();
            await loading;
            await preview.Pending;
        }
    }

    [Fact]
    public async Task Checks_RunOffTheCallingThread()
    {
        var gate = new ManualResetEventSlim(false);
        try
        {
            PreviewModel preview = Build(EnabledPanelOf(_ => new RenameToRule("x.txt", "a", gate)));

            await _files.LoadAsync(@"C:\A");

            Assert.False(preview.Pending.IsCompleted);
            Assert.Equal("", preview.ConflictSummary);
            gate.Set();
            await preview.Pending;
            Assert.Equal(preview.Snapshot.Results.Count, preview.Snapshot.Conflicts.Count);
            Assert.Equal("2 conflicts", preview.ConflictSummary);
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task StaleRun_ConflictsAreDiscarded()
    {
        var gate = new ManualResetEventSlim(false);
        try
        {
            // The older run renames all three files to x.txt and waits on the last one (b.txt): the pipeline's per-item
            // cancellation check has already passed, so only the run's own cancellation and version checks discard it.
            var stale = new RenameToRule("x.txt", gate: gate, gateItemName: "b.txt");
            FakeRulePanelModel panel = PanelOf(t => t == "none" ? new CountingRule(RuleStage.Replace, t, new ApplyCounter()) : stale);
            PreviewModel preview = Build(panel);
            panel.Enabled = true;
            await _files.LoadAsync(@"C:\A");
            Task firstRun = preview.Pending;
            Assert.True(stale.Waiting.Wait(TimeSpan.FromSeconds(10)));

            panel.Text = "none";
            _delay.ReleaseAll();
            await preview.Pending;
            PreviewSnapshot applied = preview.Snapshot;
            Assert.Equal("", preview.ConflictSummary);

            gate.Set();
            await firstRun;

            Assert.Same(applied, preview.Snapshot);
            Assert.Equal("", preview.ConflictSummary);
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task ItemOfAnotherFolder_HasItsOwnExistingNames()
    {
        // NS-33: the names are grouped by each item's own folder, so DuplicateTargetCheck always finds its folder
        // (before, an item whose folder was not the listed folder made the check throw).
        _source.SetEntries(@"C:\C", File("q.txt", folder: @"C:\Elsewhere"), Hidden("x.txt", @"C:\Elsewhere"));
        PreviewModel preview = Build(EnabledPanelOf(_ => new RenameToRule("x.txt")));

        await _files.LoadAsync(@"C:\C");
        await preview.Pending;

        Assert.False(preview.HasFailed);
        Assert.Single(preview.Snapshot.Results);
        Assert.Equal(Message(ConflictCode.TargetExists), preview.StatusForRow(0).Text);
        Assert.Equal("1 conflict", preview.ConflictSummary);
    }

    [Fact]
    public async Task ErrorRow_HasNoStatus()
    {
        PreviewModel preview = Build(EnabledPanelOf(_ => new ThrowingRule(RuleStage.Replace, 1, "boom")));

        await _files.LoadAsync(@"C:\A");
        await preview.Pending;

        Assert.False(preview.StatusForRow(1).IsConflict);
        Assert.Equal("", preview.StatusForRow(1).Text);
        Assert.Equal(1, preview.Snapshot.ErrorCount);
    }

    private async Task<PreviewModel> BuildLoaded(FakeRulePanelModel panel)
    {
        PreviewModel preview = Build(panel);
        panel.Enabled = true;
        await _files.LoadAsync(@"C:\A");
        await preview.Pending;
        return preview;
    }

    [Fact]
    public async Task NoSelection_EveryRowIsInTheBatch()
    {
        PreviewModel preview = await BuildLoaded(ReplacePanel("a"));

        Assert.Equal("_1.txt", preview.CellForRow(0).Text);
        Assert.Equal("_2.txt", preview.CellForRow(1).Text);
        Assert.Equal("b.txt", preview.CellForRow(2).Text);
        Assert.True(preview.Snapshot.Batch.IsWholeList);
    }

    [Fact]
    public async Task Selection_OnlySelectedRowsHaveANewName()
    {
        PreviewModel preview = await BuildLoaded(ReplacePanel("a"));

        _files.SetSelection(new[] { 0, 2 });
        _delay.ReleaseAll();
        await preview.Pending;

        Assert.Equal(new PreviewCell("_1.txt", PreviewCellKind.Changed), preview.CellForRow(0), CellComparer.Instance);
        Assert.Equal(new PreviewCell("b.txt", PreviewCellKind.Unchanged), preview.CellForRow(2), CellComparer.Instance);
        Assert.Equal(PreviewCellKind.None, preview.CellForRow(1).Kind);
        Assert.Equal("", preview.StatusForRow(1).Text);
        Assert.Equal(2, preview.Snapshot.Results.Count);
    }

    [Fact]
    public async Task UnselectedRow_HasNoStatus()
    {
        PreviewModel preview = await BuildLoaded(PanelOf(_ => new RenameToRule("x.txt")));

        _files.SetSelection(new[] { 0 });
        _delay.ReleaseAll();
        await preview.Pending;

        Assert.False(preview.StatusForRow(0).IsConflict);
        Assert.Equal("", preview.StatusForRow(1).Text);
        Assert.False(preview.StatusForRow(1).IsConflict);
        Assert.Equal("", preview.StatusForRow(2).Text);
        Assert.Equal("", preview.ConflictSummary);
    }

    [Fact]
    public async Task UnselectedFile_StillCausesTargetExists()
    {
        PreviewModel preview = await BuildLoaded(PanelOf(_ => new ReplaceRule("a1", "b")));

        _files.SetSelection(new[] { 0 });
        _delay.ReleaseAll();
        await preview.Pending;

        Assert.Equal(Message(ConflictCode.TargetExists), preview.StatusForRow(0).Text);
        Assert.Equal("1 conflict", preview.ConflictSummary);
    }

    [Fact]
    public async Task Selection_InClickOrder_RunsInListOrder_IndexesFromZero()
    {
        var rule = new ContextRecordingRule();
        PreviewModel preview = await BuildLoaded(PanelOf(_ => rule));
        Assert.Equal(3, rule.Calls.Count);

        // Rules run on the pool in order for one run: take only what the selection's run recorded.
        int skip = rule.Calls.Count;
        _files.SetSelection(new[] { 2, 0 });
        _delay.ReleaseAll();
        await preview.Pending;

        Assert.Equal(
            new[] { ("a1.txt", 0, 2), ("b.txt", 1, 2) },
            rule.Calls.Skip(skip).ToArray());
    }

    [Fact]
    public async Task Selection_Numbering_FollowsTheList()
    {
        PreviewModel preview = await BuildLoaded(
            PanelOf(_ => new NumberingRule(NumberingMode.Suffix, start: 1, pad: 2, separator: "_"), stage: RuleStage.Numbering));

        _files.SetSelection(new[] { 2, 1 });
        _delay.ReleaseAll();
        await preview.Pending;

        Assert.Equal("a2_01.txt", preview.CellForRow(1).Text);
        Assert.Equal("b_02.txt", preview.CellForRow(2).Text);
        Assert.Equal(PreviewCellKind.None, preview.CellForRow(0).Kind);
    }

    [Fact]
    public async Task SelectionChange_RecomputesAfterTheDebounce()
    {
        PreviewModel preview = await BuildLoaded(ReplacePanel("a"));
        PreviewSnapshot before = preview.Snapshot;
        int changes = _changes;

        _files.SetSelection(new[] { 1 });

        Assert.Equal(new[] { TimeSpan.FromMilliseconds(150) }, _delay.Calls);
        Assert.Same(before, preview.Snapshot);
        Assert.Equal("_1.txt", preview.CellForRow(0).Text);
        _delay.ReleaseAll();
        await preview.Pending;

        Assert.Equal(1, preview.Snapshot.Batch.Count);
        Assert.Equal(changes + 1, _changes);
        Assert.Equal(PreviewCellKind.None, preview.CellForRow(0).Kind);
    }

    [Fact]
    public async Task SelectionChange_WithNoFolderLoaded_DoesNothing()
    {
        PreviewModel preview = Build(ReplacePanel("a"));

        _files.SetSelection(Array.Empty<int>());

        Assert.Empty(_delay.Calls);
        Assert.Same(PreviewSnapshot.Empty, preview.Snapshot);
        await preview.Pending;
    }

    [Fact]
    public async Task RuleChange_KeepsTheSelection()
    {
        FakeRulePanelModel panel = ReplacePanel("a");
        PreviewModel preview = await BuildLoaded(panel);
        _files.SetSelection(new[] { 0 });
        _delay.ReleaseAll();
        await preview.Pending;

        panel.Text = "ab";
        _delay.ReleaseAll();
        await preview.Pending;

        Assert.Equal(new[] { 0 }, preview.Snapshot.Batch.Rows);
        Assert.Equal(PreviewCellKind.None, preview.CellForRow(1).Kind);
    }

    [Fact]
    public async Task TenThousandItems_SelectHalf_FollowsTheList()
    {
        RenameItem[] items = Enumerable.Range(0, 10000).Select(i => File($"f{i:D5}.txt", folder: @"C:\Big")).ToArray();
        _source.SetEntries(@"C:\Big", items);
        PreviewModel preview = Build(PanelOf(_ => new NumberingRule(NumberingMode.Suffix, start: 1, pad: 5, separator: "_"), stage: RuleStage.Numbering));
        await _files.LoadAsync(@"C:\Big");
        await preview.Pending;

        _files.SetSelection(Enumerable.Range(0, 5000).Select(i => 9999 - (2 * i)).ToArray());
        _delay.ReleaseAll();
        await preview.Pending;

        Assert.Equal(5000, preview.Snapshot.Results.Count);
        Assert.True(preview.Snapshot.Batch.TryGetIndex(1, out int first));
        Assert.Equal(0, first);
        Assert.True(preview.Snapshot.Batch.TryGetIndex(9999, out int last));
        Assert.Equal(4999, last);
        Assert.Equal(PreviewCellKind.None, preview.CellForRow(0).Kind);
        Assert.Equal(PreviewCellKind.None, preview.CellForRow(9998).Kind);
    }

    [Fact]
    public async Task ChangedCount_CountsChangedOnly()
    {
        PreviewModel preview = Build(EnabledPanelOf(_ => new ReplaceRule("a", "_")));

        await _files.LoadAsync(@"C:\A");
        await preview.Pending;

        Assert.Equal(2, preview.Snapshot.ChangedCount);
        Assert.Equal(0, PreviewSnapshot.Empty.ChangedCount);
    }

    [Fact]
    public async Task ChangedCount_ExcludesErrors()
    {
        PreviewModel preview = Build(EnabledPanelOf(_ => new ThrowingRule(RuleStage.Replace, 1, "boom")));

        await _files.LoadAsync(@"C:\A");
        await preview.Pending;

        Assert.Equal(0, preview.Snapshot.ChangedCount);
        Assert.Equal(1, preview.Snapshot.ErrorCount);
    }

    [Fact]
    public async Task IsCurrent_FalseFromRequestUntilApplied_OutdatedRaisedOnce()
    {
        FakeRulePanelModel panel = ReplacePanel("a");
        PreviewModel preview = await BuildLoaded(panel);
        int outdated = 0;
        preview.Outdated += (_, _) => Interlocked.Increment(ref outdated);
        Assert.True(preview.IsCurrent);

        panel.Text = "ab";

        Assert.False(preview.IsCurrent);
        Assert.Equal(1, outdated);
        _delay.ReleaseAll();
        await preview.Pending;
        Assert.True(preview.IsCurrent);
        Assert.Equal(1, outdated);
    }

    [Fact]
    public async Task IsCurrent_StaleRunCancelled_StaysFalseUntilNewestApplied()
    {
        var gate = new ManualResetEventSlim(false);
        try
        {
            FakeRulePanelModel panel = PanelOf(t =>
                t == "go" ? new ReplaceRule("a", "_") : new GateRule(RuleStage.Replace, gate, "b.txt"));
            PreviewModel preview = Build(panel);
            panel.Enabled = true;
            await _files.LoadAsync(@"C:\A");
            Task firstRun = preview.Pending;
            Assert.False(preview.IsCurrent);

            panel.Text = "go";
            gate.Set();
            await firstRun;

            Assert.False(preview.IsCurrent);
            _delay.ReleaseAll();
            await preview.Pending;
            Assert.True(preview.IsCurrent);
            Assert.Equal(2, preview.Snapshot.ChangedCount);
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task IsCurrent_AfterFailedRun_False_SnapshotEmpty()
    {
        FakeRulePanelModel panel = PanelOf(t => t == "bad" ? new BrokenRule(RuleStage.Replace) : new ReplaceRule("a", "_"));
        PreviewModel preview = Build(panel);
        panel.Enabled = true;
        await _files.LoadAsync(@"C:\A");
        await preview.Pending;

        panel.Text = "bad";
        _delay.ReleaseAll();
        await preview.Pending;

        Assert.True(preview.HasFailed);
        Assert.Same(PreviewSnapshot.Empty, preview.Snapshot);
        Assert.False(preview.IsCurrent);

        panel.Text = "ok";
        _delay.ReleaseAll();
        await preview.Pending;

        Assert.True(preview.IsCurrent);
    }

    [Fact]
    public async Task IsCurrent_SelectionChanged_FalseUntilApplied()
    {
        PreviewModel preview = await BuildLoaded(ReplacePanel("a"));

        _files.SetSelection(new[] { 0 });

        Assert.False(preview.IsCurrent);
        _delay.ReleaseAll();
        await preview.Pending;
        Assert.True(preview.IsCurrent);
    }

    [Fact]
    public async Task IsCurrent_WhileLoading_TrueForTheEmptyList()
    {
        PreviewModel preview = await BuildLoaded(ReplacePanel("a"));
        ManualResetEventSlim gate = _source.Gate(@"C:\B");
        try
        {
            Task load = _files.LoadAsync(@"C:\B");

            Assert.Same(PreviewSnapshot.Empty, preview.Snapshot);
            Assert.True(preview.IsCurrent);
            gate.Set();
            await load;
            await preview.Pending;
            Assert.True(preview.IsCurrent);
        }
        finally
        {
            gate.Set();
        }
    }

    [Fact]
    public async Task Sort_RecomputesOnce_WithoutDebounce()
    {
        var counter = new ApplyCounter();
        PreviewModel preview = await BuildLoaded(PanelOf(t => new CountingRule(RuleStage.Replace, t, counter), "x"));
        int delays = _delay.Calls.Count;
        int before = counter.Count;

        _files.SortBy(FileListColumn.Name);
        Assert.Equal(delays, _delay.Calls.Count);
        await preview.Pending;

        Assert.Equal(before + 3, counter.Count);
        Assert.True(preview.IsCurrent);
        Assert.Equal("b.txt", preview.CellForRow(0).Text);
        Assert.Equal("a1.txt", preview.CellForRow(2).Text);
    }

    [Fact]
    public async Task Sort_OldSnapshotStopsAnsweringAtOnce()
    {
        PreviewModel preview = await BuildLoaded(ReplacePanel("a"));
        Assert.True(preview.IsCurrent);

        _files.SortBy(FileListColumn.Name);

        Assert.False(preview.IsCurrent);
        Assert.Equal(PreviewCellKind.None, preview.CellForRow(0).Kind);
        _delay.ReleaseAll();
        await preview.Pending;
        Assert.True(preview.IsCurrent);
    }

    [Fact]
    public async Task Numbering_FollowsSizeDescending()
    {
        _source.SetEntries(@"C:\A", File("a.txt", 1), File("b.txt", 3), File("c.txt", 2));
        PreviewModel preview = await BuildLoaded(
            PanelOf(_ => new NumberingRule(NumberingMode.Suffix, start: 1, pad: 2, separator: "_"), stage: RuleStage.Numbering));

        _files.SortBy(FileListColumn.Size);
        Assert.Empty(_delay.Calls);
        await preview.Pending;
        _files.SortBy(FileListColumn.Size);
        Assert.Empty(_delay.Calls);
        await preview.Pending;

        Assert.Equal("b_01.txt", preview.CellForRow(0).Text);
        Assert.Equal("c_02.txt", preview.CellForRow(1).Text);
        Assert.Equal("a_03.txt", preview.CellForRow(2).Text);
    }

    [Fact]
    public async Task Snapshot_EqualsThePlannersPlan()
    {
        FakeRulePanelModel panel = ReplacePanel("a");
        var host = new RulePanelHostModel(new IRulePanelModel[] { panel });
        var preview = new PreviewModel(host, _files, _delay.Delay);
        panel.Enabled = true;

        await _files.LoadAsync(@"C:\A");
        await preview.Pending;

        PreviewSnapshot snapshot = preview.Snapshot;
        RenamePlan expected = RenamePlanner.Plan(host.Rules, snapshot.Items, _files.ExistingNamesByFolder, CancellationToken.None);
        Assert.Equal(3, snapshot.Results.Count);
        Assert.Equal(expected.Results.Select(r => r.NewName).ToArray(), snapshot.Results.Select(r => r.NewName).ToArray());
        Assert.Equal(expected.Results.Select(r => r.Status).ToArray(), snapshot.Results.Select(r => r.Status).ToArray());
        for (int i = 0; i < expected.Results.Count; i++)
        {
            Assert.Equal(
                expected.Conflicts.ForItem(i).Select(c => c.Code).ToArray(),
                snapshot.Conflicts.ForItem(i).Select(c => c.Code).ToArray());
        }

        Assert.Equal(expected.Blocker, snapshot.Blocker);
    }

    // NS-33: the filtered list

    private static FakeRulePanelModel ExtensionPanel(string extension)
    {
        FakeRulePanelModel panel = PanelOf(_ => new ExtensionRule(ExtensionMode.Fixed, extension), stage: RuleStage.Extension);
        panel.Enabled = true;
        return panel;
    }

    [Fact]
    public async Task HiddenByFilter_StillOccupiesItsName()
    {
        _source.SetEntries(@"C:\H", File("a.png", folder: @"C:\H"), File("a.jpg", folder: @"C:\H"));
        PreviewModel preview = Build(ExtensionPanel("png"));
        await _files.LoadAsync(@"C:\H");
        await Within(preview.Pending);

        Assert.True(await Within(_files.SetFilterAsync(new ItemFilter(mask: "*.jpg"))));
        await Within(preview.Pending);

        Assert.Equal(new[] { "a.jpg" }, _files.Items.Select(i => i.Name));
        Assert.Equal(new PreviewCell("a.png", PreviewCellKind.Changed), preview.CellForRow(0), CellComparer.Instance);
        Assert.Equal(Message(ConflictCode.TargetExists), preview.StatusForRow(0).Text);
    }

    [Fact]
    public async Task HiddenFile_StillOccupiesItsName()
    {
        _source.SetEntries(@"C:\H", Hidden("a.png", @"C:\H"), File("a.jpg", folder: @"C:\H"));
        PreviewModel preview = Build(ExtensionPanel("png"));

        await _files.LoadAsync(@"C:\H");
        await Within(preview.Pending);

        Assert.Equal(new[] { "a.jpg" }, _files.Items.Select(i => i.Name));
        Assert.True(_files.IsFilterDefault);
        Assert.Equal(Message(ConflictCode.TargetExists), preview.StatusForRow(0).Text);
    }

    [Fact]
    public async Task FilterApplied_RecomputesOnce_WithoutDebounce()
    {
        var counter = new ApplyCounter();
        PreviewModel preview = await BuildLoaded(PanelOf(t => new CountingRule(RuleStage.Replace, t, counter), "x"));
        int delays = _delay.Calls.Count;
        int before = counter.Count;

        Assert.True(await Within(_files.SetFilterAsync(new ItemFilter(mask: "a*"))));
        await Within(preview.Pending);

        Assert.Equal(before + 2, counter.Count);
        Assert.Equal(delays, _delay.Calls.Count);
        Assert.True(preview.IsCurrent);
        Assert.Equal("a1.txt", preview.CellForRow(0).Text);
        Assert.Equal("a2.txt", preview.CellForRow(1).Text);
        Assert.Equal(PreviewCellKind.None, preview.CellForRow(2).Kind);
    }

    [Fact]
    public async Task FilterPending_PreviewUnchanged()
    {
        var seam = new FilterSeam();
        var files = new FileListModel(_source, seam.Includes);
        FakeRulePanelModel panel = ReplacePanel("a");
        panel.Enabled = true;
        var preview = new PreviewModel(new RulePanelHostModel(new IRulePanelModel[] { panel }), files, _delay.Delay);
        await files.LoadAsync(@"C:\A");
        await Within(preview.Pending);
        PreviewSnapshot snapshot = preview.Snapshot;
        PreviewCell first = preview.CellForRow(0);
        var slow = new ItemFilter(mask: "b*");
        using var gate = new TestGate();
        seam.Use(slow, item =>
        {
            gate.Block();
            return slow.Includes(item);
        });

        Task<bool> run = files.SetFilterAsync(slow);
        gate.WaitUntilReached();

        Assert.True(files.IsFilterPending);
        Assert.True(preview.IsCurrent);
        Assert.Same(snapshot, preview.Snapshot);
        Assert.Equal(new PreviewCell("_1.txt", PreviewCellKind.Changed), first, CellComparer.Instance);
        Assert.Equal(first, preview.CellForRow(0), CellComparer.Instance);
        gate.Release();
        Assert.True(await Within(run));
        await Within(preview.Pending);
        Assert.Equal(new PreviewCell("b.txt", PreviewCellKind.Unchanged), preview.CellForRow(0), CellComparer.Instance);
    }

    [Fact]
    public async Task FilterApplied_OldSnapshotStopsAnsweringAtOnce()
    {
        using var gate = new TestGate();
        var rule = new HoldRule(gate);
        PreviewModel preview = await BuildLoaded(PanelOf(_ => rule, "x"));
        Assert.True(preview.IsCurrent);
        rule.Armed = true;

        Assert.True(await Within(_files.SetFilterAsync(new ItemFilter(mask: "a*"))));

        Assert.False(preview.IsCurrent);
        Assert.Equal(PreviewCellKind.None, preview.CellForRow(0).Kind);
        gate.Release();
        await Within(preview.Pending);
        Assert.True(preview.IsCurrent);
        Assert.Equal("a1.txt", preview.CellForRow(0).Text);
    }

    /// <summary>Changes nothing; once armed, holds every Apply at the gate (which has a timeout).</summary>
    private sealed class HoldRule : IRenameRule
    {
        private readonly TestGate _gate;
        private volatile bool _armed;

        public HoldRule(TestGate gate)
        {
            _gate = gate;
        }

        public bool Armed
        {
            get => _armed;
            set => _armed = value;
        }

        public RuleStage Stage => RuleStage.Replace;

        public bool Enabled => true;

        public NameParts Apply(NameParts name, ItemContext context)
        {
            if (_armed)
            {
                _gate.Block();
            }

            return name;
        }
    }
}
