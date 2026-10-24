using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NameShift.Core;
using NameShift.Ui.Folders;

namespace NameShift.Ui.Files;

/// <summary>
/// The file list's data for the current folder: every entry (<see cref="AllItems"/>) and the entries the filter keeps
/// (<see cref="Items"/>). Loads and filters off the calling thread; a newer load or filter supersedes an older one,
/// and <see cref="Dispose"/> supersedes both. All state is read and written on the calling (UI) thread; only the
/// listing, the sort and the filter loop run on the pool.
/// </summary>
public sealed class FileListModel : IDisposable
{
    private static readonly Task<bool> NotApplied = Task.FromResult(false);

    private readonly IFileListSource _source;
    private readonly Func<ItemFilter, RenameItem, bool> _includes;

    // Shared by loads, filter runs and Dispose: a result applies only while its version is the latest.
    private int _version;

    // The current filter work (a filter run, or the filter step of a load). Old sources are cancelled, never disposed:
    // a pool task may still hold the token.
    private CancellationTokenSource? _filterCts;
    private ItemFilter _requestedFilter;
    private bool _disposed;

    /// <summary>A file list over the source, with the default filter.</summary>
    public FileListModel(IFileListSource source)
        : this(source, (filter, item) => filter.Includes(item))
    {
    }

