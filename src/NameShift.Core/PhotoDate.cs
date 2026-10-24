using System;
using System.IO;

namespace NameShift.Core;

/// <summary>Reads the date a photo was taken (EXIF DateTimeOriginal, tag 0x9003) from the start of a JPEG stream. The
/// bytes are untrusted: every length and offset is checked before use, at most 262,144 bytes are consumed, only IFD0
/// and the Exif IFD are visited, and the only buffer sized from the file is one APP1 segment (at most 65,527
/// bytes).</summary>
public static class PhotoDate
{
    private const int Cap = 262144;
    private const int ScratchSize = 4096;
    private const int ExifHeaderLength = 6;
    private const int TiffHeaderLength = 8;
    private const int EntrySize = 12;
    private const ushort ExifPointerTag = 0x8769;
    private const ushort DateTimeOriginalTag = 0x9003;
    private const ushort TypeAscii = 2;
    private const ushort TypeLong = 4;
    private const int DateTextLength = 19;

    /// <summary>The camera's DateTimeOriginal as stored (Kind Unspecified, no time-zone conversion), or null when the
    /// bytes hold none or are damaged in any way. Reads forward from the current position, at most 262,144 bytes;
    /// never seeks, never closes the stream.</summary>
    /// <exception cref="ArgumentNullException">stream is null.</exception>
    /// <exception cref="IOException">The stream itself failed (not caught).</exception>
    public static DateTime? Read(Stream stream)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        long consumed = 0;
        byte[] small = new byte[ExifHeaderLength];
        byte[] scratch = new byte[ScratchSize];

        if (!TryReadExactly(stream, small, 0, 2, ref consumed) || small[0] != 0xFF || small[1] != 0xD8)
        {
            return null;
        }

