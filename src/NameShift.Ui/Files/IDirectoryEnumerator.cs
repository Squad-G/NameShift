using System.Collections.Generic;

namespace NameShift.Ui.Files;

/// <summary>The disk seam of <see cref="DiskFileListSource"/>: the entries of one folder.</summary>
internal interface IDirectoryEnumerator
{
    /// <summary>The entries of one folder (files and sub-folders, hidden and system ones included), lazily. May throw
    /// when called or while enumerated, with the errors a real disk throws.</summary>
    IEnumerable<DirectoryEntry> Enumerate(string path);
}
