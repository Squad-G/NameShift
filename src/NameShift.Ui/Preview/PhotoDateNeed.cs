using System;
using System.Collections.Generic;
using NameShift.Core;

namespace NameShift.Ui.Preview;

/// <summary>When photo dates are needed, and when an item has one. Used by the window's preview and by the command
/// line, so both decide the same way.</summary>
public static class PhotoDateNeed
{
    /// <summary>True when one of the built rules is an <see cref="AutoDateRule"/> using <see cref="DateSource.Taken"/>.
    /// A panel that is on but built no rule does not count.</summary>
    public static bool In(IReadOnlyList<IRenameRule> rules)
    {
        if (rules == null)
        {
            throw new ArgumentNullException(nameof(rules));
        }

        foreach (IRenameRule rule in rules)
        {
            if (rule is AutoDateRule date && date.Source == DateSource.Taken)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when the item has a photo date: <see cref="RenameItem.Taken"/> is neither null nor
    /// <c>default(DateTime)</c> (the test <see cref="AutoDateRule"/> uses to fall back to the modified date).</summary>
    public static bool HasDate(RenameItem item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        return item.Taken.HasValue && item.Taken.Value != default(DateTime);
    }
}
