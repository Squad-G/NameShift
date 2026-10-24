namespace NameShift.Core;

/// <summary>Where <see cref="MoveCopyRule"/> puts the taken text, in what is left of the base name.</summary>
public enum MoveCopyTarget
{
    /// <summary>In front of the remainder.</summary>
    Start,

    /// <summary>After the remainder.</summary>
    End,

    /// <summary>At a 1-based position inside the remainder.</summary>
    Position,
}
