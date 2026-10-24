namespace NameShift.Ui.Presets;

/// <summary>The result of <see cref="PresetStore.Save"/>. Immutable.</summary>
public sealed class PresetSaveResult
{
    private PresetSaveResult(PresetSaveStatus status, string? filePath, string? systemMessage)
    {
        Status = status;
        FilePath = filePath;
        SystemMessage = systemMessage;
    }

    /// <summary>How the save ended.</summary>
    public PresetSaveStatus Status { get; }

    /// <summary>The file written when Saved; otherwise null.</summary>
    public string? FilePath { get; }

    /// <summary>The system's message when Failed; otherwise null.</summary>
    public string? SystemMessage { get; }

    internal static PresetSaveResult Saved(string filePath) => new PresetSaveResult(PresetSaveStatus.Saved, filePath, null);

    internal static PresetSaveResult Exists() => new PresetSaveResult(PresetSaveStatus.Exists, null, null);

    internal static PresetSaveResult InvalidName() => new PresetSaveResult(PresetSaveStatus.InvalidName, null, null);

    internal static PresetSaveResult Failed(string message) => new PresetSaveResult(PresetSaveStatus.Failed, null, message);
}
