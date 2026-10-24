namespace NameShift.Core;

/// <summary>How the Name rule rewrites the base name.</summary>
public enum NameMode
{
    /// <summary>The base name is left as it is (the panel's neutral state).</summary>
    Keep = 0,

    /// <summary>The base name is replaced by the value.</summary>
    Fixed,

    /// <summary>The base name is emptied (the extension stays).</summary>
    Remove,

    /// <summary>The base name is reversed, text element by text element.</summary>
    Reverse,
}
