namespace NameShift.Ui.Presets;

/// <summary>The result of <see cref="PresetStore.Delete"/>. Immutable.</summary>
public sealed class PresetDeleteResult
{
    private PresetDeleteResult(PresetDeleteStatus status, string? systemMessage)
    {
        Status = status;
        SystemMessage = systemMessage;
    }

    /// <summary>How the delete ended.</summary>
    public PresetDeleteStatus Status { get; }

    /// <summary>The system's message when Failed; otherwise null.</summary>
    public string? SystemMessage { get; }

    internal static PresetDeleteResult Deleted() => new PresetDeleteResult(PresetDeleteStatus.Deleted, null);

    internal static PresetDeleteResult NotFound() => new PresetDeleteResult(PresetDeleteStatus.NotFound, null);

    internal static PresetDeleteResult Failed(string message) => new PresetDeleteResult(PresetDeleteStatus.Failed, message);
}
