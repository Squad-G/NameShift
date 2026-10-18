using System;

namespace NameShift.Core;

/// <summary>The Windows attributes the engine cares about.</summary>
[Flags]
public enum ItemAttributes
{
    /// <summary>No attribute.</summary>
    None = 0,

    /// <summary>The item is read-only.</summary>
    ReadOnly = 1,

    /// <summary>The item is hidden.</summary>
    Hidden = 2,

    /// <summary>The item is a system item.</summary>
    System = 4,
}
