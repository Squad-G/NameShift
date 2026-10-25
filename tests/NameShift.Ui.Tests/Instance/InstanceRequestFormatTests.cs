using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NameShift.Ui.Instance;
using Xunit;

namespace NameShift.Ui.Tests.Instance;

/// <summary>NS-89 AC 2 and 7: the request's bytes round-trip, and anything else is refused without an exception.</summary>
public sealed class InstanceRequestFormatTests
{
    private const string Work = @"C:\Users\Some One\Docs";

    [Fact]
    public void Constants_Pinned()
    {
        Assert.Equal(1_048_576, InstanceRequestFormat.MaxBytes);
        Assert.Equal(2_000, InstanceRequestFormat.MaxPaths);
    }

    [Fact]
    public void RoundTrip()
    {
        var request = new InstanceRequest(
            new[] { @"D:\My Pics\a b.jpg", "\"quoted\"", "caf\u00E9.txt", "", "-x", @"..\up" },
            Work);

        byte[] data = InstanceRequestFormat.Serialize(request);

        Assert.True(InstanceRequestFormat.TryParse(data, out InstanceRequest? back));
        Assert.Equal(request.Args, back!.Args);
        Assert.Equal(Work, back.CurrentDirectory);
    }

    [Fact]
    public void RoundTrip_NoArgs()
    {
        byte[] data = InstanceRequestFormat.Serialize(new InstanceRequest(Array.Empty<string>(), Work));

        Assert.True(InstanceRequestFormat.TryParse(data, out InstanceRequest? back));
        Assert.Empty(back!.Args);
        Assert.Equal(Work, back.CurrentDirectory);
    }

    [Fact]
    public void Layout_Pinned()
    {
        byte[] data = InstanceRequestFormat.Serialize(new InstanceRequest(new[] { "a" }, "C:"));

        Assert.Equal(
            new byte[] { (byte)'N', (byte)'S', (byte)'R', (byte)'1', 1, 0, 0, 0, 2, 0, 0, 0, (byte)'C', (byte)':', 1, 0, 0, 0, (byte)'a' },
            data);
    }

    [Fact]
    public void Frame_IsLengthThenMessage()
    {
        var request = new InstanceRequest(new[] { "a" }, "C:");
        byte[] message = InstanceRequestFormat.Serialize(request);

        byte[] framed = InstanceRequestFormat.Frame(request);

        Assert.Equal(new byte[] { (byte)message.Length, 0, 0, 0 }, framed.Take(4).ToArray());
        Assert.Equal(message, framed.Skip(4).ToArray());
    }

