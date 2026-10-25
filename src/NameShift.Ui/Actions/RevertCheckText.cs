using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NameShift.Disk;

namespace NameShift.Ui.Actions;

/// <summary>The words of the Revert dialog: one line per entry a Revert would skip, and the reason words the command
/// line's dry run shares.</summary>
public static class RevertCheckText
{
    /// <summary>The most entries the dialog lists before "and N more".</summary>
    public const int MaxLines = 10;

    /// <summary>"name: reason" for the first MaxLines skipped entries in journal order, then "and N more" for the rest.
    /// An item is named by its new file name.</summary>
    public static IReadOnlyList<string> Lines(RevertCheck check, CultureInfo culture)
    {
        if (check == null)
        {
            throw new ArgumentNullException(nameof(check));
        }

        if (culture == null)
        {
            throw new ArgumentNullException(nameof(culture));
        }

        var lines = new List<string>();
        int shown = Math.Min(MaxLines, check.Skipped.Count);
        for (int i = 0; i < shown; i++)
        {
            SkippedRevert skip = check.Skipped[i];
            lines.Add(string.Format(culture, Strings.RevertCheckLine, Path.GetFileName(skip.Entry.NewPath), Reason(skip.Reason)));
        }

        int more = check.Skipped.Count - shown;
        if (more == 1)
        {
            lines.Add(Strings.RevertCheckMoreOne);
        }
        else if (more > 1)
        {
            lines.Add(string.Format(culture, Strings.RevertCheckMoreMany, more.ToString("N0", culture)));
        }

        return lines;
    }

    /// <summary>The words for why an entry is skipped.</summary>
    public static string Reason(RevertSkipReason reason)
    {
        switch (reason)
        {
            case RevertSkipReason.Modified:
                return Strings.RevertReasonModified;
            case RevertSkipReason.ItemMissing:
                return Strings.RevertReasonMissing;
            case RevertSkipReason.NameTaken:
                return Strings.RevertReasonNameTaken;
            case RevertSkipReason.WaitsForSkippedItem:
                return Strings.RevertReasonWaits;
            case RevertSkipReason.RenameFailed:
                // A check cannot produce this (only a Revert can); a word keeps the builder total.
                return Strings.RevertReasonFailed;
            default:
                throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown skip reason.");
        }
    }
}
