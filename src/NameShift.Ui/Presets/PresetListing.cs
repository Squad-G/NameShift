using System;
using System.Collections.Generic;
using System.Linq;

namespace NameShift.Ui.Presets;

/// <summary>The result of <see cref="PresetStore.List"/>. Immutable.</summary>
public sealed class PresetListing
{
    /// <exception cref="ArgumentNullException">The presets are null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The count is negative.</exception>
    public PresetListing(IEnumerable<PresetEntry> presets, int unusableCount)
    {
        if (presets == null)
        {
            throw new ArgumentNullException(nameof(presets));
        }

        if (unusableCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unusableCount), unusableCount, "The count is negative.");
        }

        Presets = presets.ToList().AsReadOnly();
        UnusableCount = unusableCount;
    }

    /// <summary>The readable presets, sorted by name (natural order, ignoring case).</summary>
    public IReadOnlyList<PresetEntry> Presets { get; }

    /// <summary>How many ".json" files could not be used (unreadable, or written by a newer version). They are never
    /// changed or deleted.</summary>
    public int UnusableCount { get; }
}
