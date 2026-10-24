namespace NameShift.Ui.Presets;

/// <summary>One entry of the Load and Delete sub-menus.</summary>
public sealed class PresetMenuItem
{
    internal PresetMenuItem(string text, string? presetName, bool enabled)
    {
        Text = text;
        PresetName = presetName;
        Enabled = enabled;
    }

    /// <summary>Ready for the menu: the name with its ampersands doubled, or the Loading or No saved presets text.</summary>
    public string Text { get; }

    /// <summary>The listed (trimmed) name; null for a placeholder.</summary>
    public string? PresetName { get; }

    /// <summary>False for a placeholder.</summary>
    public bool Enabled { get; }
}
