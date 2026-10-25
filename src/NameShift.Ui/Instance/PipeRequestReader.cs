using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NameShift.Ui.Instance;

/// <summary>
/// Reads one framed message from the pipe (NS-89): an <c>int32</c> length (refused below 0 or above
/// <see cref="InstanceRequestFormat.MaxBytes"/>), then exactly that many bytes, all within one overall timeout. A client
/// that connects and never sends, or sends too little, gives null at the timeout and never blocks the next one.
/// </summary>
public static class PipeRequestReader
{
    /// <summary>The message bytes, or null on a timeout, a cancellation, a length out of range, an early end of
    /// stream or any exception. Never takes much longer than <paramref name="timeout"/>: each read is also raced against a
    /// delay, because a pipe read may ignore the token. After a null, the caller disposes the stream, which ends a
    /// read still pending.</summary>
    /// <exception cref="ArgumentNullException">stream is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">timeout is not positive or exceeds <see cref="int.MaxValue"/>
    /// milliseconds.</exception>
    public static Task<byte[]?> ReadAsync(Stream stream, TimeSpan timeout, CancellationToken token)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        return ReadCoreAsync(stream, timeout, token);
    }

    private static async Task<byte[]?> ReadCoreAsync(Stream stream, TimeSpan timeout, CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(timeout);

            var header = new byte[4];
            if (!await ReadExactlyAsync(stream, header, watch, timeout, cts.Token).ConfigureAwait(false))
            {
                return null;
            }

            int length = InstanceRequestFormat.GetInt32(header, 0);
            if (length < 0 || length > InstanceRequestFormat.MaxBytes)
            {
                return null;
            }

            var body = new byte[length];
            return await ReadExactlyAsync(stream, body, watch, timeout, cts.Token).ConfigureAwait(false) ? body : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<bool> ReadExactlyAsync(Stream stream, byte[] buffer, Stopwatch watch, TimeSpan timeout, CancellationToken token)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            TimeSpan remaining = timeout - watch.Elapsed;
            if (remaining <= TimeSpan.Zero || token.IsCancellationRequested)
            {
                return false;
            }

            Task<int> read = stream.ReadAsync(buffer, offset, buffer.Length - offset, token);
            using (var delayCts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                Task delay = Task.Delay(remaining, delayCts.Token);
                Task done = await Task.WhenAny(read, delay).ConfigureAwait(false);
                delayCts.Cancel(); // stops the timer when the read won
                if (done != read)
                {
                    Observe(read);
                    return false;
                }
            }

            int n = await read.ConfigureAwait(false);
            if (n <= 0)
            {
                return false; // the client closed early
            }

            offset += n;
        }

        return true;
    }

    /// <summary>A read left behind ends when the caller disposes the stream; its exception is observed here.</summary>
    private static void Observe(Task task) =>
        task.ContinueWith(
            t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
}
