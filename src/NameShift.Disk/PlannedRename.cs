using System;
using System.IO;
using NameShift.Core;

namespace NameShift.Disk;

/// <summary>One rename to perform: an item in a folder gets a new name in the same folder.</summary>
public sealed class PlannedRename
{
    private static readonly char[] InvalidNameChars = Path.GetInvalidFileNameChars();

    /// <summary>Creates a rename; both names must be plain names (no folder part) that differ ordinal, so a case-only
    /// change is allowed and every move stays inside its folder.</summary>
    public PlannedRename(string folderPath, string oldName, string newName, ItemKind kind)
    {
        if (folderPath == null)
        {
            throw new ArgumentNullException(nameof(folderPath));
        }

        if (folderPath.Length == 0 || !Path.IsPathRooted(folderPath))
        {
            throw new ArgumentException("The folder must be a full path.", nameof(folderPath));
        }

        ValidateName(oldName, nameof(oldName));
        ValidateName(newName, nameof(newName));
        if (string.Equals(oldName, newName, StringComparison.Ordinal))
        {
            throw new ArgumentException("The new name equals the old name.", nameof(newName));
        }

        if (!Enum.IsDefined(typeof(ItemKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        FolderPath = folderPath;
        OldName = oldName;
        NewName = newName;
        Kind = kind;
    }

    /// <summary>The containing folder, as given.</summary>
    public string FolderPath { get; }

    /// <summary>The item's current name (no folder).</summary>
    public string OldName { get; }

    /// <summary>The item's new name in the same folder.</summary>
    public string NewName { get; }

    /// <summary>Whether the item is a file or a folder.</summary>
    public ItemKind Kind { get; }

    private static void ValidateName(string name, string parameterName)
    {
        if (name == null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (name.Length == 0 || name == "." || name == ".." || name.IndexOfAny(InvalidNameChars) >= 0)
        {
            throw new ArgumentException("The name must be a plain file or folder name.", parameterName);
        }
    }
}
