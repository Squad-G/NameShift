using System;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace NameShift.App.Style;

/// <summary>Reads the Windows accent colour. Read only: one registry value, never a write, never another key.</summary>
internal static class AccentSource
{
    /// <summary>The DWM <c>ColorizationColor</c> as ARGB, or 0 when it is missing or unreadable (0 means "use the highlight").</summary>
    public static int ReadArgb()
    {
        try
        {
            using (RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
            {
                return key?.GetValue("ColorizationColor") is int value ? value : 0;
            }
        }
        catch (SecurityException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
        catch (IOException)
        {
            return 0;
        }
    }
}
