using System;
using System.IO;
using Xunit;
using static NameShift.Core.Tests.JpegBuilder;

namespace NameShift.Core.Tests;

public class PhotoDateTests
{
    private const int Cap = 262144;

    private static readonly DateTime Expected = new(2023, 7, 1, 10, 0, 0);

    private static DateTime? ReadBytes(byte[] bytes) => PhotoDate.Read(new MemoryStream(bytes, writable: false));

    private static byte[] WithTiff(Action<TiffBuilder> change, bool bigEndian = false)
    {
        var b = new TiffBuilder(bigEndian);
        change(b);
        return File(b.Build());
    }

    private static byte[] Date(string text) => Ascii(text);

    // The helper must produce the documented layout, or a helper bug could hide a reader bug.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValidFile_HasTheDocumentedLayout(bool bigEndian)
    {
        byte[] tiff = new TiffBuilder(bigEndian).Build();
        byte[] file = ValidFile(bigEndian);

        Assert.Equal(64, tiff.Length);
        Assert.Equal(Ascii("2023:07:01 10:00:00"), Slice(tiff, 44, 19));
        Assert.Equal(0, tiff[63]);
        Assert.Equal(bigEndian ? (byte)0x4D : (byte)0x49, tiff[0]);
        Assert.Equal(76, ValidExifEnd(bigEndian));
        Assert.Equal(new byte[] { 0xFF, 0xD8, 0xFF, 0xE1, 0x00, 72 }, Slice(file, 0, 6));
        Assert.Equal(tiff, Slice(file, 12, 64));
    }

    // AC 1
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Read_LittleAndBigEndian(bool bigEndian)
    {
        Assert.Equal(Expected, ReadBytes(ValidFile(bigEndian)));
    }

    [Theory]
    [InlineData(19, false)]
    [InlineData(20, false)]
    [InlineData(19, true)]
    [InlineData(20, true)]
    public void Read_Count19And20(int count, bool bigEndian)
    {
        byte[] text = count == 19 ? Date("2023:07:01 10:00:00") : Concat(Date("2023:07:01 10:00:00"), [0x00]);
        byte[] file = WithTiff(b => b.WithText(text), bigEndian);

        Assert.Equal(Expected, ReadBytes(file));
    }

    // AC 2
    private static byte[] FileAfterJfifAndXmp(bool bigEndian) =>
        Concat(Soi, Jfif, Xmp, Exif(new TiffBuilder(bigEndian).Build()), Trailer);

    private static byte[] FileWithRstFillAndSoi(bool bigEndian) =>
        Concat(Soi, [0xFF, 0xD0], [0xFF, 0x01], [0xFF, 0xFF], Jfif, [0xFF, 0xD7], Soi, [0xFF, 0xFF, 0xFF],
            Exif(new TiffBuilder(bigEndian).Build()), Trailer);

    [Fact]
    public void Read_AfterJfifAndXmpSegments()
    {
        Assert.Equal(Expected, ReadBytes(FileAfterJfifAndXmp(false)));
        Assert.Equal(Expected, ReadBytes(FileAfterJfifAndXmp(true)));
    }

    [Fact]
    public void Read_RstMarkersFillBytesAndRepeatedSoi()
    {
        Assert.Equal(Expected, ReadBytes(FileWithRstFillAndSoi(false)));
        Assert.Equal(Expected, ReadBytes(FileWithRstFillAndSoi(true)));
    }

    // AC 3
    [Fact]
    public void Read_NoExifSegment_Null()
    {
        Assert.Null(ReadBytes(Concat(Soi, Jfif, Trailer)));
    }

    [Fact]
    public void Read_OnlyDateTimeInIfd0_Null()
    {
        byte[] file = WithTiff(b =>
        {
            b.Ifd0Entries.Clear();
            b.Ifd0Entries.Add(new TiffEntry(0x0132, 2, 20, 44));
            b.ExifPosition = -1;
        });

        Assert.Null(ReadBytes(file));
    }

    [Theory]
    [InlineData(0xDA)]
    [InlineData(0xD9)]
    public void Read_SosOrEoiBeforeExif_Null(byte code)
    {
        byte[] file = Concat(Soi, Jfif, [0xFF, code], Exif(new TiffBuilder(false).Build()), Trailer);

        Assert.Null(ReadBytes(file));
    }