        while (true)
        {
            if (!TryReadExactly(stream, small, 0, 1, ref consumed) || small[0] != 0xFF)
            {
                return null;
            }

            byte code;
            do
            {
                if (!TryReadExactly(stream, small, 0, 1, ref consumed))
                {
                    return null;
                }

                code = small[0];
            }
            while (code == 0xFF);

            if (code == 0x00 || code == 0xD9 || code == 0xDA)
            {
                return null;
            }

            if (code == 0x01 || (code >= 0xD0 && code <= 0xD8))
            {
                continue;
            }

            if (!TryReadExactly(stream, small, 0, 2, ref consumed))
            {
                return null;
            }

            int length = (small[0] << 8) | small[1];
            if (length < 2)
            {
                return null;
            }

            int payload = length - 2;
            if (consumed + payload > Cap)
            {
                return null;
            }

            if (code == 0xE1 && payload >= ExifHeaderLength)
            {
                if (!TryReadExactly(stream, small, 0, ExifHeaderLength, ref consumed))
                {
                    return null;
                }

                if (IsExifHeader(small))
                {
                    byte[] tiff = new byte[payload - ExifHeaderLength];
                    if (!TryReadExactly(stream, tiff, 0, tiff.Length, ref consumed))
                    {
                        return null;
                    }

                    return ParseTiff(tiff);
                }

                if (!TrySkip(stream, scratch, payload - ExifHeaderLength, ref consumed))
                {
                    return null;
                }
            }
            else if (!TrySkip(stream, scratch, payload, ref consumed))
            {
                return null;
            }
        }
    }

    private static bool IsExifHeader(byte[] b) =>
        b[0] == 0x45 && b[1] == 0x78 && b[2] == 0x69 && b[3] == 0x66 && b[4] == 0x00 && b[5] == 0x00;

    private static bool TryReadExactly(Stream stream, byte[] buffer, int offset, int count, ref long consumed)
    {
        if (count > Cap - consumed)
        {
            return false;
        }

        int got = 0;
        while (got < count)
        {
            int n = stream.Read(buffer, offset + got, count - got);
            if (n <= 0 || n > count - got)
            {
                return false;
            }

            got += n;
            consumed += n;
        }

        return true;
    }

    private static bool TrySkip(Stream stream, byte[] scratch, long count, ref long consumed)
    {
        if (count > Cap - consumed)
        {
            return false;
        }

        long remaining = count;
        while (remaining > 0)
        {
            int chunk = remaining < scratch.Length ? (int)remaining : scratch.Length;
            if (!TryReadExactly(stream, scratch, 0, chunk, ref consumed))
            {
                return false;
            }

            remaining -= chunk;
        }

        return true;
    }

    private static DateTime? ParseTiff(byte[] t)
    {
        if (t.Length < TiffHeaderLength)
        {
            return null;
        }

        bool bigEndian;
        if (t[0] == 0x49 && t[1] == 0x49)
        {
            bigEndian = false;
        }
        else if (t[0] == 0x4D && t[1] == 0x4D)
        {
            bigEndian = true;
        }
        else
        {
            return null;
        }

        if (U16(t, bigEndian, 2) != 42)
        {
            return null;
        }

        long ifd0 = U32(t, bigEndian, 4);
        if (ifd0 < TiffHeaderLength || !TryIfd(t, bigEndian, ifd0, out int ifd0Count))
        {
            return null;
        }

        long pointer = FindFirst(t, bigEndian, ifd0, ifd0Count, ExifPointerTag);
        if (pointer < 0 || U16(t, bigEndian, pointer + 2) != TypeLong || U32(t, bigEndian, pointer + 4) != 1)
        {
            return null;
        }

        long exif = U32(t, bigEndian, pointer + 8);
        if (exif < TiffHeaderLength || exif == ifd0 || !TryIfd(t, bigEndian, exif, out int exifCount))
        {
            return null;
        }

        long date = FindFirst(t, bigEndian, exif, exifCount, DateTimeOriginalTag);
        if (date < 0 || U16(t, bigEndian, date + 2) != TypeAscii)
        {
            return null;
        }

        long count = U32(t, bigEndian, date + 4);
        if (count < DateTextLength)
        {
            return null;
        }

        long offset = U32(t, bigEndian, date + 8);
        if (offset < TiffHeaderLength || !Fits(t, offset, count))
        {
            return null;
        }

        return ParseText(t, (int)offset, (int)count);
    }

    private static bool Fits(byte[] t, long offset, long size) =>
        offset >= 0 && size >= 0 && offset <= t.Length - size;

    private static bool TryIfd(byte[] t, bool bigEndian, long offset, out int count)
    {
        count = 0;
        if (!Fits(t, offset, 2))
        {
            return false;
        }

        count = U16(t, bigEndian, offset);
        return Fits(t, offset + 2, count * (long)EntrySize);
    }

    private static long FindFirst(byte[] t, bool bigEndian, long ifd, int count, ushort tag)
    {
        for (int i = 0; i < count; i++)
        {
            long entry = ifd + 2 + (i * (long)EntrySize);
            if (U16(t, bigEndian, entry) == tag)
            {
                return entry;
            }
        }

        return -1;
    }

    private static int U16(byte[] t, bool bigEndian, long position)
    {
        int p = (int)position;
        return bigEndian
            ? (t[p] << 8) | t[p + 1]
            : t[p] | (t[p + 1] << 8);
    }

    private static long U32(byte[] t, bool bigEndian, long position)
    {
        int p = (int)position;
        uint value = bigEndian
            ? ((uint)t[p] << 24) | ((uint)t[p + 1] << 16) | ((uint)t[p + 2] << 8) | t[p + 3]
            : t[p] | ((uint)t[p + 1] << 8) | ((uint)t[p + 2] << 16) | ((uint)t[p + 3] << 24);
        return value;
    }

    private static DateTime? ParseText(byte[] t, int offset, int count)
    {
        int year = Number(t, offset, 4);
        int month = Number(t, offset + 5, 2);
        int day = Number(t, offset + 8, 2);
        int hour = Number(t, offset + 11, 2);
        int minute = Number(t, offset + 14, 2);
        int second = Number(t, offset + 17, 2);
        if (year < 0 || month < 0 || day < 0 || hour < 0 || minute < 0 || second < 0)
        {
            return null;
        }

        if (t[offset + 4] != 0x3A || t[offset + 7] != 0x3A || t[offset + 10] != 0x20
            || t[offset + 13] != 0x3A || t[offset + 16] != 0x3A)
        {
            return null;
        }

        if (year < 1 || year > 9999 || month < 1 || month > 12)
        {
            return null;
        }

        if (day < 1 || day > DateTime.DaysInMonth(year, month) || hour > 23 || minute > 59 || second > 59)
        {
            return null;
        }

        for (int i = DateTextLength; i < count; i++)
        {
            byte b = t[offset + i];
            if (b != 0x00 && b != 0x20)
            {
                return null;
            }
        }

        return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);
    }

    /// <summary>The value of <paramref name="digits"/> ASCII digits at <paramref name="position"/>, or -1 when any byte
    /// is not 0x30..0x39.</summary>
    private static int Number(byte[] t, int position, int digits)
    {
        int value = 0;
        for (int i = 0; i < digits; i++)
        {
            int d = t[position + i] - 0x30;
            if (d < 0 || d > 9)
            {
                return -1;
            }

            value = (value * 10) + d;
        }

        return value;
    }
}
