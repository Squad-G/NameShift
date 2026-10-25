using System;
using System.Globalization;
using NameShift.Core;

namespace NameShift.Ui.Files;

/// <summary>The text of the Size, Modified and Folder cells, formatted in the given culture.</summary>
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
    public static string SelectionCount(int batchCount, int total, CultureInfo culture) =>
        SelectionCount(batchCount, total, false, culture);

    /// <summary>"N of M files" for the line under the list, followed by " (filtered)" when <paramref name="filtered"/>
    /// (NS-70: the filter is not at default and hides an entry); empty when the list has no items.</summary>
    public static string SelectionCount(int batchCount, int total, bool filtered, CultureInfo culture)
    {
        if (total <= 0)
        {
            return "";
        }

        string text = total == 1
            ? Strings.SelectionCountOne
            : string.Format(culture, Strings.SelectionCountMany, batchCount.ToString("N0", culture), total.ToString("N0", culture));
        return filtered ? string.Format(culture, Strings.SelectionCountFiltered, text) : text;
    }

    /// <summary>Short date and time; empty when the date is unknown (never invented).</summary>
    public static string Modified(RenameItem item, CultureInfo culture) =>
        item.Modified == default ? "" : item.Modified.ToString("g", culture);

    /// <summary>The item's folder under <paramref name="rootFolder"/>: "" for an item of the root itself, the path
    /// below it for a deeper one (<c>sub\deep</c>), ignoring case and trailing separators (a drive root such as
    /// <c>C:\</c> works); the item's full folder path when it is not under the root (<c>C:\Pics2</c> is not under
    /// <c>C:\Pics</c>). Text only: the disk is never read.</summary>
    /// <exception cref="ArgumentNullException">When rootFolder or item is null.</exception>
    public static string RelativeFolder(string rootFolder, RenameItem item)
    {
        if (rootFolder == null)
        {
            throw new ArgumentNullException(nameof(rootFolder));
        }

        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        // C:\ trimmed is C:, so C:\x is C: followed by \x.
        string root = rootFolder.TrimEnd('\\', '/');
        string folder = item.FolderPath.TrimEnd('\\', '/');
        if (root.Length == 0)
        {
            return item.FolderPath;
        }

        if (string.Equals(folder, root, StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        bool under = folder.Length > root.Length + 1
            && folder[root.Length] == '\\'
            && string.Compare(folder, 0, root, 0, root.Length, StringComparison.OrdinalIgnoreCase) == 0;
        return under ? folder.Substring(root.Length + 1) : item.FolderPath;
    }
}
