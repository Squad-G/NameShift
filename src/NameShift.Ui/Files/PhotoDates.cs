using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NameShift.Core;

namespace NameShift.Ui.Files;

/// <summary>
/// Gives each listed JPEG its photo date (<see cref="RenameItem.Taken"/>), read from the file's first bytes and
/// remembered in memory until the file changes.
/// <list type="bullet">
/// <item>Which files: a file whose name ends in <c>.jpg</c> or <c>.jpeg</c> (ignoring case). Every other item, every
/// folder included, is returned as is and never opened.</item>
/// <item>The cache: one entry per full path (ignoring case), valid while the item's size and modified time (ticks)
/// are the ones it was read with; a changed file is read again and its entry replaced. A date, or no date from the
/// file's own bytes, is remembered; a failure to read (locked, access denied, a cloud placeholder not opened) is not,
/// so the next call tries again. When it holds the capacity (50,000 by default), it is emptied before the next entry
/// is added. It is never written to disk and lives as long as this instance.</item>
/// <item>Threads: several calls may run at once. The cache is locked only to look up or store an entry, never while
/// a file is read, so a slow file does not hold up another call; two calls may read the same uncached file.</item>
/// </list>
/// </summary>
public sealed class PhotoDates
{
    /// <summary>The number of entries kept before the cache is emptied.</summary>
    public const int DefaultCapacity = 50000;

    private readonly IPhotoFile _file;
    private readonly int _capacity;
    private readonly object _gate = new object();
    private readonly Dictionary<string, Entry> _cache = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Photo dates read from the real disk, with the default capacity.</summary>
    public PhotoDates()
        : this(new DiskPhotoFile(), DefaultCapacity)
    {
    }

    /// <summary>For tests: photo dates read through <paramref name="file"/>, emptied at <paramref name="capacity"/>
    /// entries.</summary>
    internal PhotoDates(IPhotoFile file, int capacity)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "The capacity must be at least 1.");
        }

        _file = file ?? throw new ArgumentNullException(nameof(file));
        _capacity = capacity;
    }

    /// <summary>The number of cached entries (for tests).</summary>
    internal int CachedCount
    {
        get
        {
            lock (_gate)
            {
                return _cache.Count;
            }
        }
    }

    /// <summary>A new list, same length and order: each JPEG file as <c>item.WithTaken(date)</c> (null when it has
    /// no date or could not be read), every other item the same instance. The input list and its items are not
    /// changed. May block on the disk: call it off the UI thread. Never throws, except
    /// <see cref="ArgumentNullException"/> (a null list or item) and <see cref="OperationCanceledException"/> when
    /// <paramref name="cancellationToken"/> is cancelled, checked before each file; what was read stays cached. A read
    /// blocked on a dead share cannot be cancelled, only superseded. That holds for items that came from a folder
    /// listing: an item built by hand with invalid path characters can throw <see cref="ArgumentException"/>
    /// (net48).</summary>
    public IReadOnlyList<RenameItem> Fill(IReadOnlyList<RenameItem> items, CancellationToken cancellationToken)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        var result = new RenameItem[items.Count];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = items[i] ?? throw new ArgumentNullException(nameof(items), "The list holds a null item.");
        }

        for (int i = 0; i < result.Length; i++)
        {
            RenameItem item = result[i];
            if (!IsPhoto(item))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            string path = Path.Combine(item.FolderPath, item.Name);
            long modifiedTicks = item.Modified.Ticks;

            lock (_gate)
            {
                if (_cache.TryGetValue(path, out Entry entry) && entry.Size == item.Size && entry.ModifiedTicks == modifiedTicks)
                {
                    result[i] = item.WithTaken(entry.Date);
                    continue;
                }
            }

            // Outside the lock: a slow file must not hold up another call's cache hits.
            PhotoRead read = _file.Read(path);
            if (read.Outcome != PhotoReadOutcome.Failed)
            {
                lock (_gate)
                {
                    if (_cache.Count >= _capacity && !_cache.ContainsKey(path))
                    {
                        _cache.Clear();
                    }

                    _cache[path] = new Entry(item.Size, modifiedTicks, read.Date);
                }
            }

            result[i] = item.WithTaken(read.Date);
        }

        return result;
    }

    private static bool IsPhoto(RenameItem item) =>
        item.Kind == ItemKind.File
        && (item.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || item.Name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));

    /// <summary>One remembered answer: the file's stamp when it was read, and its date (null: no date).</summary>
    private readonly struct Entry(long size, long modifiedTicks, DateTime? date)
    {
        public long Size { get; } = size;

        public long ModifiedTicks { get; } = modifiedTicks;

        public DateTime? Date { get; } = date;
    }
}
