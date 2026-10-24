using System;
using System.Collections.Generic;

namespace NameShift.Core.Tests;

/// <summary>One IFD entry: tag, type, count and the 4-byte value (an offset or an inline number).</summary>
internal readonly struct TiffEntry(ushort tag, ushort type, uint count, uint value)
{
    public ushort Tag { get; } = tag;

    public ushort Type { get; } = type;

    public uint Count { get; } = count;

    public uint Value { get; } = value;
}

/// <summary>Builds JPEG bytes in code (no binary fixtures), valid or deliberately broken.</summary>
internal static class JpegBuilder
{
    /// <summary>The largest payload a JPEG segment can hold (a length of 0xFFFF counts its own 2 bytes).</summary>
    public const int MaxPayload = 65533;

    public static byte[] Soi => [0xFF, 0xD8];

    /// <summary>A start of scan with a few data bytes, then the end marker.</summary>
    public static byte[] Trailer => [0xFF, 0xDA, 0x00, 0x02, 0x12, 0x34, 0x56, 0xFF, 0xD9];

    public static byte[] ExifHeader => [0x45, 0x78, 0x69, 0x66, 0x00, 0x00];

    /// <summary>The bytes of an ASCII string, one byte per character.</summary>
    public static byte[] Ascii(string text)
    {
        var bytes = new byte[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] > 0x7F)
            {
                throw new ArgumentException("ASCII only; write other bytes as numbers.", nameof(text));
            }

            bytes[i] = (byte)text[i];
        }

        return bytes;
    }

    public static byte[] Concat(params byte[][] parts)
    {
        int length = 0;
        foreach (byte[] part in parts)
        {
            length += part.Length;
        }

        var result = new byte[length];
        int at = 0;
        foreach (byte[] part in parts)
        {
            Buffer.BlockCopy(part, 0, result, at, part.Length);
            at += part.Length;
        }

        return result;
    }

    /// <summary>A copy of <paramref name="length"/> bytes from <paramref name="start"/>.</summary>
    public static byte[] Slice(byte[] source, int start, int length)
    {
        var result = new byte[length];
        Buffer.BlockCopy(source, start, result, 0, length);
        return result;
    }

    /// <summary>A segment with a correct length (payload + 2).</summary>
    public static byte[] Segment(byte code, byte[] payload)
    {
        if (payload.Length > MaxPayload)
        {
            throw new ArgumentException("Payload too long for one segment.", nameof(payload));
        }

        return RawSegment(code, payload.Length + 2, payload);
    }

    /// <summary>A segment with any length field, whatever the payload really holds.</summary>
    public static byte[] RawSegment(byte code, int length, byte[] payload) =>
        Concat([0xFF, code, (byte)(length >> 8), (byte)length], payload);

    /// <summary>An APP1 segment holding <c>Exif\0\0</c> and the TIFF data.</summary>
    public static byte[] Exif(byte[] tiff) => Segment(0xE1, Concat(ExifHeader, tiff));

    /// <summary>The size of the Exif segment that <see cref="Exif"/> writes for this TIFF data.</summary>
    public static int ExifSegmentLength(byte[] tiff) => 4 + ExifHeader.Length + tiff.Length;

    /// <summary>SOI, one Exif APP1 segment, the trailer.</summary>
    public static byte[] File(byte[] tiff) => Concat(Soi, Exif(tiff), Trailer);

    /// <summary>The default valid file (date 2023:07:01 10:00:00).</summary>
    public static byte[] ValidFile(bool bigEndian) => File(new TiffBuilder(bigEndian).Build());

    /// <summary>Where the Exif segment of <see cref="ValidFile"/> ends (the byte count up to and including it).</summary>
    public static int ValidExifEnd(bool bigEndian) =>
        Soi.Length + ExifSegmentLength(new TiffBuilder(bigEndian).Build());

    public static byte[] Jfif => Segment(0xE0, Concat(Ascii("JFIF"), [0x00, 0x01, 0x02, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00]));

    public static byte[] Xmp => Segment(0xE1, Concat(Ascii("http://ns.adobe.com/xap/1.0/"), [0x00], Ascii("<x:xmpmeta/>")));

    /// <summary>APP2 filler segments of exactly <paramref name="total"/> bytes (markers and lengths included): full
    /// segments, the last one sized to fit.</summary>
    public static byte[] Fillers(int total)
    {
        var parts = new List<byte[]>();
        int remaining = total;
        while (remaining > 0)
        {
            int segment = Math.Min(remaining, 4 + MaxPayload);
            if (segment < 4 || (remaining - segment > 0 && remaining - segment < 4))
            {
                throw new ArgumentException("The filler total cannot be split into whole segments.", nameof(total));
            }

            var payload = new byte[segment - 4];
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = 0x5A;
            }

            parts.Add(Segment(0xE2, payload));
            remaining -= segment;
        }

        return Concat(parts.ToArray());
    }
}

/// <summary>Builds TIFF data (the part after <c>Exif\0\0</c>) with every field settable, so a test can break one.
/// Default layout: header (8 bytes, IFD0 at 8); IFD0 at 8 with (0x8769, LONG, 1, 26), next 0; Exif IFD at 26 with
/// (0x9003, ASCII, 20, 44), next 0; the text <c>2023:07:01 10:00:00</c> + NUL at 44; total length 64.</summary>
internal sealed class TiffBuilder
{
    private readonly List<(int Position, byte[] Bytes)> extras = [];
    private readonly List<(int Position, ushort? Count, List<TiffEntry> Entries, uint Next)> extraIfds = [];

