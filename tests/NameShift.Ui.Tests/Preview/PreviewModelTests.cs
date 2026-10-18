using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NameShift.Core;
using NameShift.Ui.Files;
using NameShift.Ui.Preview;
using NameShift.Ui.Rules;
using NameShift.Ui.Tests.Rules;
using Xunit;
using static NameShift.Ui.Tests.FakeFileListSource;

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

    private static FakeRulePanelModel PanelOf(Func<string, IRenameRule?> build, string text = "")
    {
        var panel = new FakeRulePanelModel(RuleStage.Replace, build, "&Replace");
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
}
