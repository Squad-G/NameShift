using System;
using System.IO;
using NameShift.Core;

namespace NameShift.Disk;

/// <summary>The real disk for the executor: the only class that renames user items.</summary>
internal sealed class DiskFileSystem : IFileSystem
{
    /// <inheritdoc />
    public bool Exists(string path, ItemKind kind) =>
        kind == ItemKind.File ? File.Exists(path) : Directory.Exists(path);

    /// <inheritdoc />
    public bool ExistsAny(string path) => File.Exists(path) || Directory.Exists(path);

    /// <summary>The file's Length and LastWriteTimeUtc through a fresh FileInfo (one stat, no handle opened, nothing
    /// written); null for a folder, a missing path or any error.</summary>
    public FileFingerprint? TryGetFingerprint(string path)
    {
        try
        {
            var info = new FileInfo(path);
            info.Refresh();
            return info.Exists ? new FileFingerprint(info.Length, info.LastWriteTimeUtc) : null;
        }
        catch (Exception)
        {
            // Any failure (access denied, an invalid path, the file replaced mid-read): not readable.
            return null;
        }
    }

    /// <summary>Renames the item. The two-argument File.Move and Directory.Move both throw when the target exists (file
    /// or folder, hidden and system included): neither ever replaces.</summary>
    public void Move(string sourcePath, string targetPath, ItemKind kind)
    {
        if (kind == ItemKind.File)
        {
            File.Move(sourcePath, targetPath);
        }
        else
        {
            Directory.Move(sourcePath, targetPath);
        }
    }
}
