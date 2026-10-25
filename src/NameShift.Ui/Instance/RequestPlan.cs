using System;

namespace NameShift.Ui.Instance;

/// <summary>What the window does with a request when it arrives (NS-89). It always comes to the front first.</summary>
public enum ArrivalAction
{
    /// <summary>No path argument: nothing else changes (AC 3).</summary>
    FrontOnly,

    /// <summary>A Rename or Revert runs, or a dialog is open: the busy message, and the request is dropped (AC 4).</summary>
    Busy,

    /// <summary>Gathered with the other requests of a burst (AC 5).</summary>
    Gather,
}

/// <summary>What the window does with the gathered paths once the burst is over (NS-89).</summary>
public enum DueAction
{
    /// <summary>Nothing was gathered.</summary>
    Nothing,

    /// <summary>The window became busy or opened a dialog meanwhile: the busy message, and the paths are dropped.</summary>
    Busy,

    /// <summary>One path: NS-47's open, where a folder opens.</summary>
    OpenOne,

    /// <summary>Several paths: NS-88's open, the first existing path's folder with those items selected.</summary>
    OpenMany,
}

/// <summary>
/// The window's decisions on a request from another start (NS-89). A request never changes the folder while a Rename
/// or Revert runs, nor under an open dialog (a Revert check, a preset name, a question): it is dropped with the busy
/// message rather than queued, as decided for a busy window.
/// </summary>
public static class RequestPlan
{
    /// <exception cref="ArgumentNullException">request is null.</exception>
    public static ArrivalAction OnArrival(InstanceRequest request, bool isBusy, bool dialogOpen)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (isBusy || dialogOpen)
        {
            return ArrivalAction.Busy;
        }

        return request.PathArguments().Count == 0 ? ArrivalAction.FrontOnly : ArrivalAction.Gather;
    }

    /// <exception cref="ArgumentOutOfRangeException">pathCount is negative.</exception>
    public static DueAction OnDue(int pathCount, bool isBusy, bool dialogOpen)
    {
        if (pathCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pathCount));
        }

        if (pathCount == 0)
        {
            return DueAction.Nothing;
        }

        if (isBusy || dialogOpen)
        {
            return DueAction.Busy;
        }

        return pathCount == 1 ? DueAction.OpenOne : DueAction.OpenMany;
    }
}
