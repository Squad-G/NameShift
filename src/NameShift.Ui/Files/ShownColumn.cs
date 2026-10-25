namespace NameShift.Ui.Files;

/// <summary>A column the file list can show (NS-78). <see cref="FileListColumns"/> maps it to its index.</summary>
public enum ShownColumn
{
    /// <summary>The item's name.</summary>
    Name,

    /// <summary>The item's folder, relative to the shown one; shown only with the Sub-folders box ticked.</summary>
    Folder,

    /// <summary>The preview's new name.</summary>
    NewName,

    /// <summary>The preview's status.</summary>
    Status,

    /// <summary>The size.</summary>
    Size,

    /// <summary>The last modification time.</summary>
    Modified,
}
