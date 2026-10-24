#if NETFRAMEWORK
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using NameShift.Core;
using NameShift.Disk;
using NameShift.Ui.Preview;
using Xunit;

namespace NameShift.Cmd.Tests;

/// <summary>NameShiftCmd.exe (net48 only: the net48 test build references the exe project, which copies it here). The
/// started process only runs a dry run or an input error, in a temp folder: neither writes a journal.</summary>
public sealed class ConsoleExeTests : IDisposable
{
    private const int ConsoleSubsystem = 3;
    private const int ExitTimeoutMilliseconds = 30000;

    private readonly TempFolder _temp = new TempFolder();

    public void Dispose() => _temp.Dispose();

    private static string ExePath => Path.Combine(AppContext.BaseDirectory, "NameShiftCmd.exe");

    [Fact]
    public void Exe_IsAConsoleProgram()
    {
        Assert.True(File.Exists(ExePath), ExePath);

        using var stream = new FileStream(ExePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream);
        stream.Position = 0x3C;
        int peOffset = reader.ReadInt32();
        stream.Position = peOffset;
        Assert.Equal(new byte[] { (byte)'P', (byte)'E', 0, 0 }, reader.ReadBytes(4));
        long optionalHeader = peOffset + 4 + 20;
        stream.Position = optionalHeader + 68;
        ushort subsystem = reader.ReadUInt16();

        Assert.Equal(ConsoleSubsystem, subsystem);
    }

    [Fact]
    public void NoAssemblyReferencesWinForms()
    {
        Assembly[] assemblies =
        {
            Assembly.LoadFrom(ExePath),
            typeof(CommandRunner).Assembly,
            typeof(RenamePlanner).Assembly,
            typeof(RenamePipeline).Assembly,
            typeof(JournaledRenamer).Assembly,
        };

        foreach (Assembly assembly in assemblies)
        {
            string[] references = assembly.GetReferencedAssemblies().Select(name => name.Name).ToArray();
            Assert.DoesNotContain(references, name => string.Equals(name, "System.Windows.Forms", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public async Task Exe_DryRun_WritesToStdout_ExitCode0()
    {
        _temp.File("IMG_001.JPG");
        _temp.File("IMG_002.JPG");

        var (code, output, error) = await RunExeAsync("apply \"" + _temp.Files + "\" --case lower --dry-run");

        Assert.Equal(0, code);
        Assert.Equal(new[] { "IMG_001.JPG -> img_001.JPG", "IMG_002.JPG -> img_002.JPG" }, Cmd.Lines(output));
        Assert.Equal("", error);
        Assert.Equal(new[] { "IMG_001.JPG", "IMG_002.JPG" }, _temp.FileNames());
    }

    [Fact]
    public async Task Exe_UsageError_WritesToStderr_ExitCode1()
    {
        var (code, output, error) = await RunExeAsync("apply");

        Assert.Equal(1, code);
        Assert.Equal(new[] { Strings.NoFolder + " " + Strings.Usage + " " + Strings.HelpPointer }, Cmd.Lines(error));
        Assert.Equal("", output);
    }

    [Fact]
    public async Task Exe_Version_MatchesItsProductVersion()
    {
        string expected = ProductVersion.Strip(FileVersionInfo.GetVersionInfo(ExePath).ProductVersion!);

        var (code, output, error) = await RunExeAsync("--version");

        Assert.Equal(0, code);
        Assert.Equal(new[] { "NameShift " + expected }, Cmd.Lines(output));
        Assert.Equal("", error);
    }

    [Fact]
    public async Task Exe_Help_Exit0()
    {
        var (code, output, error) = await RunExeAsync("--help");

        Assert.Equal(0, code);
        Assert.StartsWith(Strings.HelpUsageApply.Replace("\r", "").Split('\n')[0], output, StringComparison.Ordinal);
        Assert.Equal("", error);
    }

    [Fact]
    public async Task Exe_Utf8_ToPipe()
    {
        _temp.File("\u00c9COLE.TXT");
        _temp.File("\u65e5\u672c\u8a9eABC.TXT");

        var (code, output, error) = await RunExeRawAsync(ExePath, "apply \"" + _temp.Files + "\" --case lower --dry-run");

        Assert.Equal(0, code);
        AssertUtf8Lines(output);
        Assert.Empty(error);
    }

    [Fact]
    public async Task Exe_Utf8_ToFile()
    {
        _temp.File("\u00c9COLE.TXT");
        _temp.File("\u65e5\u672c\u8a9eABC.TXT");
        string target = Path.Combine(_temp.Root, "out.txt");
        string arguments = "/c \"\"" + ExePath + "\" apply \"" + _temp.Files + "\" --case lower --dry-run > \"" + target + "\"\"";

        var (code, _, _) = await RunExeRawAsync("cmd.exe", arguments);

        Assert.Equal(0, code);
        AssertUtf8Lines(File.ReadAllBytes(target));
    }

    /// <summary>The raw bytes are UTF-8 (strictly decoded), without a BOM, with the two renamed names.</summary>
    private static void AssertUtf8Lines(byte[] bytes)
    {
        Assert.True(bytes.Length > 3);
        Assert.False(bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "The output starts with a BOM.");
        string text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        string[] lines = Cmd.Lines(text).OrderBy(line => line, StringComparer.Ordinal).ToArray();

        Assert.Equal(new[] { "\u00c9COLE.TXT -> \u00e9cole.TXT", "\u65e5\u672c\u8a9eABC.TXT -> \u65e5\u672c\u8a9eabc.TXT" }, lines);
    }

    /// <summary>Starts a program in the temp folder and returns the raw bytes of both streams; on timeout kills only this
    /// process and fails.</summary>
    private async Task<(int Code, byte[] Output, byte[] Error)> RunExeRawAsync(string fileName, string arguments)
    {
        var start = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _temp.Files,
        };

        using Process process = Process.Start(start)!;
        process.StandardInput.Close();
        Task<byte[]> output = ReadAllAsync(process.StandardOutput.BaseStream);
        Task<byte[]> error = ReadAllAsync(process.StandardError.BaseStream);
        if (!process.WaitForExit(ExitTimeoutMilliseconds))
        {
            try
            {
                process.Kill();
            }
            catch (InvalidOperationException)
            {
                // It exited between the wait and the kill.
            }

            Assert.Fail(fileName + " did not exit within " + ExitTimeoutMilliseconds + " ms.");
        }

        process.WaitForExit();
        return (process.ExitCode, await output, await error);
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    /// <summary>Starts the exe in the temp folder and waits up to 30 s; on timeout kills only this process and fails.</summary>
    private async Task<(int Code, string Output, string Error)> RunExeAsync(string arguments)
    {
        var start = new ProcessStartInfo(ExePath, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _temp.Files,
        };

        using Process process = Process.Start(start)!;
        process.StandardInput.Close();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(ExitTimeoutMilliseconds))
        {
            try
            {
                process.Kill();
            }
            catch (InvalidOperationException)
            {
                // It exited between the wait and the kill.
            }

            Assert.Fail("NameShiftCmd.exe did not exit within " + ExitTimeoutMilliseconds + " ms.");
        }

        process.WaitForExit();
        return (process.ExitCode, await output, await error);
    }
}
#endif
