using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Threading;
using NameShift.Core;
using NameShift.Ui.Folders;

namespace NameShift.Ui.Files;

/// <summary>
/// Lists every file and sub-folder of one folder from the real disk, hidden and system ones included, each with its
/// attributes (the filter decides what is shown); with sub-folders, every folder below it too.
/// <list type="bullet">
/// <item>Order: grouped by folder in tree order: the folder's own entries first, then each sub-folder's entries right
/// after its parent's, siblings in natural name order (<c>x</c>, <c>x\y</c>, <c>x2</c>). The walk keeps its own stack,
/// never recursion, and does linear work only.</item>
/// <item>Reparse points (junctions, symbolic links, possibly cloud placeholder folders): listed as items, never entered,
/// and not counted as skipped, so a loop ends and nothing is listed twice. The folder chosen is always read, even when
/// it is one.</item>
/// <item>A sub-folder that cannot be read (access denied, path too long, removed meanwhile, any I/O error), at the
/// call or midway: skipped and counted in <see cref="FileListing.SkippedFolders"/>; the entries read before the error,
/// and the sub-folders found among them, are kept. The chosen folder failing is a failed listing.</item>
/// <item>Cancellation: the token is checked before the walk, once per folder, once per 1,000 entries and at the
/// end; a cancelled walk throws <see cref="OperationCanceledException"/> and returns nothing. A read blocked on a dead
/// share cannot be cancelled, only superseded: its thread waits for the disk, and the caller never applies its
/// result.</item>
/// </list>
/// </summary>
public sealed class DiskFileListSource : IFileListSource
{
    private static readonly IComparer<string> SiblingOrder = new NaturalThenOrdinal();

    private readonly IDirectoryEnumerator _enumerator;

    /// <summary>A source over the real disk.</summary>
    public DiskFileListSource()
        : this(new DiskDirectoryEnumerator())
    {
    }

    /// <summary>For tests: a source over <paramref name="enumerator"/> (an in-memory tree).</summary>
    internal DiskFileListSource(IDirectoryEnumerator enumerator)
    {
        _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
    }

    /// <summary>Every entry of the folder and, with <paramref name="includeSubfolders"/>, of every sub-folder not
    /// behind a reparse point, grouped by folder in tree order (see the class). May block: call off the UI thread.
    /// Never throws, except <see cref="OperationCanceledException"/> when <paramref name="cancellationToken"/> is
    /// cancelled (at once when it already is).</summary>
    public FileListing ListEntries(string folderPath, bool includeSubfolders, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var walk = new Walk(_enumerator, includeSubfolders, cancellationToken);

            // The chosen folder: any error here is the usual folder problem (the catch-all below).
            walk.Read(folderPath);
            walk.PushChildren();

            while (walk.HasFolders)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path = walk.Pop();
                try
                {
                    walk.Read(path);
                }
                catch (Exception e) when (IsUnreadable(e))
                {
                    // Skipped: the entries and the sub-folders read before the error are kept.
                    walk.Skipped++;
                }

                walk.PushChildren();
            }

            // Cancelled while the last folder was read: still no listing.
            cancellationToken.ThrowIfCancellationRequested();
            return FileListing.Ok(walk.Items, walk.Skipped);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception e)
        {
            // The one deliberate catch-all: whatever the disk throws (a dead share, a card reader,
            // a security policy), a folder listing must never crash the window.
            return FileListing.Failed(DiskFolderSource.Classify(e));
        }
    }

    private static bool IsUnreadable(Exception e) =>
        e is UnauthorizedAccessException || e is IOException || e is SecurityException;

    /// <summary>The state of one listing: the items so far, the folders still to read, and the sub-folders of the
    /// folder being read.</summary>
    private sealed class Walk
    {
        private readonly IDirectoryEnumerator _enumerator;
        private readonly bool _includeSubfolders;
        private readonly CancellationToken _token;
        private readonly Stack<string> _stack = new Stack<string>();
        private readonly List<string> _children = new List<string>();
        private string _parent = "";
        private int _count;

        public Walk(IDirectoryEnumerator enumerator, bool includeSubfolders, CancellationToken token)
        {
            _enumerator = enumerator;
            _includeSubfolders = includeSubfolders;
            _token = token;
        }

        public List<RenameItem> Items { get; } = new List<RenameItem>();

        public int Skipped { get; set; }

        public bool HasFolders => _stack.Count > 0;

        public string Pop() => _stack.Pop();

        /// <summary>Adds the folder's entries, and remembers its sub-folders that are not reparse points. May throw
        /// midway: what was read stays.</summary>
        public void Read(string path)
        {
            _parent = path;
            foreach (DirectoryEntry entry in _enumerator.Enumerate(path))
            {
                if (++_count % 1000 == 0)
                {
                    _token.ThrowIfCancellationRequested();
                }

                Items.Add(new RenameItem(entry.Name, path, entry.Kind, entry.Attributes, entry.Size, entry.Created, entry.Modified));
                if (_includeSubfolders && entry.Kind == ItemKind.Folder && !entry.IsReparsePoint)
                {
                    _children.Add(entry.Name);
                }
            }
        }

        /// <summary>Pushes the remembered sub-folders of the folder just read in reverse natural name order, so the first
        /// one is read next. Sorts names, never whole paths (a deep path would cost its length per comparison).</summary>
        public void PushChildren()
        {
            _children.Sort(SiblingOrder);
            for (int i = _children.Count - 1; i >= 0; i--)
            {
                _stack.Push(Path.Combine(_parent, _children[i]));
            }

            _children.Clear();
        }
    }

    /// <summary>Natural name order, then ordinal for names that compare equal (a total order).</summary>
    private sealed class NaturalThenOrdinal : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            int natural = NaturalNameComparer.Instance.Compare(x, y);
            return natural != 0 ? natural : string.CompareOrdinal(x, y);
        }
    }
}
