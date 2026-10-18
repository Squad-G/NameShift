using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NameShift.Ui.Tests.Preview;

/// <summary>A controllable stand-in for Task.Delay: each call waits until released, or is cancelled with its token.</summary>
internal sealed class FakeDelay
{
    private readonly List<TaskCompletionSource<bool>> _sources = new List<TaskCompletionSource<bool>>();
    private int _cancelled;

    /// <summary>The duration of every call, in order.</summary>
    public List<TimeSpan> Calls { get; } = new List<TimeSpan>();

    /// <summary>How many calls were cancelled.</summary>
    public int Cancelled => Volatile.Read(ref _cancelled);

    public Task Delay(TimeSpan duration, CancellationToken token)
    {
        var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Calls.Add(duration);
        _sources.Add(source);
        token.Register(() =>
        {
            if (source.TrySetCanceled())
            {
                Interlocked.Increment(ref _cancelled);
            }
        });
        return source.Task;
    }

    public void ReleaseAll()
    {
        foreach (TaskCompletionSource<bool> source in _sources)
        {
            source.TrySetResult(true);
        }
    }

    public void ReleaseLast() => _sources[_sources.Count - 1].TrySetResult(true);
}
