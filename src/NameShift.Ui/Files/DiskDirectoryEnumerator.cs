using System.Collections.Generic;
using System.IO;
using NameShift.Core;

namespace NameShift.Ui.Files;

/// <summary>The real disk: <see cref="DirectoryInfo.EnumerateFileSystemInfos()"/>, lazily, so an error midway surfaces
/// from the enumeration as it does on the disk.</summary>
internal sealed class DiskDirectoryEnumerator : IDirectoryEnumerator
{
    public IEnumerable<DirectoryEntry> Enumerate(string path)
    {
        // One pass: on .NET Framework the entries come pre-filled from the find data, so reading attributes, size and
        // times costs no extra disk call per entry.
        foreach (FileSystemInfo info in new DirectoryInfo(path).EnumerateFileSystemInfos())
        {
            yield return ToEntry(info);
        }
    }

    internal static DirectoryEntry ToEntry(FileSystemInfo info)
    {
        FileAttributes attributes = info.Attributes;
        return new DirectoryEntry(
            info.Name,
            info is DirectoryInfo ? ItemKind.Folder : ItemKind.File,
            Has(attributes, FileAttributes.ReparsePoint),
            ToItemAttributes(attributes),
            info is FileInfo file ? file.Length : 0,
            info.CreationTime,
            info.LastWriteTime);
    }

    private static ItemAttributes ToItemAttributes(FileAttributes attributes) =>
        (Has(attributes, FileAttributes.ReadOnly) ? ItemAttributes.ReadOnly : ItemAttributes.None)
        | (Has(attributes, FileAttributes.Hidden) ? ItemAttributes.Hidden : ItemAttributes.None)
        | (Has(attributes, FileAttributes.System) ? ItemAttributes.System : ItemAttributes.None);

    private static bool Has(FileAttributes attributes, FileAttributes flag) => (attributes & flag) != 0;
}
