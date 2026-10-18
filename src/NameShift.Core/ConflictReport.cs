using System;
using System.Collections.Generic;
using System.Linq;

namespace NameShift.Core;

/// <summary>The conflicts of every item, by position in the results list.</summary>
public sealed class ConflictReport
{
    private readonly IReadOnlyList<Conflict>[] _items;

    /// <summary>Creates an immutable report from one conflict list per item.</summary>
    public ConflictReport(IReadOnlyList<IReadOnlyList<Conflict>> itemConflicts)
    {
        if (itemConflicts == null)
        {
            throw new ArgumentNullException(nameof(itemConflicts));
        }

        _items = itemConflicts
            .Select(list => list == null
                ? throw new ArgumentException("An item's conflict list is null.", nameof(itemConflicts))
                : (IReadOnlyList<Conflict>)Array.AsReadOnly(list.ToArray()))
            .ToArray();
    }

    /// <summary>The number of items (the number of results).</summary>
    public int Count => _items.Length;

    /// <summary>True when any item has at least one conflict.</summary>
    public bool HasConflicts => _items.Any(list => list.Count > 0);

    /// <summary>The item's conflicts, by position in the results list (equal to RenameResult.Index for one Run).</summary>
    public IReadOnlyList<Conflict> ForItem(int index)
    {
        if (index < 0 || index >= _items.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return _items[index];
    }
}
