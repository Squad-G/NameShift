using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NameShift.Ui.Instance;

/// <summary>
/// The bytes of an <see cref="InstanceRequest"/> on the pipe (NS-89). Pipe input is untrusted: <see cref="TryParse"/>
/// reads only this fixed layout, with hard caps, and never throws. Layout (little-endian): the magic <c>NSR1</c>, an
/// <c>int32</c> count of arguments, then per string (the current directory first, then each argument) an <c>int32</c>
/// byte length and that many bytes of strict UTF-8. On the pipe the message is framed by an <c>int32</c> length first
/// (<see cref="Frame"/>, read back by <see cref="PipeRequestReader"/>).
/// </summary>
public static class InstanceRequestFormat
{
    /// <summary>The largest message read, in bytes (1 MiB).</summary>
    public const int MaxBytes = 1_048_576;

    /// <summary>The most arguments in one message (Windows gives a command line at most 32,000 characters).</summary>
    public const int MaxPaths = 2_000;

    private static readonly byte[] Magic = { (byte)'N', (byte)'S', (byte)'R', (byte)'1' };

    private static readonly UTF8Encoding Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>The message bytes of a request.</summary>
    /// <exception cref="ArgumentNullException">request is null.</exception>
    /// <exception cref="ArgumentException">The request holds more than <see cref="MaxPaths"/> arguments, a string with
    /// a NUL or a lone surrogate, or comes to more than <see cref="MaxBytes"/> bytes: the window would refuse it.</exception>
    public static byte[] Serialize(InstanceRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.Args.Count > MaxPaths)
        {
            throw new ArgumentException("Too many arguments.", nameof(request));
        }

        using var stream = new MemoryStream();
        stream.Write(Magic, 0, Magic.Length);
        WriteInt32(stream, request.Args.Count);
        WriteString(stream, request.CurrentDirectory);
        foreach (string arg in request.Args)
        {
            WriteString(stream, arg);
            if (stream.Length > MaxBytes)
            {
                throw new ArgumentException("The request is too long.", nameof(request));
            }
        }

        if (stream.Length > MaxBytes)
        {
            throw new ArgumentException("The request is too long.", nameof(request));
        }

        return stream.ToArray();
    }

    /// <summary>The message as sent on the pipe: its <c>int32</c> length, then the message.</summary>
    /// <exception cref="ArgumentNullException">request is null.</exception>
    /// <exception cref="ArgumentException">See <see cref="Serialize"/>.</exception>
    public static byte[] Frame(InstanceRequest request)
    {
        byte[] message = Serialize(request);
        var framed = new byte[4 + message.Length];
        PutInt32(framed, 0, message.Length);
        Buffer.BlockCopy(message, 0, framed, 4, message.Length);
        return framed;
    }

    /// <summary>Reads a message. False, with no exception, for anything but one well-formed message within the
    /// caps: null, too long, truncated, a wrong magic, a count out of range, a length out of range, bytes left over,
    /// invalid UTF-8 or a NUL in a string.</summary>
    public static bool TryParse(byte[]? data, out InstanceRequest? request)
    {
        request = null;
        try
        {
            if (data == null || data.Length > MaxBytes || data.Length < Magic.Length + 4)
            {
                return false;
            }

            for (int i = 0; i < Magic.Length; i++)
            {
                if (data[i] != Magic[i])
                {
                    return false;
                }
            }

            int position = Magic.Length;
            int count = GetInt32(data, position);
            position += 4;
            if (count < 0 || count > MaxPaths)
            {
                return false;
            }

            if (!TryReadString(data, ref position, out string currentDirectory))
            {
                return false;
            }

            var args = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                if (!TryReadString(data, ref position, out string arg))
                {
                    return false;
                }

                args.Add(arg);
            }

            if (position != data.Length)
            {
                return false;
            }

            request = new InstanceRequest(args, currentDirectory);
            return true;
        }
        catch (Exception)
        {
            // Never throws on untrusted input.
            request = null;
            return false;
        }
    }

    internal static int GetInt32(byte[] data, int offset) =>
        data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24);

    internal static void PutInt32(byte[] data, int offset, int value)
    {
        data[offset] = (byte)value;
        data[offset + 1] = (byte)(value >> 8);
        data[offset + 2] = (byte)(value >> 16);
        data[offset + 3] = (byte)(value >> 24);
    }

    private static bool TryReadString(byte[] data, ref int position, out string value)
    {
        value = "";
        if (data.Length - position < 4)
        {
            return false;
        }

        int length = GetInt32(data, position);
        position += 4;
        if (length < 0 || length > data.Length - position)
        {
            return false;
        }

        string text;
        try
        {
            text = Strict.GetString(data, position, length);
        }
        catch (ArgumentException)
        {
            return false; // invalid UTF-8 (DecoderFallbackException is an ArgumentException)
        }

        if (text.IndexOf('\0') >= 0)
        {
            return false;
        }

        position += length;
        value = text;
        return true;
    }

    private static void WriteInt32(Stream stream, int value)
    {
        var bytes = new byte[4];
        PutInt32(bytes, 0, value);
        stream.Write(bytes, 0, 4);
    }

    private static void WriteString(Stream stream, string text)
    {
        if (text.IndexOf('\0') >= 0)
        {
            throw new ArgumentException("A string holds a NUL.", nameof(text));
        }

        byte[] bytes;
        try
        {
            bytes = Strict.GetBytes(text);
        }
        catch (EncoderFallbackException e)
        {
            throw new ArgumentException("A string is not valid Unicode.", nameof(text), e);
        }

        WriteInt32(stream, bytes.Length);
        stream.Write(bytes, 0, bytes.Length);
    }
}
