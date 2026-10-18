using System.Collections.Generic;

namespace NameShift.Ui.Folders;

/// <summary>Where a path sits in the tree: under which root, and the segments below it.</summary>
public sealed class FolderTreeLocation
{
    public FolderTreeLocation(int rootIndex, IReadOnlyList<string> segments)
    {
        RootIndex = rootIndex;
        Segments = segments;
    }

    public int RootIndex { get; }

    public IReadOnlyList<string> Segments { get; }
}