    // AC 4
    public static TheoryData<string, byte[]> BadTexts => new()
    {
        { "zero date", Concat(Ascii("0000:00:00 00:00:00"), [0x00]) },
        { "19 NULs", new byte[19] },
        { "empty", [] },
        { "month 13 day 40", Concat(Ascii("2023:13:40 10:00:00"), [0x00]) },
        { "30 February", Concat(Ascii("2023:02:30 10:00:00"), [0x00]) },
        { "dashes", Concat(Ascii("2023-07-01 10:00:00"), [0x00]) },
        { "byte above 0x7F", Concat(Ascii("2023:07:01 10:00:0"), [0xB2, 0x00]) },
        { "Arabic-Indic digit", Concat(Ascii("2023:07:01 10:00:"), [0xD9, 0xA3, 0x00]) },
        { "x as 20th byte", Ascii("2023:07:01 10:00:00x") },
        { "hour 24", Concat(Ascii("2023:07:01 24:00:00"), [0x00]) },
        { "minute 60", Concat(Ascii("2023:07:01 10:60:00"), [0x00]) },
        { "second 60", Concat(Ascii("2023:07:01 10:00:60"), [0x00]) },
        { "day 00", Concat(Ascii("2023:07:00 10:00:00"), [0x00]) },
        { "T separator", Concat(Ascii("2023:07:01T10:00:00"), [0x00]) },
    };

    [Theory]
    [MemberData(nameof(BadTexts))]
    public void Read_BadDateText_Null(string name, byte[] text)
    {
        Assert.True(ReadBytes(WithTiff(b => b.WithText(text))) == null, name + " (II) gave a date");
        Assert.True(ReadBytes(WithTiff(b => b.WithText(text), bigEndian: true)) == null, name + " (MM) gave a date");
    }

    [Fact]
    public void Read_TrailingNulsAndSpaces_Accepted()
    {
        byte[] text = Concat(Ascii("2023:07:01 10:00:00"), [0x00, 0x20, 0x00, 0x20]);
        Assert.Equal(23, text.Length);

        Assert.Equal(Expected, ReadBytes(WithTiff(b => b.WithText(text))));
    }

    [Fact]
    public void Read_LeapDay()
    {
        Assert.Equal(new DateTime(2024, 2, 29, 23, 59, 59),
            ReadBytes(WithTiff(b => b.WithText(Concat(Ascii("2024:02:29 23:59:59"), [0x00])))));
        Assert.Null(ReadBytes(WithTiff(b => b.WithText(Concat(Ascii("2023:02:29 23:59:59"), [0x00])))));
    }

    // AC 5
    public static TheoryData<string> MarkerWalkProblems => new()
    {
        "no SOI", "length 0", "length 1", "not FF", "code 00", "length past end",
    };

    private static byte[] MarkerWalkFile(string name)
    {
        byte[] exif = Exif(new TiffBuilder(false).Build());
        return name switch
        {
            "no SOI" => Concat([0xFF, 0xD9], exif, Trailer),
            "length 0" => Concat(Soi, RawSegment(0xE0, 0, []), exif, Trailer),
            "length 1" => Concat(Soi, RawSegment(0xE0, 1, []), exif, Trailer),
            "not FF" => Concat(Soi, [0x12], exif, Trailer),
            "code 00" => Concat(Soi, [0xFF, 0x00], exif, Trailer),
            "length past end" => Concat(Soi, RawSegment(0xE0, 1000, new byte[10])),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };
    }

    [Theory]
    [MemberData(nameof(MarkerWalkProblems))]
    public void Read_MarkerWalkProblems_Null(string name)
    {
        byte[] file = MarkerWalkFile(name);
        var stream = new ProbeStream(file);

        Assert.Null(PhotoDate.Read(stream));
        Assert.True(stream.Served <= file.Length);
    }

    // AC 6
    public static TheoryData<string> TiffHeaderProblems => new()
    {
        "order XX", "magic 43", "IFD0 0", "IFD0 4", "IFD0 7", "IFD0 65", "pointer count 0", "pointer count 2",
        "Exif IFD 0", "Exif IFD 4", "Exif IFD 7", "lone APP1 of 5 bytes", "cut inside Exif header",
        "TIFF shorter than 8",
    };

