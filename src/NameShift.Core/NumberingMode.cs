namespace NameShift.Core;

/// <summary>Where the number goes in the base name.</summary>
public enum NumberingMode
{
    /// <summary>Before the base name: number, separator, name.</summary>
    Prefix,

    /// <summary>After the base name, before the extension: name, separator, number.</summary>
    Suffix,

    /// <summary>Inside the base name at a 1-based position (the TextPosition rule): number, then separator.</summary>
    Insert,
}
