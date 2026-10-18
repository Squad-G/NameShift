using System;

namespace NameShift.Core;

/// <summary>One reason a new name cannot be used, with raw data for the UI to format.</summary>
public sealed class Conflict : IEquatable<Conflict>
{
    /// <summary>Creates a conflict; the detail is optional.</summary>
    public Conflict(ConflictCode code, string? detail = null)
    {
        if (!Enum.IsDefined(typeof(ConflictCode), code))
        {
            throw new ArgumentOutOfRangeException(nameof(code));
        }

        Code = code;
        Detail = detail;
    }

    /// <summary>Why the name cannot be used.</summary>
    public ConflictCode Code { get; }

    /// <summary>Raw data about the conflict (the offending characters; for a reserved name, the text before the first dot, which is a reserved device name), or null.</summary>
    public string? Detail { get; }

    /// <summary>Same code and ordinal-equal detail.</summary>
    public bool Equals(Conflict? other) =>
        other != null && Code == other.Code && string.Equals(Detail, other.Detail, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as Conflict);

    /// <inheritdoc />
    public override int GetHashCode() => unchecked((((int)Code).GetHashCode() * 397) ^ (Detail?.GetHashCode() ?? 0));

    /// <inheritdoc />
    public override string ToString() => Detail == null ? Code.ToString() : $"{Code}: {Detail}";
}
