using System;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The list text of each move or copy mode.</summary>
public static class MoveCopyModeText
{
    /// <exception cref="ArgumentOutOfRangeException">The mode is not a defined value.</exception>
    public static string For(MoveCopyMode mode)
    {
        switch (mode)
        {
            case MoveCopyMode.Move:
                return Strings.MoveCopyModeMove;
            case MoveCopyMode.Copy:
                return Strings.MoveCopyModeCopy;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown move or copy mode.");
        }
    }
}
