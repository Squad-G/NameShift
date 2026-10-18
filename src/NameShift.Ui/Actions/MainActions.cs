using System;
using System.Globalization;
using System.Threading.Tasks;
using NameShift.Disk;
using NameShift.Ui.Files;
using NameShift.Ui.Preview;
using NameShift.Ui.Rules;

namespace NameShift.Ui.Actions;

/// <summary>The window's Rename, Revert and Reset: when each is allowed, and running them. All state is touched on the
/// calling (UI) thread; only the renamer runs on the pool. Lifetime: the window's (no Dispose).</summary>
public sealed class MainActions
{
    /// <summary>Above this many changed names, Rename asks first (criterion 7).</summary>
    public const int ConfirmAbove = 500;

    private readonly PreviewModel _preview;
    private readonly FileListModel _files;
    private readonly RulePanelHostModel _rules;
    private readonly IBatchRenamer _renamer;

    /// <summary>The last journal read; null until the first read finished, or when it failed (Revert off).</summary>
    private LastBatch? _lastBatch;

    /// <summary>Bumped by every journal read and every action: a read applies only when nothing started after it.</summary>
    private int _journalVersion;

    public MainActions(PreviewModel preview, FileListModel files, RulePanelHostModel rules, IBatchRenamer renamer)
    {
        _preview = preview ?? throw new ArgumentNullException(nameof(preview));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _renamer = renamer ?? throw new ArgumentNullException(nameof(renamer));
        _preview.Changed += (_, _) => RaiseChanged();
        _preview.Outdated += (_, _) => RaiseChanged();
        _files.Changed += (_, _) => RaiseChanged();
        _files.SelectionChanged += (_, _) => RaiseChanged();
        _rules.RulesChanged += (_, _) => RaiseChanged();
    }

    /// <summary>True from a Rename or Revert click until the folder has reloaded: everything is locked.</summary>
    public bool IsBusy { get; private set; }

    /// <summary>True when Rename may run the preview on screen. The only place this rule is written (criterion 2): the
    /// list is loaded, the preview is current and did not fail, and it has at least one changed name, no conflict and
    /// no rule error.</summary>
    public bool CanRename
    {
        get
        {
            if (IsBusy || _files.State != FileListState.Loaded || !_preview.IsCurrent || _preview.HasFailed)
            {
                return false;
            }

            PreviewSnapshot snapshot = _preview.Snapshot;
            return snapshot.ChangedCount > 0 && !snapshot.HasConflicts && snapshot.ErrorCount == 0;
        }
    }

    /// <summary>True when the last journal read says the last batch can be reverted.</summary>
    public bool CanRevert => !IsBusy && _lastBatch != null && _lastBatch.CanRevert;

    /// <summary>True when at least one rule panel has something to reset.</summary>
    public bool CanReset => !IsBusy && _rules.CanResetAny;

    /// <summary>Why Rename is disabled (conflicts, rule errors); "" while busy, with no loaded list, or with nothing to fix.</summary>
    public string RenameHint => IsBusy || _files.State != FileListState.Loaded ? "" : ActionText.Hint(_preview.Snapshot);

    /// <summary>What Revert would undo; "" unless CanRevert.</summary>
    public string RevertDescription => CanRevert ? ActionText.RevertDescription(_lastBatch!, CultureInfo.CurrentCulture) : "";

    /// <summary>"Renaming 12 files..." or "Reverting..." while busy; "" when idle.</summary>
    public string BusyText { get; private set; } = "";

    /// <summary>Raised on the calling thread when any of the above may have changed.</summary>
    public event EventHandler? Changed;

    /// <summary>The latest journal read's task, for tests (never faults).</summary>
    internal Task PendingJournalRead { get; private set; } = Task.CompletedTask;

    /// <summary>Reads the journal off the calling thread. Never throws.</summary>
    public Task LoadJournalAsync()
    {
        PendingJournalRead = ReadJournalAsync();
        return PendingJournalRead;
    }

