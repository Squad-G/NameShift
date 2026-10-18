using System;
using System.Collections.Generic;
using System.IO;
using NameShift.Core;
using NameShift.Ui.Folders;

namespace NameShift.Ui.Files;

/// <summary>Lists one folder's visible files and sub-folders from the real disk.</summary>
public sealed class DiskFileListSource : IFileListSource
{
    public FileListing ListEntries(string folderPath)
    {
        try
        {
            var items = new List<RenameItem>();

            // One pass: on .NET Framework the entries come pre-filled from the find data, so reading
            // attributes, size and times costs no extra disk call per entry.
            foreach (FileSystemInfo info in new DirectoryInfo(folderPath).EnumerateFileSystemInfos())
            {
                if (IsShown(info.Attributes))
                {
                    items.Add(ToItem(info, folderPath));
                }
            }

            return FileListing.Ok(items);
        }
        catch (Exception e)
        {
            // The one deliberate catch-all: whatever the disk throws (a dead share, a card reader,
            // a security policy), a folder listing must never crash the window.
            return FileListing.Failed(DiskFolderSource.Classify(e));
        }
    }

    internal static bool IsShown(FileAttributes attributes) =>
        (attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0;

    internal static RenameItem ToItem(FileSystemInfo info, string folderPath)
    {
        ItemAttributes attributes = ItemAttributes.None;
        if ((info.Attributes & FileAttributes.ReadOnly) != 0)
        {
            attributes |= ItemAttributes.ReadOnly;
        }

        if ((info.Attributes & FileAttributes.Hidden) != 0)
        {
            attributes |= ItemAttributes.Hidden;
        }

        if ((info.Attributes & FileAttributes.System) != 0)
        {
            attributes |= ItemAttributes.System;
        }

        return new RenameItem(
            info.Name,
            folderPath,
            info is DirectoryInfo ? ItemKind.Folder : ItemKind.File,
            attributes,
            info is FileInfo file ? file.Length : 0,
            info.CreationTime,
            info.LastWriteTime);
    }
}