namespace NameShift.Ui.Files;

/// <summary>Where the file list is in loading a folder.</summary>
public enum FileListState
{
    /// <summary>No folder yet.</summary>
    None,

    /// <summary>The folder is being listed.</summary>
    Loading,

    /// <summary>The folder's entries are available.</summary>
    Loaded,

    /// <summary>The folder could not be listed.</summary>
    Failed,
}