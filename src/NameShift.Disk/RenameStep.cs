using NameShift.Core;

namespace NameShift.Disk;

/// <summary>One move of the ordered plan: to the item's final name or to a temporary name.</summary>
internal sealed class RenameStep
{
    public RenameStep(int renameIndex, string fromPath, string toPath, ItemKind kind, bool isFinal)
    {
        RenameIndex = renameIndex;
        FromPath = fromPath;
        ToPath = toPath;
        Kind = kind;
        IsFinal = isFinal;
    }

    /// <summary>The index into the renames list.</summary>
    public int RenameIndex { get; }

    /// <summary>The full path before the move.</summary>
    public string FromPath { get; }

    /// <summary>The full path after the move.</summary>
    public string ToPath { get; }

    /// <summary>Whether the item is a file or a folder.</summary>
    public ItemKind Kind { get; }

    /// <summary>True when ToPath is the item's final target; false for a move to a temporary name.</summary>
    public bool IsFinal { get; }
}
