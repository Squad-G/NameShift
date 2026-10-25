using System;
using System.Collections.Generic;

namespace NameShift.Ui.Instance;

/// <summary>
/// Gathers a burst of requests (NS-89 AC 5): Explorer can start one process per selected item, and the window applies
/// their paths together once no request arrived for <see cref="Quiet"/>, at most <see cref="Max"/> after the first. A
/// request without a path argument is not gathered: the window only comes to the front, at once (AC 3). At most
/// <see cref="InstanceRequestFormat.MaxPaths"/> paths are kept; later ones are dropped.
/// </summary>
public sealed class RequestGatherer
{
    /// <summary>The quiet time after the last request.</summary>
    public static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(300);

    /// <summary>The longest wait after the first request.</summary>
    public static readonly TimeSpan Max = TimeSpan.FromSeconds(3);

    private readonly Func<DateTime> _utcNow;
    private readonly List<PathArgument> _paths = new List<PathArgument>();
    private DateTime _first;
    private DateTime _last;

    /// <param name="utcNow">The clock (UTC); a test passes a fake one.</param>
    /// <exception cref="ArgumentNullException">utcNow is null.</exception>
    public RequestGatherer(Func<DateTime> utcNow)
    {
        _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
    }

    /// <summary>True while requests wait to be applied.</summary>
    public bool HasPending => _paths.Count > 0;

    /// <summary>Gathers the request's path arguments, each with the request's own current directory. False, and
    /// nothing gathered, when it has no path argument.</summary>
    /// <exception cref="ArgumentNullException">request is null.</exception>
    public bool Add(InstanceRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        IReadOnlyList<PathArgument> paths = request.PathArguments();
        if (paths.Count == 0)
        {
            return false;
        }

        DateTime now = _utcNow();
        if (_paths.Count == 0)
        {
            _first = now;
        }

        _last = now;
        for (int i = 0; i < paths.Count && _paths.Count < InstanceRequestFormat.MaxPaths; i++)
        {
            _paths.Add(paths[i]);
        }

        return true;
    }

    /// <summary>True when there are paths and either no request came for <see cref="Quiet"/> or the first came
    /// <see cref="Max"/> ago.</summary>
    public bool IsDue(DateTime now) => _paths.Count > 0 && (now - _last >= Quiet || now - _first >= Max);

    /// <summary>How long until <see cref="IsDue"/> may become true (zero when due), or null with nothing gathered.</summary>
    public TimeSpan? NextCheck(DateTime now)
    {
        if (_paths.Count == 0)
        {
            return null;
        }

        DateTime quietEnd = _last + Quiet;
        DateTime maxEnd = _first + Max;
        TimeSpan wait = (quietEnd < maxEnd ? quietEnd : maxEnd) - now;
        return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
    }

    /// <summary>Every gathered path argument in arrival order, then clears.</summary>
    public IReadOnlyList<PathArgument> Take()
    {
        PathArgument[] taken = _paths.ToArray();
        _paths.Clear();
        return taken;
    }
}
