using System;
using System.Runtime.InteropServices;

namespace NameShift.Ui.Folders;

/// <summary>Resolves the Downloads folder, which has no <see cref="Environment.SpecialFolder"/>.</summary>
internal static class KnownFolderPaths
{
    private static readonly Guid DownloadsId = new Guid("374DE290-123F-4565-9164-39C4925E467B");

    /// <summary>The Downloads path, or null when Windows reports none.</summary>
    public static string? Downloads()
    {
        Guid id = DownloadsId;
        IntPtr buffer = IntPtr.Zero;
        try
        {
            int hr = SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out buffer);
            return hr < 0 ? null : Marshal.PtrToStringUni(buffer);
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(buffer);
            }
        }
    }

    [DllImport("shell32.dll", PreserveSig = true)]
    private static extern int SHGetKnownFolderPath(ref Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);
}