    private static byte[] TiffHeaderFile(string name, bool bigEndian)
    {
        return name switch
        {
            "order XX" => WithTiff(b => b.Order = [0x58, 0x58], bigEndian),
            "magic 43" => WithTiff(b => b.Magic = 43, bigEndian),
            "IFD0 0" => WithTiff(b => b.Ifd0Offset = 0, bigEndian),
            "IFD0 4" => WithTiff(b => b.Ifd0Offset = 4, bigEndian),
            "IFD0 7" => WithTiff(b => b.Ifd0Offset = 7, bigEndian),
            "IFD0 65" => WithTiff(b => b.Ifd0Offset = 65, bigEndian),
            "pointer count 0" => WithTiff(b => b.Ifd0Entries[0] = new TiffEntry(0x8769, 4, 0, 26), bigEndian),
            "pointer count 2" => WithTiff(b => b.Ifd0Entries[0] = new TiffEntry(0x8769, 4, 2, 26), bigEndian),
            "Exif IFD 0" => WithTiff(b => b.Ifd0Entries[0] = new TiffEntry(0x8769, 4, 1, 0), bigEndian),
            "Exif IFD 4" => WithTiff(b => b.Ifd0Entries[0] = new TiffEntry(0x8769, 4, 1, 4), bigEndian),
            "Exif IFD 7" => WithTiff(b => b.Ifd0Entries[0] = new TiffEntry(0x8769, 4, 1, 7), bigEndian),
            "lone APP1 of 5 bytes" => Concat(Soi, Segment(0xE1, Ascii("Exif\0")), Trailer),
            "cut inside Exif header" => Concat(Soi, [0xFF, 0xE1, 0x00, 0x48], Ascii("Exif\0")),
            "TIFF shorter than 8" => Concat(Soi, Segment(0xE1, Concat(ExifHeader, Ascii(bigEndian ? "MM\0*" : "II*\0"))),
                Trailer),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };
    }

    [Theory]
    [MemberData(nameof(TiffHeaderProblems))]
    public void Read_TiffHeaderProblems_Null(string name)
    {
        Assert.Null(ReadBytes(TiffHeaderFile(name, false)));
        Assert.Null(ReadBytes(TiffHeaderFile(name, true)));
    }

    // AC 7
    public static TheoryData<string> HostileNumbers => new()
    {
        "count FFFFFFFF", "count 80000000", "count 18", "offset FFFFFFFF", "offset FFFFFFF0",
        "offset + count one past end", "pointer FFFFFFFF", "date type SHORT", "date count 0", "pointer type ASCII",
    };

    private static byte[] HostileFile(string name, bool bigEndian)
    {
        return name switch
        {
            "count FFFFFFFF" => Date9003(bigEndian, 2, 0xFFFFFFFF, 44),
            "count 80000000" => Date9003(bigEndian, 2, 0x80000000, 44),
            "count 18" => Date9003(bigEndian, 2, 18, 44),
            "offset FFFFFFFF" => Date9003(bigEndian, 2, 20, 0xFFFFFFFF),
            "offset FFFFFFF0" => Date9003(bigEndian, 2, 20, 0xFFFFFFF0),
            "offset + count one past end" => Date9003(bigEndian, 2, 20, 45),
            "pointer FFFFFFFF" => WithTiff(b => b.Ifd0Entries[0] = new TiffEntry(0x8769, 4, 1, 0xFFFFFFFF), bigEndian),
            "date type SHORT" => Date9003(bigEndian, 3, 20, 44),
            "date count 0" => Date9003(bigEndian, 2, 0, 44),
            "pointer type ASCII" => WithTiff(b => b.Ifd0Entries[0] = new TiffEntry(0x8769, 2, 1, 26), bigEndian),
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };
    }

    private static byte[] Date9003(bool bigEndian, ushort type, uint count, uint offset) =>
        WithTiff(b => b.ExifEntries[0] = new TiffEntry(0x9003, type, count, offset), bigEndian);

    [Theory]
    [MemberData(nameof(HostileNumbers))]
    public void Read_HostileNumbers_Null(string name)
    {
        byte[] little = HostileFile(name, false);
        byte[] big = HostileFile(name, true);
        Assert.Equal(64, little.Length - 12 - Trailer.Length);

        Assert.Null(ReadBytes(little));
        Assert.Null(ReadBytes(big));
    }

#if NET
    [Theory]
    [MemberData(nameof(HostileNumbers))]
    public void Read_HostileNumbers_AllocateLittle(string name)
    {
        byte[] file = HostileFile(name, false);
        var stream = new MemoryStream(file, writable: false);
        PhotoDate.Read(stream);
        stream.Position = 0;

        long before = GC.GetAllocatedBytesForCurrentThread();
        DateTime? result = PhotoDate.Read(stream);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Null(result);
        Assert.True(allocated < 1024 * 1024, name + ": allocated " + allocated + " bytes");
    }
#endif

    // AC 8
    public static TheoryData<string> Loops => new() { "pointer = IFD0", "next link to IFD0", "65535 entries" };

