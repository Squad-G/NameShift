using System;
using System.Collections.Generic;
using NameShift.Core;

namespace NameShift.Ui.Presets;

/// <summary>One rule panel in a preset: its stage (the key), its enable checkbox and its flat settings map.
/// Immutable.</summary>
public sealed class PresetPanel
{
    /// <summary>Copies the map (keys compared ordinally); a null value is kept as "".</summary>
    /// <exception cref="ArgumentOutOfRangeException">The stage is not a defined value.</exception>
    /// <exception cref="ArgumentNullException">The map is null.</exception>
    public PresetPanel(RuleStage stage, bool enabled, IReadOnlyDictionary<string, string> settings)
    {
        if (!Enum.IsDefined(typeof(RuleStage), stage))
        {
            throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown rule stage.");
        }

        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        var copy = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> pair in settings)
        {
            copy[pair.Key] = pair.Value ?? "";
        }

        Stage = stage;
        Enabled = enabled;
        Settings = copy;
    }

    /// <summary>The panel's stage: one entry per stage in a preset.</summary>
    public RuleStage Stage { get; }

    /// <summary>The panel's enable checkbox.</summary>
    public bool Enabled { get; }

    /// <summary>Every setting by property name, as <see cref="Rules.IRulePanelModel.SaveSettings"/> gives it.</summary>
    public IReadOnlyDictionary<string, string> Settings { get; }
}
