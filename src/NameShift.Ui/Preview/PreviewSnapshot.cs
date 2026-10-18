using System;
using System.Collections.Generic;
using NameShift.Core;

namespace NameShift.Ui.Preview;

/// <summary>The input a preview run used and its output. Immutable.</summary>
public sealed class PreviewSnapshot
{
    /// <summary>No preview: no items and no results.</summary>
    public static readonly PreviewSnapshot Empty = new PreviewSnapshot(
        Array.Empty<RenameItem>(),
        Array.Empty<RenameResult>());

    /// <summary>Creates a snapshot; there must be exactly one result per item.</summary>
    public PreviewSnapshot(IReadOnlyList<RenameItem> items, IReadOnlyList<RenameResult> results)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        if (results == null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        if (items.Count != results.Count)
        {
            throw new ArgumentException("There must be one result per item.", nameof(results));
        }

        Items = items;
        Results = results;
    }

    /// <summary>The exact list the pipeline ran on (the reference is kept: it is compared with the file list's items).</summary>
    public IReadOnlyList<RenameItem> Items { get; }

    /// <summary>One result per item, Results[i].Index == i.</summary>
    public IReadOnlyList<RenameResult> Results { get; }
}
