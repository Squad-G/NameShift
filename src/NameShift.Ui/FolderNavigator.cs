using System;
using System.Threading.Tasks;
using NameShift.Ui.Folders;

namespace NameShift.Ui;

/// <summary>
/// Owns the current folder and raises <see cref="FolderChanged"/> only when it actually changes, so the tree and the
/// path bar can follow each other without loops. Used from the UI thread.
/// </summary>
public sealed class FolderNavigator
{
    private readonly IFolderSource _source;
    private int _version;

    public FolderNavigator(IFolderSource source)
    {
        _source = source;
    }

    public string? CurrentFolder { get; private set; }

    public event EventHandler<FolderChangedEventArgs>? FolderChanged;

    /// <summary>Goes to the source's start folder.</summary>
    public void Start() => SelectFromTree(_source.StartFolder);

    /// <summary>A path the tree supplied (trusted). Supersedes a pending typed navigation.</summary>
    public void SelectFromTree(string folderPath)
    {
        _version++;
        if (CurrentFolder is null || !PathInput.SameFolder(CurrentFolder, folderPath))
        {
            Change(folderPath);
        }
    }

    /// <summary>Navigates to a path typed by the user; the disk check runs off the calling thread.</summary>
    public async Task<PathBarResult> NavigateToTypedPathAsync(string text)
    {
        switch (PathInput.TryNormalize(text, out string path))
        {
            case PathInputResult.Empty:
                return PathBarResult.Empty;
            case PathInputResult.Invalid:
                return PathBarResult.Invalid;
        }

        int version = ++_version;
        FolderCheck check = await Task.Run(() => _source.CheckFolder(path));

        // The version check and the state change stay together: nothing awaits between them.
        if (version != _version)
        {
            return PathBarResult.Superseded;
        }

        switch (check)
        {
            case FolderCheck.File:
                return PathBarResult.NotAFolder;
            case FolderCheck.NotFound:
                return PathBarResult.NotFound;
        }

        if (CurrentFolder != null && PathInput.SameFolder(CurrentFolder, path))
        {
            return PathBarResult.Unchanged;
        }

        Change(path);
        return PathBarResult.Navigated;
    }

    private void Change(string path)
    {
        CurrentFolder = path;
        FolderChanged?.Invoke(this, new FolderChangedEventArgs(path));
    }
}