    public static IEnumerable<object[]> BadMessages() =>
        new[]
        {
            "null", "empty", "wrong magic", "truncated header", "negative count", "count 2001", "negative length",
            "length past end", "trailing bytes", "invalid UTF-8", "NUL in a string", "1 MiB + 1", "missing argument",
            "lone surrogate bytes",
        }.Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(BadMessages))]
    public void TryParse_Rejects(string name)
    {
        byte[]? data = Bad(name);

        bool parsed = InstanceRequestFormat.TryParse(data, out InstanceRequest? request);

        Assert.False(parsed);
        Assert.Null(request);
    }

    [Fact]
    public void TryParse_Limits_AtCap()
    {
        string[] args = Enumerable.Range(0, 2_000).Select(i => "f" + i).ToArray();

        Assert.True(InstanceRequestFormat.TryParse(Raw(args.Length, Utf8("C:"), args.Select(Utf8).ToArray()), out InstanceRequest? many));
        Assert.Equal(args, many!.Args);

        byte[] full = InstanceRequestFormat.Serialize(OfSize(1_048_576));
        Assert.Equal(1_048_576, full.Length);
        Assert.True(InstanceRequestFormat.TryParse(full, out InstanceRequest? big));
        Assert.Equal(1_048_576 - 18, big!.Args[0].Length);
    }

    [Fact]
    public void Serialize_RefusesWhatTheWindowWouldIgnore()
    {
        string[] tooMany = Enumerable.Range(0, 2_001).Select(i => "f").ToArray();

        Assert.Throws<ArgumentException>(() => InstanceRequestFormat.Serialize(new InstanceRequest(tooMany, "C:")));
        Assert.Throws<ArgumentException>(() => InstanceRequestFormat.Serialize(OfSize(1_048_577)));
        Assert.Throws<ArgumentException>(() => InstanceRequestFormat.Serialize(new InstanceRequest(new[] { "a\0b" }, "C:")));
        Assert.Throws<ArgumentException>(() => InstanceRequestFormat.Serialize(new InstanceRequest(new[] { "\uD800" }, "C:")));
        Assert.Throws<ArgumentNullException>(() => InstanceRequestFormat.Serialize(null!));
    }

    [Fact]
    public void Request_RefusesNulls()
    {
        Assert.Throws<ArgumentNullException>(() => new InstanceRequest(null!, "C:"));
        Assert.Throws<ArgumentNullException>(() => new InstanceRequest(new[] { "a", null! }, "C:"));
        Assert.Throws<ArgumentNullException>(() => new InstanceRequest(new[] { "a" }, null!));
    }

    /// <summary>A valid request of exactly <paramref name="size"/> bytes: one argument after the 18 bytes of header,
    /// "C:" and lengths.</summary>
    private static InstanceRequest OfSize(int size) => new InstanceRequest(new[] { new string('a', size - 18) }, "C:");

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    /// <summary>Magic, count, then each string as its length and bytes; nothing is checked.</summary>
    private static byte[] Raw(int count, byte[] currentDirectory, params byte[][] args) =>
        RawWith(Encoding.ASCII.GetBytes("NSR1"), count, new[] { currentDirectory }.Concat(args).Select(b => (b.Length, b)));

    private static byte[] RawWith(byte[] magic, int count, IEnumerable<(int Length, byte[] Bytes)> strings)
    {
        using var stream = new MemoryStream();
        stream.Write(magic, 0, magic.Length);
        stream.Write(BitConverter.GetBytes(count), 0, 4); // the test machines are little-endian
        foreach ((int length, byte[] bytes) in strings)
        {
            stream.Write(BitConverter.GetBytes(length), 0, 4);
            stream.Write(bytes, 0, bytes.Length);
        }

        return stream.ToArray();
    }

    private static byte[]? Bad(string name)
    {
        byte[] valid = InstanceRequestFormat.Serialize(new InstanceRequest(new[] { "a.jpg" }, "C:"));
        switch (name)
        {
            case "null":
                return null;
            case "empty":
                return Array.Empty<byte>();
            case "wrong magic":
                return Encoding.ASCII.GetBytes("NSR2").Concat(valid.Skip(4)).ToArray();
            case "truncated header":
                return valid.Take(6).ToArray();
            case "negative count":
                return Raw(-1, Utf8("C:"));
            case "count 2001":
                return Raw(2_001, Utf8("C:"), Enumerable.Range(0, 2_001).Select(_ => Utf8("f")).ToArray());
            case "negative length":
                return RawWith(Encoding.ASCII.GetBytes("NSR1"), 0, new[] { (-1, Utf8("C:")) });
            case "length past end":
                return RawWith(Encoding.ASCII.GetBytes("NSR1"), 0, new[] { (10, Utf8("C:")) });
            case "trailing bytes":
                return valid.Concat(new byte[] { 0 }).ToArray();
            case "invalid UTF-8":
                return Raw(1, Utf8("C:"), new byte[] { 0xC3, 0x28 });
            case "NUL in a string":
                return Raw(1, Utf8("C:"), new byte[] { (byte)'a', 0, (byte)'b' });
            case "1 MiB + 1":
                // Well formed but one byte over the cap.
                return Raw(1, Utf8("C:"), Utf8(new string('a', 1_048_577 - 18)));
            case "missing argument":
                return Raw(2, Utf8("C:"), Utf8("a"));
            case "lone surrogate bytes":
                return Raw(1, Utf8("C:"), new byte[] { 0xED, 0xA0, 0x80 });
            default:
                throw new ArgumentException(name);
        }
    }
}
