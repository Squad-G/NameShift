using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NameShift.Ui.Tests;

/// <summary>
/// Runs a test body on the calling thread under a single-threaded, pumped <see cref="SynchronizationContext"/>, as the
/// window's UI thread does: every await continuation is posted back and runs one at a time, so a continuation never
/// runs in the middle of a synchronous event handler (NS-78: the filter run cannot land before the binding's reload
/// has started).
/// </summary>
internal static class UiThread
{
    public static void Run(Func<Task> body)
    {
        if (body == null)
        {
            throw new ArgumentNullException(nameof(body));
        }

        SynchronizationContext? previous = SynchronizationContext.Current;
        var context = new PumpedContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            Task task = body();
            task.ContinueWith(_ => context.Complete(), TaskScheduler.Default);
            context.Pump();
            task.GetAwaiter().GetResult();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private sealed class PumpedContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _queue = new Queue<(SendOrPostCallback, object?)>();
        private bool _completed;

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (_queue)
            {
                // A late continuation (a superseded run) after the body ended has nothing left to update.
                if (_completed)
                {
                    return;
                }

                _queue.Enqueue((d, state));
                Monitor.Pulse(_queue);
            }
        }

        public override void Send(SendOrPostCallback d, object? state) =>
            throw new NotSupportedException("The test UI thread only takes posts.");

        public void Complete()
        {
            lock (_queue)
            {
                _completed = true;
                Monitor.Pulse(_queue);
            }
        }

        public void Pump()
        {
            while (true)
            {
                (SendOrPostCallback Callback, object? State) next;
                lock (_queue)
                {
                    while (_queue.Count == 0 && !_completed)
                    {
                        Monitor.Wait(_queue);
                    }

                    if (_queue.Count == 0)
                    {
                        return;
                    }

                    next = _queue.Dequeue();
                }

                next.Callback(next.State);
            }
        }
    }
}
