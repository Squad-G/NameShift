namespace NameShift.Ui.Presets;

/// <summary>How a delete ended.</summary>
public enum PresetDeleteStatus
{
    /// <summary>The preset's own file was deleted.</summary>
    Deleted,

    /// <summary>No readable preset of the store has that name: nothing was deleted.</summary>
    NotFound,

    /// <summary>The disk refused (read-only, in use, access denied): the file is still there;
    /// <see cref="PresetDeleteResult.SystemMessage"/> says why.</summary>
    Failed,
}
