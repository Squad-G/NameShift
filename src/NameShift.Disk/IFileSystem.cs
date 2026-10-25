using NameShift.Core;

namespace NameShift.Disk;

/// <summary>The executor's only access to the disk: checks, a read of a file's size and time, and a move that never
/// replaces. There is no delete, copy or attribute change on purpose.</summary>
internal interface IFileSystem
{
    /// <summary>True when a file (File) or a folder (Folder) exists at the path.</summary>
    bool Exists(string path, ItemKind kind);

    /// <summary>True when a file or a folder exists at the path.</summary>
    bool ExistsAny(string path);

    /// <summary>The size and last write time of the file at the path; null when no file is there or it cannot be
    /// read. Read-only, and never throws.</summary>
    FileFingerprint? TryGetFingerprint(string path);

    /// <summary>Renames the item; fails rather than replace an existing entry.</summary>
    void Move(string sourcePath, string targetPath, ItemKind kind);
}
