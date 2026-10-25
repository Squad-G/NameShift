using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NameShift.Ui.Settings;

namespace NameShift.App;

/// <summary>The screens as <see cref="WindowPlacement.Fit"/> needs them (NS-37): each working area in device pixels
/// (the process is PerMonitorV2) and its scale, the primary screen first.</summary>
internal static class ScreenAreas
{
    private const uint MonitorDefaultToNearest = 2;
    private const int MdtEffectiveDpi = 0;

    /// <summary>The current screens. A scale that cannot be read is 1.0. Never throws (no screen: an empty list).</summary>
    public static IReadOnlyList<ScreenArea> Current()
    {
        var areas = new List<ScreenArea>();
        try
        {
            foreach (Screen screen in Screen.AllScreens.OrderBy(s => s.Primary ? 0 : 1))
            {
                Rectangle area = screen.WorkingArea;
                if (area.Width > 0 && area.Height > 0)
                {
                    areas.Add(new ScreenArea(area.Left, area.Top, area.Width, area.Height, ScaleOf(screen.Bounds)));
                }
            }
        }
        catch (Exception)
        {
            // No placement is better than a start that fails: the window then opens centred.
        }

        return areas;
    }

    private static double ScaleOf(Rectangle bounds)
    {
        try
        {
            var centre = new NativePoint { X = bounds.Left + (bounds.Width / 2), Y = bounds.Top + (bounds.Height / 2) };
            IntPtr monitor = MonitorFromPoint(centre, MonitorDefaultToNearest);
            if (monitor != IntPtr.Zero && GetDpiForMonitor(monitor, MdtEffectiveDpi, out uint dpiX, out _) == 0 && dpiX > 0)
            {
                return dpiX / 96.0;
            }
        }
        catch (Exception)
        {
            // shcore.dll missing or the call failing: 100 %.
        }

        return 1.0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint pt, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);
}
