using System;
using System.Collections.Generic;

namespace NameShift.Ui.Settings;

/// <summary>A window placement as saved: the position in device (screen) pixels, the size in 96-DPI logical pixels
/// (the restore size when maximised). Immutable.</summary>
public sealed class SavedWindow
{
    /// <exception cref="ArgumentOutOfRangeException">The width or the height is not above 0.</exception>
    public SavedWindow(int left, int top, int width, int height, bool maximized)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "The width must be above 0.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "The height must be above 0.");
        }

        Left = left;
        Top = top;
        Width = width;
        Height = height;
        Maximized = maximized;
    }

    /// <summary>Device pixels.</summary>
    public int Left { get; }

    /// <summary>Device pixels.</summary>
    public int Top { get; }

    /// <summary>Logical pixels.</summary>
    public int Width { get; }

    /// <summary>Logical pixels.</summary>
    public int Height { get; }

    /// <summary>True when the window was maximised (the size is then its restore size).</summary>
    public bool Maximized { get; }
}

/// <summary>One screen: its working area in device pixels and its scale (1.0 = 100 %). Immutable.</summary>
public sealed class ScreenArea
{
    /// <exception cref="ArgumentOutOfRangeException">The width, the height or the scale is not above 0.</exception>
    public ScreenArea(int left, int top, int width, int height, double scale)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "The width must be above 0.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "The height must be above 0.");
        }

        if (!(scale > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "The scale must be above 0.");
        }

        Left = left;
        Top = top;
        Width = width;
        Height = height;
        Scale = scale;
    }

    public int Left { get; }

    public int Top { get; }

    public int Width { get; }

    public int Height { get; }

    /// <summary>1.0 = 100 % (96 DPI).</summary>
    public double Scale { get; }
}

/// <summary>Where the window opens: a device-pixel rectangle, maximised or not, and the scale of the screen it lands
/// on. Immutable.</summary>
public sealed class PlacedWindow
{
    internal PlacedWindow(int left, int top, int width, int height, bool maximized, double scale)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
        Maximized = maximized;
        Scale = scale;
    }

    public int Left { get; }

    public int Top { get; }

    public int Width { get; }

    public int Height { get; }

    public bool Maximized { get; }

    /// <summary>The landing screen's scale (1.0 = 100 %).</summary>
    public double Scale { get; }
}

/// <summary>
/// Where a saved window opens again (NS-37 AC 5, 7). Pure arithmetic on plain integers: the position stays in device
/// pixels, the size is the saved logical size times the scale of the screen the window lands on (the one it overlaps
/// most), never below the minimum and never above that screen's working area. A window whose title-bar band cannot be
/// grabbed on any screen has no placement: the window then opens centred, as without a file.
/// </summary>
public static class WindowPlacement
{
    /// <summary>The title-bar band that must stay reachable: its height, in device pixels.</summary>
    public const int GrabStripHeight = 40;

    /// <summary>The least width of that band on one screen, in device pixels.</summary>
    public const int MinGrabWidth = 100;

    /// <summary>The placement, or null for "no placement" (no screen, a screen that is gone, a window out of reach).</summary>
    /// <param name="minLogicalWidth">The window's minimum width, logical pixels.</param>
    /// <param name="minLogicalHeight">The window's minimum height, logical pixels.</param>
    /// <exception cref="ArgumentNullException">The saved window, the screens or one screen is null.</exception>
    public static PlacedWindow? Fit(SavedWindow saved, IReadOnlyList<ScreenArea> screens, int minLogicalWidth, int minLogicalHeight)
    {
        if (saved == null)
        {
            throw new ArgumentNullException(nameof(saved));
        }

        if (screens == null)
        {
            throw new ArgumentNullException(nameof(screens));
        }

        ScreenArea? landing = null;
        long largest = 0;
        foreach (ScreenArea screen in screens)
        {
            if (screen == null)
            {
                throw new ArgumentNullException(nameof(screens), "A screen is null.");
            }

            long overlap = OverlapArea(
                saved.Left, saved.Top, Scale(saved.Width, screen.Scale), Scale(saved.Height, screen.Scale), screen);
            if (overlap > largest)
            {
                largest = overlap;
                landing = screen;
            }
        }

        if (landing == null)
        {
            return null;
        }

        int width = Math.Min(Math.Max(Scale(saved.Width, landing.Scale), Scale(minLogicalWidth, landing.Scale)), landing.Width);
        int height = Math.Min(Math.Max(Scale(saved.Height, landing.Scale), Scale(minLogicalHeight, landing.Scale)), landing.Height);

        bool grabbable = false;
        foreach (ScreenArea screen in screens)
        {
            Intersect(saved.Left, saved.Top, width, GrabStripHeight, screen, out long stripWidth, out long stripHeight);
            if (stripWidth >= MinGrabWidth && stripHeight > 0)
            {
                grabbable = true;
                break;
            }
        }

        return grabbable ? new PlacedWindow(saved.Left, saved.Top, width, height, saved.Maximized, landing.Scale) : null;
    }

    /// <summary>The placement to save: the position as it is (device pixels), the size divided by the window's scale.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A size or the scale is not above 0.</exception>
    public static SavedWindow ToSaved(int left, int top, int deviceWidth, int deviceHeight, double scale, bool maximized)
    {
        if (!(scale > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "The scale must be above 0.");
        }

        if (deviceWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deviceWidth), "The width must be above 0.");
        }

        if (deviceHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deviceHeight), "The height must be above 0.");
        }

        return new SavedWindow(left, top, Logical(deviceWidth, scale), Logical(deviceHeight, scale), maximized);
    }

    private static int Scale(int logical, double scale) =>
        (int)Math.Round(logical * scale, MidpointRounding.AwayFromZero);

    private static int Logical(int device, double scale) =>
        (int)Math.Round(device / scale, MidpointRounding.AwayFromZero);

    private static long OverlapArea(int left, int top, int width, int height, ScreenArea screen)
    {
        Intersect(left, top, width, height, screen, out long w, out long h);
        return w * h;
    }

    /// <summary>The size of the rectangle's intersection with the screen's working area (0 when they do not meet).
    /// In longs, so far-away coordinates never overflow.</summary>
    private static void Intersect(int left, int top, int width, int height, ScreenArea screen, out long w, out long h)
    {
        long x1 = Math.Max((long)left, screen.Left);
        long x2 = Math.Min((long)left + width, (long)screen.Left + screen.Width);
        long y1 = Math.Max((long)top, screen.Top);
        long y2 = Math.Min((long)top + height, (long)screen.Top + screen.Height);
        w = Math.Max(0, x2 - x1);
        h = Math.Max(0, y2 - y1);
    }
}
