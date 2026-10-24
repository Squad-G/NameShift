using System;

namespace NameShift.Ui.Presets;

/// <summary>A listed preset: the name inside its file, and the file. Immutable.</summary>
public sealed class PresetEntry
{
    /// <exception cref="ArgumentNullException">The name or the path is null.</exception>
    public PresetEntry(string name, string filePath)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    /// <summary>The name as typed when saved (from inside the file).</summary>
    public string Name { get; }

    /// <summary>The full path of the preset's file.</summary>
    public string FilePath { get; }
}
