using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using NameShift.Core;
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

    /// <summary>Set after a Rename that renamed something (or left items stranded, or failed unexpectedly) in the
    /// folder still shown; cleared by a rule edit, a selection change, a sort, a folder load outside an action, or a
    /// Revert that ran. While set, Rename is off.</summary>
    private bool _renameLocked;

    public MainActions(PreviewModel preview, FileListModel files, RulePanelHostModel rules, IBatchRenamer renamer)
    {
        _preview = preview ?? throw new ArgumentNullException(nameof(preview));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _renamer = renamer ?? throw new ArgumentNullException(nameof(renamer));
        _preview.Changed += (_, _) => RaiseChanged();
        _preview.Outdated += (_, _) => RaiseChanged();
        _files.Changed += (_, _) =>
        {
            // A folder load. The action's own reload raises this before RunAsync sets the lock, so it cannot clear it.
            ClearLock();
            RaiseChanged();
        };
        _files.SelectionChanged += (_, _) =>
        {
            // Not while busy: a FolderContentsChanged handler may change the selection after the lock was set.
            if (!IsBusy)
            {
                ClearLock();
            }

            RaiseChanged();
        };
        _files.OrderChanged += (_, _) =>
        {
            // Not while busy, for the same reason as the selection: the new order changes the numbering preview.
            if (!IsBusy)
            {
                ClearLock();
            }

            RaiseChanged();
        };
        _rules.RulesChanged += (_, _) =>
        {
            ClearLock();
            RaiseChanged();
        };
    }

    /// <summary>True from a Rename or Revert click until the folder has reloaded: everything is locked.</summary>
    public bool IsBusy { get; private set; }

    /// <summary>True when Rename may run the preview on screen. The window's conditions are written here: the list is
    /// loaded, the preview is current and did not fail, no Rename just ran (until a rule, the selection or the folder
    /// changes), and the plan has no blocker. "A change, no conflict and no rule error" is RenamePlan.Blocker's.</summary>
    public bool CanRename
    {
        get
        {
            if (IsBusy || _renameLocked || _files.State != FileListState.Loaded || !_preview.IsCurrent || _preview.HasFailed)
            {
                return false;
            }

            return _preview.Snapshot.Blocker == RenameBlocker.None;
        }
    }

    /// <summary>True when the last journal read says the last batch can be reverted.</summary>
    public bool CanRevert => !IsBusy && _lastBatch != null && _lastBatch.CanRevert;

    /// <summary>True when at least one rule panel has something to reset.</summary>
    public bool CanReset => !IsBusy && _rules.CanResetAny;

    /// <summary>Why Rename is disabled: conflicts or rule errors (these win), else "change a rule or the selection" after a
    /// Rename; "" while busy, with no loaded list, or with nothing to say.</summary>
    public string RenameHint
    {
        get
        {
            if (IsBusy || _files.State != FileListState.Loaded)
            {
                return "";
            }

            string hint = ActionText.Hint(_preview.Snapshot);
            return hint.Length == 0 && _renameLocked ? Strings.RenameHintLocked : hint;
        }
    }

    /// <summary>What Revert would undo; "" unless CanRevert.</summary>
    public string RevertDescription => CanRevert ? ActionText.RevertDescription(_lastBatch!, CultureInfo.CurrentCulture) : "";

    /// <summary>"Renaming 12 files..." or "Reverting..." while busy; "" when idle.</summary>
    public string BusyText { get; private set; } = "";

    /// <summary>Raised on the calling thread when any of the above may have changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised once on the calling thread after a Rename or Revert ran (whatever its outcome) and the file list
    /// reloaded, before IsBusy goes back to false. Not raised when the action did not run. Handlers must not throw.</summary>
    public event EventHandler<FolderContentsChangedEventArgs>? FolderContentsChanged;

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

        // Same snapshot, so same batch, so the folder it was made from.
        string folder = _files.FolderPath!;
        return RunAsync(
            ActionText.Busy(count, CultureInfo.CurrentCulture),
            folder,
            RenameFolders(folder, snapshot.Items),
            () => _renamer.Rename(snapshot.Results, snapshot.Conflicts),
            ActionText.ForRename,
            r => r.Execution != null && (r.Execution.Renamed.Count > 0 || r.Execution.NeedsAttention.Count > 0),
            true);
    }

    /// <summary>Reverts the journal's last batch, then reloads the shown folder. Null when not allowed. Never throws.</summary>
    public Task<ActionReport?> RevertAsync()
    {
        if (!CanRevert)
        {
            return Task.FromResult<ActionReport?>(null);
        }

        // Read before the Revert runs: afterwards the journal reads Reverted and its folder list is empty.
        IReadOnlyList<string> changed = RevertFolders(_lastBatch!, _files.FolderPath);
        return RunAsync(ActionText.Reverting, _files.FolderPath, changed, () => _renamer.Revert(), ActionText.ForRevert, _ => false, false);
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
    /// (when there is one), sets the Rename lock (see _renameLocked), then unlocks. The lock is taken before the first await, and the finally always releases it.
    /// A startup journal read may still be running (CanRename does not wait for it): JournaledRenamer is "one operation
    /// at a time", but its read only reads, the read's result is dropped by the version check, and the worst overlap is
    /// a journal file briefly opened by both, which Rename reports as a journal problem in its result, never a wrong rename.</summary>
    private async Task<ActionReport?> RunAsync<T>(string busyText, string? folder, IReadOnlyList<string> changedFolders, Func<T> action, Func<T, CultureInfo, ActionReport> describe, Func<T, bool> locksAfter, bool locksIfUnexpected)
    {
        IsBusy = true;
        BusyText = busyText;
        _journalVersion++;
        try
        {
            RaiseChanged();
            ActionReport report;
            bool wantLock;
            try
            {
                (T result, LastBatch last) = await Task.Run(() =>
                {
                    T done = action();
                    return (done, _renamer.LoadLastBatch());
                });
                _lastBatch = last;
                report = describe(result, CultureInfo.CurrentCulture);
                wantLock = locksAfter(result);
            }
            catch (Exception)
            {
                // The one deliberate catch-all: the renamer reports failures as results, so this is a bug and the disk
                // state is unknown. Say so, read the journal again (it then reads Interrupted, so Revert is off) and
                // reload, instead of taking the window down.
                report = new ActionReport(Strings.ActionUnexpected);
                wantLock = locksIfUnexpected;
                _lastBatch = await ReadLastBatchAsync();
            }

            if (folder != null)
            {
                await _files.LoadAsync(folder);
            }

            // Locked only if the folder shown is still the action's (a newer load may have superseded the reload).
            _renameLocked = wantLock && folder != null && _files.FolderPath != null && PathInput.SameFolder(_files.FolderPath, folder);

            FolderContentsChanged?.Invoke(this, new FolderContentsChangedEventArgs(changedFolders));

            return report;
        }
        finally
        {
            IsBusy = false;
            BusyText = "";
            RaiseChanged();
        }
    }

    /// <summary>The shown folder, then every other folder of the batch's items (sub-folders listed) in order of first
    /// appearance; no duplicates (case-insensitive). One folder gives just the shown one.</summary>
    private static IReadOnlyList<string> RenameFolders(string shown, IReadOnlyList<RenameItem> items) =>
        new[] { shown }.Concat(items.Select(item => item.FolderPath)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>The batch's folders (a revertable batch always has them), then the shown folder; no duplicates (case-insensitive).</summary>
    private static IReadOnlyList<string> RevertFolders(LastBatch batch, string? shown)
    {
        IEnumerable<string> folders = shown is null ? batch.Folders : batch.Folders.Concat(new[] { shown });
        return folders.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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

    private void ClearLock() => _renameLocked = false;

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
