namespace NameShift.Core;

/// <summary>Whether <see cref="MoveCopyRule"/> removes the taken text from where it was.</summary>
public enum MoveCopyMode
{
    /// <summary>The taken text is cut out and put somewhere else.</summary>
    Move,

    /// <summary>The taken text stays where it is and a copy is put somewhere else.</summary>
    Copy,
}
