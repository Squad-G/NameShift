using System;
using System.Collections.Generic;
using System.Linq;
using NameShift.Core;
using NameShift.Ui.Folders;

namespace NameShift.Ui.Files;

/// <summary>The result of listing one folder's entries: items, or a problem.</summary>
public sealed class FileListing
{
    private FileListing(IReadOnlyList<RenameItem> items, IReadOnlyList<string> existingNames, FolderProblem? problem)
    {
        Items = items;
        ExistingNames = existingNames;
        Problem = problem;
    }

    /// <summary>The visible files and sub-folders; empty when the listing failed.</summary>
    public IReadOnlyList<RenameItem> Items { get; }

    /// <summary>The names of every entry in the folder, hidden and system ones included (a superset of the names of
    /// Items); empty when the listing failed.</summary>
    public IReadOnlyList<string> ExistingNames { get; }

    /// <summary>Null when the listing succeeded.</summary>
    public FolderProblem? Problem { get; }

    /// <summary>A successful listing of a folder that has no hidden or system entries: the existing names are the items' names.</summary>
    public static FileListing Ok(IReadOnlyList<RenameItem> items)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        return Ok(items, items.Select(item => item.Name).ToArray());
    }

    /// <summary>A successful listing with the names of every entry, hidden and system ones included.</summary>
    public static FileListing Ok(IReadOnlyList<RenameItem> items, IReadOnlyList<string> existingNames)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        if (existingNames == null)
        {
            throw new ArgumentNullException(nameof(existingNames));
        }

        return new FileListing(items, existingNames, null);
    }

    public static FileListing Failed(FolderProblem problem) =>
        new FileListing(Array.Empty<RenameItem>(), Array.Empty<string>(), problem);
}