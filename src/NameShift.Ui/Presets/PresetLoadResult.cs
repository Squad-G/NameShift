namespace NameShift.Ui.Presets;

/// <summary>The result of <see cref="PresetStore.Load"/>: the preset, or not found. Immutable.</summary>
public sealed class PresetLoadResult
{
    private PresetLoadResult(Preset? preset)
    {
        Preset = preset;
    }

    /// <summary>True when a readable preset with that name (ignoring case) is in the store.</summary>
    public bool Found => Preset != null;

    /// <summary>The preset when found; otherwise null.</summary>
    public Preset? Preset { get; }

    internal static PresetLoadResult Loaded(Preset preset) => new PresetLoadResult(preset);

    internal static PresetLoadResult NotFound() => new PresetLoadResult(null);
}
