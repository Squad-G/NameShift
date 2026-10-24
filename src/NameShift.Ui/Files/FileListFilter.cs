using System;
using System.Collections.Generic;
using System.Threading;
using NameShift.Core;

namespace NameShift.Ui.Files;

/// <summary>The file list's filter loop: <see cref="ItemFilter.Apply"/> with a cancellation check before every item,
/// so a superseded run stops within one item (at most one pattern match, bounded by its timeout).</summary>
internal static class FileListFilter
{
    /// <summary>The items <paramref name="includes"/> keeps: the same instances, in input order, in a new list.</summary>
    /// <exception cref="ArgumentNullException">When items or includes is null.</exception>
    /// <exception cref="OperationCanceledException">When the token is cancelled before an item is checked.</exception>
    internal static IReadOnlyList<RenameItem> Apply(
        IReadOnlyList<RenameItem> items, Func<RenameItem, bool> includes, CancellationToken token)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        if (includes == null)
        {
            throw new ArgumentNullException(nameof(includes));
        }

        var included = new List<RenameItem>();
        foreach (RenameItem item in items)
        {
            token.ThrowIfCancellationRequested();
            if (includes(item))
            {
                included.Add(item);
            }
        }

        return included;
    }
}
