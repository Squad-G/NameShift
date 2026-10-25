using System;
using System.IO;
using System.Security;
using System.Text;
using System.Threading;
using NameShift.Core;
using NameShift.Core.Tests;
using NameShift.Ui.Files;
using Xunit;

namespace NameShift.Ui.Tests.Files;

public sealed class DiskPhotoFileTests : IDisposable
{
    private static readonly DateTime Date1 = new DateTime(2023, 7, 1, 10, 0, 0, DateTimeKind.Unspecified);
    private static readonly string FakePath = Path.Combine(Path.GetTempPath(), "x", "p.jpg");

    private readonly string _temp = Path.Combine(Path.GetTempPath(), "NameShift.Ui.Tests", Guid.NewGuid().ToString("N"));

    public DiskPhotoFileTests()
    {
        Directory.CreateDirectory(_temp);
    }

    public void Dispose()
    {
        foreach (string path in Directory.GetFileSystemEntries(_temp, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        Directory.Delete(_temp, true);
    }

    private string MakeFile(string name, byte[] bytes)
    {
        string path = Path.Combine(_temp, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private static RenameItem ItemOf(string path)
    {
        var info = new FileInfo(path);
        return new RenameItem(info.Name, info.DirectoryName!, ItemKind.File, ItemAttributes.None, info.Length, info.CreationTime, info.LastWriteTime);
    }

    private static void AssertFound(PhotoRead read)
    {
        Assert.Equal(PhotoReadOutcome.Date, read.Outcome);
        Assert.Equal(Date1, read.Date);
    }

    // AC 2: the placeholder test

    [Theory]
    [InlineData(0x1000, true)]
    [InlineData(0x40000, true)]
    [InlineData(0x400000, true)]
    [InlineData(0x1000 | 0x400 | 0x20, true)]
    [InlineData((int)FileAttributes.Hidden, false)]
    [InlineData((int)FileAttributes.ReadOnly, false)]
    [InlineData((int)FileAttributes.System, false)]
    [InlineData((int)FileAttributes.ReparsePoint, false)]
    [InlineData((int)(FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReadOnly | FileAttributes.ReparsePoint), false)]
    [InlineData((int)FileAttributes.Normal, false)]
    [InlineData((int)FileAttributes.Archive, false)]
    public void IsPlaceholder_EachFlag(int attributes, bool expected)
    {
        Assert.Equal(expected, DiskPhotoFile.IsPlaceholder((FileAttributes)attributes));
    }

    [Fact]
    public void PlaceholderMask_IsTheThreeRawValues()
    {
        Assert.Equal(0x441000, DiskPhotoFile.PlaceholderMask);
    }

    [Theory]
    [InlineData(0x1000)]
    [InlineData(0x40000)]
    [InlineData(0x400000)]
    public void Placeholder_NeverOpened_Failed(int flag)
    {
        int opens = 0;
        var file = new DiskPhotoFile(
            _ => (FileAttributes)flag,
            _ =>
            {
                opens++;
                return new MemoryStream(JpegBuilder.ValidFile(false));
            });

        PhotoRead read = file.Read(FakePath);

        Assert.Equal(PhotoReadOutcome.Failed, read.Outcome);
        Assert.Null(read.Date);
        Assert.Equal(0, opens);
    }

    [Theory]
    [InlineData((int)FileAttributes.Hidden)]
    [InlineData((int)FileAttributes.ReadOnly)]
    [InlineData((int)FileAttributes.System)]
    [InlineData((int)FileAttributes.ReparsePoint)]
    public void HiddenReadOnlyReparseOnly_StillRead(int attributes)
    {
        int opens = 0;
        var file = new DiskPhotoFile(
            _ => (FileAttributes)attributes,
            _ =>
            {
                opens++;
                return new MemoryStream(JpegBuilder.ValidFile(false));
            });

        AssertFound(file.Read(FakePath));
        Assert.Equal(1, opens);
    }

    // AC 2: errors

    private static Exception Make(string kind) => kind switch
    {
        "io" => new IOException("io"),
        "notfound" => new FileNotFoundException("missing"),
        "dirnotfound" => new DirectoryNotFoundException("missing"),
        "toolong" => new PathTooLongException("long"),
        "access" => new UnauthorizedAccessException("denied"),
        "security" => new SecurityException("policy"),
        "notsupported" => new NotSupportedException("format"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    [Theory]
    [InlineData("io", false)]
    [InlineData("notfound", false)]
    [InlineData("dirnotfound", false)]
    [InlineData("toolong", false)]
    [InlineData("access", false)]
    [InlineData("security", false)]
    [InlineData("notsupported", false)]
    [InlineData("io", true)]
    [InlineData("notfound", true)]
    [InlineData("dirnotfound", true)]
    [InlineData("toolong", true)]
    [InlineData("access", true)]
    [InlineData("security", true)]
    [InlineData("notsupported", true)]
    public void Exceptions_GiveFailed(string kind, bool fromAttributes)
    {
        var file = new DiskPhotoFile(
            _ => fromAttributes ? throw Make(kind) : FileAttributes.Normal,
            _ => throw Make(kind));

        PhotoRead read = file.Read(FakePath);

        Assert.Equal(PhotoReadOutcome.Failed, read.Outcome);
        Assert.Null(read.Date);
    }

    [Fact]
    public void Exceptions_FromTheStream_GiveFailed()
    {
        var file = new DiskPhotoFile(_ => FileAttributes.Normal, _ => new ThrowingStream());

        Assert.Equal(PhotoReadOutcome.Failed, file.Read(FakePath).Outcome);
    }

    [Fact]
    public void OtherException_Surfaces()
    {
        var file = new DiskPhotoFile(_ => FileAttributes.Normal, _ => throw new InvalidOperationException("bug"));

        Assert.Throws<InvalidOperationException>(() => file.Read(FakePath));
    }

    [Fact]
    public void Empty_And_NotJpeg_NoDate()
    {
        var empty = new DiskPhotoFile(_ => FileAttributes.Normal, _ => new MemoryStream(new byte[0]));
        var text = new DiskPhotoFile(_ => FileAttributes.Normal, _ => new MemoryStream(Encoding.ASCII.GetBytes("hello, not a photo")));

        Assert.Equal(PhotoReadOutcome.NoDate, empty.Read(FakePath).Outcome);
        Assert.Equal(PhotoReadOutcome.NoDate, text.Read(FakePath).Outcome);
        Assert.Null(text.Read(FakePath).Date);
    }

    [Fact]
    public void Stream_IsDisposed()
    {
        var stream = new MemoryStream(JpegBuilder.ValidFile(false));
        var file = new DiskPhotoFile(_ => FileAttributes.Normal, _ => stream);

        AssertFound(file.Read(FakePath));
        Assert.False(stream.CanRead);
    }

    // AC 7: a real temp folder

    [Fact]
    public void RealFile_Date()
    {
        string path = MakeFile("p.jpg", JpegBuilder.ValidFile(false));

        AssertFound(new DiskPhotoFile().Read(path));

        RenameItem item = new PhotoDates().Fill([ItemOf(path)], CancellationToken.None)[0];
        Assert.Equal(Date1, item.Taken);
    }

    [Fact]
    public void RealFile_BigFile_DateFromItsStart()
    {
        // The date sits in the first bytes; the 3 MB after it are never needed.
        string path = MakeFile("big.jpg", JpegBuilder.Concat(JpegBuilder.ValidFile(false), new byte[3 * 1024 * 1024]));

        AssertFound(new DiskPhotoFile().Read(path));
    }

    [Fact]
    public void RealFile_HiddenReadOnly_StillReads()
    {
        string path = MakeFile("h.jpg", JpegBuilder.ValidFile(false));
        File.SetAttributes(path, FileAttributes.Hidden | FileAttributes.ReadOnly);

        AssertFound(new DiskPhotoFile().Read(path));
    }

    [Fact]
    public void RealFile_Missing_Failed()
    {
        Assert.Equal(PhotoReadOutcome.Failed, new DiskPhotoFile().Read(Path.Combine(_temp, "gone.jpg")).Outcome);
        Assert.Equal(PhotoReadOutcome.Failed, new DiskPhotoFile().Read(Path.Combine(_temp, "no", "gone.jpg")).Outcome);
    }

    [Fact]
    public void RealFile_OpenForWritingElsewhere_StillReads()
    {
        string path = MakeFile("p.jpg", JpegBuilder.ValidFile(false));

        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            AssertFound(new DiskPhotoFile().Read(path));
            Assert.Equal(Date1, new PhotoDates().Fill([ItemOf(path)], CancellationToken.None)[0].Taken);
        }
    }

    [Fact]
    public void RealFile_LockedNoSharing_NullThenRead()
    {
        string path = MakeFile("p.jpg", JpegBuilder.ValidFile(false));
        RenameItem item = ItemOf(path);
        var dates = new PhotoDates();

        var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        try
        {
            RenameItem first = dates.Fill([item], CancellationToken.None)[0];
            Assert.Null(first.Taken);
        }
        finally
        {
            locked.Dispose();
        }

        Assert.Equal(Date1, dates.Fill([item], CancellationToken.None)[0].Taken);
    }

    [Theory]
    [InlineData("j.jpg")]
    [InlineData("z.jpg")]
    [InlineData("t.jpeg")]
    public void RealFile_JunkAndEmptyAndText_Null(string name)
    {
        byte[] bytes;
        switch (name)
        {
            case "j.jpg":
                bytes = new byte[200 * 1024];
                for (int i = 0; i < bytes.Length; i++)
                {
                    bytes[i] = (byte)(((i * 7) + 3) % 251);
                }

                break;
            case "z.jpg":
                bytes = new byte[0];
                break;
            default:
                bytes = Encoding.ASCII.GetBytes("Just some text, not a photo.");
                break;
        }

        string path = MakeFile(name, bytes);

        PhotoRead read = new DiskPhotoFile().Read(path);

        Assert.Equal(PhotoReadOutcome.NoDate, read.Outcome);
        Assert.Null(new PhotoDates().Fill([ItemOf(path)], CancellationToken.None)[0].Taken);
    }

    /// <summary>A stream whose every read fails, as a disk error midway does.</summary>
    private sealed class ThrowingStream : Stream
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

        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("device error");

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
