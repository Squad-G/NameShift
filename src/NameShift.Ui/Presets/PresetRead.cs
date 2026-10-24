namespace NameShift.Ui.Presets;

/// <summary>The result of reading a preset file's content. Immutable.</summary>
public sealed class PresetRead
{
    private PresetRead(PresetReadStatus status, Preset? preset)
    {
        Status = status;
        Preset = preset;
    }

    /// <summary>How the content read.</summary>
    public PresetReadStatus Status { get; }

    /// <summary>The preset when <see cref="Status"/> is Ok; otherwise null.</summary>
    public Preset? Preset { get; }

    internal static PresetRead Ok(Preset preset) => new PresetRead(PresetReadStatus.Ok, preset);

    internal static PresetRead Unreadable() => new PresetRead(PresetReadStatus.Unreadable, null);

    internal static PresetRead UnsupportedVersion() => new PresetRead(PresetReadStatus.UnsupportedVersion, null);
}
