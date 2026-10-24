using System;
using System.Collections.Generic;
using System.Linq;
using NameShift.Core;
using NameShift.Ui.Folders;

namespace NameShift.Ui.Files;

/// <summary>The result of listing one folder's entries: items, or a problem.</summary>
public sealed class FileListing
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> NoNames =
        new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.OrdinalIgnoreCase);

    private FileListing(IReadOnlyList<RenameItem> items, FolderProblem? problem)
    {
        Items = items;
        Problem = problem;
        ExistingNamesByFolder = items.Count == 0 ? NoNames : GroupNames(items);
    }

    /// <summary>Every entry of the folder, files and sub-folders, hidden and system ones included (never filtered);
    /// empty when the listing failed.</summary>
    public IReadOnlyList<RenameItem> Items { get; }

    /// <summary>The names of every entry, per folder path (ignoring case), in item order: hidden and system entries
    /// included, never filtered, for the duplicate check (<see cref="Preview.RenamePlanner"/>). Built once from
    /// <see cref="Items"/>, a snapshot at listing time; empty when the listing failed or the folder is empty.</summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<string>> ExistingNamesByFolder { get; }

    /// <summary>Null when the listing succeeded.</summary>
    public FolderProblem? Problem { get; }

    /// <summary>A successful listing of every entry, hidden and system ones included.</summary>
    /// <exception cref="ArgumentNullException">When items is null.</exception>
    public static FileListing Ok(IReadOnlyList<RenameItem> items)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        return new FileListing(items, null);
    }

    /// <summary>A failed listing: no items and no names.</summary>
    public static FileListing Failed(FolderProblem problem) =>
        new FileListing(Array.Empty<RenameItem>(), problem);

    private static IReadOnlyDictionary<string, IReadOnlyCollection<string>> GroupNames(IReadOnlyList<RenameItem> items)
    {
        var names = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (RenameItem item in items)
        {
            if (!names.TryGetValue(item.FolderPath, out List<string>? folder))
            {
                folder = new List<string>();
                names.Add(item.FolderPath, folder);
            }

            folder.Add(item.Name);
        }

        return names.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyCollection<string>)pair.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }
}
