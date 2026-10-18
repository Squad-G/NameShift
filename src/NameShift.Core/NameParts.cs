using System;

namespace NameShift.Core;

/// <summary>A name as (base name, extension).</summary>
public sealed class NameParts : IEquatable<NameParts>
{
    /// <summary>Creates a name from its two parts; empty strings are allowed.</summary>
    public NameParts(string baseName, string extension)
    {
        BaseName = baseName ?? throw new ArgumentNullException(nameof(baseName));
        Extension = extension ?? throw new ArgumentNullException(nameof(extension));
    }

    /// <summary>The name without its extension.</summary>
    public string BaseName { get; }

    /// <summary>The extension without the dot: "gz", never ".gz".</summary>
    public string Extension { get; }

    /// <summary>Splits a full name at its last dot; folders and names with no usable dot have no extension.</summary>
    public static NameParts Split(string fullName, ItemKind kind)
    {
        if (fullName == null)
        {
            throw new ArgumentNullException(nameof(fullName));
        }

        if (kind == ItemKind.Folder)
        {
            return new NameParts(fullName, string.Empty);
        }

        var dot = fullName.LastIndexOf('.');
        if (dot <= 0 || dot == fullName.Length - 1)
        {
            return new NameParts(fullName, string.Empty);
        }

        return new NameParts(fullName.Substring(0, dot), fullName.Substring(dot + 1));
    }

    /// <summary>Joins the parts with a dot, only when there is an extension.</summary>
    public string ToFullName() => Extension.Length == 0 ? BaseName : BaseName + "." + Extension;

    /// <summary>Returns a copy with another base name.</summary>
    public NameParts WithBaseName(string baseName) => new NameParts(baseName, Extension);

    /// <summary>Returns a copy with another extension.</summary>
    public NameParts WithExtension(string extension) => new NameParts(BaseName, extension);

    /// <summary>Ordinal comparison of both parts.</summary>
    public bool Equals(NameParts? other) =>
        other != null
        && string.Equals(BaseName, other.BaseName, StringComparison.Ordinal)
        && string.Equals(Extension, other.Extension, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as NameParts);

    /// <inheritdoc />
    public override int GetHashCode() => unchecked((BaseName.GetHashCode() * 397) ^ Extension.GetHashCode());

    /// <inheritdoc />
    public override string ToString() => ToFullName();
}
