using System;

namespace NameShift.Ui;

/// <summary>Picks the one accent colour of the window from the Windows accent: pure arithmetic on ARGB integers.</summary>
public static class AccentColor
{
    /// <summary>The minimum contrast ratio of the accent against white (WCAG AA for body text).</summary>
    public const double MinimumContrast = 4.5;

    private const int MaxSteps = 64;
    private const int White = unchecked((int)0xFFFFFFFF);

    /// <summary>WCAG 2 contrast ratio of two opaque ARGB colours (alpha ignored): (L1 + 0.05) / (L2 + 0.05), lighter on top.</summary>
    public static double ContrastRatio(int a, int b)
    {
        double la = Luminance(a);
        double lb = Luminance(b);
        double lighter = Math.Max(la, lb);
        double darker = Math.Min(la, lb);
        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>The colour to draw accent text and lines in: the highlight in high contrast or when the accent is 0;
    /// else the accent, darkened in steps keeping its hue until its contrast with white is at least 4.5:1; alpha 0xFF.</summary>
    public static int Derive(int windowsAccentArgb, int highlightArgb, bool highContrast)
    {
        if (highContrast || windowsAccentArgb == 0)
        {
            return highlightArgb;
        }

        int r = (windowsAccentArgb >> 16) & 0xFF;
        int g = (windowsAccentArgb >> 8) & 0xFF;
        int b = windowsAccentArgb & 0xFF;
        int color = Pack(r, g, b);
        for (int step = 0; step < MaxSteps && ContrastRatio(color, White) < MinimumContrast; step++)
        {
            r = r * 9 / 10;
            g = g * 9 / 10;
            b = b * 9 / 10;
            color = Pack(r, g, b);
        }

        return color;
    }

    private static int Pack(int r, int g, int b) => unchecked((int)0xFF000000) | (r << 16) | (g << 8) | b;

    private static double Luminance(int argb)
    {
        double r = Channel((argb >> 16) & 0xFF);
        double g = Channel((argb >> 8) & 0xFF);
        double b = Channel(argb & 0xFF);
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private static double Channel(int value)
    {
        double c = value / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
