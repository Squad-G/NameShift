using System;
using System.Globalization;
using System.IO;
using System.Text;
using Xunit;
using static NameShift.Core.Tests.JpegBuilder;

namespace NameShift.Core.Tests;

public class PhotoDateFuzzTests
{
    private const int Cap = 262144;

    // AC 15
    public static TheoryData<string> NotJpegs => new() { "empty", "PNG header", "ASCII text", "1000 random bytes" };

    private static byte[] NotJpeg(string name)
    {
        switch (name)
        {
            case "empty":
                return [];
            case "PNG header":
                return Concat([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], new byte[20]);
            case "ASCII text":
                return Ascii("This is a plain text file named photo.jpg.\r\n");
            case "1000 random bytes":
                var bytes = new byte[1000];
                new Random(2701).NextBytes(bytes);
                return bytes;
            default:
                throw new ArgumentOutOfRangeException(nameof(name));
        }
    }

    [Theory]
    [MemberData(nameof(NotJpegs))]
    public void Read_NotAJpeg_Null(string name)
    {
        byte[] bytes = NotJpeg(name);
        var stream = new ProbeStream(bytes);

        Assert.Null(PhotoDate.Read(stream));
        Assert.True(stream.Served <= bytes.Length);
    }

    // AC 16
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Read_EveryByteReplaced(bool bigEndian)
    {
        byte[] valid = ValidFile(bigEndian);
        byte[] values = [0x00, 0xFF, 0x80];

        for (int position = 0; position < valid.Length; position++)
        {
            foreach (byte value in values)
            {
                byte[] file = (byte[])valid.Clone();
                file[position] = value;
                string where = "bigEndian " + bigEndian + ", position " + position + ", value 0x"
                    + value.ToString("X2", CultureInfo.InvariantCulture);

                DateTime? result = ReadOrFail(new ProbeStream(file), where);

                Assert.True(result == null || result.Value.Kind == DateTimeKind.Unspecified, where + ": wrong Kind");
            }
        }
    }

    [Theory]
    [InlineData(false, 2702)]
    [InlineData(true, 2703)]
    public void Read_RandomMutations(bool bigEndian, int seed)
    {
        byte[] valid = ValidFile(bigEndian);
        var random = new Random(seed);

        for (int iteration = 0; iteration < 5000; iteration++)
        {
            byte[] file = (byte[])valid.Clone();
            int changes = random.Next(1, 5);
            var where = new StringBuilder("seed " + seed + ", iteration " + iteration + ", positions");
            for (int i = 0; i < changes; i++)
            {
                int position = random.Next(file.Length);
                byte value = (byte)random.Next(256);
                file[position] = value;
                where.Append(' ').Append(position).Append("=0x").Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }

            int maxPerRead = random.Next(1, 65);
            where.Append(", maxPerRead ").Append(maxPerRead);
            var stream = new ProbeStream(file, maxPerRead);

            DateTime? result = ReadOrFail(stream, where.ToString());

            Assert.True(stream.Served <= Cap, where + ": served " + stream.Served);
            Assert.True(result == null || result.Value.Kind == DateTimeKind.Unspecified, where + ": wrong Kind");
        }
    }

    private static DateTime? ReadOrFail(Stream stream, string where)
    {
        try
        {
            return PhotoDate.Read(stream);
        }
        catch (Exception ex)
        {
            Assert.Fail(where + ": " + ex);
            return null;
        }
    }
}
