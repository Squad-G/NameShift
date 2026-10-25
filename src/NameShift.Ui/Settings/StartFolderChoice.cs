using System;
using NameShift.Ui.Folders;

namespace NameShift.Ui.Settings;

/// <summary>Which folder the window opens on.</summary>
public enum StartFolderSource
{
    /// <summary>Path arguments: gathered with the requests of later starts and opened together (NS-95).</summary>
    Gather,

    /// <summary>The folder shown at the last close (NS-37).</summary>
    Remembered,

    /// <summary>The usual start folder (Documents).</summary>
    StartFolder,
}

/// <summary>The start folder (NS-37 AC 8): path arguments are gathered and opened (NS-95), else the remembered
/// folder when it still exists, then the usual start folder, with no message.</summary>
public static class StartFolderChoice
{
    /// <summary>Gather when there is a path argument; else Remembered when it is not blank; else StartFolder.</summary>
    public static StartFolderSource Pick(string? argument, string? remembered)
    {
        if (argument != null)
        {
            return StartFolderSource.Gather;
        }

        return string.IsNullOrWhiteSpace(remembered) ? StartFolderSource.StartFolder : StartFolderSource.Remembered;
    }

    /// <summary>The remembered folder to open, or <see cref="OpenRequest.Nothing"/> (no message: the window falls back to
    /// the start folder silently) when it is blank, not an exact full path, gone, a file or unreachable. May block (call
    /// off the UI thread); never throws.</summary>
    public static OpenRequest Resolve(string? remembered, IFolderSource source)
    {
        if (string.IsNullOrWhiteSpace(remembered) || source == null)
        {
            return OpenRequest.Nothing;
        }

        // The folder was saved exactly as shown, so it is read back exactly (NS-82's rule for a listed row).
        if (PathInput.TryExact(remembered, out string path) != PathInputResult.Ok)
        {
            return OpenRequest.Nothing;
        }

        try
        {
            return source.CheckFolder(path) == FolderCheck.Folder ? OpenRequest.ForFolder(path) : OpenRequest.Nothing;
        }
        catch (Exception)
        {
            // The source promises never to throw; a start must not fail if one does.
            return OpenRequest.Nothing;
        }
    }
}
