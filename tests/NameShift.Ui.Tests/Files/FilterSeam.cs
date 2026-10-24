using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NameShift.Core;

namespace NameShift.Ui.Tests.Files;

/// <summary>The file list's filter seam for tests: a filter instance can be given its own behaviour (slow, blocking,
/// throwing); every other filter runs as <see cref="ItemFilter.Includes"/>. Thread-safe.</summary>
internal sealed class FilterSeam
{
    private readonly ConcurrentDictionary<ItemFilter, Func<RenameItem, bool>> _custom =
        new ConcurrentDictionary<ItemFilter, Func<RenameItem, bool>>();

    private int _calls;

    /// <summary>How many times the seam was called, every filter together.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <summary>Gives the filter instance its own behaviour. Call before the filter is passed to the model.</summary>
    public void Use(ItemFilter filter, Func<RenameItem, bool> includes) => _custom[filter] = includes;

    /// <summary>The seam the model calls.</summary>
    public bool Includes(ItemFilter filter, RenameItem item)
    {
        Interlocked.Increment(ref _calls);
        return _custom.TryGetValue(filter, out Func<RenameItem, bool>? includes) ? includes(item) : filter.Includes(item);
    }
}

/// <summary>Holds a pool thread until released, and tells the test when the thread got there. Every wait has a
/// timeout, so a test that goes wrong fails instead of hanging.</summary>
internal sealed class TestGate : IDisposable
{
    private readonly ManualResetEventSlim _reached = new ManualResetEventSlim(false);
    private readonly ManualResetEventSlim _released = new ManualResetEventSlim(false);

    /// <summary>Called on the pool thread: signals that it got here, then waits for the release.</summary>
    /// <exception cref="TimeoutException">When the test never releases the gate.</exception>
    public void Block()
    {
        _reached.Set();
        if (!_released.Wait(TestWait.Timeout))
        {
            throw new TimeoutException("The gate was never released.");
        }
    }

    /// <summary>Waits until a pool thread is held by the gate.</summary>
    /// <exception cref="TimeoutException">When no thread gets there in time.</exception>
    public void WaitUntilReached()
    {
        if (!_reached.Wait(TestWait.Timeout))
        {
            throw new TimeoutException("No thread reached the gate.");
        }
    }

    public void Release() => _released.Set();

    /// <summary>Releases the gate: always called in a finally (a using).</summary>
    public void Dispose() => _released.Set();
}

/// <summary>Awaits a model task, but fails after <see cref="Timeout"/> instead of hanging (WaitAsync is not on
/// net48).</summary>
internal static class TestWait
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public static async Task<T> Within<T>(Task<T> task)
    {
        if (await Task.WhenAny(task, Task.Delay(Timeout)) != task)
        {
            throw new TimeoutException("The task did not finish in time.");
        }

        return await task;
    }

    public static async Task Within(Task task)
    {
        if (await Task.WhenAny(task, Task.Delay(Timeout)) != task)
        {
            throw new TimeoutException("The task did not finish in time.");
        }

        await task;
    }
}
