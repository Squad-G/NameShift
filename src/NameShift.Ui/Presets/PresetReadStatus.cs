namespace NameShift.Ui.Presets;

/// <summary>How a preset file read.</summary>
public enum PresetReadStatus
{
    /// <summary>A preset of a known version, with a name.</summary>
    Ok,

    /// <summary>Not JSON, not a preset object, too large, or without a name.</summary>
    Unreadable,

    /// <summary>A preset written by a newer NameShift (a version above <see cref="PresetJson.Version"/>).</summary>
    UnsupportedVersion,
}
