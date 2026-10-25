using System;
using System.Collections.Generic;
using System.IO;
using NameShift.Core;
using NameShift.Ui.Files;

namespace NameShift.Ui.Tests.Files;

/// <summary>An in-memory tree for <see cref="DiskFileListSource"/>: no disk. Folders are keyed by full path (ignoring
/// case); a path that was never declared throws <see cref="DirectoryNotFoundException"/>, as a removed folder does.
/// Failures and callbacks happen during the enumeration, as on a real disk. Not thread-safe (one walk at a
/// time).</summary>
internal sealed class FakeDirectoryEnumerator : IDirectoryEnumerator
{
    /// <summary>A walk that never ends (a followed loop) fails at this many entries instead of hanging.</summary>
    public const int EntryCap = 100000;

    private readonly Dictionary<string, List<DirectoryEntry>> _folders = new Dictionary<string, List<DirectoryEntry>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _links = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Exception> _throwAtCall = new Dictionary<string, Exception>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (int After, Exception Exception)> _throwAfter = new Dictionary<string, (int, Exception)>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _yielded = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private int _total;

    /// <summary>Called with the folder path and the entry, before each entry is yielded.</summary>
    public Action<string, DirectoryEntry>? OnEntry { get; set; }

    /// <summary>Every Enumerate call that started enumerating, in order (the path as asked).</summary>
    public List<string> Enumerated { get; } = new List<string>();

    /// <summary>Every entry yielded, every folder together.</summary>
    public int TotalYielded => _total;

    /// <summary>How many entries a folder yielded (0 when never enumerated).</summary>
    public int YieldedIn(string path) => _yielded.TryGetValue(path, out int count) ? count : 0;

    /// <summary>Declares an empty folder (a folder must be declared to be enumerated).</summary>
    public void Declare(string path)
    {
        if (!_folders.ContainsKey(path))
        {
            _folders.Add(path, new List<DirectoryEntry>());
        }
    }

    /// <summary>Adds a file entry to the folder (declared on the way).</summary>
    public DirectoryEntry File(string folder, string name, ItemAttributes attributes = ItemAttributes.None, bool reparse = false) =>
        Add(folder, new DirectoryEntry(name, ItemKind.File, reparse, attributes, 1, default, default));

    /// <summary>Adds a folder entry to the folder and declares the sub-folder itself (unless it is a reparse point:
    /// then it is declared only by <see cref="Link"/>). Returns the sub-folder's full path.</summary>
    public string Folder(string folder, string name, bool reparse = false, ItemAttributes attributes = ItemAttributes.None)
    {
        Add(folder, new DirectoryEntry(name, ItemKind.Folder, reparse, attributes, 0, default, default));
        string path = folder + "\\" + name;
        if (!reparse)
        {
            Declare(path);
        }

        return path;
    }

    /// <summary>Makes <paramref name="path"/> and everything under it read <paramref name="target"/>'s tree (a
    /// junction: following <c>root\loop</c> pointing at <c>root</c> never ends).</summary>
    public void Link(string path, string target) => _links[path] = target;

    /// <summary>The folder throws when Enumerate is called.</summary>
    public void ThrowAtCall(string path, Exception exception) => _throwAtCall[path] = exception;

    /// <summary>The folder throws from the enumeration after yielding <paramref name="after"/> entries.</summary>
    public void ThrowAfter(string path, int after, Exception exception) => _throwAfter[path] = (after, exception);

    public IEnumerable<DirectoryEntry> Enumerate(string path)
    {
        if (_throwAtCall.TryGetValue(path, out Exception? atCall))
        {
            throw atCall;
        }

        List<DirectoryEntry> entries = _folders.TryGetValue(Resolve(path), out List<DirectoryEntry>? found)
            ? found
            : throw new DirectoryNotFoundException("No folder " + path + ".");
        return Yield(path, entries);
    }

    private IEnumerable<DirectoryEntry> Yield(string path, List<DirectoryEntry> entries)
    {
        Enumerated.Add(path);
        bool fails = _throwAfter.TryGetValue(path, out (int After, Exception Exception) failure);
        int count = 0;
        foreach (DirectoryEntry entry in entries)
        {
            if (fails && count == failure.After)
            {
                throw failure.Exception;
            }

            if (++_total > EntryCap)
            {
                throw new InvalidOperationException("The walk read more than " + EntryCap + " entries: a loop was followed.");
            }

            count++;
            _yielded[path] = count;
            OnEntry?.Invoke(path, entry);
            yield return entry;
        }

        if (fails && count == failure.After)
        {
            throw failure.Exception;
        }
    }

    /// <summary>The path with every link replaced by its target, repeatedly (a loop of links resolves level by
    /// level).</summary>
    private string Resolve(string path)
    {
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (KeyValuePair<string, string> link in _links)
            {
                if (string.Equals(path, link.Key, StringComparison.OrdinalIgnoreCase))
                {
                    path = link.Value;
                    changed = true;
                    break;
                }

                if (path.StartsWith(link.Key + "\\", StringComparison.OrdinalIgnoreCase))
                {
                    path = link.Value + path.Substring(link.Key.Length);
                    changed = true;
                    break;
                }
            }
        }

        return path;
    }

    private DirectoryEntry Add(string folder, DirectoryEntry entry)
    {
        Declare(folder);
        _folders[folder].Add(entry);
        return entry;
    }
}
