using System;
using System.Collections.Generic;

namespace NameShift.Ui.Folders;

/// <summary>The result of listing one folder's sub-folders: a list, or a problem.</summary>
public sealed class FolderListing
{
    private FolderListing(IReadOnlyList<FolderEntry> folders, FolderProblem? problem)
    {
        Folders = folders;
        Problem = problem;
    }

    /// <summary>The visible sub-folders; empty when the listing failed.</summary>
    public IReadOnlyList<FolderEntry> Folders { get; }

    /// <summary>Null when the listing succeeded.</summary>
    public FolderProblem? Problem { get; }

    public static FolderListing Ok(IReadOnlyList<FolderEntry> folders) => new FolderListing(folders, null);

    public static FolderListing Failed(FolderProblem problem) => new FolderListing(Array.Empty<FolderEntry>(), problem);
}