    [Theory]
    [MemberData(nameof(Loops))]
    public void Read_Loops_Null(string name)
    {
        foreach (bool bigEndian in new[] { false, true })
        {
            byte[] file = name switch
            {
                "pointer = IFD0" => WithTiff(b =>
                {
                    // IFD0 also holds a valid 0x9003: following the pointer back into IFD0 would find it.
                    b.Ifd0Entries[0] = new TiffEntry(0x8769, 4, 1, 8);
                    b.Ifd0Entries.Add(new TiffEntry(0x9003, 2, 20, 44));
                    b.ExifPosition = -1;
                }, bigEndian),
                "next link to IFD0" => WithTiff(b =>
                {
                    b.Ifd0Entries[0] = new TiffEntry(0x0132, 2, 20, 44);
                    b.Ifd0Next = 8;
                    b.ExifNext = 8;
                }, bigEndian),
                "65535 entries" => WithTiff(b =>
                {
                    b.Ifd0Count = 65535;
                    b.MinLength = 300;
                }, bigEndian),
                _ => throw new ArgumentOutOfRangeException(nameof(name)),
            };

            var stream = new ProbeStream(file);
            Assert.Null(PhotoDate.Read(stream));
            Assert.True(stream.Served <= file.Length);
        }
    }

    // AC 9
    private static byte[] FileWithFillers(int fillerBytes) =>
        Concat(Soi, Fillers(fillerBytes), Exif(new TiffBuilder(false).Build()), Trailer);

    private static int FillersToEndAtCap() => Cap - Soi.Length - ExifSegmentLength(new TiffBuilder(false).Build());

    [Fact]
    public void Read_ExifEndsExactlyAtCap_Found()
    {
        byte[] file = FileWithFillers(FillersToEndAtCap());
        Assert.Equal(Cap, file.Length - Trailer.Length);
        var stream = new ProbeStream(file);

        Assert.Equal(Expected, PhotoDate.Read(stream));
        Assert.True(stream.Served <= Cap, "served " + stream.Served);
    }

    [Fact]
    public void Read_ExifEndsOneBytePastCap_Null()
    {
        byte[] file = FileWithFillers(FillersToEndAtCap() + 1);
        Assert.Equal(Cap + 1, file.Length - Trailer.Length);
        var stream = new ProbeStream(file);

        Assert.Null(PhotoDate.Read(stream));
        Assert.True(stream.Served <= Cap, "served " + stream.Served);
    }

    [Fact]
    public void Read_ExifAfter16Fillers_Null()
    {
        byte[] file = FileWithFillers(16 * (4 + MaxPayload));
        var stream = new ProbeStream(file);

        Assert.Null(PhotoDate.Read(stream));
        Assert.True(stream.Served <= Cap, "served " + stream.Served);
    }

    // AC 10
    [Fact]
    public void Read_EndlessFillBytes_Null()
    {
        var stream = new EndlessStream(Soi, [0xFF]);

        Assert.Null(PhotoDate.Read(stream));
        Assert.True(stream.Served <= Cap, "served " + stream.Served);
    }

    [Fact]
    public void Read_EndlessEmptyApp1_Null()
    {
        var stream = new EndlessStream(Soi, [0xFF, 0xE1, 0x00, 0x02]);

        Assert.Null(PhotoDate.Read(stream));
        Assert.True(stream.Served <= Cap, "served " + stream.Served);
    }

    // AC 11, 12
    public static TheoryData<string> GoodFiles => new()
    {
        "valid II", "valid MM", "JFIF and XMP II", "JFIF and XMP MM", "RST, fill and SOI II", "RST, fill and SOI MM",
    };

    private static byte[] GoodFile(string name) => name switch
    {
        "valid II" => ValidFile(false),
        "valid MM" => ValidFile(true),
        "JFIF and XMP II" => FileAfterJfifAndXmp(false),
        "JFIF and XMP MM" => FileAfterJfifAndXmp(true),
        "RST, fill and SOI II" => FileWithRstFillAndSoi(false),
        "RST, fill and SOI MM" => FileWithRstFillAndSoi(true),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Theory]
    [MemberData(nameof(GoodFiles))]
    public void Read_OneBytePerRead(string name)
    {
        Assert.Equal(Expected, PhotoDate.Read(new ProbeStream(GoodFile(name), 1)));
    }

    [Fact]
    public void Read_EndsBeforeSegmentComplete_Null()
    {
        byte[] file = ValidFile(false);
        var stream = new ProbeStream(Slice(file, 0, ValidExifEnd(false) - 10));

        Assert.Null(PhotoDate.Read(stream));
    }

    [Theory]
    [MemberData(nameof(GoodFiles))]
    public void Read_NonSeekableStream(string name)
    {
        var stream = new ProbeStream(GoodFile(name));
        Assert.False(stream.CanSeek);
        Assert.Throws<NotSupportedException>(() => stream.Length);
        Assert.Throws<NotSupportedException>(() => stream.Position);

        Assert.Equal(Expected, PhotoDate.Read(stream));
    }

