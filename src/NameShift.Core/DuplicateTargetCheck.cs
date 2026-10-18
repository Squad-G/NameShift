using System;
using System.Collections.Generic;
using System.Linq;

namespace NameShift.Core;

/// <summary>Flags new names that collide with another item's name or with an entry already in the folder (NS-6).</summary>
public sealed class DuplicateTargetCheck : IConflictCheck
{
    private static readonly IReadOnlyList<Conflict> None = Array.Empty<Conflict>();

    private readonly Dictionary<string, HashSet<string>> existingNamesByFolder =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the check from the existing entries of every folder of the batch, as listed on disk by the app
    /// (every file and folder, hidden and system ones included; never the visible list). Folder paths and names are
    /// compared ordinal ignore-case.</summary>
    public DuplicateTargetCheck(IReadOnlyDictionary<string, IReadOnlyCollection<string>> existingNamesByFolder)
    {
        if (existingNamesByFolder == null)
        {
            throw new ArgumentNullException(nameof(existingNamesByFolder));
        }

        foreach (var pair in existingNamesByFolder)
        {
            if (!this.existingNamesByFolder.TryGetValue(pair.Key, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                this.existingNamesByFolder.Add(pair.Key, set);
            }

            set.UnionWith(pair.Value);
        }
    }

    /// <summary>Checks the changed results; unchanged and error results keep their name, so they occupy it but are
    /// not flagged.</summary>
    public IReadOnlyList<IReadOnlyList<Conflict>> Check(IReadOnlyList<RenameResult> results)
    {
        if (results == null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        var found = new IReadOnlyList<Conflict>[results.Count];
        for (var i = 0; i < found.Length; i++)
        {
            found[i] = None;
        }

        var folders = Enumerable.Range(0, results.Count)
            .GroupBy(i => results[i].Item.FolderPath, StringComparer.OrdinalIgnoreCase);
        foreach (var folder in folders)
        {
            CheckFolder(folder.Key, folder.ToArray(), results, found);
        }

        return found;
    }

    private void CheckFolder(
        string folder,
        int[] indexes,
        IReadOnlyList<RenameResult> results,
        IReadOnlyList<Conflict>[] found)
    {
        if (!existingNamesByFolder.TryGetValue(folder, out var names))
        {
            throw new InvalidOperationException($"No list of the existing names in {folder}.");
        }

        // A batch item's original name is freed or re-taken by a final name, never "existing".
        var outside = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        var occupied = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var i in indexes)
        {
            outside.Remove(results[i].Item.Name);
            occupied.TryGetValue(results[i].NewName, out var count);
            occupied[results[i].NewName] = count + 1;
        }

        foreach (var i in indexes.Where(i => results[i].Status == RenameStatus.Changed))
        {
            var newName = results[i].NewName;
            var conflicts = new List<Conflict>();
            if (occupied[newName] > 1)
            {
                conflicts.Add(new Conflict(ConflictCode.DuplicateTarget));
            }

            if (outside.Contains(newName))
            {
                conflicts.Add(new Conflict(ConflictCode.TargetExists));
            }

            if (conflicts.Count > 0)
            {
                found[i] = conflicts;
            }
        }
    }
}