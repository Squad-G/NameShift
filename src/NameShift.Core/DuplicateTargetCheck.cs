using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NameShift.Core;

/// <summary>Flags new names that collide with another item's name or with an entry already in the folder (NS-6).
/// Names are compared by their NFC form, then ordinal ignore-case, so two names that look the same are the same name
/// (NS-48); a name that cannot be normalized is compared as it is.</summary>
public sealed class DuplicateTargetCheck : IConflictCheck
{
    private static readonly IReadOnlyList<Conflict> None = Array.Empty<Conflict>();

    private readonly Dictionary<string, HashSet<string>> existingNamesByFolder =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the check from the existing entries of every folder of the batch, as listed on disk by the app
    /// (every file and folder, hidden and system ones included; never the visible list). Folder paths are compared
    /// ordinal ignore-case; names are compared by their NFC form, then ordinal ignore-case (a name that cannot be
    /// normalized is compared as it is).</summary>
    public DuplicateTargetCheck(IReadOnlyDictionary<string, IReadOnlyCollection<string>> existingNames)
    {
        if (existingNames == null)
        {
            throw new ArgumentNullException(nameof(existingNames));
        }

        foreach (var pair in existingNames)
        {
            if (!existingNamesByFolder.TryGetValue(pair.Key, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                existingNamesByFolder.Add(pair.Key, set);
            }

            set.UnionWith(pair.Value.Select(NameKey));
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
        var keys = new string[indexes.Length];
        for (var k = 0; k < indexes.Length; k++)
        {
            var i = indexes[k];
            keys[k] = NameKey(results[i].NewName);
            outside.Remove(NameKey(results[i].Item.Name));
            occupied.TryGetValue(keys[k], out var count);
            occupied[keys[k]] = count + 1;
        }

        for (var k = 0; k < indexes.Length; k++)
        {
            var i = indexes[k];
            if (results[i].Status != RenameStatus.Changed)
            {
                continue;
            }

            var key = keys[k];
            var conflicts = new List<Conflict>();
            if (occupied[key] > 1)
            {
                conflicts.Add(new Conflict(ConflictCode.DuplicateTarget));
            }

            if (outside.Contains(key))
            {
                conflicts.Add(new Conflict(ConflictCode.TargetExists));
            }

            if (conflicts.Count > 0)
            {
                found[i] = conflicts;
            }
        }
    }

    /// <summary>The comparison key of a name: its NFC form, or the name itself when it cannot be normalized
    /// (a lone surrogate or another invalid code point). Compared ordinal ignore-case.</summary>
    private static string NameKey(string name)
    {
        try
        {
            return name.IsNormalized(NormalizationForm.FormC) ? name : name.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            return name;
        }
    }
}
