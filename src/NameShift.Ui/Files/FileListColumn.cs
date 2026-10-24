namespace NameShift.Ui.Files;

/// <summary>A column the file list can be sorted by. New Name and Status are not sortable.</summary>
public enum FileListColumn
{
    /// <summary>The item's name, in natural order.</summary>
    Name,

    /// <summary>The size in bytes (a folder's is 0).</summary>
    Size,

    /// <summary>The last modification time (an unknown date is the oldest).</summary>
    Modified,
}
