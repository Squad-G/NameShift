using System.Globalization;

namespace NameShift.Ui.Rules;

/// <summary>The arrow button of a rule panel's title line (NS-83): its glyph and its accessible name.</summary>
public static class PanelToggleText
{
    /// <summary>The glyph: a right arrow when collapsed (a click expands), a down arrow when open.</summary>
    public static string Arrow(bool collapsed) => collapsed ? Strings.PanelExpandGlyph : Strings.PanelCollapseGlyph;

    /// <summary>The accessible name, e.g. "Expand Replace" when collapsed, "Collapse Replace" when open.</summary>
    public static string Name(string title, bool collapsed, CultureInfo culture) =>
        string.Format(
            culture,
            collapsed ? Strings.PanelExpandName : Strings.PanelCollapseName,
            RulePanelText.PlainTitle(title));
}
