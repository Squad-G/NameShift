using System;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The list text of each folder name placement.</summary>
public static class FolderNamePlacementText
{
    /// <exception cref="ArgumentOutOfRangeException">The placement is not a defined value.</exception>
    public static string For(FolderNamePlacement placement)
    {
        switch (placement)
        {
            case FolderNamePlacement.Prefix:
                return Strings.FolderNamePlaceBefore;
            case FolderNamePlacement.Suffix:
                return Strings.FolderNamePlaceAfter;
            default:
                throw new ArgumentOutOfRangeException(nameof(placement), placement, "Unknown folder name placement.");
        }
    }
}