    public TiffBuilder(bool bigEndian)
    {
        BigEndian = bigEndian;
        Order = bigEndian ? [0x4D, 0x4D] : [0x49, 0x49];
    }

    public bool BigEndian { get; }

    public byte[] Order { get; set; }

    public ushort Magic { get; set; } = 42;

    /// <summary>The IFD0 offset written in the header.</summary>
    public uint Ifd0Offset { get; set; } = 8;

    /// <summary>Where IFD0 is written (below 0: not written).</summary>
    public int Ifd0Position { get; set; } = 8;

    /// <summary>The entry count written for IFD0 (null: the number of entries).</summary>
    public ushort? Ifd0Count { get; set; }

    public List<TiffEntry> Ifd0Entries { get; } = [new(0x8769, 4, 1, 26)];

    public uint Ifd0Next { get; set; }

    /// <summary>Where the Exif IFD is written (below 0: not written).</summary>
    public int ExifPosition { get; set; } = 26;

    public List<TiffEntry> ExifEntries { get; } = [new(0x9003, 2, 20, 44)];

    public uint ExifNext { get; set; }

    /// <summary>Where the date text is written (below 0: not written).</summary>
    public int TextPosition { get; set; } = 44;

    public byte[] Text { get; set; } = JpegBuilder.Concat(JpegBuilder.Ascii("2023:07:01 10:00:00"), [0x00]);

    /// <summary>The smallest length of the TIFF data (it grows to hold everything written).</summary>
    public int MinLength { get; set; }

    /// <summary>Sets the text and the count of the first Exif entry to its length.</summary>
    public TiffBuilder WithText(byte[] text)
    {
        Text = text;
        TiffEntry e = ExifEntries[0];
        ExifEntries[0] = new TiffEntry(e.Tag, e.Type, (uint)text.Length, e.Value);
        return this;
    }

    public TiffBuilder WithText(string text) => WithText(JpegBuilder.Ascii(text));

    public TiffBuilder AddBytes(int position, byte[] bytes)
    {
        extras.Add((position, bytes));
        return this;
    }

    public TiffBuilder AddIfd(int position, uint next, params TiffEntry[] entries)
    {
        extraIfds.Add((position, null, [.. entries], next));
        return this;
    }

    public byte[] Build()
    {
        int length = Math.Max(8, MinLength);
        if (Ifd0Position >= 0)
        {
            length = Math.Max(length, Ifd0Position + IfdSize(Ifd0Entries.Count));
        }

        if (ExifPosition >= 0)
        {
            length = Math.Max(length, ExifPosition + IfdSize(ExifEntries.Count));
        }

        if (TextPosition >= 0)
        {
            length = Math.Max(length, TextPosition + Text.Length);
        }

        foreach (var (position, bytes) in extras)
        {
            length = Math.Max(length, position + bytes.Length);
        }

        foreach (var ifd in extraIfds)
        {
            length = Math.Max(length, ifd.Position + IfdSize(ifd.Entries.Count));
        }

        var t = new byte[length];
        t[0] = Order[0];
        t[1] = Order[1];
        WriteU16(t, 2, Magic);
        WriteU32(t, 4, Ifd0Offset);
        if (Ifd0Position >= 0)
        {
            WriteIfd(t, Ifd0Position, Ifd0Count, Ifd0Entries, Ifd0Next);
        }

        if (ExifPosition >= 0)
        {
            WriteIfd(t, ExifPosition, null, ExifEntries, ExifNext);
        }

        if (TextPosition >= 0)
        {
            Buffer.BlockCopy(Text, 0, t, TextPosition, Text.Length);
        }

        foreach (var ifd in extraIfds)
        {
            WriteIfd(t, ifd.Position, ifd.Count, ifd.Entries, ifd.Next);
        }

        foreach (var (position, bytes) in extras)
        {
            Buffer.BlockCopy(bytes, 0, t, position, bytes.Length);
        }

        return t;
    }

    private static int IfdSize(int entries) => 2 + (12 * entries) + 4;

    private void WriteIfd(byte[] t, int position, ushort? count, List<TiffEntry> entries, uint next)
    {
        WriteU16(t, position, count ?? (ushort)entries.Count);
        int at = position + 2;
        foreach (TiffEntry e in entries)
        {
            WriteU16(t, at, e.Tag);
            WriteU16(t, at + 2, e.Type);
            WriteU32(t, at + 4, e.Count);
            WriteU32(t, at + 8, e.Value);
            at += 12;
        }

        WriteU32(t, at, next);
    }

    private void WriteU16(byte[] t, int at, ushort value)
    {
        if (BigEndian)
        {
            t[at] = (byte)(value >> 8);
            t[at + 1] = (byte)value;
        }
        else
        {
            t[at] = (byte)value;
            t[at + 1] = (byte)(value >> 8);
        }
    }

    private void WriteU32(byte[] t, int at, uint value)
    {
        for (int i = 0; i < 4; i++)
        {
            int shift = BigEndian ? 24 - (8 * i) : 8 * i;
            t[at + i] = (byte)(value >> shift);
        }
    }
}