    // AC 13
    [Fact]
    public void Read_FromCurrentPosition_NotClosed_Stateless()
    {
        byte[] junk = [0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A];
        var stream = new MemoryStream(Concat(junk, ValidFile(false)), writable: false) { Position = 10 };

        Assert.Equal(Expected, PhotoDate.Read(stream));
        Assert.True(stream.CanRead);
        Assert.True(stream.Position - 10 <= Cap);

        stream.Position = 10;
        Assert.Equal(Expected, PhotoDate.Read(stream));
    }

    // AC 14
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Read_EveryTruncation(bool bigEndian)
    {
        byte[] file = ValidFile(bigEndian);
        int exifEnd = ValidExifEnd(bigEndian);

        for (int length = 0; length <= file.Length; length++)
        {
            DateTime? result = PhotoDate.Read(new ProbeStream(Slice(file, 0, length)));
            if (length < exifEnd)
            {
                Assert.True(result == null, "length " + length + " gave a date");
            }
            else
            {
                Assert.True(result == Expected, "length " + length + " gave " + result);
            }
        }
    }

    // AC 17
    [Fact]
    public void Read_DuplicateExifPointer_FirstWins()
    {
        foreach (bool bigEndian in new[] { false, true })
        {
            byte[] file = WithTiff(b =>
            {
                b.Ifd0Entries.Clear();
                b.Ifd0Entries.Add(new TiffEntry(0x8769, 4, 1, 40));
                b.Ifd0Entries.Add(new TiffEntry(0x8769, 4, 1, 80));
                b.ExifPosition = 40;
                b.ExifEntries[0] = new TiffEntry(0x9003, 2, 20, 60);
                b.TextPosition = 60;
                b.AddIfd(80, 0, new TiffEntry(0x9003, 2, 20, 100));
                b.AddBytes(100, Concat(Ascii("2021:01:02 03:04:05"), [0x00]));
            }, bigEndian);

            Assert.Equal(Expected, ReadBytes(file));
        }
    }

    [Fact]
    public void Read_DuplicateDate_FirstWins()
    {
        foreach (bool bigEndian in new[] { false, true })
        {
            byte[] file = WithTiff(b =>
            {
                b.ExifEntries.Clear();
                b.ExifEntries.Add(new TiffEntry(0x9003, 2, 20, 56));
                b.ExifEntries.Add(new TiffEntry(0x9003, 2, 20, 76));
                b.TextPosition = 56;
                b.AddBytes(76, Concat(Ascii("2021:01:02 03:04:05"), [0x00]));
            }, bigEndian);

            Assert.Equal(Expected, ReadBytes(file));
        }
    }

    [Fact]
    public void Read_InvalidFirstDate_NoSearchOn()
    {
        foreach (bool bigEndian in new[] { false, true })
        {
            byte[] file = WithTiff(b =>
            {
                b.ExifEntries.Clear();
                b.ExifEntries.Add(new TiffEntry(0x9003, 3, 20, 56));
                b.ExifEntries.Add(new TiffEntry(0x9003, 2, 20, 56));
                b.TextPosition = 56;
            }, bigEndian);

            Assert.Null(ReadBytes(file));
        }
    }

    [Fact]
    public void Read_FirstExifSegmentDecides()
    {
        var bad = new TiffBuilder(false) { Magic = 43 };
        byte[] file = Concat(Soi, Exif(bad.Build()), Exif(new TiffBuilder(false).Build()), Trailer);

        Assert.Null(ReadBytes(file));
    }

    // AC 18
    [Fact]
    public void Read_NullStream_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => PhotoDate.Read(null!));
    }

    [Fact]
    public void Read_ExactClockAndKind()
    {
        DateTime? result = ReadBytes(WithTiff(b => b.WithText(Concat(Ascii("2019:12:31 23:59:58"), [0x00])), true));

        Assert.Equal(new DateTime(2019, 12, 31, 23, 59, 58), result);
        Assert.Equal(DateTimeKind.Unspecified, result!.Value.Kind);
    }

    [Fact]
    public void Read_StreamIOException_Propagates()
    {
        var error = new IOException("disk gone");

        IOException thrown = Assert.Throws<IOException>(() => PhotoDate.Read(new FailingStream(error)));

        Assert.Same(error, thrown);
    }

    [Fact]
    public void Read_NeverDisposesTheStream()
    {
        var stream = new ProbeStream(ValidFile(false));

        Assert.Equal(Expected, PhotoDate.Read(stream));
        Assert.False(stream.Disposed);
    }
}
