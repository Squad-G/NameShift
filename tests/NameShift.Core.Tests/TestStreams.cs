using System;
using System.IO;

namespace NameShift.Core.Tests;

/// <summary>A read-only, non-seekable stream over bytes: at most <c>maxPerRead</c> bytes per call, counts what it
/// served, and throws on Length, Position, Seek, SetLength and Write. It also throws when asked to read again and again
/// at the end, so a reader that loops on a zero-byte read fails the test instead of hanging it.</summary>
internal sealed class ProbeStream(byte[] data, int maxPerRead = int.MaxValue) : Stream
{
    private const int MaxReadsAtEnd = 1000;

    private int position;
    private int readsAtEnd;

    public long Served { get; private set; }

    public bool Disposed { get; private set; }

    public override bool CanRead => !Disposed;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (Disposed)
        {
            throw new ObjectDisposedException(nameof(ProbeStream));
        }

        int n = Math.Min(Math.Min(count, maxPerRead), data.Length - position);
        if (n == 0 && count > 0 && ++readsAtEnd > MaxReadsAtEnd)
        {
            throw new InvalidOperationException("ProbeStream read at its end too often: the reader does not stop.");
        }

        Buffer.BlockCopy(data, position, buffer, offset, n);
        position += n;
        Served += n;
        return n;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }
}

/// <summary>Serves a prefix once, then repeats a pattern forever; throws once it has served more than 1 MiB, so a
/// reader that does not stop fails the test instead of hanging it.</summary>
internal sealed class EndlessStream(byte[] prefix, byte[] pattern) : Stream
{
    public const long Guard = 1024 * 1024;

    public long Served { get; private set; }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (Served > Guard)
        {
            throw new InvalidOperationException("EndlessStream served more than 1 MiB: the reader does not stop.");
        }

        for (int i = 0; i < count; i++)
        {
            long at = Served + i;
            buffer[offset + i] = at < prefix.Length
                ? prefix[at]
                : pattern[(at - prefix.Length) % pattern.Length];
        }

        Served += count;
        return count;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>A stream whose Read throws the given exception.</summary>
internal sealed class FailingStream(IOException error) : Stream
{
    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => throw error;

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
