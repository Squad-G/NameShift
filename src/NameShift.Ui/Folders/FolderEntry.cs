namespace NameShift.Ui.Folders;

/// <summary>A visible sub-folder.</summary>
public sealed class FolderEntry
{
    public FolderEntry(string name, string fullPath)
    {
        Name = name;
        FullPath = fullPath;
    }

    public string Name { get; }

    public string FullPath { get; }
}
