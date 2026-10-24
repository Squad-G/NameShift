using System.Drawing;
using System.Windows.Forms;
using NameShift.Ui;

namespace NameShift.App.Style;

/// <summary>Every colour of the window that is not a system colour. Read it when the colours are applied (construction, DPI or
/// system colour change), not on every paint: the accent reads the registry.</summary>
internal static class Palette
{
    // Green and red with at least 4.5:1 contrast on white; bold, the "Error: " prefix and the Status text are the non-colour cues.
    private static readonly Color Changed = Color.FromArgb(0x10, 0x7C, 0x10);
    private static readonly Color Conflict = Color.FromArgb(0xC4, 0x2B, 0x1C);

    /// <summary>The one accent: the Windows accent darkened to 4.5:1 on white, or the highlight (and always in a contrast theme).</summary>
    public static Color Accent =>
        Color.FromArgb(AccentColor.Derive(AccentSource.ReadArgb(), SystemColors.Highlight.ToArgb(), SystemInformation.HighContrast));

    /// <summary>The text colour of a changed name; the theme's text colour in a Windows contrast theme.</summary>
    public static Color ChangedText => SystemInformation.HighContrast ? SystemColors.WindowText : Changed;

    /// <summary>The text colour of a conflict; the theme's text colour in a Windows contrast theme.</summary>
    public static Color ConflictText => SystemInformation.HighContrast ? SystemColors.WindowText : Conflict;
}
