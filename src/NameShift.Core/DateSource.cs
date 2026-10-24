namespace NameShift.Core;

/// <summary>Which of the item's dates <see cref="AutoDateRule"/> uses.</summary>
public enum DateSource
{
    /// <summary>The date the file was last written.</summary>
    Modified,

    /// <summary>The date the file was created.</summary>
    Created,
}
