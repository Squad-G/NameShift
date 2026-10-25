using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NameShift.Ui.Instance;
using Xunit;

namespace NameShift.Ui.Tests.Instance;

/// <summary>NS-89 AC 7: one framed message within one timeout; a silent or slow client gives null and never hangs.
/// In-memory streams only: no real pipe.</summary>
public sealed class PipeRequestReaderTests
{
    private static readonly TimeSpan WindowTimeout = TimeSpan.FromSeconds(2);

    private static readonly InstanceRequest Request = new InstanceRequest(new[] { @"D:\Pics\a.jpg", "b.jpg" }, @"C:\Work");

    [Fact]
    public async Task MemoryStream_ReadsAll()
    {
        using var stream = new MemoryStream(InstanceRequestFormat.Frame(Request));

        byte[]? data = await PipeRequestReader.ReadAsync(stream, WindowTimeout, CancellationToken.None);

        Assert.Equal(InstanceRequestFormat.Serialize(Request), data);
        Assert.True(InstanceRequestFormat.TryParse(data, out InstanceRequest? back));
        Assert.Equal(Request.Args, back!.Args);
        Assert.Equal(Request.CurrentDirectory, back.CurrentDirectory);
    }

    [Fact]
    public async Task LengthPrefixed_ReadsExactlyTheMessage()
    {
        byte[] framed = InstanceRequestFormat.Frame(Request);
        using var stream = new MemoryStream(framed.Concat(new byte[] { 9, 9, 9 }).ToArray());

        byte[]? data = await PipeRequestReader.ReadAsync(stream, WindowTimeout, CancellationToken.None);

        Assert.Equal(InstanceRequestFormat.Serialize(Request), data);
        Assert.Equal(framed.Length, stream.Position); // nothing read past the message: no end of stream needed
    }

    [Fact]
    public async Task LengthPrefixed_OneByteAtATime()
    {
        using var stream = new TrickleStream(InstanceRequestFormat.Frame(Request));

        byte[]? data = await PipeRequestReader.ReadAsync(stream, WindowTimeout, CancellationToken.None);

        Assert.Equal(InstanceRequestFormat.Serialize(Request), data);
    }

    [Fact]
    public async Task Overflow_Null()
    {
        byte[] header = new byte[4];
        InstanceRequestFormat.PutInt32(header, 0, 1_048_577);
        using var stream = new MemoryStream(header);

        Assert.Null(await PipeRequestReader.ReadAsync(stream, WindowTimeout, CancellationToken.None));
        Assert.Equal(4, stream.Position); // the body is never read
    }

    [Fact]
    public async Task NegativeLength_Null()
    {
        using var stream = new MemoryStream(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 1, 2, 3 });

        Assert.Null(await PipeRequestReader.ReadAsync(stream, WindowTimeout, CancellationToken.None));
    }

    [Fact]
    public async Task EndsEarly_Null()
    {
        byte[] framed = InstanceRequestFormat.Frame(Request);
        using var shortHeader = new MemoryStream(framed.Take(2).ToArray());
        using var shortBody = new MemoryStream(framed.Take(framed.Length - 1).ToArray());

        Assert.Null(await PipeRequestReader.ReadAsync(shortHeader, WindowTimeout, CancellationToken.None));
        Assert.Null(await PipeRequestReader.ReadAsync(shortBody, WindowTimeout, CancellationToken.None));
    }

    [Fact]
    public async Task ZeroLength_ReadsEmpty()
    {
        using var stream = new MemoryStream(new byte[4]);

        byte[]? data = await PipeRequestReader.ReadAsync(stream, WindowTimeout, CancellationToken.None);

        Assert.NotNull(data);
        Assert.Empty(data);
        Assert.False(InstanceRequestFormat.TryParse(data, out _));
    }

    [Fact]
    public async Task NeverSends_TimesOut()
    {
        using var stream = new SilentStream(Array.Empty<byte>());
        var watch = Stopwatch.StartNew();

        Task<byte[]?> read = PipeRequestReader.ReadAsync(stream, WindowTimeout, CancellationToken.None);
        Task done = await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(10)));

        Assert.Same(read, done);
        Assert.Null(await read);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10));
        Assert.True(watch.Elapsed >= WindowTimeout - TimeSpan.FromMilliseconds(100), watch.Elapsed.ToString());
    }

    [Fact]
    public async Task SendsHalf_TimesOut()
    {
        byte[] framed = InstanceRequestFormat.Frame(Request);
        using var stream = new SilentStream(framed.Take(framed.Length / 2).ToArray());

        Task<byte[]?> read = PipeRequestReader.ReadAsync(stream, TimeSpan.FromMilliseconds(300), CancellationToken.None);
        Task done = await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(10)));

        Assert.Same(read, done);
        Assert.Null(await read);
    }

    [Fact]
    public async Task Cancelled_Null()
    {
        using var stream = new SilentStream(Array.Empty<byte>());
        using var cts = new CancellationTokenSource();

        Task<byte[]?> read = PipeRequestReader.ReadAsync(stream, TimeSpan.FromSeconds(30), cts.Token);
        cts.Cancel();
        Task done = await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(10)));

        Assert.Same(read, done);
        Assert.Null(await read);
    }

    [Fact]
    public async Task Throwing_Null()
    {
        using var stream = new ThrowingStream();

        Assert.Null(await PipeRequestReader.ReadAsync(stream, WindowTimeout, CancellationToken.None));
    }

    [Fact]
    public void BadArguments_Throw()
    {
        using var stream = new MemoryStream();

        Assert.Throws<ArgumentNullException>(() => { _ = PipeRequestReader.ReadAsync(null!, WindowTimeout, CancellationToken.None); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = PipeRequestReader.ReadAsync(stream, TimeSpan.Zero, CancellationToken.None); });
    }

    /// <summary>A read-only stream over bytes.</summary>
    private abstract class ReadOnlyStream : Stream
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

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    /// <summary>Gives one byte per read.</summary>
    private sealed class TrickleStream : ReadOnlyStream
    {
        private readonly byte[] _data;
        private int _position;

        public TrickleStream(byte[] data) => _data = data;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= _data.Length || count == 0)
            {
                return 0;
            }

            buffer[offset] = _data[_position++];
            return 1;
        }
    }

    /// <summary>Gives its bytes, then never completes a read again, and ignores the token (as a pipe read on net48
    /// may). Disposing it ends the pending read.</summary>
    private sealed class SilentStream : ReadOnlyStream
    {
        private readonly byte[] _data;
        private readonly TaskCompletionSource<int> _never = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _position;

        public SilentStream(byte[] data) => _data = data;

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException("async only");

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_position < _data.Length)
            {
                int n = Math.Min(count, _data.Length - _position);
                Array.Copy(_data, _position, buffer, offset, n);
                _position += n;
                return Task.FromResult(n);
            }

            return _never.Task;
        }

        protected override void Dispose(bool disposing)
        {
            _never.TrySetException(new ObjectDisposedException(nameof(SilentStream)));
            base.Dispose(disposing);
        }
    }

    private sealed class ThrowingStream : ReadOnlyStream
    {
        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("broken pipe");

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Task.FromException<int>(new IOException("broken pipe"));
    }
}
