using System;

namespace NameShift.Ui;

/// <summary>What the window does with a resolved <see cref="OpenRequest"/>.</summary>
public enum OpenAction
{
    /// <summary>Nothing: the window is busy, or the user or a newer open moved on meanwhile.</summary>
    Discard,

    /// <summary>Go to <see cref="OpenRequest.Folder"/> (and select the file, if any).</summary>
    Open,

    /// <summary>No folder is shown yet: go to the start folder, then show the message, if any.</summary>
    OpenStartFolder,

    /// <summary>Keep the shown folder; show the message, if any.</summary>
    Stay,
}

/// <summary>
/// Orders the opens of the command line and of drops against every other navigation: the last action wins, not the
/// last resolve to finish. Used from the UI thread.
/// </summary>
public sealed class OpenSequence
{
    private int _version;

    /// <summary>Starts an open; the ticket is passed back to <see cref="Complete"/> once its path is resolved.</summary>
    public int Begin() => ++_version;

    /// <summary>Any navigation (tree, path bar, an open applied) makes the pending opens stale.</summary>
    public void Invalidate() => _version++;

    /// <summary>
    /// What to do with a resolved request. A stale ticket or a busy window discards it (a busy window with no folder
    /// stays empty until the user picks one). With no folder shown, a request with no folder falls back to the start
    /// folder.
    /// </summary>
    /// <exception cref="ArgumentNullException">request is null.</exception>
    public OpenAction Complete(int ticket, OpenRequest request, bool isBusy, bool folderShown)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (isBusy || ticket != _version)
        {
            return OpenAction.Discard;
        }

        if (request.Folder != null)
        {
            return OpenAction.Open;
        }

        return folderShown ? OpenAction.Stay : OpenAction.OpenStartFolder;
    }
}
