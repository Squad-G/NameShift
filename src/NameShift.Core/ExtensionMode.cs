namespace NameShift.Core;

/// <summary>How the Extension rule rewrites the last extension.</summary>
public enum ExtensionMode
{
    /// <summary>The extension is left as it is.</summary>
    Same = 0,

    /// <summary>The extension in lower case.</summary>
    Lower,

    /// <summary>The extension in upper case.</summary>
    Upper,

    /// <summary>The extension is replaced by the value (added when there is none).</summary>
    Fixed,

    /// <summary>The value is added after the whole name, as a new extension.</summary>
    Extra,

    /// <summary>The last extension is removed.</summary>
    Remove,
}
