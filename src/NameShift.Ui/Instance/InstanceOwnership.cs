namespace NameShift.Ui.Instance;

/// <summary>Whether this window owns the single instance (NS-89). Only the owner saves the settings at close (NS-37): a
/// fallback window, opened because the first one did not answer, never overwrites the first one's settings.</summary>
public sealed class InstanceOwnership
{
    public InstanceOwnership(bool ownsInstance)
    {
        OwnsInstance = ownsInstance;
    }

    /// <summary>True when this process holds the instance mutex.</summary>
    public bool OwnsInstance { get; }

    /// <summary>True when the window saves the settings at close: only the owner does.</summary>
    public bool ShouldSaveSettings => OwnsInstance;
}
