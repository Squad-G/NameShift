using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NameShift.Ui.Presets;

/// <summary>
/// Saves, lists, loads and deletes presets as version 1 JSON files (<see cref="PresetJson"/>) in one folder. A preset is
/// known by the name inside its file (compared ignoring case), never by its file name; files are named by
/// <see cref="PresetFileName"/>. Every path the store writes or deletes comes from its own folder listing or from a safe
/// file name in its folder, never from the caller. Unreadable files and files of a newer version are counted, never
/// changed or deleted. The methods do disk work: the window calls them off the UI thread. Not thread-safe.
/// </summary>
public sealed class PresetStore
{
    private readonly IPresetFiles _files;

    /// <summary>A store over the real disk.</summary>
    /// <exception cref="ArgumentException">The folder is null, empty or white space, or not a valid path.</exception>
    public PresetStore(string folder)
        : this(folder, new DiskPresetFiles())
    {
    }

    /// <summary>A store over the given file access (tests).</summary>
    internal PresetStore(string folder, IPresetFiles files)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            throw new ArgumentException("The folder is required.", nameof(folder));
        }

        _files = files ?? throw new ArgumentNullException(nameof(files));
        try
        {
            string full = Path.GetFullPath(folder);
            string root = Path.GetPathRoot(full) ?? "";

            // Without a trailing separator (but a drive root keeps its own), so it equals Path.GetDirectoryName of a file in it.
            Folder = full.Length > root.Length
                ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : full;
        }
        catch (Exception e) when (e is NotSupportedException || e is PathTooLongException || e is ArgumentException)
        {
            throw new ArgumentException("The folder is not a valid path.", nameof(folder), e);
        }
    }

    /// <summary>The user's presets folder: %APPDATA%\NameShift\presets (roaming: presets are small and the user's
    /// own). Only computed here, never created; the first save creates it.</summary>
    public static string DefaultFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NameShift", "presets");

    /// <summary>The store's folder, a full path without a trailing separator.</summary>
    public string Folder { get; }

    /// <summary>Saves the preset. A preset with the same name (ignoring case) is replaced in its own file when
    /// <paramref name="overwrite"/> is true, otherwise the result is Exists. A new preset gets the first free file
    /// name of <c>name.json</c>, <c>name (2).json</c>, ...: a file that is taken, even an unreadable one, is never
    /// replaced. The write is atomic; a failure returns Failed with the system message and leaves any old file intact.
    /// Creates the folder when it is missing. Never throws on the disk.</summary>
    /// <exception cref="ArgumentNullException">The preset is null.</exception>
    public PresetSaveResult Save(Preset preset, bool overwrite)
    {
        if (preset == null)
        {
            throw new ArgumentNullException(nameof(preset));
        }

        if (string.IsNullOrWhiteSpace(preset.Name))
        {
            return PresetSaveResult.InvalidName();
        }

        try
        {
            Scan scan = ScanFolder();
            PresetEntry? existing = scan.Find(preset.Name);
            string path;
            if (existing != null)
            {
                if (!overwrite)
                {
                    return PresetSaveResult.Exists();
                }

                path = existing.FilePath;
            }
            else
            {
                path = FreePath(preset.Name);
            }

            _files.WriteAtomic(path, PresetJson.Serialize(preset));
            return PresetSaveResult.Saved(path);
        }
        catch (Exception e)
        {
            // The one deliberate catch-all: a store must never take the window down (a roaming folder may be on a share).
            return PresetSaveResult.Failed(e.Message);
        }
    }

    /// <summary>The readable presets sorted by name (natural order, ignoring case), and the count of files that could
    /// not be used. Never writes, deletes or creates anything; a missing or unreachable folder lists as empty.</summary>
    public PresetListing List()
    {
        Scan scan;
        try
        {
            scan = ScanFolder();
        }
        catch (Exception)
        {
            // An unreachable folder lists as empty, as a missing one does.
            return new PresetListing(Array.Empty<PresetEntry>(), 0);
        }

        IEnumerable<PresetEntry> sorted = scan.Presets
            .Select(found => found.Entry)
            .OrderBy(entry => entry.Name, NaturalNameComparer.Instance)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .ThenBy(entry => entry.FilePath, StringComparer.Ordinal);
        return new PresetListing(sorted, scan.UnusableCount);
    }

    /// <summary>The preset with that name (ignoring case), or not found (also for a null or blank name, or an
    /// unreachable folder).</summary>
    public PresetLoadResult Load(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return PresetLoadResult.NotFound();
        }

        try
        {
            Found? found = ScanFolder().FindFound(name);
            return found == null ? PresetLoadResult.NotFound() : PresetLoadResult.Loaded(found.Preset);
        }
        catch (Exception)
        {
            return PresetLoadResult.NotFound();
        }
    }

    /// <summary>Deletes the file of the readable preset with that name (ignoring case), found by the name inside the
    /// files of this folder: never a path from the caller, never another file, never a folder. A name that is not a
    /// listed preset (such as <c>..\victim</c>) is NotFound. A disk failure returns Failed with the system message and
    /// the file stays.</summary>
    public PresetDeleteResult Delete(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return PresetDeleteResult.NotFound();
        }

        try
        {
            PresetEntry? entry = ScanFolder().Find(name);
            if (entry == null || !IsOwnPresetFile(entry.FilePath))
            {
                return PresetDeleteResult.NotFound();
            }

            _files.Delete(entry.FilePath);
            return PresetDeleteResult.Deleted();
        }
        catch (Exception e)
        {
            return PresetDeleteResult.Failed(e.Message);
        }
    }

    /// <summary>Defence in depth: the path is a ".json" file directly in this folder.</summary>
    private bool IsOwnPresetFile(string path) =>
        string.Equals(Path.GetDirectoryName(path), Folder, StringComparison.OrdinalIgnoreCase)
        && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    /// <summary>The first of name.json, name (2).json, ... that no file has (any file, readable or not).</summary>
    private string FreePath(string name)
    {
        for (int copy = 1; ; copy++)
        {
            string path = Path.Combine(Folder, PresetFileName.For(name, copy));
            if (!_files.FileExists(path))
            {
                return path;
            }
        }
    }

    /// <summary>Reads every ".json" file of the folder in ordinal path order; nothing (and nothing created) when the
    /// folder is missing. A file that cannot be read or parsed counts as unusable.</summary>
    private Scan ScanFolder()
    {
        var presets = new List<Found>();
        int unusable = 0;
        if (!_files.FolderExists(Folder))
        {
            return new Scan(presets, unusable);
        }

        foreach (string path in _files.ListJsonFiles(Folder).OrderBy(p => p, StringComparer.Ordinal))
        {
            PresetRead read;
            try
            {
                read = PresetJson.Read(_files.ReadAll(path));
            }
            catch (Exception)
            {
                // Locked, denied or gone since the listing: a file this scan cannot use.
                unusable++;
                continue;
            }

            if (read.Status == PresetReadStatus.Ok)
            {
                presets.Add(new Found(new PresetEntry(read.Preset!.Name, path), read.Preset));
            }
            else
            {
                unusable++;
            }
        }

        return new Scan(presets, unusable);
    }

    private sealed class Found
    {
        public Found(PresetEntry entry, Preset preset)
        {
            Entry = entry;
            Preset = preset;
        }

        public PresetEntry Entry { get; }

        public Preset Preset { get; }
    }

    private sealed class Scan
    {
        public Scan(List<Found> presets, int unusableCount)
        {
            Presets = presets;
            UnusableCount = unusableCount;
        }

        /// <summary>The readable presets in ordinal path order.</summary>
        public List<Found> Presets { get; }

        public int UnusableCount { get; }

        /// <summary>The first preset by path with that name, ignoring case: "the" preset when two files hold one name.</summary>
        public Found? FindFound(string name) =>
            Presets.FirstOrDefault(found => string.Equals(found.Entry.Name, name, StringComparison.OrdinalIgnoreCase));

        public PresetEntry? Find(string name) => FindFound(name)?.Entry;
    }
}
