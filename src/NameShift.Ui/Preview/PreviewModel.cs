using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NameShift.Core;
using NameShift.Ui.Files;
using NameShift.Ui.Rules;

namespace NameShift.Ui.Preview;

/// <summary>
/// Computes the New Name column: runs RenamePipeline over the file list's batch (the selected rows, or every row with no
/// selection) off the UI thread, 150 ms after the last rule or selection change and at once when a folder finishes
/// loading. All state is touched on the calling (UI) thread; only the pipeline runs on the pool.
/// A newer request cancels and supersedes an older one.
/// Its lifetime is the window's: it has no Dispose and never unsubscribes (the window's controls guard with IsDisposed).
/// </summary>
public sealed class PreviewModel
{
    /// <summary>How long after the last rule change the preview is recomputed.</summary>
    public static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(150);

    private readonly RulePanelHostModel _rules;
    private readonly FileListModel _files;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private CancellationTokenSource? _current;
    private int _version;

    public PreviewModel(RulePanelHostModel rules, FileListModel files)
        : this(rules, files, (delay, token) => Task.Delay(delay, token))
    {
    }

    internal PreviewModel(RulePanelHostModel rules, FileListModel files, Func<TimeSpan, CancellationToken, Task> delay)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _delay = delay ?? throw new ArgumentNullException(nameof(delay));
        _rules.RulesChanged += (_, _) => OnRulesChanged();
        _files.Changed += (_, _) => OnFilesChanged();
        _files.SelectionChanged += (_, _) => OnSelectionChanged();
    }

    /// <summary>The last applied run; Empty when there is no preview (no folder, loading, failed, or after a failure).</summary>
    public PreviewSnapshot Snapshot { get; private set; } = PreviewSnapshot.Empty;

    /// <summary>True when the last run failed unexpectedly (not a rule error: those are Error results). Reset by the next applied run.</summary>
    public bool HasFailed { get; private set; }

    /// <summary>Raised on the calling thread when Snapshot (or HasFailed) changes.</summary>
    public event EventHandler? Changed;

    /// <summary>The latest request's task, for tests (never faults).</summary>
    internal Task Pending { get; private set; } = Task.CompletedTask;

    /// <summary>The New Name cell of a list row; None when the snapshot is not for the list's current items or the row is out of range.</summary>
    public PreviewCell CellForRow(int row)
    {
        return TryGetRow(row, out int index) ? PreviewText.For(Snapshot.Results[index]) : PreviewCell.None;
    }

    /// <summary>The Status cell of a list row: the conflicts' text; StatusCell.None when the row is not in the current snapshot or has no conflict.</summary>
    public StatusCell StatusForRow(int row)
    {
        if (!TryGetRow(row, out int index))
        {
            return StatusCell.None;
        }

        IReadOnlyList<Conflict> conflicts = Snapshot.Conflicts.ForItem(index);
        return conflicts.Count == 0 ? StatusCell.None : new StatusCell(ConflictText.For(conflicts), true);
    }

    /// <summary>"N conflicts" for the line under the list; "" when there are none.</summary>
    public string ConflictSummary => ConflictText.Summary(Snapshot.ConflictCount);

    /// <summary>Maps a list row to its position in the snapshot; false when the snapshot is not for the list's current items or the row is not in the snapshot's batch.</summary>
    private bool TryGetRow(int row, out int index)
    {
        index = -1;
        return ReferenceEquals(Snapshot.Batch.ListItems, _files.Items) && Snapshot.Batch.TryGetIndex(row, out index);
    }

    private void OnFilesChanged()
    {
        if (_files.State == FileListState.Loaded)
        {
            Pending = RecomputeAsync(debounce: false);
        }
        else
        {
            Clear();
        }
    }

    private void OnRulesChanged()
    {
        if (_files.State == FileListState.Loaded)
        {
            Pending = RecomputeAsync(debounce: true);
        }
    }

    private void OnSelectionChanged()
    {
        if (_files.State == FileListState.Loaded)
        {
            Pending = RecomputeAsync(debounce: true);
        }
    }

    private void Clear()
    {
        _current?.Cancel();
        _version++;
        Snapshot = PreviewSnapshot.Empty;
        HasFailed = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The pipeline and the three conflict checks, in one run on the pool.</summary>
    private static PreviewSnapshot Compute(
        IReadOnlyList<IRenameRule> rules,
        FileBatch batch,
        string folder,
        IReadOnlyList<string> existingNames,
        CancellationToken token)
    {
        IReadOnlyList<RenameResult> results = new RenamePipeline(rules).Run(batch.Items, token);
        token.ThrowIfCancellationRequested();
        var existing = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [folder] = existingNames as IReadOnlyCollection<string> ?? existingNames.ToArray(),
        };
        IConflictCheck[] checks = { new InvalidNameCheck(), new DuplicateTargetCheck(existing), new PathLengthCheck() };
        ConflictReport conflicts = ConflictChecker.Run(results, checks);
        return new PreviewSnapshot(batch, results, conflicts);
    }

    /// <summary>Runs the pipeline and the conflict checks; true when the result was applied. Never throws.</summary>
    private async Task<bool> RecomputeAsync(bool debounce)
    {
        // Old sources are not disposed: a running Task.Delay or Task.Run may still hold the token.
        _current?.Cancel();
        var cts = new CancellationTokenSource();
        _current = cts;
        int version = ++_version;
        PreviewSnapshot snapshot;
        try
        {
            if (debounce)
            {
                await _delay(DebounceDelay, cts.Token);
            }

            IReadOnlyList<IRenameRule> rules = _rules.Rules; // read after the delay: the latest settings
            FileBatch batch = _files.Batch;
            string folder = _files.FolderPath!;
            IReadOnlyList<string> existing = _files.ExistingNames;
            snapshot = await Task.Run(() => Compute(rules, batch, folder, existing, cts.Token), cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception)
        {
            // The one deliberate catch-all: a broken rule must never take the window down.
            if (version != _version)
            {
                return false;
            }

            Snapshot = PreviewSnapshot.Empty;
            HasFailed = true;
            Changed?.Invoke(this, EventArgs.Empty);
            return false;
        }

        // The version check and the state change stay together: nothing awaits between them.
        // A handler that throws here is not a run failure, so it stays outside the try.
        if (version != _version)
        {
            return false;
        }

        Snapshot = snapshot;
        HasFailed = false;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
