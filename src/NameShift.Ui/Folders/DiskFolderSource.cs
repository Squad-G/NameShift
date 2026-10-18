using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NameShift.Ui.Folders;

/// <summary>The real disk behind the folder tree and the path bar.</summary>
public sealed class DiskFolderSource : IFolderSource
{
    private const int ErrorNotReady = unchecked((int)0x80070015);

    private readonly Func<KnownFolder, string?> _resolveKnownFolder;
    private readonly Func<IReadOnlyList<string>> _driveRoots;
    private readonly Func<string, bool> _folderExists;
    private readonly string _systemDriveRoot;

    public DiskFolderSource()
        : this(
            ResolveKnownFolder,
            () => DriveInfo.GetDrives().Select(d => d.Name).ToList(),
            Directory.Exists,
            Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\")
    {
    }

    internal DiskFolderSource(
        Func<KnownFolder, string?> resolveKnownFolder,
        Func<IReadOnlyList<string>> driveRoots,
        Func<string, bool> folderExists,
        string systemDriveRoot)
    {
        _resolveKnownFolder = resolveKnownFolder;
        _driveRoots = driveRoots;
        _folderExists = folderExists;
        _systemDriveRoot = systemDriveRoot;
    }

    public string StartFolder => ExistingKnownFolder(KnownFolder.Documents) ?? _systemDriveRoot;

    public IReadOnlyList<FolderRoot> GetRoots()
    {
        var roots = new List<FolderRoot>();
        AddSpecial(roots, KnownFolder.Desktop, Strings.Desktop);
        AddSpecial(roots, KnownFolder.Documents, Strings.Documents);
        AddSpecial(roots, KnownFolder.Downloads, Strings.Downloads);
        AddSpecial(roots, KnownFolder.Pictures, Strings.Pictures);

        // Only the drive names are read: asking a drive anything else can block on an empty card reader.
        foreach (string drive in _driveRoots())
        {
            roots.Add(new FolderRoot(drive.TrimEnd('\\'), drive, FolderRootKind.Drive));
        }

        return roots;
    }

    public FolderListing ListChildren(string folderPath)
    {
        try
        {
            var folders = new List<FolderEntry>();
            foreach (DirectoryInfo directory in new DirectoryInfo(folderPath).EnumerateDirectories())
            {
                if ((directory.Attributes & FileAttributes.Hidden) == 0)
                {
                    folders.Add(new FolderEntry(directory.Name, directory.FullName));
                }
            }

            folders.Sort((a, b) => NaturalNameComparer.Instance.Compare(a.Name, b.Name));
            return FolderListing.Ok(folders);
        }
        catch (Exception e)
        {
            // The one deliberate catch-all: whatever the disk throws (a dead share, a card reader,
            // a security policy), a folder listing must never crash the window.
            return FolderListing.Failed(Classify(e));
        }
    }

    public FolderCheck CheckFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                return FolderCheck.Folder;
            }

            return File.Exists(path) ? FolderCheck.File : FolderCheck.NotFound;
        }
        catch (Exception)
        {
            // Directory.Exists and File.Exists should not throw; a path that makes them is simply not a folder.
            return FolderCheck.NotFound;
        }
    }

    internal static FolderProblem Classify(Exception exception)
    {
        // DirectoryNotFoundException and DriveNotFoundException derive from IOException: test them first.
        switch (exception)
        {
            case UnauthorizedAccessException _:
                return FolderProblem.AccessDenied;
            case DirectoryNotFoundException _:
            case DriveNotFoundException _:
                return FolderProblem.NotFound;
            case IOException io when io.HResult == ErrorNotReady:
                return FolderProblem.NotReady;
            default:
                return FolderProblem.Unavailable;
        }
    }

    private static string? ResolveKnownFolder(KnownFolder folder)
    {
        switch (folder)
        {
            case KnownFolder.Desktop:
                return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            case KnownFolder.Documents:
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            case KnownFolder.Pictures:
                return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            default:
                return KnownFolderPaths.Downloads();
        }
    }

    private string? ExistingKnownFolder(KnownFolder folder)
    {
        string? path = _resolveKnownFolder(folder);
        return string.IsNullOrEmpty(path) || !_folderExists(path!) ? null : path;
    }

    private void AddSpecial(List<FolderRoot> roots, KnownFolder folder, string displayName)
    {
        string? path = ExistingKnownFolder(folder);
        if (path != null)
        {
            roots.Add(new FolderRoot(displayName, path, FolderRootKind.SpecialFolder));
        }
    }
}
