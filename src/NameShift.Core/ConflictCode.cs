namespace NameShift.Core;

/// <summary>Why a new name cannot be used.</summary>
public enum ConflictCode
{
    /// <summary>The name contains a character Windows forbids.</summary>
    InvalidCharacter,

    /// <summary>The text before the first dot is a reserved device name such as CON or NUL.</summary>
    ReservedName,

    /// <summary>The name ends with a dot or a space.</summary>
    TrailingDotOrSpace,

    /// <summary>The name is empty or only dots and spaces.</summary>
    EmptyName,

    /// <summary>The name is over 255 characters.</summary>
    NameTooLong,

    /// <summary>Two items get the same new name. Set by the duplicate check (NS-6).</summary>
    DuplicateTarget,

    /// <summary>The new name is taken by another file on disk. Set by the duplicate check (NS-6).</summary>
    TargetExists,

    /// <summary>The full path is too long. Set by the path length check (NS-7).</summary>
    PathTooLong,
}
