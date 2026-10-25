using System;
using System.Collections.Generic;
using System.Linq;

namespace NameShift.Ui.Instance;

/// <summary>What a second start hands to the running window (NS-89): its arguments as given and the directory it started
/// in, so a relative path means what it meant where the user typed it.</summary>
public sealed class InstanceRequest
{
    /// <exception cref="ArgumentNullException">args, one of its items, or currentDirectory is null.</exception>
    public InstanceRequest(IEnumerable<string> args, string currentDirectory)
    {
        if (args == null)
        {
            throw new ArgumentNullException(nameof(args));
        }

        string[] copy = args.ToArray();
        if (copy.Any(a => a == null))
        {
            throw new ArgumentNullException(nameof(args), "An argument is null.");
        }

        Args = copy;
        CurrentDirectory = currentDirectory ?? throw new ArgumentNullException(nameof(currentDirectory));
    }

    /// <summary>The command line arguments of the second start, as given, in order.</summary>
    public IReadOnlyList<string> Args { get; }

    /// <summary>The directory the second start began in.</summary>
    public string CurrentDirectory { get; }

    /// <summary>The path arguments (options and blanks left out, as at a start: NS-47), each joined to this request's
    /// own current directory.</summary>
    public IReadOnlyList<PathArgument> PathArguments() =>
        OpenTarget.PathArguments(Args.ToArray()).Select(t => new PathArgument(t, CurrentDirectory)).ToList();
}
