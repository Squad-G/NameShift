using System.Globalization;
using NameShift.Core;

namespace NameShift.Ui.Files;

/// <summary>The text of the Size and Modified cells, formatted in the given culture.</summary>
public static class FileListText
{
    /// <summary>Whole kilobytes rounded up, like Explorer's details view; empty for a folder.</summary>
    public static string Size(RenameItem item, CultureInfo culture)
    {
        if (item.Kind == ItemKind.Folder)
        {
            return "";
        }

        long kilobytes = (item.Size + 1023) / 1024;
        return string.Format(culture, Strings.SizeKilobytes, kilobytes.ToString("N0", culture));
    }

    /// <summary>"N of M files" for the line under the list; empty when the list has no items.</summary>
    public static string SelectionCount(int batchCount, int total, CultureInfo culture)
    {
        if (total <= 0)
        {
            return "";
        }

        return total == 1
            ? Strings.SelectionCountOne
            : string.Format(culture, Strings.SelectionCountMany, batchCount.ToString("N0", culture), total.ToString("N0", culture));
    }

    /// <summary>Short date and time; empty when the date is unknown (never invented).</summary>
    public static string Modified(RenameItem item, CultureInfo culture) =>
        item.Modified == default ? "" : item.Modified.ToString("g", culture);
}