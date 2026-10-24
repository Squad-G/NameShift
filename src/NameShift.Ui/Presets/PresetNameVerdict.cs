namespace NameShift.Ui.Presets;

/// <summary>What <see cref="PresetNames.Check"/> says about a typed preset name.</summary>
public enum PresetNameVerdict
{
    /// <summary>The trimmed name can be saved.</summary>
    Ok,

    /// <summary>Nothing, or only white space.</summary>
    Empty,

    /// <summary>More than <see cref="PresetNames.MaxLength"/> characters after trimming.</summary>
    TooLong,
}