    /// <summary>For tests: <paramref name="includes"/> replaces <see cref="ItemFilter.Includes"/> (a slow, blocking or
    /// throwing filter).</summary>
    internal FileListModel(IFileListSource source, Func<ItemFilter, RenameItem, bool> includes)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _includes = includes ?? throw new ArgumentNullException(nameof(includes));
        _requestedFilter = Filter;
    }

    public string? FolderPath { get; private set; }

    public FileListState State { get; private set; }

    /// <summary>Every entry of the listing, hidden and system ones included, in list order; empty before a load, while
    /// loading and when the listing failed. Never goes to the pipeline: <see cref="Items"/> does.</summary>
    public IReadOnlyList<RenameItem> AllItems { get; private set; } = Array.Empty<RenameItem>();

    /// <summary>The rows in display order: <see cref="AllItems"/> kept by <see cref="Filter"/>, the same instances in
    /// the same order. Never partially filtered: while a filter run is pending it is still the previous complete
    /// result. What the pipeline runs on is <see cref="Batch"/>: row i is pipeline index i only when the batch is the
    /// whole list.</summary>
    public IReadOnlyList<RenameItem> Items { get; private set; } = Array.Empty<RenameItem>();

    /// <summary>The filter applied to <see cref="Items"/>; the default filter (no hidden or system item) until another
    /// one is applied.</summary>
    public ItemFilter Filter { get; private set; } = new ItemFilter();

    /// <summary>True from a <see cref="SetFilterAsync"/> that started a run until that run is applied, fails, or is
    /// superseded by a load. <see cref="Items"/> still shows the previous result meanwhile.</summary>
    public bool IsFilterPending { get; private set; }

    /// <summary>True when <see cref="Filter"/> has every setting at its default.</summary>
    public bool IsFilterDefault => IsDefaultFilter(Filter);

    /// <summary>Every entry name, per folder path (ignoring case), hidden and system included, never filtered: for the
    /// duplicate check (NS-6:3), the map the planner takes. Not rebuilt by a filter or a sort. Empty before a load,
    /// while loading and when the listing failed. A snapshot of the folder at load time: a file created later is not
    /// seen until the folder reloads; NS-8 re-checks every target on disk before renaming and never replaces, so this
    /// is safe.</summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> ExistingNamesByFolder { get; private set; } = FileListing.NoNames;

    /// <summary>The rows that go into the pipeline: the selected rows in list order, or the whole list with no selection.
    /// Rebuilt on every selection change, sort, applied filter and load (a load resets the selection to none).</summary>
    public FileBatch Batch { get; private set; } = FileBatch.Empty;

    /// <summary>The order of <see cref="Items"/>. Default (Name, ascending) until the user sorts; kept when the same
    /// folder reloads, reset when another folder loads.</summary>
    public FileListSort Sort { get; private set; } = FileListSort.Default;

    /// <summary>False until the user sorts the folder shown: the window draws the sort arrow only when true.</summary>
    public bool IsSortChosen { get; private set; }

    /// <summary>The selected rows, distinct and ascending: empty for none, every row for all (a whole-list batch alone
    /// cannot tell the two apart). Empty after every load.</summary>
    public IReadOnlyList<int> SelectedRows { get; private set; } = Array.Empty<int>();

    /// <summary>Set only when the state is Failed.</summary>
    public FolderProblem? Problem { get; private set; }

    /// <summary>The latest filter run, for tests (never faults).</summary>
    internal Task PendingFilter { get; private set; } = Task.CompletedTask;

    /// <summary>Loading, the problem text, "No item matches the filters." (entries listed, none kept by a filter that is
    /// not at default), "This folder is empty", or null when rows are shown.</summary>
    public string? Message
    {
        get
        {
            switch (State)
            {
                case FileListState.Loading:
                    return Strings.Loading;
                case FileListState.Failed:
                    return FolderProblemText.For(Problem!.Value);
                case FileListState.Loaded when Items.Count == 0:
                    return AllItems.Count > 0 && !IsFilterDefault ? Strings.NoItemMatchesFilter : Strings.FolderEmpty;
                default:
                    return null;
            }
        }
    }

    /// <summary>Raised on the calling thread when the state or the items change (load started, load applied). A load
    /// also resets the batch, and raises this event only, not <see cref="SelectionChanged"/>.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised on the calling thread when <see cref="Batch"/> changes through <see cref="SetSelection"/>.</summary>
    public event EventHandler? SelectionChanged;

    /// <summary>Raised on the calling thread once per <see cref="SortBy"/> that ran and once per applied filter result,
    /// after <see cref="Items"/>, <see cref="Batch"/>, <see cref="SelectedRows"/> and <see cref="Sort"/> or
    /// <see cref="Filter"/> are updated; after a filter <see cref="Items"/> may change in count. Not
    /// <see cref="Changed"/> and not <see cref="SelectionChanged"/>: the selected items still listed stay
    /// selected.</summary>
    public event EventHandler? OrderChanged;

    /// <summary>The row of the listed item named <paramref name="name"/> (ordinal, ignoring case), or -1: also before
    /// the folder is loaded, when it failed, and for an entry the filter leaves out (not in <see cref="Items"/>).</summary>
    public int FindRow(string? name)
    {
        if (State != FileListState.Loaded || string.IsNullOrEmpty(name))
        {
            return -1;
        }

        IReadOnlyList<RenameItem> listItems = Items;
        for (int row = 0; row < listItems.Count; row++)
        {
            if (string.Equals(listItems[row].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return row;
            }
        }

        return -1;
    }

    /// <summary>Selects the rows (any order, duplicates allowed). No rows, or every row, is the whole list.
    /// Raises <see cref="SelectionChanged"/> only when the batch changes.</summary>
    /// <exception cref="ArgumentNullException">When rows is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">When a row is outside the listed items.</exception>
    public void SetSelection(IReadOnlyCollection<int> rows)
    {
        if (rows == null)
        {
            throw new ArgumentNullException(nameof(rows));
        }

        IReadOnlyList<RenameItem> listItems = Items;
        var selected = new bool[listItems.Count];
        int count = 0;
        foreach (int row in rows)
        {
            if (row < 0 || row >= selected.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(rows), row, "A selected row is not in the list.");
            }

            if (!selected[row])
            {
                selected[row] = true;
                count++;
            }
        }

        var selectedRows = new int[count];
        int at = 0;
        for (int row = 0; row < selected.Length; row++)
        {
            if (selected[row])
            {
                selectedRows[at++] = row;
            }
        }

        SelectedRows = selectedRows;
        FileBatch batch = count == 0 || count == selected.Length ? FileBatch.WholeList(listItems) : BatchOf(listItems, selected, count);
        if (SameRows(Batch, batch))
        {
            return;
        }

        Batch = batch;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Sorts by the column: a new column ascending, the current column in the other direction. Sorts every
    /// entry (<see cref="AllItems"/>), so a later filter change shows the sorted order; <see cref="Items"/> keeps the
    /// entries of the last applied filter without running it again. The same items stay selected. Does nothing unless
    /// a folder with entries is loaded. Runs on the calling thread (in memory only); a pending filter run is not
    /// cancelled and lands in the new order.</summary>
    /// <exception cref="ArgumentOutOfRangeException">When the column is not a defined value.</exception>
    public void SortBy(FileListColumn column)
    {
        if (!Enum.IsDefined(typeof(FileListColumn), column))
        {
            throw new ArgumentOutOfRangeException(nameof(column), column, "The column is not sortable.");
        }

        if (State != FileListState.Loaded || AllItems.Count == 0)
        {
            return;
        }

        FileListSort next = column == Sort.Column ? Sort.Toggled() : new FileListSort(column, false);
        IReadOnlyList<RenameItem> oldItems = Items;
        IReadOnlyList<int> oldRows = SelectedRows;
        IReadOnlyList<RenameItem> sortedAll = FileListOrder.Sort(AllItems, next);
        IReadOnlyList<RenameItem> sorted = KeepListed(sortedAll, oldItems);
        (FileBatch batch, IReadOnlyList<int> newRows) = MapSelection(oldItems, oldRows, sorted);

        AllItems = sortedAll;
        Items = sorted;
        Batch = batch;
        SelectedRows = newRows;
        Sort = next;
        IsSortChosen = true;
        OrderChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Asks for a new filter. The run goes on the pool and its result is applied on the calling thread as one
    /// <see cref="OrderChanged"/>: <see cref="Items"/> by the new filter, the source not called again, the selected
    /// items still listed kept selected. While a folder loads, the load applies the filter when it lands; before any
    /// load or after a failed one, the filter is only kept. A newer filter, a load or <see cref="Dispose"/> supersedes
    /// a pending run.</summary>
    /// <returns>True when this call's result was applied; false when nothing ran (the filter was already asked for, no
    /// folder is loaded, the model is disposed) or the run was superseded, cancelled or failed.</returns>
    /// <exception cref="ArgumentNullException">When filter is null.</exception>
    public Task<bool> SetFilterAsync(ItemFilter filter)
    {
        if (filter == null)
        {
            throw new ArgumentNullException(nameof(filter));
        }

        if (_disposed || ReferenceEquals(filter, _requestedFilter))
        {
            return NotApplied;
        }

        _requestedFilter = filter;
        switch (State)
        {
            case FileListState.Loading:
                // The load in flight applies the requested filter when it lands.
                return NotApplied;
            case FileListState.Loaded:
                Task<bool> run = RunFilterAsync(filter);
                PendingFilter = run;
                return run;
            default:
                // No items: nothing to filter and nothing to raise.
                Filter = filter;
                return NotApplied;
        }
    }

    /// <summary>The selection of <paramref name="oldItems"/> carried by item to <paramref name="newItems"/>: the
    /// selected items still listed stay selected, items new to the list are not. No selected item left, or every row
    /// selected, is the whole list, as <see cref="SetSelection"/> decides.</summary>
    private static (FileBatch Batch, IReadOnlyList<int> Rows) MapSelection(
        IReadOnlyList<RenameItem> oldItems, IReadOnlyList<int> oldRows, IReadOnlyList<RenameItem> newItems)
    {
        if (oldRows.Count == 0)
        {
            return (FileBatch.WholeList(newItems), Array.Empty<int>());
        }

        var selectedItems = new HashSet<RenameItem>(ReferenceComparer.Instance);
        foreach (int row in oldRows)
        {
            selectedItems.Add(oldItems[row]);
        }

        var selected = new bool[newItems.Count];
        int count = 0;
        for (int row = 0; row < newItems.Count; row++)
        {
            if (selectedItems.Contains(newItems[row]))
            {
                selected[row] = true;
                count++;
            }
        }

        if (count == 0)
        {
            return (FileBatch.WholeList(newItems), Array.Empty<int>());
        }

        if (count == newItems.Count)
        {
            return (FileBatch.WholeList(newItems), Enumerable.Range(0, count).ToArray());
        }

        FileBatch batch = BatchOf(newItems, selected, count);
        return (batch, batch.Rows);
    }

    private static FileBatch BatchOf(IReadOnlyList<RenameItem> listItems, bool[] selected, int count)
    {
        var indexOfRow = new int[selected.Length];
        var rows = new int[count];
        int next = 0;
        for (int row = 0; row < selected.Length; row++)
        {
            if (selected[row])
            {
                indexOfRow[row] = next;
                rows[next++] = row;
            }
            else
            {
                indexOfRow[row] = -1;
            }
        }

        return FileBatch.Of(listItems, indexOfRow, rows);
    }

    private static bool SameRows(FileBatch current, FileBatch next)
    {
        if (current.IsWholeList || next.IsWholeList)
        {
            return current.IsWholeList && next.IsWholeList;
        }

        return current.Rows.SequenceEqual(next.Rows);
    }

    /// <summary>Lists the folder and applies the filter asked for last; false when a newer load or
    /// <see cref="Dispose"/> superseded this one. Never throws.</summary>
    public async Task<bool> LoadAsync(string folderPath)
    {
        if (_disposed)
        {
            return false;
        }

        _filterCts?.Cancel();
        var cts = new CancellationTokenSource();
        _filterCts = cts;
        int version = ++_version;
        IsFilterPending = false;
        bool sameFolder = FolderPath != null && PathInput.SameFolder(FolderPath, folderPath);
        if (!sameFolder)
        {
            Sort = FileListSort.Default;
            IsSortChosen = false;
        }

        FileListSort sort = Sort;
        ItemFilter filter = _requestedFilter;
        FolderPath = folderPath;
        State = FileListState.Loading;
        AllItems = Array.Empty<RenameItem>();
        Items = Array.Empty<RenameItem>();
        Batch = FileBatch.Empty;
        SelectedRows = Array.Empty<int>();
        ExistingNamesByFolder = FileListing.NoNames;
        Problem = null;
        Changed?.Invoke(this, EventArgs.Empty);

        LoadResult result;
        try
        {
            result = await Task.Run(
                () =>
                {
                    FileListing found = _source.ListEntries(folderPath);
                    if (found.Problem != null)
                    {
                        return new LoadResult(found, Array.Empty<RenameItem>(), Array.Empty<RenameItem>());
                    }

                    IReadOnlyList<RenameItem> sorted = FileListOrder.Sort(found.Items, sort);
                    IReadOnlyList<RenameItem> filtered = FileListFilter.Apply(sorted, item => _includes(filter, item), cts.Token);
                    return new LoadResult(found, sorted, filtered);
                },
                cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // A newer load or Dispose took over.
            return false;
        }
        catch (Exception)
        {
            // A source must not throw, but a failed listing must never take the window down.
            result = new LoadResult(FileListing.Failed(FolderProblem.Unavailable), Array.Empty<RenameItem>(), Array.Empty<RenameItem>());
        }

        // The version check and the state change stay together: nothing awaits between them.
        if (version != _version || _disposed)
        {
            return false;
        }

        bool loaded = result.Listing.Problem is null;
        AllItems = result.All;
        Items = result.Filtered;
        Filter = loaded ? filter : _requestedFilter;
        Batch = FileBatch.WholeList(Items);
        SelectedRows = Array.Empty<int>();
        ExistingNamesByFolder = result.Listing.ExistingNamesByFolder;
        Problem = result.Listing.Problem;
        State = loaded ? FileListState.Loaded : FileListState.Failed;
        Changed?.Invoke(this, EventArgs.Empty);

        // A filter asked for while the folder was loading: its result arrives as one OrderChanged.
        // Not after Dispose (a Changed handler may have disposed the model).
        if (State == FileListState.Loaded && !_disposed && !ReferenceEquals(_requestedFilter, filter))
        {
            PendingFilter = RunFilterAsync(_requestedFilter);
        }

        return true;
    }

    /// <summary>Supersedes every pending load and filter run: none applies and no event is raised after this, and later
    /// loads and filters do nothing. The current state is kept (the window is closing). Safe to call more than
    /// once.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _filterCts?.Cancel();
        ++_version;
    }

    /// <summary>Filters <see cref="AllItems"/> on the pool and applies the result; true when it was applied. Never
    /// throws.</summary>
    private async Task<bool> RunFilterAsync(ItemFilter filter)
    {
        _filterCts?.Cancel();
        var cts = new CancellationTokenSource();
        _filterCts = cts;
        int version = ++_version;
        IsFilterPending = true;
        IReadOnlyList<RenameItem> all = AllItems;

        IReadOnlyList<RenameItem> filtered;
        try
        {
            filtered = await Task.Run(() => FileListFilter.Apply(all, item => _includes(filter, item), cts.Token), cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            // A newer filter, a load or Dispose already took over: touch nothing.
            return false;
        }
        catch (Exception)
        {
            // The one deliberate catch-all: a filter bug must never take the window down. The list stays as it was,
            // and the same filter can be asked for again.
            if (version == _version && !_disposed)
            {
                IsFilterPending = false;
                _requestedFilter = Filter;
            }

            return false;
        }

        // The version check and the state change stay together: nothing awaits between them.
        if (version != _version || _disposed)
        {
            return false;
        }

        // A sort ran meanwhile (a load would have changed the version): the result follows the current order.
        IReadOnlyList<RenameItem> newItems = ReferenceEquals(all, AllItems) ? filtered : KeepListed(AllItems, filtered);

        // The selection by item, from the current selection (it may have changed meanwhile).
        (FileBatch batch, IReadOnlyList<int> newRows) = MapSelection(Items, SelectedRows, newItems);

        Filter = filter;
        Items = newItems;
        Batch = batch;
        SelectedRows = newRows;
        IsFilterPending = false;
        OrderChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>The entries of <paramref name="ordered"/> that are in <paramref name="listed"/>, in the order of
    /// <paramref name="ordered"/>, in a new list.</summary>
    private static List<RenameItem> KeepListed(IReadOnlyList<RenameItem> ordered, IReadOnlyCollection<RenameItem> listed)
    {
        var keep = new HashSet<RenameItem>(listed, ReferenceComparer.Instance);
        var kept = new List<RenameItem>(listed.Count);
        foreach (RenameItem item in ordered)
        {
            if (keep.Contains(item))
            {
                kept.Add(item);
            }
        }

        return kept;
    }

    private static bool IsDefaultFilter(ItemFilter f) =>
        f.Mask.Length == 0
        && f.Pattern.Length == 0
        && !f.IgnoreCase
        && f.IncludeFiles
        && f.IncludeFolders
        && !f.IncludeHidden
        && f.MinLength == null
        && f.MaxLength == null;

    /// <summary>A load's work from the pool: the listing, every entry in list order, and the entries the filter
    /// keeps.</summary>
    private sealed class LoadResult
    {
        public LoadResult(FileListing listing, IReadOnlyList<RenameItem> all, IReadOnlyList<RenameItem> filtered)
        {
            Listing = listing;
            All = all;
            Filtered = filtered;
        }

        public FileListing Listing { get; }

        public IReadOnlyList<RenameItem> All { get; }

        public IReadOnlyList<RenameItem> Filtered { get; }
    }

    /// <summary>Items are equal only when they are the same instance (the list holds each item once).</summary>
    private sealed class ReferenceComparer : IEqualityComparer<RenameItem>
    {
        public static readonly ReferenceComparer Instance = new ReferenceComparer();

        public bool Equals(RenameItem? x, RenameItem? y) => ReferenceEquals(x, y);

        public int GetHashCode(RenameItem obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
