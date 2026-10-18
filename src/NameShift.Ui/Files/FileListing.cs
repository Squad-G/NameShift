using System;
using System.Collections.Generic;
using NameShift.Core;
using NameShift.Ui.Folders;

namespace NameShift.Ui.Files;

/// <summary>The result of listing one folder's entries: items, or a problem.</summary>
public sealed class FileListing
{
    private FileListing(IReadOnlyList<RenameItem> items, FolderProblem? problem)
    {
        Items = items;
        Problem = problem;
    }

    /// <summary>The visible files and sub-folders; empty when the listing failed.</summary>
    public IReadOnlyList<RenameItem> Items { get; }

    /// <summary>Null when the listing succeeded.</summary>
    public FolderProblem? Problem { get; }

    public static FileListing Ok(IReadOnlyList<RenameItem> items) => new FileListing(items, null);

    public static FileListing Failed(FolderProblem problem) => new FileListing(Array.Empty<RenameItem>(), problem);
}