using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NameShift.Ui.Tests.Files;

namespace NameShift.Ui.Tests;

/// <summary>
/// Runs a test body on the calling thread under a single-threaded, pumped <see cref="SynchronizationContext"/>, as the
/// window's UI thread does: every posted continuation runs one at a time, never in the middle of a synchronous event
/// handler. An await whose task has already ended posts nothing and carries on synchronously, so a test that needs one
/// run to land after another holds the first one's work (NS-77, FilterBindingTests). FolderTreeModel's tests run here
/// too (NS-77): its state belongs to the UI thread, and on pool threads two continuations wrote its dictionaries at
/// once. A body with no post and no end for <see cref="TestWait.Timeout"/> fails with a TimeoutException instead of
/// hanging the test host.
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
            // Inline on the thread that ends the body (this one when it ended synchronously): no wait for a pool thread,
            // which a starved pool can delay past the timeout below.
            task.ContinueWith(
                _ => context.Complete(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
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
                        if (!Monitor.Wait(_queue, TestWait.Timeout))
                        {
                            throw new TimeoutException("The test body did not finish in time.");
                        }
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
