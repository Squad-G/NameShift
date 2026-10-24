namespace NameShift.Ui.Presets;

/// <summary>How a save ended.</summary>
public enum PresetSaveStatus
{
    /// <summary>The preset file was written.</summary>
    Saved,

    /// <summary>A preset with that name (ignoring case) exists and overwriting was not asked: nothing was written.</summary>
    Exists,

    /// <summary>The name is empty or white space: nothing was written.</summary>
    InvalidName,

    /// <summary>The disk refused: any old file is intact; <see cref="PresetSaveResult.SystemMessage"/> says why.</summary>
    Failed,
}
