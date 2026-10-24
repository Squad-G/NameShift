namespace NameShift.Core;

/// <summary>Which end of the base name <see cref="MoveCopyRule"/> takes its characters from.</summary>
public enum MoveCopySource
{
    /// <summary>The first characters of the base name.</summary>
    Start,

    /// <summary>The last characters of the base name.</summary>
    End,
}
