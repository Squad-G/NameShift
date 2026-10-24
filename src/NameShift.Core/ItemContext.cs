using System;

namespace NameShift.Core;

/// <summary>What a rule sees about the item, built by the pipeline.</summary>
public sealed class ItemContext
{
    /// <summary>Creates the context of the item at <paramref name="index"/> in a list of <paramref name="count"/>;
    /// <paramref name="folderIndex"/> is its 0-based position among the items of the same folder (null: the same as
    /// <paramref name="index"/>, i.e. one folder).</summary>
    public ItemContext(RenameItem item, int index, int count, int? folderIndex = null)
    {
        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "The count must be at least 1.");
        }

        if (index < 0 || index >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "The index must be inside the list.");
        }

        Item = item ?? throw new ArgumentNullException(nameof(item));
        if (folderIndex < 0 || folderIndex > index)
        {
            throw new ArgumentOutOfRangeException(nameof(folderIndex), "The folder index must be between 0 and the index.");
        }

        Index = index;
        Count = count;
        FolderIndex = folderIndex ?? index;
    }

    /// <summary>The item being renamed.</summary>
    public RenameItem Item { get; }

    /// <summary>The 0-based position in the list given to the pipeline.</summary>
    public int Index { get; }

    /// <summary>The number of items in that list.</summary>
    public int Count { get; }

    /// <summary>The 0-based position among the items of the same folder in the list given to the pipeline, in list
    /// order. Two items are in the same folder when their FolderPath is equal ignoring case (ordinal), after removing
    /// trailing '' and '/'.</summary>
    public int FolderIndex { get; }
}
