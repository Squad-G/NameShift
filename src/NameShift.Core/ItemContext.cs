using System;

namespace NameShift.Core;

/// <summary>What a rule sees about the item, built by the pipeline.</summary>
public sealed class ItemContext
{
    /// <summary>Creates the context of the item at <paramref name="index"/> in a list of <paramref name="count"/>.</summary>
    public ItemContext(RenameItem item, int index, int count)
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
        Index = index;
        Count = count;
    }

    /// <summary>The item being renamed.</summary>
    public RenameItem Item { get; }

    /// <summary>The 0-based position in the list given to the pipeline.</summary>
    public int Index { get; }

    /// <summary>The number of items in that list.</summary>
    public int Count { get; }
}
