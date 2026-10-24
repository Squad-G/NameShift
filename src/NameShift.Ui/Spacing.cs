namespace NameShift.Ui;

/// <summary>The gaps of the window, in 96-DPI logical pixels. Always pass them through <c>LogicalToDeviceUnits</c>.</summary>
public static class Spacing
{
    /// <summary>The tight gap: between a field and its neighbours, inside a frame.</summary>
    public const int Small = 4;

    /// <summary>The normal gap: window margin, between the path bar and the rest.</summary>
    public const int Medium = 8;

    /// <summary>The wide gap: with <see cref="Medium"/>, the room kept for the error icon.</summary>
    public const int Large = 12;
}
