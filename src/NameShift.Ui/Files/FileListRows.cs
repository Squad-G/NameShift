using System.Collections.Generic;

namespace NameShift.Ui.Files;

/// <summary>Row arithmetic for the file list when a filter changes the number of rows (NS-70).</summary>
public static class FileListRows
{
    /// <summary>The rows from 0 up to <paramref name="count"/> (excluded), in their order: a selection read queued
    /// before the list shrank never indexes past the end.</summary>
    public static int[] InRange(IReadOnlyList<int> rows, int count)
    {
        var kept = new List<int>(rows.Count);
        foreach (int row in rows)
        {
            if (row >= 0 && row < count)
            {
                kept.Add(row);
            }
        }

        return kept.ToArray();
    }

    /// <summary>The row to scroll into view after the row count went from <paramref name="oldCount"/> to
    /// <paramref name="newCount"/>: the first selected row when it is still in the list; else the top when the list
    /// shrank and is not empty; else null (the scroll position is left alone).</summary>
    public static int? ScrollTarget(IReadOnlyList<int> selectedRows, int oldCount, int newCount)
    {
        if (selectedRows.Count > 0 && selectedRows[0] >= 0 && selectedRows[0] < newCount)
        {
            return selectedRows[0];
        }

        if (newCount < oldCount && newCount > 0)
        {
            return 0;
        }

        return null;
    }
}
