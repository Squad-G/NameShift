using NameShift.Core;

namespace NameShift.Disk;

/// <summary>The executor's only access to the disk: checks and a move that never replaces. There is no delete, copy or
/// attribute change on purpose.</summary>
internal interface IFileSystem
{
    /// <summary>True when a file (File) or a folder (Folder) exists at the path.</summary>
    bool Exists(string path, ItemKind kind);

    /// <summary>True when a file or a folder exists at the path.</summary>
    bool ExistsAny(string path);

    /// <summary>Renames the item; fails rather than replace an existing entry.</summary>
    void Move(string sourcePath, string targetPath, ItemKind kind);
}
