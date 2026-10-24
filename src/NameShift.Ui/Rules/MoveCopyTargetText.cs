using System;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The list text of each move or copy target.</summary>
public static class MoveCopyTargetText
{
    /// <exception cref="ArgumentOutOfRangeException">The target is not a defined value.</exception>
    public static string For(MoveCopyTarget target)
    {
        switch (target)
        {
            case MoveCopyTarget.Start:
                return Strings.MoveCopyTargetStart;
            case MoveCopyTarget.End:
                return Strings.MoveCopyTargetEnd;
            case MoveCopyTarget.Position:
                return Strings.MoveCopyTargetPosition;
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown move or copy target.");
        }
    }
}