    /// <summary>Runs the current preview. Null when nothing ran and nothing needs saying (not allowed, or the user said
    /// No). confirm is called on the calling thread, only above ConfirmAbove changed names. Never throws (unless
    /// confirm does).</summary>
    public Task<ActionReport?> RenameAsync(Func<string, bool> confirm)
    {
        if (confirm == null)
        {
            throw new ArgumentNullException(nameof(confirm));
        }

        if (!CanRename)
        {
            return Task.FromResult<ActionReport?>(null);
        }

        PreviewSnapshot snapshot = _preview.Snapshot;
        string folder = _files.FolderPath!;
        int count = snapshot.ChangedCount;
        if (count > ConfirmAbove && !confirm(ActionText.Confirm(count, CultureInfo.CurrentCulture)))
        {
            return Task.FromResult<ActionReport?>(null);
        }

        // The confirmation pumps messages: a preview may have been requested or applied meanwhile.
        if (!CanRename || !ReferenceEquals(_preview.Snapshot, snapshot))
        {
            return Task.FromResult<ActionReport?>(new ActionReport(Strings.PreviewChanged));
        }

        return RunAsync(
            ActionText.Busy(count, CultureInfo.CurrentCulture),
            folder,
            () => _renamer.Rename(snapshot.Results, snapshot.Conflicts),
            ActionText.ForRename);
    }

    /// <summary>Reverts the journal's last batch, then reloads the shown folder. Null when not allowed. Never throws.</summary>
    public Task<ActionReport?> RevertAsync()
    {
        if (!CanRevert)
        {
            return Task.FromResult<ActionReport?>(null);
        }

        return RunAsync(ActionText.Reverting, _files.FolderPath, () => _renamer.Revert(), ActionText.ForRevert);
    }

    /// <summary>Resets every rule panel; no-op unless CanReset. Never touches files, the journal or the selection.</summary>
    public void Reset()
    {
        if (CanReset)
        {
            _rules.ResetAll();
        }
    }

    /// <summary>Locks, runs the action and the journal read in one pool task, applies the result, reloads the folder
    /// (when there is one), then unlocks. The lock is taken before the first await.</summary>
    private async Task<ActionReport?> RunAsync<T>(string busyText, string? folder, Func<T> action, Func<T, CultureInfo, ActionReport> describe)
    {
        IsBusy = true;
        BusyText = busyText;
        _journalVersion++;
        RaiseChanged();
        try
        {
            ActionReport report;
            try
            {
                (T result, LastBatch last) = await Task.Run(() =>
                {
                    T done = action();
                    return (done, _renamer.LoadLastBatch());
                });
                _lastBatch = last;
                report = describe(result, CultureInfo.CurrentCulture);
            }
            catch (Exception)
            {
                // The one deliberate catch-all: the renamer reports failures as results, so this is a bug and the disk
                // state is unknown. Say so, read the journal again (it then reads Interrupted, so Revert is off) and
                // reload, instead of taking the window down.
                report = new ActionReport(Strings.ActionUnexpected);
                _lastBatch = await ReadLastBatchAsync();
            }

            if (folder != null)
            {
                await _files.LoadAsync(folder);
            }

            return report;
        }
        finally
        {
            IsBusy = false;
            BusyText = "";
            RaiseChanged();
        }
    }

    private async Task ReadJournalAsync()
    {
        int version = ++_journalVersion;
        LastBatch? last = await ReadLastBatchAsync();

        // The version check and the state change stay together: nothing awaits between them.
        if (version != _journalVersion)
        {
            return;
        }

        _lastBatch = last;
        RaiseChanged();
    }

    /// <summary>The journal off the calling thread; null when the read failed (Revert off). Never throws.</summary>
    private async Task<LastBatch?> ReadLastBatchAsync()
    {
        try
        {
            return await Task.Run(() => _renamer.LoadLastBatch());
        }
        catch (Exception)
        {
            // LoadLastBatch never throws; if it ever does, Revert stays off rather than the window going down.
            return null;
        }
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
