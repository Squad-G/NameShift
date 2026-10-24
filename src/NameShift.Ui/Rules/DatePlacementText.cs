using System;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The list text of each date placement.</summary>
public static class DatePlacementText
{
    /// <exception cref="ArgumentOutOfRangeException">The placement is not a defined value.</exception>
    public static string For(DatePlacement placement)
    {
        switch (placement)
        {
            case DatePlacement.Prefix:
                return Strings.AutoDatePlaceBefore;
            case DatePlacement.Suffix:
                return Strings.AutoDatePlaceAfter;
            default:
                throw new ArgumentOutOfRangeException(nameof(placement), placement, "Unknown date placement.");
        }
    }
}
