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
