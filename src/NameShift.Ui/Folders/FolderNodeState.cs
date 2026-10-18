namespace NameShift.Ui.Folders;

/// <summary>Where a tree node is in its lazy load.</summary>
public enum FolderNodeState
{
    NotLoaded,
    Loading,
    Loaded,
    Failed,
}
