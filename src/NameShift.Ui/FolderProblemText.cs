using System;
using NameShift.Ui.Folders;

namespace NameShift.Ui;

/// <summary>The short message the tree shows for a folder that could not be listed.</summary>
public static class FolderProblemText
{
    public static string For(FolderProblem problem)
    {
        switch (problem)
        {
            case FolderProblem.NotReady:
                return Strings.DriveNotReady;
            case FolderProblem.AccessDenied:
                return Strings.AccessDenied;
            case FolderProblem.NotFound:
                return Strings.FolderNotFound;
            case FolderProblem.Unavailable:
                return Strings.FolderUnavailable;
            default:
                throw new ArgumentOutOfRangeException(nameof(problem), problem, "Unknown folder problem.");
        }
    }
}
