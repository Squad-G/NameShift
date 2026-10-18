namespace NameShift.Ui.Files;

/// <summary>The window's only access to the disk for the file list.</summary>
public interface IFileListSource
{
    /// <summary>The visible entries of one folder, in any order, and the names of all its entries (hidden and system included). May block (call off the UI thread); never throws.</summary>
    FileListing ListEntries(string folderPath);
}