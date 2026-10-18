using System.Collections.Generic;

namespace NameShift.Ui.Folders;

/// <summary>The window's only access to the disk for folders.</summary>
public interface IFolderSource
{
    /// <summary>
    /// The tree roots: Desktop, Documents, Downloads, Pictures (when they exist), then the drives.
    /// Fast: never touches a drive.
    /// </summary>
    IReadOnlyList<FolderRoot> GetRoots();

    /// <summary>The folder the window opens on: Documents, else the system drive root.</summary>
    string StartFolder { get; }

    /// <summary>Visible sub-folders in natural order. May block (call off the UI thread); never throws.</summary>
    FolderListing ListChildren(string folderPath);

    /// <summary>May block (call off the UI thread); never throws (any failure is <see cref="FolderCheck.NotFound"/>).</summary>
    FolderCheck CheckFolder(string path);
}
