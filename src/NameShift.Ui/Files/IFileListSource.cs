namespace NameShift.Ui.Files;

/// <summary>The window's only access to the disk for the file list.</summary>
public interface IFileListSource
{
    /// <summary>Every entry of one folder (files and sub-folders, hidden and system ones included, with their
    /// attributes), in any order. May block (call off the UI thread); never throws.</summary>
    FileListing ListEntries(string folderPath);
}
