using System;
using System.Collections.Generic;

namespace NameShift.Ui.Instance;

/// <summary>The owner window's own start (NS-95): its path arguments enter the gatherer as the first request, so a
/// cold start of several processes ends like a start into a running window.</summary>
public static class StartGathering
{
    /// <summary>The owner's request: the start arguments as given and the process's own current directory.</summary>
    /// <exception cref="ArgumentNullException">args or currentDirectory is null.</exception>
    public static InstanceRequest OwnerRequest(IReadOnlyList<string> args, string currentDirectory)
    {
        if (args == null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        return new InstanceRequest(args, currentDirectory);
    }

    /// <summary>Adds the owner's request to <paramref name="gatherer"/>; true when it held a path (gathering
    /// started), false (nothing added) when it held none.</summary>
    /// <exception cref="ArgumentNullException">gatherer, args or currentDirectory is null.</exception>
    public static bool Begin(RequestGatherer gatherer, IReadOnlyList<string> args, string currentDirectory)
    {
        if (gatherer == null)
        {
            throw new ArgumentNullException(nameof(gatherer));
        }

        return gatherer.Add(OwnerRequest(args, currentDirectory));
    }
}
