namespace NameShift.Ui.Folders;

/// <summary>A top-level entry of the folder tree: a special folder or a drive.</summary>
public sealed class FolderRoot
{
    public FolderRoot(string displayName, string fullPath, FolderRootKind kind)
    {
        DisplayName = displayName;
        FullPath = fullPath;
        Kind = kind;
    }

    public string DisplayName { get; }

    public string FullPath { get; }

    public FolderRootKind Kind { get; }
}
