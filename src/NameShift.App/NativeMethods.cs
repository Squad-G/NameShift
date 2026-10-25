using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace NameShift.App;

/// <summary>The Windows calls of the single instance (NS-89) that .NET Framework 4.8 does not wrap: a pipe created with
/// FILE_FLAG_FIRST_PIPE_INSTANCE (no PipeOptions value for it on net48), the pipe server's process id and that
/// process's user, and the foreground hand-over.</summary>
internal static class NativeMethods
{
    internal const string LocalPipePrefix = @"\\.\pipe\";

    internal const uint PipeAccessDuplex = 0x00000003;
    internal const uint FileFlagFirstPipeInstance = 0x00080000;
    internal const uint FileFlagOverlapped = 0x40000000;
    internal const uint PipeTypeByte = 0x00000000;
    internal const uint PipeReadModeByte = 0x00000000;
    internal const uint PipeWait = 0x00000000;
    internal const uint PipeRejectRemoteClients = 0x00000008;

    /// <summary>Only one instance is ever listening; closed instances linger while their client keeps its handle.</summary>
    internal const uint PipeUnlimitedInstances = 255;

    internal const uint ProcessQueryLimitedInformation = 0x00001000;
    internal const uint TokenQuery = 0x0008;
    internal const int TokenUserClass = 1;

    [StructLayout(LayoutKind.Sequential)]
    internal struct SecurityAttributes
    {
        public int Length;
        public IntPtr SecurityDescriptor;
        public int InheritHandle;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateNamedPipeW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern SafePipeHandle CreateNamedPipe(
        string name,
        uint openMode,
        uint pipeMode,
        uint maxInstances,
        uint outBufferSize,
        uint inBufferSize,
        uint defaultTimeout,
        ref SecurityAttributes securityAttributes);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern SafeProcessHandle OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool OpenProcessToken(SafeProcessHandle process, uint desiredAccess, out SafeAccessTokenHandle token);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetTokenInformation(SafeAccessTokenHandle token, int informationClass, IntPtr information, int length, out int returnLength);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AllowSetForegroundWindow(uint processId);

    /// <summary>False while a modal dialog (a form or a message box) has disabled the window.</summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWindowEnabled(IntPtr window);
}
