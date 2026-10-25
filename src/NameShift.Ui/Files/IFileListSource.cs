using System;
using System.Threading;

namespace NameShift.Ui.Files;

/// <summary>The window's only access to the disk for the file list.</summary>
public interface IFileListSource
{
    /// <summary>Every entry of the folder and, with <paramref name="includeSubfolders"/>, of every sub-folder not
    /// behind a reparse point (files and sub-folders, hidden and system ones included, with their attributes), grouped
    /// by folder in tree order. May block (call off the UI thread). Never throws, except
    /// <see cref="OperationCanceledException"/> when <paramref name="cancellationToken"/> is cancelled.</summary>
    FileListing ListEntries(string folderPath, bool includeSubfolders, CancellationToken cancellationToken);
}
