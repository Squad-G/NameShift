using System;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The list text of each date source.</summary>
public static class DateSourceText
{
    /// <exception cref="ArgumentOutOfRangeException">The source is not a defined value.</exception>
    public static string For(DateSource source)
    {
        switch (source)
        {
            case DateSource.Modified:
                return Strings.AutoDateSourceModified;
            case DateSource.Created:
                return Strings.AutoDateSourceCreated;
            case DateSource.Taken:
                return Strings.AutoDateSourceTaken;
            default:
                throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown date source.");
        }
    }
}
